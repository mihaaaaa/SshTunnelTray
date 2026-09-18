namespace SshTunnelTray.UI;

public enum UiLanguage { Ru, En }

public static class Localization
{
    public static UiLanguage Normalize(string? value) => string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? UiLanguage.En : UiLanguage.Ru;
    public static string Code(UiLanguage language) => language == UiLanguage.En ? "en" : "ru";
    public static string NewProfile(UiLanguage language) => language == UiLanguage.En ? "New profile" : "Новый профиль";
    public static UiLanguage Toggle(UiLanguage language) => language == UiLanguage.En ? UiLanguage.Ru : UiLanguage.En;
    public static string Text(string value, UiLanguage language)
    {
        if (language == UiLanguage.Ru) return value;
        return value switch
        {
            "SshTunnelTray — настройки" => "SshTunnelTray — Settings", "Туннель" => "Tunnel", "Новый" => "New", "Подключение" => "Connection", "Проброс порта" => "Port forwarding", "SSH-клиент" => "SSH client", "Ошибки и диагностика" => "Errors and diagnostics", "Сохранить" => "Save", "Закрыть" => "Close", "Имя туннеля" => "Tunnel name", "SSH-сервер" => "SSH server", "SSH-порт" => "SSH port", "Пользователь" => "User", "Аутентификация" => "Authentication", "Файл ключа" => "Key file", "Пароль" => "Password", "Путь к ключу" => "Key path", "Локальный адрес" => "Local address", "Локальный порт" => "Local port", "Удалённый адрес" => "Remote address", "Удалённый порт" => "Remote port", "Запускать туннель после сохранения" => "Start tunnel after saving", "Переподключаться автоматически" => "Reconnect automatically", "Копировать" => "Copy", "Установлен: " => "Installed: ", "Не найден. Скачайте SSH-клиент:" => "Not found. Download an SSH client:", "Включить" => "Enable", "Выключить" => "Disable", "Настройки" => "Settings", "Выйти" => "Exit", "Туннель: " => "Tunnel: ", "нет профиля" => "no profile", "Профиль «" => "Profile ‘", "» не найден." => "’ not found.", "Профиль уже запущен." => "Profile is already running.", _ => value
        };
    }
}
