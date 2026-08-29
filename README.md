# Qoder CN OpenAI-Compatible Patcher

一个针对 Qoder CN 桌面版的实验性运行时补丁项目，用于研究自定义 OpenAI-compatible Provider、Ollama、vLLM 和自建网关的接入方式。

## 当前状态

本仓库当前版本为 **v2.1.1 experimental**，目标版本为：

- Qoder CN Desktop `0.1.2`
- Qoder CN Runtime / CLI `1.1.31`

已经验证：

- 不修改 `resources/app.asar`，Qoder CN 能正常启动。
- 能在“设置 → 模型 → 添加模型”中注入自定义 Provider 和模型列表。
- 补丁支持 `Inspect`、`DryRun`、`Apply`、`Restore`。
- 补丁前会校验版本哈希和代码锚点，并创建运行时备份。
- `DryRun` 会检查直连路由注入，并使用 Node.js 校验生成后的 Worker Runtime 语法。
- 从已安装的 v2 升级时，会从经过哈希校验的原始备份重新生成 v2.1 Runtime。
- 已在 CPA `192.168.50.241:8317` 上完成真实对话验证。
- 提供免安装 Windows GUI，可供同事双击使用。

v2.1 的核心变化：

- 桌面端仍可复用现有 BYOK 界面保存 API Key。
- Worker 根据配置中的模型 ID 构造本地 OpenAI-compatible 目标。
- 请求复用 Qoder 自带的 `external-openai` SSE 传输，直接访问 `upstreamBaseUrl/chat/completions`。
- 命中的自定义模型缺少 API Key 或上游 URL 时立即报错，不回退到 Qoder 网关。

详细设计和验收步骤见 [v2.1 计划](docs/v2.1-plan.md)，GUI 使用方法见 [GUI 指南](docs/gui.md)，v2 的失败原因保留在 [v2 已知问题](docs/v2-known-issue.md)。

## 项目结构

```text
.
├── bin/       编译输出目录（包含单文件原生可执行程序 QoderCN-Patcher.exe）
├── configs/   示例 Provider 配置，不包含 API Key
├── docs/      架构、研究结论和后续计划
├── src/       源码（包含 PowerShell 补丁脚本与 src/gui 原生 C# GUI 源码）
├── tests/     静态检查和 DryRun 入口
├── build.cmd  Windows 双击一键编译脚本
├── build.ps1  PowerShell 一键编译脚本
└── Launch-QoderCN-Patcher-GUI.cmd  PowerShell 备用启动器
```

## 原生桌面 EXE 使用 (推荐)

直接双击运行：

```text
bin\QoderCN-Patcher.exe
```

如需重新编译生成 EXE，只需双击运行项目根目录的 `build.cmd`（或执行 `.\build.ps1`）。构建基于 Windows 10/11 内置的 .NET Framework 编译器，**无需安装任何额外 SDK 或运行库**。

### EXE 核心功能与操作流程：
1. **状态卡片**：自动探测 Qoder CN 安装路径与当前运行库状态（🟢 已修补 v2.1 / 🟡 官方原版 / 🔴 异常）。
2. **可视化配置与模型管理**：
   - 下拉切换 `configs/` 预设配置。
   - 直接在界面上编辑渠道名称、上游 Base URL 并提供【测试连接】按钮。
   - 可视化添加/删除自定义模型（配置 ID、别名、Reasoning 思维链、Vision 视觉、Tools 工具调用），点【💾 保存配置】自动持久化。
3. **一键操作**：
   - 【🚀 一键安装 / 更新修补】：全自动备份原版、写入运行时配置并应用 v2.1 补丁（遇权限不足自动引导 UAC 提权）。
   - 【🔄 恢复官方原版】：支持从备份或确定性逆向还原（Unpatch）一键回滚。
   - 【⚡ 启动 Qoder CN】：修补完成后直接一键打开 Qoder CN。

*(原 PowerShell 脚本 `src/QoderCN-Patcher-GUI.ps1` 及 `Launch-QoderCN-Patcher-GUI.cmd` 仍保留作为命令行与备用后备模式。)*

## 安全原则

- API Key 不写入项目配置、Git、日志或补丁器参数。
- API Key 只能在 Qoder 的模型设置界面中输入。
- v2.1 不修改 Electron 主包 `app.asar`。
- Qoder 更新后必须重新验证哈希和注入锚点，不能强制打补丁。
- 应用或恢复补丁前必须关闭 Qoder CN。

## 检查与 DryRun

普通 PowerShell 中运行：

```powershell
& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action Inspect

& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action DryRun `
  -ConfigPath '.\configs\cpa-192.168.50.241.json'
```

如果 `node` 不在 PATH 中，可通过 `-NodePath '<node.exe>'` 让 DryRun 对生成后的 Runtime 执行 JavaScript 语法检查。

运行项目检查：

```powershell
& '.\tests\Test-Project.ps1'
```

## 应用与恢复

建议先运行 `DryRun`。实际应用前关闭 Qoder CN，并确认已有可恢复的原始 Runtime 备份。

如需研究性应用，关闭 Qoder CN 后，以管理员 PowerShell 运行：

```powershell
& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action Apply `
  -ConfigPath '.\configs\cpa-192.168.50.241.json'
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
