using System.Diagnostics;

namespace ProxyGuard;

internal static class LauncherMode
{
    public static async Task RunAsync(string executablePath)
    {
        var config = ConfigStore.Load();
        var app = config.Apps.FirstOrDefault(a => Path.GetFullPath(a.ExecutablePath).Equals(
            Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase));
        if (app is null || !app.Enabled)
        {
            MessageBox.Show("这个快捷方式对应的软件未在代理守护中启用。", "代理守护",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EnsureTrayGuardRunning();
        var health = await HealthChecker.CheckAsync(config, CancellationToken.None);
        if (!health.IsHealthy)
        {
            MessageBox.Show($"未启动 {app}。\n\n原因：{health.Message}", "代理守护",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
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
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法启动 {app}：\n{ex.Message}", "代理守护",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void EnsureTrayGuardRunning()
    {
        var currentId = Environment.ProcessId;
        using var current = Process.GetCurrentProcess();
        var alreadyRunning = Process.GetProcessesByName(current.ProcessName).Any(p =>
        {
            try { return p.Id != currentId; }
            finally { p.Dispose(); }
        });
        if (alreadyRunning) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            Arguments = "--background",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }
}
