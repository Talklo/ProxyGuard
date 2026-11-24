namespace ProxyGuard;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _clientType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _clientPath = new();
    private readonly CheckBox _requireClientProcess = new() { Text = "要求代理客户端进程必须正在运行" };
    private readonly CheckBox _useController = new() { Text = "检查 Clash/Mihomo 控制接口（支持 Clash、Mihomo、sing-box Clash API）" };
    private readonly TextBox _controller = new();
    private readonly TextBox _secret = new();
    private readonly TextBox _proxy = new();
    private readonly TextBox _probe = new();
    private readonly NumericUpDown _timeout = new() { Minimum = 2, Maximum = 60 };
    private readonly NumericUpDown _interval = new() { Minimum = 2, Maximum = 300 };
    private readonly NumericUpDown _failures = new() { Minimum = 1, Maximum = 20 };
    private readonly TextBox _latencyProxy = new();
    private readonly NumericUpDown _maxLatency = new() { Minimum = 0, Maximum = 10000, Increment = 50 };
    private readonly CheckBox _startup = new() { Text = "登录 Windows 后自动运行" };
    private readonly ListView _apps = new() { View = View.Details, FullRowSelect = true, CheckBoxes = true };
    private readonly List<TargetApp> _appItems;

    public GuardConfig Result { get; private set; }

    public SettingsForm(GuardConfig config)
    {
        Result = config;
        _appItems = config.Apps.Select(CloneApp).ToList();
        Text = "代理守护设置";
        Icon = SystemIcons.Shield;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 680);
        Size = new Size(820, 760);
        Font = new Font("Microsoft YaHei UI", 9F);

        _clientType.Items.AddRange(["FlClash", "Clash Verge", "Clash / Mihomo", "V2Ray / Xray", "sing-box", "Hiddify", "其他自定义客户端"]);
        _clientType.SelectedItem = _clientType.Items.Cast<string>().Contains(config.ProxyClientType)
            ? config.ProxyClientType : "其他自定义客户端";
        _clientPath.Text = config.ProxyClientExecutablePath;
        _requireClientProcess.Checked = config.RequireProxyClientProcess;
        _useController.Checked = config.UseClashController;
        _controller.Text = config.ControllerUrl;
        _secret.Text = config.ControllerSecret;
        _secret.UseSystemPasswordChar = true;
        _proxy.Text = config.ProxyUrl;
        _probe.Text = config.ProbeUrl;
        _timeout.Value = Math.Clamp(config.RequestTimeoutSeconds, 2, 60);
        _interval.Value = Math.Clamp(config.MonitorIntervalSeconds, 2, 300);
        _failures.Value = Math.Clamp(config.ConsecutiveFailures, 1, 20);
        _latencyProxy.Text = config.LatencyProxyName;
        _maxLatency.Value = Math.Clamp(config.MaxLatencyMilliseconds, 0, 10000);
        _startup.Checked = config.StartWithWindows;
        _clientType.SelectedIndexChanged += (_, _) => ApplyClientPreset();
        _useController.CheckedChanged += (_, _) => UpdateControllerFields();

        BuildUi();
        UpdateControllerFields();
        RefreshApps();
    }

    private void BuildUi()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        _requireClientProcess.AutoSize = true;
        _useController.Text = "检查 Clash/Mihomo 控制接口";
        _useController.AutoSize = true;
        _startup.AutoSize = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var proxyPage = new TabPage("代理设置") { AutoScroll = true, Padding = new Padding(8) };
        var appsPage = new TabPage("受保护软件") { Padding = new Padding(8) };
        tabs.TabPages.Add(proxyPage);
        tabs.TabPages.Add(appsPage);

        var connection = new GroupBox
        {
            Text = "代理客户端与联网检测", Dock = DockStyle.Top,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10), ColumnCount = 2
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(fields, "客户端类型", _clientType, 0);
        var clientPathPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(6, 0, 0, 0) };
        clientPathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        clientPathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _clientPath.Dock = DockStyle.Fill;
        var browseClient = new Button { Text = "浏览…", AutoSize = true, Margin = new Padding(6, 2, 0, 2) };
        browseClient.Click += (_, _) => BrowseProxyClient();
        clientPathPanel.Controls.Add(_clientPath, 0, 0);
        clientPathPanel.Controls.Add(browseClient, 1, 0);
        fields.Controls.Add(new Label { Text = "客户端程序", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        fields.Controls.Add(clientPathPanel, 1, 1);
        fields.Controls.Add(_requireClientProcess, 1, 2);
        fields.Controls.Add(_useController, 1, 3);
        AddField(fields, "控制接口地址", _controller, 4);
        AddField(fields, "控制接口密钥", _secret, 5);
        AddField(fields, "本地代理地址", _proxy, 6);
        AddField(fields, "联网检测网址", _probe, 7);
        AddField(fields, "节点/策略组（可选）", _latencyProxy, 8);

        var numbers = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        numbers.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        numbers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddField(numbers, "请求超时（秒）", _timeout, 0);
        AddField(numbers, "检查间隔（秒）", _interval, 1);
        AddField(numbers, "连续失败次数", _failures, 2);
        AddField(numbers, "延迟上限（毫秒，0=关闭）", _maxLatency, 3);
        fields.Controls.Add(numbers, 1, 9);
        fields.Controls.Add(new Label { Text = "监控参数", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
        fields.Controls.Add(new Label
        {
            Text = "代理地址示例：http://、socks4://、socks5://（端口以客户端为准）",
            AutoSize = false, Dock = DockStyle.Fill, Height = 76, ForeColor = SystemColors.GrayText
        }, 1, 10);
        fields.Controls.Add(_startup, 1, 11);
        connection.Controls.Add(fields);
        proxyPage.Controls.Add(connection);

        var appsGroup = new GroupBox { Text = "受保护软件", Dock = DockStyle.Fill, Padding = new Padding(10) };
        var appsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        appsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        appsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _apps.Dock = DockStyle.Fill;
        _apps.Columns.Add("名称", 150);
        _apps.Columns.Add("程序路径", 430);
        _apps.DoubleClick += (_, _) => EditSelected();
        _apps.Resize += (_, _) =>
        {
            if (_apps.Columns.Count > 1)
                _apps.Columns[1].Width = Math.Max(180, _apps.ClientSize.Width - _apps.Columns[0].Width - 8);
        };
        appsLayout.Controls.Add(_apps, 0, 0);
        var appButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        appButtons.Controls.Add(MakeButton("添加…", (_, _) => AddApp()));
        appButtons.Controls.Add(MakeButton("编辑…", (_, _) => EditSelected()));
        appButtons.Controls.Add(MakeButton("移除", (_, _) => RemoveSelected()));
        appButtons.Controls.Add(MakeButton("创建受保护快捷方式…", (_, _) => CreateShortcut()));
        appsLayout.Controls.Add(appButtons, 0, 1);
        appsGroup.Controls.Add(appsLayout);
        appsPage.Controls.Add(appsGroup);
        root.Controls.Add(tabs, 0, 0);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };
        var save = MakeButton("保存", (_, _) => SaveAndClose());
        save.Width = 90;
        var cancel = MakeButton("取消", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        cancel.Width = 90;
        bottom.Controls.Add(save);
        bottom.Controls.Add(cancel);
        root.Controls.Add(bottom, 0, 1);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control, int row)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(6, 4, 0, 4);
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
        button.Click += handler;
        return button;
    }

    private void AddApp()
    {
        using var dialog = new TargetAppForm(new TargetApp());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _appItems.Add(dialog.Result);
        RefreshApps();
    }

    private void BrowseProxyClient()
    {
        using var dialog = new OpenFileDialog { Filter = "Windows 程序 (*.exe)|*.exe|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _clientPath.Text = dialog.FileName;
    }

    private void ApplyClientPreset()
    {
        var type = _clientType.SelectedItem?.ToString() ?? "其他自定义客户端";
        _useController.Checked = type is "FlClash" or "Clash Verge" or "Clash / Mihomo" or "sing-box";
    }

    private void UpdateControllerFields()
    {
        _controller.Enabled = _useController.Checked;
        _secret.Enabled = _useController.Checked;
        _latencyProxy.Enabled = _useController.Checked;
        _maxLatency.Enabled = _useController.Checked;
    }

    private void EditSelected()
    {
        if (_apps.SelectedIndices.Count == 0) return;
        var index = _apps.SelectedIndices[0];
        using var dialog = new TargetAppForm(CloneApp(_appItems[index]));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _appItems[index] = dialog.Result;
        RefreshApps();
    }

    private void RemoveSelected()
    {
        if (_apps.SelectedIndices.Count == 0) return;
        _appItems.RemoveAt(_apps.SelectedIndices[0]);
        RefreshApps();
    }

    private void CreateShortcut()
    {
        if (_apps.SelectedIndices.Count == 0)
        {
            MessageBox.Show(this, "请先选择一个受保护软件。", "创建快捷方式",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var app = _appItems[_apps.SelectedIndices[0]];
        using var dialog = new SaveFileDialog
        {
            Filter = "Windows 快捷方式 (*.lnk)|*.lnk",
            FileName = $"{app}（受保护）.lnk",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            AddExtension = true,
            DefaultExt = "lnk"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ShortcutService.Create(dialog.FileName, app);
            MessageBox.Show(this, "受保护快捷方式已创建。可以删除或停用原来的快捷方式。", "创建成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"创建快捷方式失败：\n{ex.Message}", "创建失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshApps()
    {
        _apps.Items.Clear();
        foreach (var app in _appItems)
            _apps.Items.Add(new ListViewItem([app.ToString(), app.ExecutablePath]) { Checked = app.Enabled });
    }

    private void SaveAndClose()
    {
        if ((_useController.Checked && !Uri.TryCreate(_controller.Text.Trim(), UriKind.Absolute, out _)) ||
            !Uri.TryCreate(_proxy.Text.Trim(), UriKind.Absolute, out _) ||
            !Uri.TryCreate(_probe.Text.Trim(), UriKind.Absolute, out _))
        {
            MessageBox.Show(this, "请检查控制接口、代理和联网检测地址。", "地址无效",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_requireClientProcess.Checked && !File.Exists(_clientPath.Text.Trim()))
        {
            MessageBox.Show(this, "启用进程检查时，必须选择有效的代理客户端程序。", "客户端路径无效",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        for (var i = 0; i < _apps.Items.Count; i++) _appItems[i].Enabled = _apps.Items[i].Checked;
        Result = new GuardConfig
        {
            ProxyClientType = _clientType.SelectedItem?.ToString() ?? "其他自定义客户端",
            ProxyClientExecutablePath = _clientPath.Text.Trim(),
            RequireProxyClientProcess = _requireClientProcess.Checked,
            UseClashController = _useController.Checked,
            ControllerUrl = _controller.Text.Trim().TrimEnd('/'),
            ControllerSecret = _secret.Text,
            ProxyUrl = _proxy.Text.Trim(),
            ProbeUrl = _probe.Text.Trim(),
            RequestTimeoutSeconds = (int)_timeout.Value,
            MonitorIntervalSeconds = (int)_interval.Value,
            ConsecutiveFailures = (int)_failures.Value,
            LatencyProxyName = _latencyProxy.Text.Trim(),
            MaxLatencyMilliseconds = (int)_maxLatency.Value,
            StartWithWindows = _startup.Checked,
            Apps = _appItems
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static TargetApp CloneApp(TargetApp app) => new()
    {
        Name = app.Name, ExecutablePath = app.ExecutablePath,
        Arguments = app.Arguments, Enabled = app.Enabled,
        AutoRestartOnRecovery = app.AutoRestartOnRecovery
    };
}

internal sealed class TargetAppForm : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _path = new();
    private readonly TextBox _arguments = new();
    private readonly CheckBox _autoRestart = new() { Text = "代理恢复后自动重新启动此软件" };
    public TargetApp Result { get; private set; }

    public TargetAppForm(TargetApp app)
    {
        Result = app;
        Text = "受保护软件";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(620, 300);
        Size = new Size(700, 340);
        MaximizeBox = true;
        MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9F);
        _name.Text = app.Name;
        _path.Text = app.ExecutablePath;
        _arguments.Text = app.Arguments;
        _autoRestart.Checked = app.AutoRestartOnRecovery;

        _autoRestart.AutoSize = true;
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 3, RowCount = 5
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddRow(table, "显示名称", _name, 0);
        AddRow(table, "程序文件", _path, 1);
        var browse = new Button { Text = "浏览…", AutoSize = true };
        browse.Click += (_, _) => Browse();
        table.Controls.Add(browse, 2, 1);
        AddRow(table, "启动参数", _arguments, 2);
        table.Controls.Add(_autoRestart, 1, 3);
        table.SetColumnSpan(_autoRestart, 2);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft, Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        var ok = new Button { Text = "确定", AutoSize = true, MinimumSize = new Size(90, 0) };
        ok.Click += (_, _) => Save();
        var cancel = new Button { Text = "取消", AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 1, 4);
        Controls.Add(table);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, int row)
    {
        control.Dock = DockStyle.Fill;
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = "Windows 程序 (*.exe)|*.exe|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _path.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_name.Text)) _name.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private void Save()
    {
        var path = _path.Text.Trim();
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "请选择存在的程序文件。", "文件不存在",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Result = new TargetApp
        {
            Name = string.IsNullOrWhiteSpace(_name.Text) ? Path.GetFileNameWithoutExtension(path) : _name.Text.Trim(),
            ExecutablePath = path,
            Arguments = _arguments.Text.Trim(), Enabled = true,
            AutoRestartOnRecovery = _autoRestart.Checked
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
