using System.Diagnostics;

namespace SshTunnelTray.Runtime;

public sealed record SshClientStatus(bool Found, string? Path, string? Version)
{
    public string Text => Found ? $"SSH-клиент найден: {Path}{(string.IsNullOrWhiteSpace(Version) ? "" : $" ({Version})")}" : "SSH-клиент не найден.";
}

public static class SshClientDetector
{
    public static SshClientStatus Detect(string? configuredPath)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredPath) ? FindOnPath() : configuredPath.Trim();
        if (candidate is null || !File.Exists(candidate)) return new(false, null, null);
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(candidate, "-V") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            if (!process.Start()) return new(false, null, null);
            var output = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(2000)) { try { process.Kill(); } catch (InvalidOperationException) { } return new(false, null, null); }
            if (process.ExitCode != 0) return new(false, null, null);
            var version = string.Join(" ", new[] { output.GetAwaiter().GetResult(), stdout.GetAwaiter().GetResult() }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            return new(true, Path.GetFullPath(candidate), string.IsNullOrWhiteSpace(version) ? null : version);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception) { return new(false, null, null); }
    }

    private static string? FindOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim().Trim('"'), "ssh.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
