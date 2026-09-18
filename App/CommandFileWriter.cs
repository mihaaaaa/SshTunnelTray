using System.Text;
using SshTunnelTray.Domain;

namespace SshTunnelTray.App;

public static class CommandFileWriter
{
    private const string Marker = "REM SshTunnelTray generated command file";
    public static string Write(string directory, TunnelProfile profile)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, profile.Name + ".cmd");
        if (File.Exists(path) && !File.ReadAllText(path).Contains(Marker, StringComparison.Ordinal))
            throw new IOException($"Файл CMD уже существует и не помечен SshTunnelTray: {path}");
        var text = $"{Marker}\r\n@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" /b SshTunnelTray.exe -t \"{profile.Name.Replace("\"", "") }\"\r\n";
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return path;
    }
}
