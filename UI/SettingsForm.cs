using System.Diagnostics;
using SshTunnelTray.App;
using SshTunnelTray.Config;
using SshTunnelTray.Domain;
using SshTunnelTray.Runtime;
using SshTunnelTray.Tray;

namespace SshTunnelTray.UI;

public sealed class SettingsForm : Form
{
    private readonly ConfigStore store; private readonly string directory; private readonly ConfigDocument initialDocument;
    private readonly ComboBox profile = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox name = new(), host = new(), user = new(), localAddress = new(), remoteHost = new(), keyPath = new(), password = new(), sshPath = new();
    private readonly NumericUpDown sshPort = Number(22), localPort = Number(10022), remotePort = Number(22);
    private readonly RadioButton keyMode = new() { Text = "Файл ключа", AutoSize = true }, passwordMode = new() { Text = "Пароль", AutoSize = true };
    private readonly CheckBox checkAfterSave = new() { Text = "Запускать туннель после сохранения", AutoSize = true, Checked = true };
    private readonly CheckBox reconnect = new() { Text = "Переподключаться автоматически", AutoSize = true };
    private const string SshDownloadUrl = "https://github.com/PowerShell/Win32-OpenSSH/releases";
    private readonly Label sshStatus = new() { AutoSize = true }; private readonly LinkLabel sshLink = new() { Text = SshDownloadUrl, AutoSize = true, Visible = false, LinkArea = new LinkArea(0, SshDownloadUrl.Length), LinkColor = Color.FromArgb(37, 99, 235) }; private readonly Button copySshLink = new() { Text = "Копировать", AutoSize = true, Visible = false, FlatStyle = FlatStyle.Flat };
    private readonly TextBox diagnostics = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 70, Dock = DockStyle.Fill }; private bool loading; private string? originalName;
    private readonly Button browseKey = new() { Text = "…", Width = 34 };
    private readonly Button languageButton = new() { Width = 90, Height = 36, BackColor = Color.White, FlatStyle = FlatStyle.Flat };
    private UiLanguage language;
    public ConfigDocument Document { get; private set; } public TunnelProfile Profile { get; private set; } public bool StartAfterSave { get; private set; }
    public SettingsForm(ConfigDocument document, TunnelProfile? selected, ConfigStore configStore, string launchDirectory, string? error, Icon? applicationIcon = null)
    {
        initialDocument = document; Document = document; store = configStore; directory = launchDirectory; language = Localization.Normalize(document.Settings.Language); Profile = Clone(selected ?? new TunnelProfile { Name = Localization.NewProfile(language), LocalPort = 10022, RemotePort = 22 }); originalName = selected?.Name; password.UseSystemPasswordChar = true;
        Icon = applicationIcon is null ? TrayIconProvider.Create(TunnelState.Stopped, selected is not null, selected is null) : (Icon)applicationIcon.Clone(); ShowIcon = true;
        Text = Localization.Text("SshTunnelTray — настройки", language); StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(760, 560); MinimumSize = new Size(680, 520); BuildUi(); diagnostics.Text = error ?? ""; LoadProfileList(); LoadFields(Profile); RefreshSshStatus(); ApplyLanguage();
    }
    private void BuildUi()
    {
        BackColor = Color.FromArgb(244, 247, 250); AutoScroll = true; Font = new Font("Segoe UI", 9.5F);
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16, 16, 16, 8), BackColor = BackColor };
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, RowCount = 1, Height = 32, Margin = new Padding(0, 0, 0, 16) }; top.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.Controls.Add(new Label { Text = "Туннель", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(100, 116, 139), Padding = new Padding(0, 0, 8, 0) }, 0, 0); profile.Dock = DockStyle.Fill; top.Controls.Add(profile, 1, 0); var newButton = new Button { Text = "Новый", Dock = DockStyle.Fill, MinimumSize = new Size(0, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat }; newButton.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 232); top.Controls.Add(newButton, 2, 0); t.Controls.Add(top, 0, 0);
        var panels = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 16) }; panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); panels.Controls.Add(BuildConnection(), 0, 0); panels.Controls.Add(BuildForward(), 1, 0); t.Controls.Add(panels, 0, 1);
        t.Controls.Add(BuildSshClient(), 0, 2);
        diagnostics.BackColor = Color.White;
        var dg = new GroupBox { Text = "Ошибки и диагностика", Dock = DockStyle.Fill, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 8), BackColor = Color.White, ForeColor = Color.FromArgb(31, 41, 55), Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; dg.Controls.Add(diagnostics); t.Controls.Add(dg, 0, 3);
        var save = new Button { Text = "Сохранить", Width = 140, Height = 36, Margin = new Padding(4, 2, 0, 2), BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; save.FlatAppearance.BorderSize = 0; save.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216); var close = new Button { Text = "Закрыть", Width = 140, Height = 36, Margin = new Padding(4, 2, 0, 2), DialogResult = DialogResult.Cancel, BackColor = Color.White, FlatStyle = FlatStyle.Flat }; close.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 232); languageButton.Margin = new Padding(0, 2, 0, 2); languageButton.Anchor = AnchorStyles.Left | AnchorStyles.Top; languageButton.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 232); languageButton.Click += (_, _) => { language = Localization.Toggle(language); ApplyLanguage(); }; var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) }; actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = false, Margin = new Padding(0), Padding = new Padding(0) }; right.Controls.Add(close); right.Controls.Add(save); actions.Controls.Add(languageButton, 0, 0); actions.Controls.Add(right, 1, 0); t.Controls.Add(actions, 0, 4);
        profile.SelectedIndexChanged += (_, _) => SelectProfile(); newButton.Click += (_, _) => profile.SelectedIndex = profile.Items.Count - 1; keyMode.CheckedChanged += (_, _) => AuthModeChanged(); passwordMode.CheckedChanged += (_, _) => AuthModeChanged(); browseKey.Click += (_, _) => BrowseKey(); sshPath.TextChanged += (_, _) => RefreshSshStatus(); sshLink.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(SshDownloadUrl) { UseShellExecute = true }); copySshLink.Click += (_, _) => Clipboard.SetText(SshDownloadUrl); save.Click += (_, _) => Save(); CancelButton = close;
        Controls.Add(t); AcceptButton = save; return;
    }
    private void ApplyLanguage()
    {
        Text = Localization.Text("SshTunnelTray — настройки", language);
        languageButton.Text = language == UiLanguage.En ? "Русский" : "English";
        foreach (Control c in Controls) TranslateControls(c);
        if (originalName is null)
        {
            var oldNewName = Localization.NewProfile(Localization.Toggle(language));
            for (var i = 0; i < profile.Items.Count; i++) if (Equals(profile.Items[i], oldNewName)) profile.Items[i] = Localization.NewProfile(language);
            if (string.Equals(name.Text, oldNewName, StringComparison.Ordinal)) name.Text = Localization.NewProfile(language);
            if (string.Equals(Profile.Name, oldNewName, StringComparison.Ordinal)) Profile.Name = Localization.NewProfile(language);
        }
        RefreshSshStatus();
    }
    private void TranslateControls(Control c)
    {
        if (c != languageButton && c is not TextBox && c is not ComboBox && c.Text.Length > 0)
        {
            c.Tag ??= c.Text;
            c.Text = Localization.Text((string)c.Tag, language);
        }
        foreach (Control child in c.Controls) TranslateControls(child);
    }
    private GroupBox BuildConnection() { var b = new GroupBox { Text = "Подключение", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12), Margin = new Padding(0, 0, 8, 0), BackColor = Color.White, ForeColor = Color.FromArgb(31, 41, 55), Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; var x = PanelTable(); Add(x, "Имя туннеля", name); Add(x, "SSH-сервер", host); Add(x, "SSH-порт", sshPort); Add(x, "Пользователь", user); var m = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false }; m.Controls.Add(passwordMode); m.Controls.Add(keyMode); Add(x, "Аутентификация", m); var keyRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true }; keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38)); keyPath.Dock = DockStyle.Fill; browseKey.Dock = DockStyle.None; browseKey.Size = new Size(34, 24); browseKey.Anchor = AnchorStyles.Left; keyRow.Controls.Add(keyPath, 0, 0); keyRow.Controls.Add(browseKey, 1, 0); Add(x, "Путь к ключу", keyRow); Add(x, "Пароль", password); b.Controls.Add(x); return b; }
    private GroupBox BuildForward() { var b = new GroupBox { Text = "Проброс порта", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12), Margin = new Padding(8, 0, 0, 0), BackColor = Color.White, ForeColor = Color.FromArgb(31, 41, 55), Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; var x = PanelTable(); Add(x, "Локальный адрес", localAddress); Add(x, "Локальный порт", localPort); Add(x, "Удалённый адрес", remoteHost); Add(x, "Удалённый порт", remotePort); AddFullWidth(x, checkAfterSave); AddFullWidth(x, reconnect); b.Controls.Add(x); return b; }
    private GroupBox BuildSshClient()
    {
        var b = new GroupBox { Text = "SSH-клиент", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 16), BackColor = Color.White, ForeColor = Color.FromArgb(31, 41, 55), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 2, AutoSize = true, BackColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Regular) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sshStatus.Padding = new Padding(0, 2, 0, 2);
        content.Controls.Add(sshStatus, 0, 0);
        var download = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
        download.Controls.Add(sshLink);
        download.Controls.Add(copySshLink);
        content.Controls.Add(download, 0, 1);
        b.Controls.Add(content);
        return b;
    }
    private static TableLayoutPanel PanelTable() { var t = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(0), BackColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Regular) }; t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); return t; }
    private static void Add(TableLayoutPanel t, string label, Control c) { var r = t.RowCount++; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(100, 116, 139), Padding = new Padding(0, 6, 8, 0) }, 0, r); c.Anchor = AnchorStyles.Left | AnchorStyles.Right; t.Controls.Add(c, 1, r); }
    private static void AddFullWidth(TableLayoutPanel t, Control c) { var r = t.RowCount++; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); c.Anchor = AnchorStyles.Left; t.Controls.Add(c, 0, r); t.SetColumnSpan(c, 2); }
    private static NumericUpDown Number(int value) => new() { Minimum = 1, Maximum = 65535, Value = value, Width = 120 };
    private void BrowseKey() { using var d = new OpenFileDialog { Filter = "All files|*.*", CheckFileExists = true }; if (d.ShowDialog(this) == DialogResult.OK) keyPath.Text = d.FileName; }
    private void LoadProfileList() { loading = true; profile.Items.Clear(); foreach (var p in initialDocument.Tunnels) profile.Items.Add(p.Name); profile.Items.Add(Localization.NewProfile(language)); profile.SelectedIndex = originalName is null ? profile.Items.Count - 1 : Math.Max(0, initialDocument.Tunnels.FindIndex(p => p.Name == originalName)); loading = false; }
    private void SelectProfile() { if (loading || profile.SelectedIndex < 0) return; var isNew = profile.SelectedIndex >= initialDocument.Tunnels.Count; var p = isNew ? new TunnelProfile { Name = Localization.NewProfile(language), LocalPort = 10022, RemotePort = 22 } : initialDocument.Tunnels[profile.SelectedIndex]; originalName = isNew ? null : p.Name; Profile = Clone(p); LoadFields(Profile); }
    private void LoadFields(TunnelProfile p) { loading = true; name.Text = p.Name; host.Text = p.Host; sshPort.Value = Math.Clamp(p.Port, 1, 65535); user.Text = p.User; localAddress.Text = p.LocalAddress; localPort.Value = Math.Clamp(p.LocalPort, 1, 65535); remoteHost.Text = p.RemoteHost; remotePort.Value = Math.Clamp(p.RemotePort, 1, 65535); keyPath.Text = p.KeyPath ?? ""; sshPath.Text = initialDocument.Settings.SshExecutablePath ?? ""; password.Clear(); keyMode.Checked = p.AuthMode == TunnelAuthMode.KeyFile; passwordMode.Checked = p.AuthMode == TunnelAuthMode.Password; checkAfterSave.Checked = p.CheckAfterSave; reconnect.Checked = p.ReconnectEnabled; loading = false; AuthModeChanged(); }
    private void AuthModeChanged() { if (loading) return; keyPath.Enabled = keyMode.Checked; browseKey.Enabled = keyMode.Checked; password.Enabled = passwordMode.Checked; if (keyMode.Checked) password.Clear(); }
    private void RefreshSshStatus() { var result = SshClientDetector.Detect(sshPath.Text); var prefix = language == UiLanguage.En ? "Installed: " : "Установлен: "; sshStatus.Text = result.Found ? prefix + result.Path : (language == UiLanguage.En ? "Not found. Download an SSH client:" : "Не найден. Скачайте SSH-клиент:"); sshStatus.ForeColor = result.Found ? Color.DarkGreen : Color.Maroon; sshLink.Visible = !result.Found; copySshLink.Visible = !result.Found; }
    private void Save()
    {
        try
        {
            var edited = Clone(Profile);
            edited.Name = name.Text.Trim();
            edited.Host = host.Text.Trim();
            edited.Port = (int)sshPort.Value;
            edited.User = user.Text.Trim();
            edited.LocalAddress = localAddress.Text.Trim();
            edited.LocalPort = (int)localPort.Value;
            edited.RemoteHost = remoteHost.Text.Trim();
            edited.RemotePort = (int)remotePort.Value;
            edited.AuthMode = keyMode.Checked ? TunnelAuthMode.KeyFile : TunnelAuthMode.Password;
            edited.KeyPath = keyMode.Checked ? keyPath.Text.Trim() : null;
            edited.Password = passwordMode.Checked && string.IsNullOrEmpty(password.Text)
                ? Profile.Password
                : passwordMode.Checked ? SecretBox.Encrypt(password.Text) : null;
            edited.CheckAfterSave = checkAfterSave.Checked;
            edited.ReconnectEnabled = reconnect.Checked;

            var settings = new AppSettings
            {
                SshExecutablePath = string.IsNullOrWhiteSpace(sshPath.Text) ? null : sshPath.Text.Trim(),
                Language = Localization.Code(language)
            };
            var saved = store.SaveProfile(edited, originalName, settings);
            CommandFileWriter.Write(directory, edited);
            Document = saved;
            Profile = edited;
            StartAfterSave = checkAfterSave.Checked;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            diagnostics.Text = ex.ToString();
        }
    }
    private static TunnelProfile Clone(TunnelProfile p) => new() { Name = p.Name, Host = p.Host, Port = p.Port, User = p.User, AuthMode = p.AuthMode, KeyPath = p.KeyPath, Password = p.Password, LocalAddress = p.LocalAddress, LocalPort = p.LocalPort, RemoteHost = p.RemoteHost, RemotePort = p.RemotePort, CheckAfterSave = p.CheckAfterSave, ReconnectEnabled = p.ReconnectEnabled, ReconnectDelaySeconds = p.ReconnectDelaySeconds, KeepAliveIntervalSeconds = p.KeepAliveIntervalSeconds, KeepAliveCount = p.KeepAliveCount, ConnectTimeoutSeconds = p.ConnectTimeoutSeconds };
}
