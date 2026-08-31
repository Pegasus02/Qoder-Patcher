# Qoder CN Gateway Manager (macOS 版使用指南)

针对 Qoder CN macOS 桌面版的原生可视化管理与运行时补丁工具，用于接入自定义 OpenAI-compatible Provider、Ollama、vLLM、DeepSeek、SiliconFlow 及自建局域网网关（如 CPA）。

---

## 🎯 核心功能特性

- **多 AI 提供方统一管理与模型池勾选**：
  - 支持配置任意数量的外部 AI 提供方（CPA、Ollama、SiliconFlow、DeepSeek、OpenAI、OneAPI 等）。
  - 支持一键通过 `/models` 接口自动拉取上游全部模型，或手动添加个性化模型。
  - 自动识别并标注思考模式（Reasoning / DeepSeek-R1 / o1 / o3）、多模态视觉（Vision）、工具调用（Tools）与 `max_completion_tokens` 参数字段。
- **状态驱动 macOS 桌面控制台**：
  - 基于反应式状态机实时检测 `/Applications/Qoder CN.app` 安装与运行库状态。
  - **三层变更解耦**：日常修改模型勾选 / 上游 Base URL 直接热重载，无需重启 Qoder；API Key 采用本地 AES-256-GCM 独立加密存储，不写入任何配置文件。
- **双击即用与命令行双重支持**：
  - 双击 `Launch-QoderCN-Patcher-GUI-Mac.command` 即刻启动可视化控制台。
  - 支持终端脚本 `./qoder-patcher-mac.sh` 高效执行自动化检测、修补、还原与热重载。
- **高可靠性与安全原则**：
  - 自动创建带时间戳与 SHA-256 校验的原始运行库备份（保存在 `~/.qoder-cn/backups`）。
  - 支持从任意版本一键安全恢复官方原版。
  - 补丁不触碰 Electron 主包 `app.asar`，仅原子化修改解包的 Worker 运行库。

---

## 🚀 快速开始

### 方式一：可视化图形界面 (推荐)

在 Finder 中直接**双击根目录启动器**：
```text
Launch-QoderCN-Patcher-GUI-Mac.command
```
或在终端中运行：
```bash
./qoder-patcher-mac.sh gui
```
系统将自动启动本地管理服务并在默认浏览器中打开控制台。

### 方式二：终端命令行 (CLI)

1. **状态检测 (Inspect)**：
   ```bash
   ./qoder-patcher-mac.sh inspect
   ```
2. **一键备份并应用补丁 (Apply)**：
   ```bash
   ./qoder-patcher-mac.sh apply
   ```
3. **热重载最新模型配置 (Hot Reload)**：
   ```bash
   ./qoder-patcher-mac.sh hot-reload
   ```
4. **一键恢复官方原版 (Restore)**：
   ```bash
   ./qoder-patcher-mac.sh restore
   ```
5. **启动 Qoder CN (Launch)**：
   ```bash
   ./qoder-patcher-mac.sh launch
   ```
6. **测试上游模型接口 (Probe)**：
   ```bash
   ./qoder-patcher-mac.sh probe --url "http://127.0.0.1:8317/v1"
   ```

---

## 📁 存储路径规范

- **运行时配置文件**：`~/.qoder-cn/custom-openai-provider-v3.2.0.json`
- **工作区多 Provider 存档**：`~/.qoder-cn/workspace.json`
- **加密密钥库**：`~/.qoder-cn/secrets/`
- **原版备份库**：`~/.qoder-cn/backups/`
- **目标 macOS 应用路径**：`/Applications/Qoder CN.app`
