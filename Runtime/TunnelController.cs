using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using SshTunnelTray.Config;
using SshTunnelTray.Domain;

namespace SshTunnelTray.Runtime;

public enum TunnelErrorKind { Auth, HostKey, Port, Connection, Config, Unknown }
public sealed record TunnelError(TunnelErrorKind Kind, string Message);
public sealed class TunnelStatus : EventArgs
{
    public TunnelStatus(TunnelState state, TunnelState desiredState, TunnelError? error = null)
        => (State, DesiredState, Error) = (state, desiredState, error);

    public TunnelState State { get; }
    public TunnelState DesiredState { get; }
    public TunnelError? Error { get; }
}

public sealed class TunnelController : IAsyncDisposable
{
    private enum DesiredTunnelState { Stopped, Connected }

    private readonly object gate = new();
    private readonly string? executablePath;
    private readonly string? askPassExecutablePath;
    private Process? process;
    private TunnelProfile? profile;
    private TunnelState observed = TunnelState.Stopped;
    private DesiredTunnelState desired = DesiredTunnelState.Stopped;
    private bool disposed;
    private CancellationTokenSource? runCts;
    private Task? runTask;
    private int generation;

    public TunnelController(string? executablePath = null, string? askPassExecutablePath = null)
        => (this.executablePath, this.askPassExecutablePath) = (executablePath, askPassExecutablePath);
    public TunnelState State { get { lock (gate) return observed; } }
    public TunnelState DesiredState { get { lock (gate) return ToPublicState(desired); } }
    public TunnelError? LastError { get; private set; }
    public event EventHandler<TunnelStatus>? StateChanged;

    public async Task StartAsync(TunnelProfile snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Task startedTask;
        lock (gate)
        {
            ThrowIfDisposed();
            profile = snapshot;
            desired = DesiredTunnelState.Connected;
            runCts?.Cancel();
            runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var startup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var currentGeneration = ++generation;
            runTask = RunAsync(snapshot, runCts.Token, currentGeneration, startup);
            startedTask = startup.Task;
        }
        await startedTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? task;
        Process? processToStop;
        lock (gate)
        {
            if (disposed) return;
            desired = DesiredTunnelState.Stopped;
            runCts?.Cancel();
            task = runTask;
            processToStop = process;
        }
        await StopProcessAsync(processToStop, cancellationToken).ConfigureAwait(false);
        if (task is not null) { try { await task.ConfigureAwait(false); } catch (OperationCanceledException) { } }
        SetState(TunnelState.Stopped, null);
    }

