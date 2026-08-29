# Qoder CN OpenAI-Compatible Patcher

一个针对 Qoder CN 桌面版的实验性运行时补丁项目，用于研究自定义 OpenAI-compatible Provider、Ollama、vLLM 和自建网关的接入方式。

## 当前状态

本仓库保存的是 **v2 实验基线**，目标版本为：

- Qoder CN Desktop `0.1.2`
- Qoder CN Runtime / CLI `1.1.31`

已经验证：

- 不修改 `resources/app.asar`，Qoder CN 能正常启动。
- 能在“设置 → 模型 → 添加模型”中注入自定义 Provider 和模型列表。
- 补丁支持 `Inspect`、`DryRun`、`Apply`、`Restore`。
- 补丁前会校验版本哈希和代码锚点，并创建运行时备份。

已知限制：

- 当前桌面端保存模型时仍将 Provider 记录为被替换的官方 Provider，例如 `bailian`。
- 保存结果缺少自定义 URL，实际聊天请求仍会发送到 Qoder 网关。
- Qoder 网关会返回 `400 Failed to generate custom pool`。
- 因此 v2 **尚未实现端到端自定义模型调用**，请勿作为生产补丁使用。

详细分析见 [v2 已知问题](docs/v2-known-issue.md)。

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
- v2 不修改 Electron 主包 `app.asar`。
- Qoder 更新后必须重新验证哈希和注入锚点，不能强制打补丁。
- 应用或恢复补丁前必须关闭 Qoder CN。

## 检查与 DryRun

普通 PowerShell 中运行：

```powershell
& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action Inspect

& '.\src\QoderCN-OpenAI-Compatible-Patcher.ps1' -Action DryRun `
  -ConfigPath '.\configs\cpa-192.168.50.241.json'
```

运行项目检查：

```powershell
& '.\tests\Test-Project.ps1'
```

## 应用与恢复

> 当前 v2 存在端到端路由限制，通常只建议使用 `Inspect` 和 `DryRun`。

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

v2.1 需要把桌面端保存的模型转换为真正的 `provider: custom`，并确保 Headless 会话中的 `custom_model.url` 与 `model_config.url` 都包含自定义上游地址。在完成日志级目标地址验证前，不会将状态标记为可用。
