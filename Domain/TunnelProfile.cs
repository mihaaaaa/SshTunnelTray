using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SshTunnelTray.Domain;

public enum TunnelAuthMode { KeyFile, Password }

public sealed class TunnelProfile
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "";
    public TunnelAuthMode AuthMode { get; set; } = TunnelAuthMode.KeyFile;
    public string? KeyPath { get; set; }
    public Config.EncryptedSecret? Password { get; set; }
    public string LocalAddress { get; set; } = "127.0.0.1";
    public int LocalPort { get; set; }
    public int RemotePort { get; set; }
    public string RemoteHost { get; set; } = "127.0.0.1";
    public bool CheckAfterSave { get; set; } = true;
    public bool ReconnectEnabled { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int KeepAliveIntervalSeconds { get; set; } = 30;
    public int KeepAliveCount { get; set; } = 3;
    public int ConnectTimeoutSeconds { get; set; } = 15;
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    public static string NormalizeName(string value) => value.Normalize(NormalizationForm.FormC);
}
