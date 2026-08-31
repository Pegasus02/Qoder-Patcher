# Changelog

## 3.2.2 - 2026-09-01

- **修复 Windows 原生客户端 Provider 模型列表解析与拉取问题**：
  - 修复 `JavaScriptSerializer` 反序列化 JSON 数组为 `object[]` 时未能正确匹配 `IEnumerable` 导致模型列表被判为空（`DiscoveredModels.Count == 0`）的关键 Bug。
  - 兼容 OpenAI 标准格式（`{ "data": [ ... ] }`）、Ollama 格式（`{ "models": [ ... ] }`）及直接数组格式（`[ ... ]`）。
  - 优化 Base URL 规范化逻辑，智能处理带 `/models` 或不带后缀的上游地址。
  - 显式启用 .NET 运行时的 TLS 1.2 / TLS 1.3 现代加密协议，确保对主流商业 AI 网关 HTTPS 端点的连通性与模型拉取。
  - 在 Provider 添加/编辑弹窗中保存时，自动将探测到的可用模型同步注入工作区模型池。

## 3.2.1 - 2026-09-01

- **支持 Qoder CN 新版运行时 (v1.1.35 / Qoder 0.1.3+)**：
  - 逆向并提取 Qoder CN 新版 Worker Runtime (`1.1.35`) 混淆重构后的全新注入特征点（`TvA`, `bje`, `ree`, `FIo` 等）。
  - 实现双版本基线动态匹配引擎（Dual-Baseline Engine），同时无缝兼容支持 Qoder CN `1.1.35` (Qoder 0.1.3+) 与 `1.1.31` (Qoder 0.1.2)。
- **安装路径自动探测增强 (Auto Installation Discovery)**：
  - 自动检测并优先匹配当前用户的安装目录（`%LOCALAPPDATA%\Programs\Qoder CN`）以及全局目录（`%ProgramFiles%\Qoder\Qoder CN`），解决非管理员单用户安装无法自动定位的问题。
- **稳定性与测试验证**：
  - 更新原生引擎与自动化测试套件，全面覆盖双版本补丁生成、Node.js 语法校验与安全回滚链。

## 3.2.0 - 2026-08-29

- **多 AI Provider 统一管理与按需模型注入架构 (Multi-Provider & Unified Model Pool)**：
  - 全面重构为 **三步交互范式**：
    1. **添加 Provider**：配置多个独立的 AI 提供方（名称、Base URL、DPAPI 加密 API Key、超时参数）。
    2. **自动探测模型**：一键请求上游 `/models` 接口自动提取所有可用模型，支持对无法自动拉取或非标上游手动添加/编辑模型。
    3. **统一模型池勾选注入**：跨所有已配置的 Provider 集中汇总模型列表，用户直接勾选（Checkboxes）特定模型注入 Qoder CN。
- **现代化多标签页桌面架构 (Tabbed Workspace Architecture)**：
  - `🌐 AI 提供方 (AI Providers)`：集中管理多 Provider 终端，提供连通性测试、模型拉取、快捷导入历史配置等操作。
  - `🎯 统一模型池 (Model Pool & Injection)`：全量模型统一视图，支持按 Provider 过滤、模型名称搜索、全选/反选、能力徽标标识（`[Vision]`、`[Reasoning]`、`[Tools]`、`[DeepSeek]`）。
  - `📜 活动日志与诊断 (Logs & Diagnostics)`：实时捕获补丁事件、上游测试与注入日志，提供完整安装审计与校验恢复。
- **Windows DPAPI 密钥安全隔离与多 Provider 绑定**：
  - 每个 Provider 的 API Key 独立加密绑定至 Windows `CurrentUser` 凭据存储区。
  - 导出/分享 Profile 时零密钥泄露风险，跨机器使用安全无虞。
- **高 DPI 视觉排版与防文字截断优化**：
  - 适配 100% ~ 150% Windows 缩放，重构底部主操作流与操作按钮尺寸，杜绝 Emoji 与中文字符显示不全问题。

## 3.1.0 - 2026-08-29

- **状态驱动工作流与动态主操作按钮 (State-Driven Workflow GUI)**：
  - 图形界面全面重构为反应式状态机 (`GuiState`)，根据 Qoder 安装路径、运行时 SHA-256、补丁状态、进程占用与配置同步状态自动判定系统上下文。
  - 主操作按钮（Hero Action Button）自适应文案、主题色与操作意图：
    - `Patch & Install Runtime (Admin Required)` (绿色)
    - `Upgrade Patch to v3.1.0 (Admin Required)` (绿色)
    - `Apply Configuration (No UAC Required)` (青色)
    - `Launch Qoder CN` (蓝色)
    - `Restart Running Qoder` (橙色)
    - `Close Running Qoder First` (红色禁用)
    - `Incomplete Configuration` (灰色提示)
