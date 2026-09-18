using System.Text.Json.Serialization;
using SshTunnelTray.Domain;

namespace SshTunnelTray.Config;

public sealed class ConfigDocument
{
    public const string CurrentSchema = "ssh-tunnel-tray.config";
    public const int CurrentVersion = 1;
    public string Schema { get; set; } = CurrentSchema;
    public int Version { get; set; } = CurrentVersion;
    public List<TunnelProfile> Tunnels { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
    [JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
}

public sealed class AppSettings
{
    public string? SshExecutablePath { get; set; }
    [JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
}
