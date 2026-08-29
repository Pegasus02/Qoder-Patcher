# GUI 使用指南（v3.0.1）

## 启动方式

推荐双击 `bin\QoderCN-Patcher.exe`，也可以运行根目录的 `Launch-QoderCN-Patcher-GUI.cmd`。启动器会优先使用原生 EXE；二进制缺失时才进入 PowerShell 备用界面。

管理器默认以普通用户权限运行。Inspect、Dry Run、编辑配置、测试连接和启动 Qoder 都不会提权；只有 Install/Upgrade 与 Restore 会显示 Windows UAC 确认。

## 配置与模型

- `Qoder CN Directory`：目标安装目录。
- `Profile File`：不含凭据的 Provider/模型 JSON。
- `Upstream Base URL`：实际 OpenAI-compatible API 根地址。
- `UI Base URL`：在 Qoder BYOK 界面中使用的地址。
- `API Key`：输入框使用密码掩码。保存后由 Windows DPAPI CurrentUser 加密，密文位于 `%LOCALAPPDATA%\QoderCNOpenAICompatiblePatcher\secrets`。
- `Test Conn`：使用内存中的 Key 请求上游 `/models`；不会把 Key写入日志。
- 模型列表支持增删、双击编辑、勾选注入，以及 Token 上限、Tools、Reasoning、Vision 等属性。

## API Key 运行逻辑

1. Profile JSON 和 `~/.qoder-cn/custom-openai-provider-v3.0.1.json` 只保存非敏感配置。
2. `Save Profile` 将 Key 用 DPAPI 加密；旧 profile 中的明文 `apiKey` 会在加载时自动迁移并从 JSON 删除。
3. 点击 `Launch Qoder CN` 时，管理器只给新启动的 Qoder 进程设置 `QODER_CN_CUSTOM_PROVIDER_API_KEY`，随后不修改系统或用户级环境变量。
4. 如果从开始菜单直接启动 Qoder，管理器保存的 Key不会自动注入；此时需要使用 Qoder 自己保存的 BYOK Key。
5. 命中的自定义模型缺少 Key 或上游 URL 无效时会明确报错，不会回退到 Qoder 官方推理路由。

## 操作按钮

- `Save Profile`：保存无密钥 JSON，并更新 DPAPI 密文。
- `Inspect`：检查 Runtime、`app.asar` 哈希和补丁状态。
- `Dry Run`：从受验证的官方 Runtime 生成补丁并检查注入点。
- `Install / Upgrade`：保存运行时配置、请求 UAC、创建/复用当前安装目录的备份并事务式安装 v3.0.1。
- `Restore latest`：只从属于当前安装目录且 SHA-256 验证通过的备份恢复。
- `Launch Qoder CN`：以普通用户权限启动 Qoder，并传递当前 profile 的临时 Key。

## 签名说明

正式发布应使用受信任代码签名证书：

```powershell
& '.\build.ps1' -Sign -CertificateThumbprint '<thumbprint>'
```

本地开发可显式使用 `-CreateDevelopmentCertificate`，但该自签名证书只受当前 Windows 用户信任，不能替代公开受信任的发布证书。签名脚本只有在 Authenticode 状态为 `Valid` 时才返回成功。
