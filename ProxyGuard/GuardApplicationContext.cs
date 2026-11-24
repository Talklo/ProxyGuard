using System.Diagnostics;

namespace ProxyGuard;

internal sealed class GuardApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly Control _dispatcher = new();
    private readonly CancellationTokenSource _shutdown = new();
    private GuardConfig _config;
    private int _failureCount;
    private bool _alertVisible;
    private bool _monitoring;
    private readonly HashSet<string> _pendingAutoRestart = new(StringComparer.OrdinalIgnoreCase);

    public GuardApplicationContext()
    {
        _dispatcher.CreateControl();
        _ = _dispatcher.Handle;
        _config = ConfigStore.Load();
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "代理守护",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
        RebuildMenu();
        _ = MonitorLoopAsync();

        if (_config.Apps.Count == 0)
            BeginInvokeOnUi(ShowSettings);
    }

    private void RebuildMenu()
    {
        var menu = new ContextMenuStrip();
        var status = new ToolStripMenuItem("正在检查连接…") { Enabled = false, Name = "Status" };
        menu.Items.Add(status);
        menu.Items.Add(new ToolStripSeparator());

        if (_config.Apps.Count == 0)
            menu.Items.Add(new ToolStripMenuItem("尚未添加受保护软件") { Enabled = false });
        else
            foreach (var app in _config.Apps.Where(a => a.Enabled))
            {
                var captured = app;
                menu.Items.Add($"启动 {app}", null, async (_, _) => await LaunchProtectedAsync(captured));
            }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("立即检测", null, async (_, _) => await CheckAndUpdateStatusAsync(showHealthy: true));
        menu.Items.Add("设置…", null, (_, _) => ShowSettings());
        menu.Items.Add("退出", null, (_, _) => Exit());
        var oldMenu = _trayIcon.ContextMenuStrip;
        _trayIcon.ContextMenuStrip = menu;
        oldMenu?.Dispose();
    }

    private async Task<bool> LaunchProtectedAsync(TargetApp app)
    {
        if (!File.Exists(app.ExecutablePath))
        {
            ShowAlert($"找不到 {app} 的程序文件：\n{app.ExecutablePath}", MessageBoxIcon.Warning);
            return false;
        }

        SetStatus("正在检查代理客户端和代理节点…");
        var health = await HealthChecker.CheckAsync(_config, _shutdown.Token);
        if (!health.IsHealthy)
        {
            SetStatus(health.Message);
            ShowAlert($"未启动 {app}。\n\n原因：{health.Message}", MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = app.ExecutablePath,
                Arguments = app.Arguments,
                WorkingDirectory = Path.GetDirectoryName(app.ExecutablePath) ?? "",
                UseShellExecute = true
            });
            SetStatus("连接正常");
            return true;
        }
        catch (Exception ex)
        {
            ShowAlert($"无法启动 {app}：\n{ex.Message}", MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task MonitorLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await CheckAndUpdateStatusAsync(showHealthy: false);
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_config.MonitorIntervalSeconds, 2, 300)),
                    _shutdown.Token);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckAndUpdateStatusAsync(bool showHealthy)
    {
        if (_monitoring) return;
        _monitoring = true;
        try
        {
            var runningApps = FindRunningApps();
            var health = await HealthChecker.CheckAsync(_config, _shutdown.Token);
            SetStatus(health.IsHealthy ? "连接正常" : health.Message);

            if (health.IsHealthy)
            {
                _failureCount = 0;
                if (showHealthy) ShowBalloon("检测通过", health.Message, ToolTipIcon.Info);
                await RestartRecoveredAppsAsync();
                return;
            }

            _failureCount++;
            if (runningApps.Count == 0 || _failureCount < Math.Clamp(_config.ConsecutiveFailures, 1, 20))
                return;

            var stopped = StopProcesses(runningApps);
            _failureCount = 0;
            if (stopped.Count > 0)
            {
                foreach (var app in stopped.Where(a => a.AutoRestartOnRecovery))
                    _pendingAutoRestart.Add(app.ExecutablePath);
                ShowAlert($"检测到代理连接中断，已关闭：\n{string.Join("、", stopped.Select(a => a.ToString()).Distinct())}\n\n原因：{health.Message}",
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _monitoring = false;
        }
    }

    private List<(TargetApp App, Process Process)> FindRunningApps()
    {
        var result = new List<(TargetApp, Process)>();
        foreach (var app in _config.Apps.Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.ProcessName)))
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(app.ProcessName))
                {
                    try
                    {
                        var runningPath = process.MainModule?.FileName;
                        if (runningPath != null && Path.GetFullPath(runningPath).Equals(
                                Path.GetFullPath(app.ExecutablePath), StringComparison.OrdinalIgnoreCase))
                            result.Add((app, process));
                        else
                            process.Dispose();
                    }
                    catch
                    {
                        process.Dispose();
                    }
                }
            }
            catch { }
        }
        return result;
    }

    private static List<TargetApp> StopProcesses(List<(TargetApp App, Process Process)> runningApps)
    {
        var stopped = new List<TargetApp>();
        foreach (var item in runningApps)
        {
            try
            {
                if (item.Process.HasExited) continue;
                item.Process.Kill(entireProcessTree: true);
                stopped.Add(item.App);
            }
            catch { }
            finally { item.Process.Dispose(); }
        }
        return stopped;
    }

    private async Task RestartRecoveredAppsAsync()
    {
        if (_pendingAutoRestart.Count == 0) return;
        var apps = _config.Apps.Where(a => _pendingAutoRestart.Contains(a.ExecutablePath)).ToList();
        foreach (var app in apps)
        {
            if (await LaunchProtectedAsync(app))
            {
                _pendingAutoRestart.Remove(app.ExecutablePath);
                ShowBalloon("代理已恢复", $"已自动重新启动 {app}", ToolTipIcon.Info);
            }
        }
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_config);
        if (form.ShowDialog() != DialogResult.OK) return;
        try
        {
            ConfigStore.Save(form.Result);
            _config = form.Result;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存设置失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        _failureCount = 0;
        RebuildMenu();
        _ = CheckAndUpdateStatusAsync(showHealthy: true);
    }

    private void SetStatus(string text)
    {
        BeginInvokeOnUi(() =>
        {
            var item = _trayIcon.ContextMenuStrip?.Items.Find("Status", false).FirstOrDefault();
            if (item != null) item.Text = text.Length > 70 ? text[..70] : text;
            _trayIcon.Text = text.Length > 60 ? text[..60] : $"代理守护 - {text}";
        });
    }

    private void ShowAlert(string message, MessageBoxIcon icon)
    {
        BeginInvokeOnUi(() =>
        {
            if (_alertVisible) return;
            _alertVisible = true;
            MessageBox.Show(message, "代理守护", MessageBoxButtons.OK, icon,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            _alertVisible = false;
        });
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon) =>
        BeginInvokeOnUi(() => _trayIcon.ShowBalloonTip(4000, title, text, icon));

    private void BeginInvokeOnUi(Action action)
    {
        if (_dispatcher.IsDisposed) return;
        _dispatcher.BeginInvoke(action);
    }

    private void Exit()
    {
        _shutdown.Cancel();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dispatcher.Dispose();
            _shutdown.Dispose();
        }
        base.Dispose(disposing);
    }
}
