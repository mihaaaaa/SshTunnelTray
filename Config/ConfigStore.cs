using System.Text.Json;
using SshTunnelTray.Domain;

namespace SshTunnelTray.Config;

public sealed record ConfigLoadResult(ConfigDocument? Document, IReadOnlyList<ConfigIssue> Errors);

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public string FilePath { get; }
    public ConfigStore(string? launchDirectory = null) => FilePath = Path.Combine(Path.GetFullPath(launchDirectory ?? Directory.GetCurrentDirectory()), "SshTunnelTray.json");
    public ConfigLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new(new ConfigDocument(), []);
        try { var d = ConfigMigrator.Migrate(JsonSerializer.Deserialize<ConfigDocument>(File.ReadAllText(FilePath), JsonOptions) ?? new()); return new(d, ConfigValidator.Validate(d)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException) { return new(null, [new("file", e.Message)]); }
    }
    public void Save(ConfigDocument document)
    {
        var errors = ConfigValidator.Validate(document); if (errors.Count != 0) throw new InvalidOperationException(string.Join("; ", errors.Select(x => $"{x.Path}: {x.Message}")));
        var temp = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        using var mutex = new Mutex(false, "Local\\SshTunnelTray.ConfigStore");
        if (!mutex.WaitOne(TimeSpan.FromSeconds(10))) throw new IOException("Не удалось захватить блокировку файла конфигурации.");
        try
        {
            var toSave = document;
            if (File.Exists(FilePath))
            {
                var current = Load();
                if (current.Document is null || current.Errors.Count != 0) throw new InvalidDataException("Актуальный файл конфигурации не прошёл проверку.");
                var merged = current.Document;
                foreach (var profile in document.Tunnels)
                {
                    var index = merged.Tunnels.FindIndex(x => string.Equals(x.Name.Normalize(), profile.Name.Normalize(), StringComparison.OrdinalIgnoreCase));
                    if (index >= 0) merged.Tunnels[index] = profile;
                    else merged.Tunnels.Add(profile);
                }
                merged.Settings = document.Settings;
                toSave = merged;
            }

            var json = JsonSerializer.Serialize(toSave, JsonOptions);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            var checkedDocument = ConfigMigrator.Migrate(JsonSerializer.Deserialize<ConfigDocument>(File.ReadAllText(temp), JsonOptions) ?? new());
            var tempErrors = ConfigValidator.Validate(checkedDocument);
            if (tempErrors.Count != 0) throw new InvalidDataException(string.Join("; ", tempErrors.Select(x => $"{x.Path}: {x.Message}")));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            mutex.ReleaseMutex();
        }
    }
    public ConfigDocument SaveProfile(TunnelProfile profile, string? originalName, AppSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var profileErrors = ConfigValidator.Validate(new ConfigDocument { Tunnels = [profile] });
        if (profileErrors.Count != 0) throw new InvalidOperationException(string.Join("; ", profileErrors.Select(x => $"{x.Path}: {x.Message}")));

        using var mutex = new Mutex(false, "Local\\SshTunnelTray.ConfigStore");
        if (!mutex.WaitOne(TimeSpan.FromSeconds(10))) throw new IOException("Не удалось захватить блокировку файла конфигурации.");
        var temp = FilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            ConfigDocument merged;
            if (File.Exists(FilePath))
            {
                var current = ConfigMigrator.Migrate(JsonSerializer.Deserialize<ConfigDocument>(File.ReadAllText(FilePath), JsonOptions) ?? new());
                var errors = ConfigValidator.Validate(current);
                if (errors.Count != 0) throw new InvalidDataException("Актуальный файл конфигурации не прошёл проверку.");
                merged = current;
            }
            else merged = new ConfigDocument();
            if (settings is not null) merged.Settings = settings;

            var conflict = merged.Tunnels.Any(x => !string.Equals(x.Name, originalName, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            if (conflict) throw new InvalidOperationException($"Профиль с именем «{profile.Name}» уже существует.");
            var index = originalName is null ? -1 : merged.Tunnels.FindIndex(x => string.Equals(x.Name, originalName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) merged.Tunnels[index] = profile;
            else if (originalName is null) merged.Tunnels.Add(profile);
            else throw new InvalidOperationException($"Профиль «{originalName}» не найден в актуальной конфигурации.");

            var finalErrors = ConfigValidator.Validate(merged);
            if (finalErrors.Count != 0) throw new InvalidOperationException(string.Join("; ", finalErrors.Select(x => $"{x.Path}: {x.Message}")));
            var json = JsonSerializer.Serialize(merged, JsonOptions);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            var checkedDocument = ConfigMigrator.Migrate(JsonSerializer.Deserialize<ConfigDocument>(File.ReadAllText(temp), JsonOptions) ?? new());
            var checkedErrors = ConfigValidator.Validate(checkedDocument);
            if (checkedErrors.Count != 0) throw new InvalidDataException(string.Join("; ", checkedErrors.Select(x => $"{x.Path}: {x.Message}")));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
            return checkedDocument;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            mutex.ReleaseMutex();
        }
    }
    public TunnelProfile? FindProfile(ConfigDocument document, string name) => document.Tunnels.FirstOrDefault(p => string.Equals(p.Name.Normalize(), name.Normalize(), StringComparison.OrdinalIgnoreCase));
    public static string[] BuildSshArguments(TunnelProfile p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.AuthMode is not (TunnelAuthMode.KeyFile or TunnelAuthMode.Password)) throw new ArgumentOutOfRangeException(nameof(p), "Unknown tunnel authentication mode.");
        var args = new List<string>
        {
            "-N", "-T", "-o", "ExitOnForwardFailure=yes",
            "-o", "StrictHostKeyChecking=yes",
            "-o", $"ServerAliveInterval={Math.Max(0, p.KeepAliveIntervalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "-o", $"ServerAliveCountMax={Math.Max(0, p.KeepAliveCount).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "-o", $"ConnectTimeout={Math.Max(1, p.ConnectTimeoutSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "-o", p.AuthMode == TunnelAuthMode.KeyFile ? "BatchMode=yes" : "BatchMode=no",
            "-p", p.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-L", $"{p.LocalAddress}:{p.LocalPort}:{p.RemoteHost}:{p.RemotePort}"
        };

        if (p.AuthMode == TunnelAuthMode.KeyFile && !string.IsNullOrWhiteSpace(p.KeyPath))
        {
            args.Add("-i");
            args.Add(p.KeyPath);
        }

        args.Add($"{p.User}@{p.Host}");
        return [.. args];
    }
}
