# Qoder CN OpenAI-Compatible Gateway Manager (v3.2.3)

一个针对 Qoder CN 桌面版的原生可视化管理与运行时补丁项目，用于接入自定义 OpenAI-compatible Provider、Ollama、vLLM 和自建局域网网关（如 CPA）。

## 当前状态

本仓库当前版本为 **v3.2.3**，目标支持版本为：

- Qoder CN Desktop `0.1.6+` / `0.1.3+` / `0.1.2`
- Qoder CN Runtime / CLI `1.1.40` / `1.1.35` / `1.1.31`

已经验证：

- **多 AI Provider 统一管理与按需模型注入架构**：
  - 支持配置任意数量的外部 AI 提供方（SiliconFlow、OpenAI、DeepSeek、Ollama、OneAPI 等）。
  - 支持一键通过 `/models` 接口自动拉取上游全部模型，或手动补充个性化模型。
  - 跨所有提供方汇总统一模型池，直观勾选所需模型注入 Qoder CN。
- **状态驱动工作流管理界面**：
  - 基于反应式状态机 (`GuiState`) 实时检测系统状态，主操作按钮智能自适应当前最关键操作（修补、升级、热重载配置、启动或重启）。
  - **三层变更解耦**：日常修改 Provider 勾选 / 上游 Base URL 等配置直接热重载，无需 UAC 提权；API Key 仅通过 Windows DPAPI 内存加密存储，不写入文件；仅文件修补/还原操作按需请求 UAC。
  - **DPAPI 密钥安全隔离**：直观展示加密保护状态，支持安全更换与清除，跨设备分发零密钥泄露风险。
  - **高效工作流**：支持模型按 Provider 筛选与实时搜索、全选/反选、活动日志一键复制/清空、全套键盘快捷键（`Ctrl+S` / `F5` / `Ctrl+L` / `Ctrl+Enter`）。
- **全新独立单文件原生 EXE 桌面程序 (`bin/QoderCN-Patcher.exe`)**，绿色便携，双击即用，零外部依赖。
- **原生 Windows 安全与发布设计**：
  - 管理器以普通用户权限运行，只有 Install/Restore 操作通过独立原生辅助模式请求 UAC；从管理器启动的 Qoder 不会被意外提升为管理员。
  - API Key 使用 Windows DPAPI（CurrentUser）加密保存在 LocalAppData，项目 profile 与运行时 JSON 均不保存明文 Key。
  - 嵌入完整 Win32 PE Assembly 属性元数据（版本 `3.2.0.0`、公司、产品说明与版权）和多分辨率图标。
  - 构建支持正式证书签名与显式开发签名；签名状态必须为 `Valid` 才视为成功。
  - 纯进程内 C# 补丁引擎，SHA-256 原生校验，零高危隐式脚本拉起。
- **可视化模型高级属性编辑器**：
  - 支持双击列表项或点击 `Edit Model...` 弹出属性编辑窗口。
  - 自由配置 `id`、`displayName`、`maxInputTokens`（上下文长度）、`maxOutputTokens`、`maxTokensField`（`max_tokens`/`max_completion_tokens`）、`tools`、`reasoning`（思考模式）、`vision`（视觉多模态）。
- **v2.3 修复个人模型管理界面显示“暂时无法管理个人模型”的问题**。
- **v2.2+ 解决退出 Qoder 后恢复历史 Session 报错无法继续聊天的问题**。
- 补丁前严格校验版本哈希和代码锚点，自动创建运行时备份，不修改 Electron 主包 `app.asar`。
- 支持从任意历史版本（v2.0 / v2.1 / v2.2 / v2.3 / v3.0 / v3.0.1 / v3.1.0）一键平滑升级至 v3.2.0。

## 项目结构

```text
.
├── bin/          编译产物 (QoderCN-Patcher.exe)
├── configs/      示例 Provider 配置，不包含 API Key
├── docs/         架构、研究结论和说明文档
├── src-native/   原生 C# 桌面应用程序源码、清单与图标
├── src/          PowerShell 核心补丁引擎与脚本 GUI (备用)
├── scripts/      构建与数字签名工具
├── tests/        静态检查与 DryRun 测试套件
├── build.cmd     双击一键编译原生 EXE
└── Launch-QoderCN-Patcher-GUI.cmd  双击快速启动管理程序
```


