# Changelog

## 2.1.1-experimental - 2026-08-29

- 增加免安装 WinForms GUI 和双击启动器。
- GUI 支持配置选择、安装检查、DryRun、管理员安装/升级、恢复和启动 Qoder CN。
- 管理员操作通过独立 UAC 进程执行，并将结果回传到 GUI 输出区。
- 增加 GUI 非交互自检；API Key 仍不进入补丁配置或日志。
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
