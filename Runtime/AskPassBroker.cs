using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace SshTunnelTray.Runtime;

/// <summary>One-shot, same-executable SSH askpass IPC.</summary>
public sealed class AskPassBroker : IAsyncDisposable
{
    public const string PipeEnvironmentVariable = "SSHTUNNELTRAY_ASKPASS_PIPE";
    private readonly NamedPipeServerStream pipe;
    private readonly byte[] password;
    private readonly CancellationTokenSource lifetime;
    private readonly Task serveTask;

    private AskPassBroker(NamedPipeServerStream pipe, byte[] password, TimeSpan timeout)
    {
        this.pipe = pipe;
        this.password = password;
        lifetime = new CancellationTokenSource(timeout);
        serveTask = ServeAsync(lifetime.Token);
    }

    public string PipeName { get; private init; } = null!;

    public static AskPassBroker Create(string password, TimeSpan? timeout = null)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Пароль для SSH не задан.", nameof(password));
        var name = "SshTunnelTray-AskPass-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(name, PipeDirection.Out, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var bytes = Encoding.UTF8.GetBytes(password);
        return new AskPassBroker(pipe, bytes, timeout ?? TimeSpan.FromSeconds(15)) { PipeName = name };
    }

    private async Task ServeAsync(CancellationToken ct)
    {
        try
        {
            await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            await pipe.WriteAsync(password, ct).ConfigureAwait(false);
            await pipe.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        finally { await DisposePipeAsync().ConfigureAwait(false); }
    }

    public static bool TryRunHelper(string[] args)
    {
        // SSH supplies its own prompt argument; the pipe environment is the helper marker.
        var name = Environment.GetEnvironmentVariable(PipeEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(name)) { Environment.SetEnvironmentVariable(PipeEnvironmentVariable, null); return false; }
        try
        {
            using var pipe = new NamedPipeClientStream(".", name, PipeDirection.In, PipeOptions.Asynchronous);
            pipe.Connect(5000);
            using var output = new MemoryStream();
            pipe.CopyTo(output);
            var bytes = output.ToArray();
            try { Console.Out.Write(Encoding.UTF8.GetString(bytes)); } finally { CryptographicOperations.ZeroMemory(bytes); }
            return true;
        }
        catch (IOException) { return false; }
        catch (TimeoutException) { return false; }
        finally { Environment.SetEnvironmentVariable(PipeEnvironmentVariable, null); }
    }

    private async ValueTask DisposePipeAsync()
    {
        if (pipe.IsConnected) pipe.Disconnect();
        await pipe.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        try { await serveTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        CryptographicOperations.ZeroMemory(password);
        lifetime.Dispose();
    }
}
