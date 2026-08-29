# Changelog

## 2.0.0-experimental - 2026-08-29

- 建立运行时补丁 v2 项目基线。
- 仅修改 unpacked Qoder Worker Runtime，不修改 `app.asar`。
- 增加双 URL 配置：桌面 UI 使用 HTTPS，Worker 可重写到 HTTP/HTTPS 上游。
- 增加版本哈希、唯一锚点、配置和备份校验。
- 增加 `Inspect`、`DryRun`、`Apply`、`Restore` 操作。
- 记录端到端已知问题：桌面保存结果缺少 URL，聊天请求仍进入 Qoder 网关。
