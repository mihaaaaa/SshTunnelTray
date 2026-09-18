using SshTunnelTray.Config;
using SshTunnelTray.Domain;
using SshTunnelTray.Runtime;
using SshTunnelTray.Tray;
using SshTunnelTray.UI;
using System.Security.Cryptography;
using System.Text;

namespace SshTunnelTray.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ConfigStore store; private readonly string directory; private readonly NotifyIcon icon;
    private TunnelProfile? profile; private TunnelController? controller; private ConfigDocument document = new(); private string? runtimeError;
    private readonly SynchronizationContext uiContext; private Mutex? profileMutex; private readonly SemaphoreSlim controllerLifecycle = new(1, 1);
    public TrayApplicationContext(LaunchDirectory launch, ConfigStore store, CommandLineOptions options)
    {
        this.store = store; directory = launch.Path; WindowsFormsSynchronizationContext.AutoInstall = true; uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext(); icon = new NotifyIcon { Visible = true, Text = "SshTunnelTray", Icon = TrayIconProvider.Create(TunnelState.Stopped, hasProfile: false, notReady: true) };
        icon.ContextMenuStrip = new ContextMenuStrip(); icon.ContextMenuStrip.Items.Add("Туннель: нет профиля");
        icon.ContextMenuStrip.Items.Add("Включить", null, async (_, _) => await StartAsync());
        icon.ContextMenuStrip.Items.Add("Выключить", null, async (_, _) => await StopAsync());
        icon.ContextMenuStrip.Items.Add("Настройки", null, (_, _) => ShowSettings(null));
        icon.ContextMenuStrip.Items.Add("Выйти", null, (_, _) => ExitThread());
        var loaded = store.Load(); document = loaded.Document ?? new();
        if (options.ProfileName is not null)
        {
            profile = store.FindProfile(document, options.ProfileName);
            if (profile is null) ShowSettings($"Профиль «{options.ProfileName}» не найден."); else _ = StartAsync();
        }
        else ShowSettings(options.Error);
        UpdateUi(TunnelState.Stopped, null);
    }
    private async void ShowSettings(string? error)
    {
        try
        {
            var wasRunning = controller?.DesiredState == TunnelState.Connected;
            using var form = new SettingsForm(document, profile, store, directory, error ?? runtimeError, icon.Icon);
            if (form.ShowDialog() != DialogResult.OK) return;

            document = form.Document;
            profile = form.Profile;
            var shouldStart = wasRunning || form.StartAfterSave;
            UpdateMenu();

            await controllerLifecycle.WaitAsync();
            try
            {
                if (controller is not null) await RecreateControllerAsync();
                if (shouldStart) await StartCoreAsync(); else UpdateUi(TunnelState.Stopped, null);
            }
            finally { controllerLifecycle.Release(); }
        }
        catch (Exception ex)
        {
            runtimeError = ex.Message;
            UpdateUi(TunnelState.Error, ex.Message);
        }
    }
    private async Task RecreateControllerAsync()
    {
        if (controller is null) return;
        var old = controller;
        old.StateChanged -= OnStateChanged;
        try { await old.StopAsync(); }
        finally
        {
            try { await old.DisposeAsync(); }
            finally { controller = null; ReleaseMutex(); }
        }
    }
    private void UpdateMenu() { if (icon.ContextMenuStrip is null) return; icon.ContextMenuStrip.Items[0].Text = "Туннель: " + (profile?.Name ?? "нет профиля"); icon.Text = (profile?.Name ?? "SshTunnelTray").Length > 63 ? (profile?.Name ?? "SshTunnelTray")[..63] : profile?.Name ?? "SshTunnelTray"; }
    private async Task StartAsync() { await controllerLifecycle.WaitAsync(); try { await StartCoreAsync(); } finally { controllerLifecycle.Release(); } }
    private async Task StartCoreAsync() { if (!IsProfileReady(profile)) { UpdateUi(TunnelState.Stopped, null); return; } if (profileMutex is null) { var canonical = profile!.Name.Normalize(NormalizationForm.FormC).ToUpperInvariant(); var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24]; profileMutex = new Mutex(false, "Local\\SshTunnelTray-" + key); if (!profileMutex.WaitOne(0)) { profileMutex.Dispose(); profileMutex = null; UpdateUi(TunnelState.Error, "Профиль уже запущен."); return; } } controller ??= new TunnelController(document.Settings.SshExecutablePath, Environment.ProcessPath); controller.StateChanged -= OnStateChanged; controller.StateChanged += OnStateChanged; try { runtimeError = null; await controller.StartAsync(Snapshot(profile!)); } catch (Exception ex) { runtimeError = ex.Message; ReleaseMutex(); UpdateUi(TunnelState.Error, ex.Message); } }
    private async Task StopAsync() { await controllerLifecycle.WaitAsync(); try { await StopCoreAsync(); } finally { controllerLifecycle.Release(); } }
    private async Task StopCoreAsync() { if (controller is not null) await controller.StopAsync(); ReleaseMutex(); }
    private void ReleaseMutex() { if (profileMutex is null) return; try { profileMutex.ReleaseMutex(); } catch (ApplicationException) { } profileMutex.Dispose(); profileMutex = null; }
    private void OnStateChanged(object? _, TunnelStatus e) => uiContext.Post(_ => { runtimeError = e.Error?.Message; UpdateUi(e.State, e.Error?.Message); }, null);
    private void UpdateUi(TunnelState state, string? error) { var ready = IsProfileReady(profile); var old = icon.Icon; icon.Icon = TrayIconProvider.Create(state, profile is not null, !ready); old?.Dispose(); UpdateMenu(); }
    private bool IsProfileReady(TunnelProfile? p)
    {
        if (p is null || string.IsNullOrWhiteSpace(p.Host) || p.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(p.User) ||
            string.IsNullOrWhiteSpace(p.LocalAddress) || p.LocalPort is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(p.RemoteHost) || p.RemotePort is < 1 or > 65535) return false;
        if (p.AuthMode is not (TunnelAuthMode.KeyFile or TunnelAuthMode.Password)) return false;
        if (p.AuthMode == TunnelAuthMode.KeyFile ? string.IsNullOrWhiteSpace(p.KeyPath) || !File.Exists(p.KeyPath) : p.Password is null) return false;
        return SshClientDetector.Detect(document.Settings.SshExecutablePath).Found;
    }
    private static TunnelProfile Snapshot(TunnelProfile p) => new() { Name=p.Name, Host=p.Host, Port=p.Port, User=p.User, AuthMode=p.AuthMode, KeyPath=p.KeyPath, Password=p.Password, LocalAddress=p.LocalAddress, LocalPort=p.LocalPort, RemoteHost=p.RemoteHost, RemotePort=p.RemotePort, CheckAfterSave=p.CheckAfterSave, ReconnectEnabled=p.ReconnectEnabled, ReconnectDelaySeconds=p.ReconnectDelaySeconds, KeepAliveIntervalSeconds=p.KeepAliveIntervalSeconds, KeepAliveCount=p.KeepAliveCount, ConnectTimeoutSeconds=p.ConnectTimeoutSeconds };
    protected override void ExitThreadCore() { icon.Visible = false; icon.Icon?.Dispose(); icon.Dispose(); ReleaseMutex(); if (controller is not null) controller.DisposeAsync().AsTask().GetAwaiter().GetResult(); base.ExitThreadCore(); }
}
