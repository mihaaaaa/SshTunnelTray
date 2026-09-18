using SshTunnelTray.App;
using SshTunnelTray.Config;
using SshTunnelTray.Runtime;

namespace SshTunnelTray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (AskPassBroker.TryRunHelper(args)) return;
        ApplicationConfiguration.Initialize();
        var launch = new LaunchDirectory();
        var store = new ConfigStore(launch.Path);
        var options = CommandLineOptions.Parse(args);
        using var context = new TrayApplicationContext(launch, store, options);
        Application.Run(context);
    }
}
