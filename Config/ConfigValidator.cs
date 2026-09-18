using System.Text.RegularExpressions;
using SshTunnelTray.Domain;

namespace SshTunnelTray.Config;

public sealed record ConfigIssue(string Path, string Message);

public static class ConfigValidator
{
    private static readonly Regex Reserved = new("^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\\..*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static IReadOnlyList<ConfigIssue> Validate(ConfigDocument document)
    {
        var issues = new List<ConfigIssue>();
        if (document.Schema != ConfigDocument.CurrentSchema) issues.Add(new("schema", "Unknown schema."));
        if (document.Version != ConfigDocument.CurrentVersion) issues.Add(new("version", "Unsupported version."));
        if (document.Extra?.Count > 0) issues.Add(new("$", "Unknown fields: " + string.Join(", ", document.Extra.Keys)));
        if (document.Settings.Extra?.Count > 0) issues.Add(new("settings", "Unknown fields: " + string.Join(", ", document.Settings.Extra.Keys)));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < document.Tunnels.Count; i++) ValidateProfile(document.Tunnels[i], $"tunnels[{i}]", names, issues);
        return issues;
    }

    public static IReadOnlyList<ConfigIssue> ValidateProfile(TunnelProfile p) {
        var issues = new List<ConfigIssue>(); ValidateProfile(p, "profile", new(StringComparer.OrdinalIgnoreCase), issues); return issues;
    }
    private static void ValidateProfile(TunnelProfile p, string path, HashSet<string> names, List<ConfigIssue> issues)
    {
        var n = p.Name.Normalize(System.Text.NormalizationForm.FormC);
        if (string.IsNullOrWhiteSpace(n) || n != p.Name || n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || n.Contains('/') || n.Contains('\\') || n.EndsWith(' ') || n.EndsWith('.') || Reserved.IsMatch(n)) issues.Add(new(path + ".name", "Invalid Windows profile name; use NFC normalized unique name."));
        if (!names.Add(n)) issues.Add(new(path + ".name", "Duplicate profile name."));
        if (string.IsNullOrWhiteSpace(p.Host) || p.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(p.User)) issues.Add(new(path, "SSH host, user and port are required."));
        if (p.LocalPort is < 1 or > 65535 || p.RemotePort is < 1 or > 65535 || string.IsNullOrWhiteSpace(p.RemoteHost)) issues.Add(new(path, "Forward endpoint is invalid."));
        if (p.AuthMode is not (TunnelAuthMode.KeyFile or TunnelAuthMode.Password)) issues.Add(new(path + ".authMode", "Unknown tunnel authentication mode."));
        else if (p.AuthMode == TunnelAuthMode.KeyFile && string.IsNullOrWhiteSpace(p.KeyPath)) issues.Add(new(path + ".keyPath", "KeyFile authentication requires keyPath."));
        else if (p.AuthMode == TunnelAuthMode.Password && p.Password is null) issues.Add(new(path + ".password", "Password authentication requires encrypted password."));
        if (p.Extra?.Count > 0) issues.Add(new(path, "Unknown fields: " + string.Join(", ", p.Extra.Keys)));
        if (string.IsNullOrWhiteSpace(p.LocalAddress) || p.ReconnectDelaySeconds < 0 || p.KeepAliveIntervalSeconds < 0 || p.KeepAliveCount < 0 || p.ConnectTimeoutSeconds <= 0) issues.Add(new(path, "Connection options are invalid."));
    }
}