- **三层变更解耦架构 (Three-Tier Mutation Architecture)**：
  - **补丁变更 (Patch Changes)**：修改 `qoder-worker-runtime.obf.mjs`，仅在基线未修补或需升级旧补丁时按需请求 Windows UAC 提权。
  - **配置变更 (Configuration Changes)**：调整上游 Base URL、超时时间、模型列表等，直接写入 Profile 及 `~/.qoder-cn/custom-openai-provider-v3.1.0.json`，零 UAC 提权。
  - **API Key 变更 (Credential Changes)**：纯 DPAPI 内存加密存储，不落盘 Profile JSON，零 UAC 提权。
- **DPAPI 密钥安全状态徽标 (Secret Badge)**：
  - 已保存密钥显示 `🔒 已安全保存 (仅当前 Windows 用户可用)`，提供快捷 `[更换]` 与 `[清除]` 按钮，杜绝明文泄露。
- **模型搜索实时过滤与操作台增强 (UX & Ergonomics)**：
  - 顶部模型搜索栏实时筛选，保持勾选状态与多选同步。
  - 活动日志控制台新增快捷 `复制日志` 与 `清空日志` 工具栏。
  - 全局键盘快捷键支持：`Ctrl+S` (保存配置)、`F5` (刷新)、`Ctrl+L` (启动 Qoder)、`Ctrl+Enter` (触发主操作)、`Esc` (取消密钥更换)。
- **测试套件升级**：
  - 新增原生引擎 P0 验收测试（DPAPI 密钥隔离、3.0.1 密钥自动迁移、v3.1.0 补丁路由与回退链、升级检测、备份恢复隔离）。

## 3.0.1 - 2026-08-29

- API Key 改用 Windows DPAPI（CurrentUser）加密保存到 LocalAppData；profile、运行时配置和 Git 跟踪文件不再包含明文密钥。
- 管理器改为普通权限启动，仅 Install/Restore 使用原生辅助模式按操作请求 UAC，避免启动出的 Qoder 意外继承管理员权限。
- 启动 Qoder 时通过进程级环境变量传递临时 Key；运行时直连仍兼容 Qoder 内存中的 BYOK Key。
- 自定义模型命中后，缺少 Key 或上游 URL 无效会明确失败，不再吞掉错误并回退官方路由。
- Restore 与历史版本升级仅选择属于当前安装目录的备份，并使用事务式 Runtime 替换。
- 修复 Provider Key 与替换索引不一致导致的模型归属错配。
- Authenticode 管道不再把 `UnknownError` 当成功；支持指定正式证书或显式创建仅供本机使用的开发证书。
- 测试套件始终重新编译全部原生源码，并覆盖 DPAPI、无明文配置、路由语义、模型字段保留和跨安装备份隔离。

## 3.0.0 - 2026-08-29

- **全新正式独立原生 Windows 桌面应用程序 (`bin/QoderCN-Patcher.exe`)**：
  - **告别黑框控制台与 CMD 弹窗**：纯 Windows GUI 窗体设计，双击直接启动现代卡片式桌面管理程序。
  - **卡巴斯基/杀毒软件防误报体系化设计**：
    - 嵌入 `app.manifest`，直接声明 `<requestedExecutionLevel level="requireAdministrator"/>`，由 Windows 操作系统原生接管 UAC 提权，杜绝普通进程在后台隐式拉起高危提权子进程 (`powershell -Verb RunAs -EncodedCommand`) 的启发式拦截。
    - 嵌入完整 Win32 PE Assembly 属性元数据（`Title`、`Description`、`Company`、`Product`、`Copyright`、版本号 `3.0.0.0`），消除匿名无签名可执行文件的启发式拦截风险。
    - 嵌入多分辨率高质应用程序图标 (`app.ico`)，编译时通过 `/win32icon` 写入 PE 资源段。
    - 纯 C# 原生进程内执行 SHA-256 哈希计算、文件修补与备份还原，零外部脚本调用。
    - 提供 Authenticode 数字签名管道 (`scripts/Sign-Binary.ps1`)，并集成进 `build.cmd` / `build.ps1`。
- **可视化模型高级属性编辑器 (Model Properties Editor)**：
  - 支持双击列表项或点击 `Edit Model...` 弹出可视化属性编辑窗口。
  - 完整支持可视化配置与即时修改：`id`（模型 API 标识）、`displayName`（UI 显示名）、`maxInputTokens`（上下文窗口大小）、`maxOutputTokens`（最大输出 Token）、`maxTokensField`（`max_tokens` 与 `max_completion_tokens` 参数切换）、`tools`（函数调用支持）、`reasoning`（思考推理模式）、`vision`（多模态视觉）。
  - 列表项直观渲染特性标签（如 `[Thinking, Tools]`, `[o-series]`）。
- **运行时补丁与回退链升级为 v3.0 (`QODER_CN_OAI_PATCH_V3_0`)**：
  - 配置文件优先读取 `~/.qoder-cn/custom-openai-provider-v3.0.json`，并支持平滑向后兼容回退链 (`v2.3` -> `v2.2` -> `v2.1` -> `v2.0`)。
  - 支持从任意历史版本（v2.0 / v2.1 / v2.2 / v2.3）一键无缝热升级至 v3.0。

