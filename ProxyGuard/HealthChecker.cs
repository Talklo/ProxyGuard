using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;

namespace ProxyGuard;

internal static class HealthChecker
{
    public static async Task<HealthResult> CheckAsync(GuardConfig config, CancellationToken cancellationToken)
    {
        var processResult = CheckProxyClientProcess(config);
        if (processResult is not null) return processResult;
        if (!Uri.TryCreate(config.ProxyUrl, UriKind.Absolute, out var proxyUri))
            return HealthResult.Failed("代理地址无效");
        if (!Uri.TryCreate(config.ProbeUrl, UriKind.Absolute, out var probeUri))
            return HealthResult.Failed("联网检测地址无效");

        var timeout = TimeSpan.FromSeconds(Math.Clamp(config.RequestTimeoutSeconds, 2, 60));

        if (config.UseClashController)
        {
            if (!Uri.TryCreate(config.ControllerUrl, UriKind.Absolute, out var controllerUri))
                return HealthResult.Failed("Clash/Mihomo 控制接口地址无效");
            try
            {
                using var controllerHandler = new HttpClientHandler { UseProxy = false };
                using var controllerClient = new HttpClient(controllerHandler) { Timeout = timeout };
                if (!string.IsNullOrWhiteSpace(config.ControllerSecret))
                    controllerClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", config.ControllerSecret);

                var versionUrl = new Uri(controllerUri, "/version");
                using var response = await controllerClient.GetAsync(versionUrl, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return HealthResult.Failed("Clash/Mihomo 控制接口密钥不正确");
                if (!response.IsSuccessStatusCode)
                    return HealthResult.Failed($"代理核心控制接口未就绪（{(int)response.StatusCode}）");

                var latencyResult = await CheckLatencyAsync(config, controllerUri, controllerClient, cancellationToken);
                if (latencyResult is not null) return latencyResult;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return HealthResult.Failed("连接 Clash/Mihomo 控制接口超时");
            }
            catch (Exception)
            {
                return HealthResult.Failed("代理客户端未运行，或 Clash/Mihomo 控制接口未开启");
            }
        }

        try
        {
            using var proxyHandler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxyUri),
                UseProxy = true,
                AllowAutoRedirect = true
            };
            using var proxyClient = new HttpClient(proxyHandler) { Timeout = timeout };
            proxyClient.DefaultRequestHeaders.UserAgent.ParseAdd("ProxyGuard/1.0");
            using var response = await proxyClient.GetAsync(probeUri, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return HealthResult.Failed($"代理节点检测失败（HTTP {(int)response.StatusCode}）");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthResult.Failed("代理节点连接超时");
        }
        catch (Exception ex)
        {
            return HealthResult.Failed($"代理节点不可用：{FriendlyMessage(ex)}");
        }

        return HealthResult.Healthy();
    }

    private static HealthResult? CheckProxyClientProcess(GuardConfig config)
    {
        if (!config.RequireProxyClientProcess) return null;
        var expectedPath = config.ProxyClientExecutablePath.Trim();
        if (expectedPath.Length == 0 || !File.Exists(expectedPath))
            return HealthResult.Failed("代理客户端程序路径未设置或文件不存在");

        var processName = Path.GetFileNameWithoutExtension(expectedPath);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var actualPath = process.MainModule?.FileName;
                if (actualPath != null && Path.GetFullPath(actualPath).Equals(
                        Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase))
                    return null;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return HealthResult.Failed($"{config.ProxyClientType} 客户端未运行");
    }

    private static async Task<HealthResult?> CheckLatencyAsync(GuardConfig config, Uri controllerUri,
        HttpClient controllerClient, CancellationToken cancellationToken)
    {
        var configuredName = config.LatencyProxyName.Trim();
        if (configuredName.Length == 0 || config.MaxLatencyMilliseconds <= 0) return null;

        var proxyName = configuredName;
        var proxyInfoUrl = new Uri(controllerUri, $"/proxies/{Uri.EscapeDataString(configuredName)}");
        using (var infoResponse = await controllerClient.GetAsync(proxyInfoUrl, cancellationToken))
        {
            if (!infoResponse.IsSuccessStatusCode)
                return HealthResult.Failed($"找不到延迟检测节点或策略组“{configuredName}”");
            using var infoJson = JsonDocument.Parse(await infoResponse.Content.ReadAsStreamAsync(cancellationToken));
            if (infoJson.RootElement.TryGetProperty("now", out var now) && now.ValueKind == JsonValueKind.String)
                proxyName = now.GetString() ?? configuredName;
        }

        var delayUrl = new Uri(controllerUri,
            $"/proxies/{Uri.EscapeDataString(proxyName)}/delay?url={Uri.EscapeDataString(config.ProbeUrl)}&timeout={Math.Clamp(config.RequestTimeoutSeconds, 2, 60) * 1000}&expected=200-299");
        using var delayResponse = await controllerClient.GetAsync(delayUrl, cancellationToken);
        if (!delayResponse.IsSuccessStatusCode)
            return HealthResult.Failed($"节点“{proxyName}”延迟检测失败");

        using var delayJson = JsonDocument.Parse(await delayResponse.Content.ReadAsStreamAsync(cancellationToken));
        if (!delayJson.RootElement.TryGetProperty("delay", out var delayElement) ||
            !delayElement.TryGetInt32(out var delay))
            return HealthResult.Failed($"节点“{proxyName}”没有返回有效延迟");
        if (delay > config.MaxLatencyMilliseconds)
            return HealthResult.Failed($"节点“{proxyName}”延迟 {delay} ms，超过上限 {config.MaxLatencyMilliseconds} ms");
        return null;
    }

    private static string FriendlyMessage(Exception ex) => ex.InnerException?.Message ?? ex.Message;
}