## macOS 版使用

针对 Mac 用户，本项目提供完整的 macOS 原生适配套件（已适配 Qoder CN 0.1.6 / Runtime 1.1.40 及 1.1.35）：

### 1. 可视化控制台 (推荐)
双击根目录文件：
```text
Launch-QoderCN-Patcher-GUI-Mac.command
```
或在终端中运行：
```bash
./qoder-patcher-mac.sh gui
```

### 2. 命令行操作
```bash
# 状态检测
./qoder-patcher-mac.sh inspect

# 一键备份并应用补丁
./qoder-patcher-mac.sh apply

# 热重载配置 (无需重启 Qoder)
./qoder-patcher-mac.sh hot-reload

# 恢复官方原版运行库
./qoder-patcher-mac.sh restore

# 启动 Qoder CN
./qoder-patcher-mac.sh launch
```

## Windows 图形界面使用

推荐直接运行原生程序：

```text
bin\QoderCN-Patcher.exe
```

或者直接双击根目录启动器：

```text
Launch-QoderCN-Patcher-GUI.cmd
```

### 核心功能与操作流程：
1. **状态检测**：自动探测 Qoder CN 安装路径与当前运行库状态（🟢 已修补 v3.0.1 / 🟡 可升级旧补丁 / 🔴 异常）。
2. **可视化配置与模型管理**：
   - 下拉切换 `configs/` 预设配置。
   - 打开配置文件目录自由添加/修改自定义模型。
3. **一键操作**：
   - 【🚀 一键安装 / 更新修补】：全自动备份原版、写入无密钥运行时配置并应用 v3.0.1 补丁，仅此操作请求 UAC。
   - 【🔄 恢复官方原版】：支持从备份一键安全回滚官方原版。
   - 【⚡ 启动 Qoder CN】：修补完成后直接一键打开 Qoder CN。

## 安全原则

- API Key 不写入项目配置、运行时 JSON、Git、日志或补丁器参数。
- 管理器把 API Key 通过 Windows DPAPI 加密保存到 `%LOCALAPPDATA%\QoderCNOpenAICompatiblePatcher\secrets`，密文仅限当前 Windows 用户解密。
- 从管理器点击 `Launch Qoder CN` 时，Key 仅通过新进程环境变量传递；直接从开始菜单启动时需使用 Qoder 自己保存的 BYOK Key。
- 补丁不修改 Electron 主包 `app.asar`。
- Qoder 更新后必须重新验证哈希和注入锚点，不能强制打补丁。
- 应用或恢复补丁前必须关闭 Qoder CN。

## 检查与 DryRun

普通 PowerShell 中运行：

```powershell
& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action Inspect

& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action DryRun `
  -ConfigPath '.\configs\custom-provider.example.json'
```

如果 `node` 不在 PATH 中，可通过 `-NodePath '<node.exe>'` 让 DryRun 对生成后的 Runtime 执行 JavaScript 语法检查。

运行项目检查：

```powershell
& '.\tests\Test-Project.ps1'
```

发布构建可使用正式代码签名证书：

```powershell
& '.\build.ps1' -Sign -CertificateThumbprint '<thumbprint>'
& '.\tests\Test-Project.ps1' -RequireSignedBinary
```

`-CreateDevelopmentCertificate` 仅生成当前用户本机信任的开发签名，不代表其他电脑会信任该程序。

## 应用与恢复

建议先运行 `DryRun`。实际应用前关闭 Qoder CN，并确认已有可恢复的原始 Runtime 备份。

如需研究性应用，关闭 Qoder CN 后，以管理员 PowerShell 运行：

```powershell
& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action Apply `
  -ConfigPath '.\configs\custom-provider.example.json'
```

恢复时使用应用输出中的备份号：

```powershell
& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action Restore `
  -BackupId '<backup-id>'
```

## 已验证的直连标志

真实 CPA 对话已经通过。排障时可在日志中确认出现：

```text
[ExternalProviderRequest] ... provider=qoder-cn-patcher
```

同一请求不应进入 `[QoderInferRequest]` 云端发送路径。Qoder 更新后仍需重新检查 Runtime 哈希和注入锚点。