- **修复个人模型管理界面显示“暂时无法管理个人模型”（可添加的 Provider 和模型暂时无法读取）问题**：
  - **防御性数据规范化**：加固 `qcv23model`、`CatalogAnchor`、`StartupBYOKAnchor` 和 `ModelListAnchor` 注入点，将 `efforts` 严格强制为 `Array.isArray(m.efforts) ? m.efforts : []`，消除因 `{}`（空对象）引发 Qoder 桌面主进程 `kO / jpA` 迭代器的 `TypeError: (A ?? []) is not iterable` 异常。
  - **GUI 配置序列化保护**：在图形界面保存配置时，限制 `efforts` 仅接收合法数组，防止 PowerShell `ConvertTo-Json` 将空哈希表序列化为 `{}`。
  - **100% 保持直接模型注入与推理路由**：完全保留已完成的模型直接注入、聊天窗口下拉列表直通、历史会话恢复与局域网 CPA 上游转发能力。
  - 升级补丁版本标识为 v2.3，支持从原始官方备份或任意历史 v2.x 版本一键升级。

## 2.2.0 - 2026-08-29

- **强化全场景注入与模型列表直通（彻底解决离线/未登录下 Provider 消失与验证失败问题）**：
  - **Provider 永不丢失**：修复 `CatalogAnchor` 在未登录或官方 BYOK 列表为空时抛出越界异常的问题，未匹配时自动前置追加完整 Provider 实体，保证 Provider 100% 出现。
  - **验证防报错**：增强 `ValidationAnchor`，增加空 URL 容错与模型 ID/Provider Key 多维度匹配，避免 `new URL` 异常引发官方云端回退。
  - **模型下拉列表自动展示**：拦截 `Zdo` 列表解析函数，无论是否在 UI 手动添加过模型，主界面下拉列表均自动同步配置中的所有自定义模型。
  - **ModelCatalog 兜底与会话恢复**：在 `ModelCatalog.getModel` 增加 `qcv22model` 动态回退解析，彻底保证历史 Session Resume 与 `isValidKey` 状态合法性。
- **全面移除二进制 EXE 文件与 C# 源码，转用透明 CMD + 原生 PowerShell 启动方案**：
  - 彻底解决卡巴斯基等杀毒软件对本地自编译 EXE 的误报/拦截问题。
  - 双击 `Launch-QoderCN-Patcher-GUI.cmd` 直接打开 WinForms 图形修补界面。
- 升级补丁版本至 v2.2，支持从任意已修补的 v2 / v2.1 运行时无缝升级。

## 2.1.1-experimental - 2026-08-29

- 新增**独立单文件原生 EXE 桌面程序** (`bin/QoderCN-Patcher.exe`)，体积约 60KB，零外部依赖，告别命令行与黑框终端。
- 新增内置 `build.cmd` 与 `build.ps1` 一键编译脚本，自动调用 Windows 内置 .NET Framework C# 编译器 (`csc.exe`)。
- 纯 C# 原生重写修补与备份引擎 (`PatcherCore.cs`)，支持脱离 PowerShell 独立运行，并增加确定性逆向还原（Unpatch）能力。
- 全中文卡片式交互界面：状态指示卡片、可视化渠道与模型表格管理、上游 Base URL 连通性测试、彩色控制台诊断。
- 支持优雅 UAC 提权引导与 Qoder CN 客户端一键拉起。
- 保留免安装 WinForms GUI 脚本和双击启动器作为命令行后备支持。
- 在 CPA `192.168.50.241:8317` 上完成 v2.1 端到端对话验证。

## 2.1.0-experimental - 2026-08-29

- 复用 Qoder 内置 `external-openai` 传输，将配置模型直接路由到 OpenAI-compatible 上游。
- 从 Qoder 的 BYOK 请求中读取内存中的 API Key，项目配置仍不保存凭据。
- 规范化 `upstreamBaseUrl`，兼容填写 API 根地址或完整 `/chat/completions` 地址。
- 对命中的配置模型增加误发保护：Key 缺失或 URL 无效时不回退 Qoder 网关。
- 支持从已安装 v2 使用经哈希验证的原始备份升级到 v2.1。
- DryRun 增加直连注入完整性检查和 Node.js 语法检查。

## 2.0.0-experimental - 2026-08-29

- 建立运行时补丁 v2 项目基线。
- 仅修改 unpacked Qoder Worker Runtime，不修改 `app.asar`。
- 增加双 URL 配置：桌面 UI 使用 HTTPS，Worker 可重写到 HTTP/HTTPS 上游。
- 增加版本哈希、唯一锚点、配置和备份校验。
- 增加 `Inspect`、`DryRun`、`Apply`、`Restore` 操作。
- 记录端到端已知问题：桌面保存结果缺少 URL，聊天请求仍进入 Qoder 网关。
