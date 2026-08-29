# Changelog

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
