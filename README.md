# 代理守护 / ProxyGuard

轻量的 Windows 托盘守护程序，用于将指定软件的运行状态与本机代理客户端及代理节点的健康状态绑定。

A lightweight Windows tray watchdog that binds selected applications to the health of a local proxy client and its active proxy connection.

- [中文说明](#中文说明)
- [English Guide](#english-guide)

---

## 中文说明

### 支持的代理客户端

- FlClash
- Clash Verge
- Clash / Mihomo
- V2Ray / Xray
- sing-box
- Hiddify
- 其他提供本地 HTTP、SOCKS4 或 SOCKS5 代理端口的自定义客户端

### 主要功能

- 启动受保护软件前检查代理客户端和代理节点。
- 可选检查指定路径的代理客户端进程是否正在运行。
- 可选检查 Clash/Mihomo 兼容控制接口及密钥。
- 通过本地 HTTP、SOCKS4 或 SOCKS5 代理发起实际联网请求。
- 可选检查指定节点或策略组当前节点的延迟上限。
- 运行期间按设定间隔持续检查。
- 连续失败达到阈值后，关闭目标程序及其子进程并弹窗提示。
- 每个目标软件可单独选择代理恢复后自动重新启动。
- 支持生成“受保护启动”Windows 快捷方式。
- 按程序完整路径识别进程，降低误关同名程序的风险。

### 安装与首次设置

1. 解压压缩包，将 `ProxyGuard.exe` 放到一个不会经常移动的位置。
2. 运行 `ProxyGuard.exe`。程序将在系统托盘显示盾牌图标。
3. 选择当前使用的代理客户端类型。
4. 如需确认客户端本身正在运行，选择客户端的 `.exe` 文件并启用“要求代理客户端进程必须正在运行”。
5. 填写客户端实际监听的本地代理地址，例如：
   - `http://127.0.0.1:7890`
   - `socks4://127.0.0.1:1080`
   - `socks5://127.0.0.1:1080`
6. 保留或修改联网检测网址、超时、检查间隔和连续失败次数。
7. 添加需要保护的软件并保存设置。

端口必须以代理客户端当前设置为准，不能仅依据本说明中的示例。

### 创建受保护快捷方式

1. 打开托盘图标的“设置”。
2. 在“受保护软件”列表中选择目标程序。
3. 点击“创建受保护快捷方式…”。
4. 将快捷方式保存到桌面或开始菜单目录。
5. 后续使用新快捷方式启动目标软件。

新快捷方式会先运行代理健康检查，检查通过后才启动目标软件。如果托盘守护尚未运行，快捷方式会先启动守护程序。

为防止绕过启动前检查，建议删除或停用原快捷方式。即使从其他入口直接启动目标软件，只要 ProxyGuard 正在运行，仍会监控其运行状态。

移动或重命名 `ProxyGuard.exe` 后，旧快捷方式将失效，需要重新创建。

### 不同客户端的建议配置

#### FlClash、Clash Verge、Clash/Mihomo

建议启用“检查 Clash/Mihomo 控制接口”，并填写实际控制接口地址、密钥和本地代理地址。常见控制地址为 `http://127.0.0.1:9090`，但不同客户端和配置可能使用其他端口。

#### sing-box

如果 sing-box 配置启用了 `experimental.clash_api.external_controller`，可以启用 Clash/Mihomo 控制接口检查；否则关闭该选项，仅使用进程检查和实际代理联网检测。

#### V2Ray/Xray、Hiddify

通常应关闭 Clash/Mihomo 控制接口检查，选择客户端程序并启用进程检查，然后填写实际监听的 HTTP 或 SOCKS 代理端口。

V2Ray/Xray 的原生远程接口是 gRPC，并非 Clash REST API，因此 ProxyGuard 默认使用实际代理请求判断连接是否可用。

#### 其他自定义客户端

只要客户端提供可用的本地 HTTP、SOCKS4 或 SOCKS5 代理端口，即可使用实际联网检测。如果客户端还提供 Clash/Mihomo 兼容 API，也可以启用控制接口检查。

### 健康判定顺序

根据启用的选项，ProxyGuard 会依次执行：

1. 检查代理客户端指定路径的进程。
2. 检查 Clash/Mihomo 兼容控制接口的 `/version`。
3. 可选检查节点或策略组当前节点的延迟。
4. 通过配置的本地代理访问联网检测网址。

所有已启用的检查均通过，目标软件才会启动。

运行期间默认每 3 秒检查一次。达到设定的连续失败次数后，ProxyGuard 会结束目标程序及其子进程。连续失败机制可以减少短暂网络抖动造成的误关闭。

### 自动重启

编辑受保护软件时，可以启用“代理恢复后自动重新启动此软件”。代理恢复并通过全部检查后，ProxyGuard 会尝试重新启动该软件。

对于包含未保存文档、交易、下载、数据库操作或其他重要状态的软件，建议关闭自动重启并手动确认后再启动。

### 配置、开机启动与升级

- 配置文件：`%APPDATA%\ProxyGuard\config.json`
- “登录 Windows 后自动运行”只写入当前用户启动项，不需要管理员权限。
- 更新程序时，先退出托盘中的旧版本，再替换 `ProxyGuard.exe`。
- 如果程序路径发生变化，需要重新创建受保护快捷方式。

### 常见问题

#### 提示代理客户端未运行

确认设置中选择的是代理客户端主程序的真实路径。如果客户端更新后安装路径发生变化，请重新选择 `.exe`。

#### 控制接口检查失败

确认客户端已开启 External Controller 或 Clash API，地址、端口和密钥正确。如果当前客户端不支持 Clash API，请关闭控制接口检查。

#### 代理联网检测失败

确认代理协议和端口正确。HTTP 端口应使用 `http://`，SOCKS 端口应使用 `socks4://` 或 `socks5://`。同时确认检测网址没有被规则设置为直连或拦截。

#### 节点延迟检测失败

部分 Provider 节点不能通过单节点延迟接口直接测试。可以清空节点/策略组名称并将延迟上限设为 `0`，只保留实际代理联网检测。

#### Windows 显示“未知发布者”

当前程序没有商业代码签名证书，Windows SmartScreen 可能显示未知发布者。请仅使用可信来源获得的文件，并在运行前核对发布者提供的 SHA-256 校验值。

### 免责声明

本软件按“现状”提供，不附带任何明示或暗示的保证，包括但不限于适销性、特定用途适用性、持续可用性、检测准确性或无错误保证。

ProxyGuard 会根据用户配置检测网络状态，并可能阻止程序启动、强制结束目标程序及其子进程，或在网络恢复后自动重新启动程序。这些操作可能导致未保存数据丢失、下载或任务中断、文件损坏、会话失效、业务中断或其他直接及间接损失。使用前请保存重要数据，并确认受保护程序能够安全退出。

网络可达性和节点健康状态可能因代理规则、DNS、TUN、系统代理、透明代理、检测网址、客户端实现和瞬时网络波动而产生误判。本软件不能作为安全边界、泄漏防护、匿名性保证、访问控制、合规控制或高可用监控系统使用。

用户应自行确认代理服务、节点、订阅及相关网络使用行为符合所在地法律法规、服务条款和组织政策。开发者及分发者不提供代理服务或节点，不对用户的代理配置、网络活动、数据损失、服务中断或任何其他后果承担责任。用户使用本软件即表示理解并自行承担全部风险。

---

## English Guide

### Supported proxy clients

- FlClash
- Clash Verge
- Clash / Mihomo
- V2Ray / Xray
- sing-box
- Hiddify
- Other custom clients exposing a local HTTP, SOCKS4, or SOCKS5 proxy

### Main features

- Checks the proxy client and proxy connection before launching a protected application.
- Optionally verifies that a proxy client executable is running from the configured path.
- Optionally checks a Clash/Mihomo-compatible controller and its secret.
- Performs a real network request through a local HTTP, SOCKS4, or SOCKS5 proxy.
- Optionally enforces a latency limit for a node or the active node of a policy group.
- Continuously monitors the connection while protected applications are running.
- Terminates a target application and its child processes after the configured number of consecutive failures.
- Can automatically restart individual applications after the proxy recovers.
- Creates protected Windows shortcuts.
- Matches processes by full executable path to reduce the risk of terminating an unrelated process with the same name.

### Installation and initial setup

1. Extract the archive and place `ProxyGuard.exe` in a stable location.
2. Run `ProxyGuard.exe`. A shield icon will appear in the system tray.
3. Select the proxy client type currently in use.
4. To require the client itself to be running, select its `.exe` and enable the proxy-client process check.
5. Enter the local proxy address actually used by the client, for example:
   - `http://127.0.0.1:7890`
   - `socks4://127.0.0.1:1080`
   - `socks5://127.0.0.1:1080`
6. Keep or change the probe URL, request timeout, monitoring interval, and consecutive-failure threshold.
7. Add one or more applications to protect, then save the settings.

Always use the ports shown by your proxy client. The values in this guide are examples only.

### Creating a protected shortcut

1. Open **Settings** from the tray icon.
2. Select an application in the protected-application list.
3. Click **Create protected shortcut…**.
4. Save the shortcut to the desktop or Start menu folder.
5. Use the new shortcut to launch the application.

The shortcut runs all configured health checks before launching the target. If the tray watchdog is not running, it starts ProxyGuard first.

To prevent bypassing the pre-launch check, remove or disable the original shortcut. Applications launched through another entry point are still monitored while ProxyGuard is running.

If `ProxyGuard.exe` is moved or renamed, recreate the protected shortcuts.

### Recommended settings by client

#### FlClash, Clash Verge, and Clash/Mihomo

Enable the Clash/Mihomo controller check and enter the actual controller address, secret, and local proxy address. `http://127.0.0.1:9090` is common, but the configured port may differ.

#### sing-box

If `experimental.clash_api.external_controller` is enabled in the sing-box configuration, the Clash/Mihomo controller check can be enabled. Otherwise, disable it and use the process check plus the real proxied request.

#### V2Ray/Xray and Hiddify

Normally disable the Clash/Mihomo controller check. Select the client executable, enable the process check, and enter the actual HTTP or SOCKS listener address.

The native V2Ray/Xray remote API uses gRPC rather than the Clash REST API, so ProxyGuard relies on a real proxied request to determine connectivity.

#### Other custom clients

Any client exposing a usable local HTTP, SOCKS4, or SOCKS5 proxy can be monitored. If it also provides a Clash/Mihomo-compatible API, the controller check may be enabled.

### Health-check order

Depending on the selected options, ProxyGuard performs the following checks:

1. Verify the configured proxy-client process.
2. Query `/version` on a Clash/Mihomo-compatible controller.
3. Optionally test the configured node or the active node of a policy group.
4. Access the probe URL through the configured local proxy.

Every enabled check must pass before a protected application is launched.

During operation, the default monitoring interval is three seconds. When the consecutive-failure threshold is reached, ProxyGuard terminates the target and its child processes. Requiring multiple failures reduces false terminations caused by brief connection fluctuations.

### Automatic restart

When editing a protected application, enable **Automatically restart this application after proxy recovery** if desired. ProxyGuard will attempt to relaunch it after every configured health check passes again.

Disable automatic restart for software containing unsaved documents, financial transactions, downloads, database operations, or other important in-memory state.

### Configuration, startup, and upgrades

- Configuration file: `%APPDATA%\ProxyGuard\config.json`
- Legacy configuration: `%APPDATA%\FlClashGuard\config.json` is imported and migrated after the next save.
- **Start after Windows sign-in** creates a startup entry for the current user and does not require administrator privileges.
- Exit the previous tray instance before replacing `ProxyGuard.exe` during an upgrade.
- Recreate protected shortcuts whenever the executable path changes.

### Troubleshooting

#### Proxy client is reported as not running

Verify that the configured path points to the real client executable. Select it again if an update changed the installation directory.

#### Controller check fails

Confirm that External Controller or Clash API is enabled and that the address, port, and secret are correct. Disable the controller check if the selected client does not provide a Clash-compatible API.

#### Proxied network check fails

Verify the proxy protocol and port. Use `http://` for an HTTP listener and `socks4://` or `socks5://` for a SOCKS listener. Also confirm that the probe URL is not routed directly or blocked by client rules.

#### Node latency check fails

Some provider-backed nodes cannot be tested through the per-node delay endpoint. Clear the node or policy-group name and set the latency limit to `0` to keep only the real proxied request.

#### Windows reports an unknown publisher

The application is not currently signed with a commercial code-signing certificate, so Windows SmartScreen may report an unknown publisher. Only use files obtained from a trusted source and verify the supplied SHA-256 checksum before running them.

### Disclaimer

This software is provided **as is**, without warranties of any kind, express or implied, including warranties of merchantability, fitness for a particular purpose, continuous availability, detection accuracy, or error-free operation.

Based on user configuration, ProxyGuard may block an application from launching, forcibly terminate the application and its child processes, or automatically restart it after connectivity is restored. These actions may cause loss of unsaved data, interrupted downloads or jobs, corrupted files, expired sessions, business interruption, or other direct or indirect damage. Save important work before use and make sure each protected application can be terminated safely.

Connectivity and proxy-health checks may produce false results because of routing rules, DNS, TUN behavior, system or transparent proxies, the selected probe URL, client implementation details, or temporary network fluctuations. ProxyGuard must not be treated as a security boundary, leak-prevention mechanism, anonymity guarantee, access-control system, compliance control, or high-availability monitoring solution.

Users are solely responsible for ensuring that their proxy services, nodes, subscriptions, and network activity comply with applicable laws, service terms, and organizational policies. The developers and distributors do not provide proxy services or nodes and accept no liability for proxy configuration, network activity, data loss, service interruption, or any other consequence. By using this software, the user acknowledges and assumes all associated risks.
