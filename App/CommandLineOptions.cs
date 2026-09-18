namespace SshTunnelTray.App;

public sealed record CommandLineOptions(bool StartMinimized = false, string? ProfileName = null, string? Error = null)
{
    public static CommandLineOptions Parse(IEnumerable<string> args)
    {
        var a = args.ToArray(); var minimized = false; string? name = null;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].Equals("--minimized", StringComparison.OrdinalIgnoreCase)) { minimized = true; continue; }
            if (a[i] != "-t") return new(minimized, null, $"Неизвестный аргумент '{a[i]}'. Ожидалось -t <name>.");
            if (name is not null || i + 1 >= a.Length || string.IsNullOrWhiteSpace(a[++i]) || a[i].StartsWith('-')) return new(minimized, null, "Аргумент -t должен иметь непустое имя и встречаться один раз.");
            name = a[i];
        }
        return new(minimized, name);
    }
}
