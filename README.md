# Qoder CN OpenAI-Compatible Patcher

一个针对 Qoder CN 桌面版的实验性运行时补丁项目，用于研究自定义 OpenAI-compatible Provider、Ollama、vLLM 和自建网关的接入方式。

## 当前状态

本仓库当前位于 **v2.1 实验候选版**，目标版本为：

- Qoder CN Desktop `0.1.2`
- Qoder CN Runtime / CLI `1.1.31`

已经验证：

- 不修改 `resources/app.asar`，Qoder CN 能正常启动。
- 能在“设置 → 模型 → 添加模型”中注入自定义 Provider 和模型列表。
- 补丁支持 `Inspect`、`DryRun`、`Apply`、`Restore`。
- 补丁前会校验版本哈希和代码锚点，并创建运行时备份。
- `DryRun` 会检查直连路由注入，并使用 Node.js 校验生成后的 Worker Runtime 语法。
- 从已安装的 v2 升级时，会从经过哈希校验的原始备份重新生成 v2.1 Runtime。

v2.1 的核心变化：

- 桌面端仍可复用现有 BYOK 界面保存 API Key。
- Worker 根据配置中的模型 ID 构造本地 OpenAI-compatible 目标。
- 请求复用 Qoder 自带的 `external-openai` SSE 传输，直接访问 `upstreamBaseUrl/chat/completions`。
- 命中的自定义模型缺少 API Key 或上游 URL 时立即报错，不回退到 Qoder 网关。

当前尚未执行安装后的 CPA 端到端对话验证，因此仍标记为实验版。详细设计和验收步骤见 [v2.1 计划](docs/v2.1-plan.md)，v2 的失败原因保留在 [v2 已知问题](docs/v2-known-issue.md)。

## 项目结构

```text
.
├── configs/   示例 Provider 配置，不包含 API Key
├── docs/      架构、研究结论和后续计划
├── src/       PowerShell 补丁器
└── tests/     静态检查和 DryRun 入口
```

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

## 下一步

应用 v2.1 后，用配置中已有的模型发送最小测试消息，并在日志中确认出现：

```text
[ExternalProviderRequest] ... provider=qoder-cn-patcher
```

同时确认不再出现该请求对应的 `[QoderInferRequest]`。通过这一步后，再把版本状态从实验候选版提升为已验证。