    private async Task RunAsync(TunnelProfile p, CancellationToken ct, int myGeneration, TaskCompletionSource startup)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested && IsDesiredConnected(myGeneration))
        {
            Process? started = null;
            AskPassBroker? askPass = null;
            try
            {
                SetState(attempt == 0 ? TunnelState.Connecting : TunnelState.Reconnecting, null);
                startup.TrySetResult();
                SshLaunchSpec spec;
                if (p.AuthMode == TunnelAuthMode.Password)
                {
                    var helperPath = askPassExecutablePath ?? Environment.ProcessPath;
                    if (string.IsNullOrWhiteSpace(helperPath)) throw new TunnelException(new(TunnelErrorKind.Config, "Не найден путь к askpass executable."));
                    if (p.Password is null) throw new TunnelException(new(TunnelErrorKind.Config, "Пароль SSH не задан."));
                    var password = SecretBox.Decrypt(p.Password);
                    try { askPass = AskPassBroker.Create(password); }
                    finally { CryptographicOperations.ZeroMemory(Encoding.UTF8.GetBytes(password)); }
                    spec = SshArgumentBuilder.Build(p, executablePath, helperPath, askPass.PipeName);
                }
                else
                {
                    spec = SshArgumentBuilder.Build(p, executablePath, askPassExecutablePath);
                }
                var psi = new ProcessStartInfo(spec.ExecutablePath) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
                foreach (var arg in spec.Arguments) psi.ArgumentList.Add(arg);
                foreach (var item in spec.Environment) psi.Environment[item.Key] = item.Value;
                started = new Process { StartInfo = psi, EnableRaisingEvents = true };
                lock (gate) process = started;
                started.Exited += (_, _) =>
                {
                    lock (gate)
                    {
                        if (generation != myGeneration || desired != DesiredTunnelState.Connected || ct.IsCancellationRequested)
                            return;
                    }
                    // The run loop classifies an unexpected exit after it has drained stderr.
                };
                if (!started.Start()) throw new InvalidOperationException("ssh.exe не запустился.");
                var stderrTask = ReadAndClassifyAsync(started.StandardError, ct);
                _ = DrainAsync(started.StandardOutput, ct);
                await WaitForForwardAsync(p, started, ct).ConfigureAwait(false);
                attempt = 0; SetState(TunnelState.Connected, null);
                await started.WaitForExitAsync(ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested || !IsDesiredConnected(myGeneration)) break;
                var error = Classify(await stderrTask.ConfigureAwait(false), started);
                if (!CanReconnect(error)) { SetState(TunnelState.Error, error); break; }
                attempt++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                var error = Classify(ex);
                SetState(TunnelState.Error, error);
                if (!CanReconnect(error) || !p.ReconnectEnabled || !IsDesiredConnected(myGeneration)) break;
                attempt++;
            }
            finally
            {
                await StopProcessAsync(started, CancellationToken.None).ConfigureAwait(false);
                if (askPass is not null) await askPass.DisposeAsync().ConfigureAwait(false);
            }
            if (!p.ReconnectEnabled || !IsDesiredConnected(myGeneration)) break;
            var delay = Math.Clamp(Math.Max(0, p.ReconnectDelaySeconds) * Math.Pow(1.7, Math.Min(attempt, 5)), 0, 300);
            var jitter = Random.Shared.NextDouble() * Math.Min(2, delay * .2);
            await Task.Delay(TimeSpan.FromSeconds(delay + jitter), ct).ConfigureAwait(false);
        }
        startup.TrySetResult();
        if (IsDesiredConnected(myGeneration) && State != TunnelState.Error) SetState(TunnelState.Stopped, null);
    }

    private async Task WaitForForwardAsync(TunnelProfile p, Process ssh, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, p.ConnectTimeoutSeconds) + 5));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (ssh.HasExited) throw new InvalidOperationException("ssh завершился до установки forward.");
            try { using var tcp = new TcpClient(); await tcp.ConnectAsync(p.LocalAddress, p.LocalPort, timeout.Token).ConfigureAwait(false); return; }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            { throw new TunnelException(new TunnelError(TunnelErrorKind.Port, "Локальный порт уже используется.")); }
            catch (SocketException) { await Task.Delay(100, timeout.Token).ConfigureAwait(false); }
        }
    }

    private async Task StopProcessAsync(CancellationToken ct)
        => await StopProcessAsync(null, ct).ConfigureAwait(false);

    private async Task StopProcessAsync(Process? expected, CancellationToken ct)
    {
        Process? p;
        lock (gate)
        {
            if (expected is not null && !ReferenceEquals(process, expected)) return;
            p = process;
            process = null;
        }
        if (p is null) return;
        try { if (!p.HasExited) { p.Kill(entireProcessTree: true); await p.WaitForExitAsync(ct).ConfigureAwait(false); } } catch (InvalidOperationException) { } finally { p.Dispose(); }
    }

    private bool IsDesiredConnected(int g) { lock (gate) return desired == DesiredTunnelState.Connected && generation == g; }
    private void SetState(TunnelState state, TunnelError? error) { lock (gate) { observed = state; LastError = error; } StateChanged?.Invoke(this, new(state, DesiredState, error)); }
    private static async Task DrainAsync(StreamReader reader, CancellationToken ct) { while (!ct.IsCancellationRequested && await reader.ReadLineAsync(ct).ConfigureAwait(false) is not null) { } }
    private static async Task<TunnelErrorKind> ReadAndClassifyAsync(StreamReader reader, CancellationToken ct)
    {
        var kind = TunnelErrorKind.Unknown;
        while (!ct.IsCancellationRequested && await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            var s = line.ToLowerInvariant();
            if (s.Contains("permission denied") || s.Contains("authentication")) kind = TunnelErrorKind.Auth;
            else if (s.Contains("host key") || s.Contains("known_hosts") || s.Contains("offending")) kind = TunnelErrorKind.HostKey;
            else if (s.Contains("address already in use") || s.Contains("cannot listen") || s.Contains("bind")) kind = TunnelErrorKind.Port;
            else if (kind == TunnelErrorKind.Unknown && (s.Contains("connection") || s.Contains("timeout") || s.Contains("unreachable"))) kind = TunnelErrorKind.Connection;
        }
        return kind;
    }
    private static bool CanReconnect(TunnelError? e) => e is null || e.Kind is TunnelErrorKind.Connection or TunnelErrorKind.Unknown;
    private static TunnelError Classify(Exception ex) => ex is TunnelException tunnel
        ? tunnel.Error
        : ex is SocketException socket && socket.SocketErrorCode == SocketError.AddressAlreadyInUse
            ? new(TunnelErrorKind.Port, "Локальный порт уже используется.")
            : new(ex is TimeoutException ? TunnelErrorKind.Connection : TunnelErrorKind.Unknown, "SSH-туннель завершился с ошибкой.");
    private static TunnelError Classify(TunnelErrorKind stderrKind, Process p)
    {
        var kind = stderrKind != TunnelErrorKind.Unknown ? stderrKind : p.ExitCode == 255 ? TunnelErrorKind.Connection : TunnelErrorKind.Unknown;
        return new(kind, $"SSH завершился с кодом {p.ExitCode}: {kind}.");
    }
    private static TunnelState ToPublicState(DesiredTunnelState state) => state == DesiredTunnelState.Connected ? TunnelState.Connected : TunnelState.Stopped;
    private sealed class TunnelException : Exception
    {
        public TunnelException(TunnelError error) => Error = error;
        public TunnelError Error { get; }
    }
    private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(TunnelController)); }
    public async ValueTask DisposeAsync() { lock (gate) { if (disposed) return; disposed = true; desired = DesiredTunnelState.Stopped; runCts?.Cancel(); } await StopProcessAsync(CancellationToken.None).ConfigureAwait(false); runCts?.Dispose(); }
}
