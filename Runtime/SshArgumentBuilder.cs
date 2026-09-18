using System.Globalization;
using System.Text;
using SshTunnelTray.Domain;

namespace SshTunnelTray.Runtime;

public sealed record SshLaunchSpec(string ExecutablePath, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string?> Environment);

public static class SshArgumentBuilder
{
    public static SshLaunchSpec Build(TunnelProfile profile, string? executablePath = null, string? askPassExecutablePath = null, string? askPassPipeName = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.AuthMode is not (TunnelAuthMode.KeyFile or TunnelAuthMode.Password)) throw new ArgumentOutOfRangeException(nameof(profile), "Unknown tunnel authentication mode.");
        var args = new List<string>
        {
            "-N", "-T", "-o", "ExitOnForwardFailure=yes",
            "-o", "StrictHostKeyChecking=yes",
            "-o", $"ServerAliveInterval={Math.Max(0, profile.KeepAliveIntervalSeconds).ToString(CultureInfo.InvariantCulture)}",
            "-o", $"ServerAliveCountMax={Math.Max(0, profile.KeepAliveCount).ToString(CultureInfo.InvariantCulture)}",
            "-o", $"ConnectTimeout={Math.Max(1, profile.ConnectTimeoutSeconds).ToString(CultureInfo.InvariantCulture)}",
            "-p", profile.Port.ToString(CultureInfo.InvariantCulture),
            "-L", $"{profile.LocalAddress}:{profile.LocalPort}:{profile.RemoteHost}:{profile.RemotePort}"
        };

        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (profile.AuthMode == TunnelAuthMode.KeyFile)
        {
            if (!string.IsNullOrWhiteSpace(profile.KeyPath)) { args.Add("-i"); args.Add(profile.KeyPath); }
            args.Insert(2, "BatchMode=yes"); args.Insert(2, "-o");
        }
        else
        {
            // The secret is deliberately not accepted by this builder and never enters argv.
            args.Insert(2, "BatchMode=no"); args.Insert(2, "-o");
            if (!string.IsNullOrWhiteSpace(askPassExecutablePath)) environment["SSH_ASKPASS"] = askPassExecutablePath;
            if (!string.IsNullOrWhiteSpace(askPassPipeName)) environment["SSH_ASKPASS_REQUIRE"] = "force";
            if (!string.IsNullOrWhiteSpace(askPassPipeName)) environment[AskPassBroker.PipeEnvironmentVariable] = askPassPipeName;
        }

        args.Add($"{profile.User}@{profile.Host}");
        return new(executablePath ?? "ssh.exe", args, environment);
    }
}
