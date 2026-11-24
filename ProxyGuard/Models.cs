namespace ProxyGuard;

internal sealed class GuardConfig
{
    public string ProxyClientType { get; set; } = "FlClash";
    public string ProxyClientExecutablePath { get; set; } = "";
    public bool RequireProxyClientProcess { get; set; }
    public bool UseClashController { get; set; } = true;
    public string ControllerUrl { get; set; } = "http://127.0.0.1:9090";
    public string ControllerSecret { get; set; } = "";
    public string ProxyUrl { get; set; } = "http://127.0.0.1:7890";
    public string ProbeUrl { get; set; } = "https://cp.cloudflare.com/generate_204";
    public int RequestTimeoutSeconds { get; set; } = 6;
    public int MonitorIntervalSeconds { get; set; } = 3;
    public int ConsecutiveFailures { get; set; } = 3;
    public string LatencyProxyName { get; set; } = "";
    public int MaxLatencyMilliseconds { get; set; }
    public bool StartWithWindows { get; set; }
    public List<TargetApp> Apps { get; set; } = [];
}

internal sealed class TargetApp
{
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool AutoRestartOnRecovery { get; set; }

    public string ProcessName => Path.GetFileNameWithoutExtension(ExecutablePath);
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? ProcessName : Name;
}

internal sealed record HealthResult(bool IsHealthy, string Message)
{
    public static HealthResult Healthy() => new(true, "代理客户端与代理节点连接正常");
    public static HealthResult Failed(string message) => new(false, message);
}
