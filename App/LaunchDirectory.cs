namespace SshTunnelTray.App;

public sealed class LaunchDirectory
{
    public string Path { get; } = System.IO.Path.GetFullPath(Directory.GetCurrentDirectory());
}
