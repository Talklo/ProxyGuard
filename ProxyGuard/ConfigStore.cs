using System.Text.Json;
using Microsoft.Win32;

namespace ProxyGuard;

internal static class ConfigStore
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ProxyGuard";
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProxyGuard");
    private static readonly string ConfigPath = Path.Combine(DirectoryPath, "config.json");

    public static GuardConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new GuardConfig();
            return JsonSerializer.Deserialize<GuardConfig>(File.ReadAllText(ConfigPath), JsonOptions())
                   ?? new GuardConfig();
        }
        catch
        {
            return new GuardConfig();
        }
    }

    public static void Save(GuardConfig config)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions()));
        try
        {
            SetStartup(config.StartWithWindows);
        }
        catch (UnauthorizedAccessException)
        {
            config.StartWithWindows = false;
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions()));
            throw new InvalidOperationException("无法修改 Windows 启动项，请取消“自动运行”后重试。");
        }
    }

    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };

    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }
}
