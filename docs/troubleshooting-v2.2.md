# Qoder CN v2.2 自定义模型注入与排障复盘总结

本文档完整记录了在 **Qoder CN v2.2** 版本中，关于自定义 OpenAI-Compatible 模型（如本地/局域网 CPA、OneAPI、NewAPI）注入、配置解析、鉴权流程及前端 UI 适配过程中遇到的核心问题、根本原因分析与最终解决方案。

---

## 📌 问题概览与核心排查结论

| 序号 | 故障表象 | 根本原因 | 最终解决方案 |
| :--- | :--- | :--- | :--- |
| **1** | 模型未注入 / Provider 列表为空 | 配置文件包含 UTF-8 BOM（`\uFEFF`），导致 Node.js `JSON.parse` 抛出语法异常，配置解析返回 `null`。 | 运行时读取配置时增加 `.replace(/^\uFEFF/,"").trim()` 过滤，所有文件写入强制采用无 BOM UTF-8。 |
| **2** | 对话时提示“登录已过期，请重新登录” | 1. 上游网关返回 `HTTP 401 Invalid API key` 时，Qoder 前端统一将其转义为“登录已过期”；<br>2. 补丁中 Key 优先级取值错误（优先使用了 Qoder 内存残留的默认占位符 `"sk-cpa"` 而非用户真实 Key）。 | 1. 调整 Key 解析优先级为 `cfg.apiKey || n.apiKey || t?.parameters?.api_key`；<br>2. 解除安装脚本禁止 `apiKey` 的历史校验限制，支持 GUI 可视化直接填写与持久化 Key。 |
| **3** | 设置页面提示“可添加的 Provider 和模型暂时无法读取” | 前端 React UI 组件强依赖 `get_byok_config` 返回体中的 `enabled: true` 标识（执行 `if (e && e.enabled)`），补丁原本仅返回 `{ providers: qcp }` 导致分支判定失败。 | 在 `get_byok_config` RPC 响应中明确序列化 `{ enabled: true, providers: qcp }`。 |
| **4** | 个人模型列表显示“Provider 已不可用” | 1. 云端未登录或离线时，官方 Provider 列表为空，旧模型无法在 Provider 列表中匹配到元数据；<br>2. Provider 的 key 与历史保存的命名空间（如 `anthropic`、`bailian`）不一致。 | 1. 注入全套官方主流 Provider（Alibaba Cloud、DeepSeek、Kimi 等）基础元数据兜底；<br>2. 将自定义 Provider 的 key 设为指定替换 key（默认为 `anthropic`），确保旧模型 100% 匹配。 |
| **5** | 历史会话或多轮对话中路由退化到云端 | 聊天窗口选中的模型 ID 可能带有渠道前缀（如 `anthropic/gemini-3.7-flash-high`），之前的严格相等判断导致 `qcv22target` 无法命中。 | 实现多维无缝匹配：自动剥离前缀（`rawKey.split("/").pop()`），同时兼容裸 ID、带前缀 ID、DisplayName 及自定义对象。 |

---

## 🔍 深度技术剖析

### 1. 鉴权失败与“登录已过期”误报机制

#### 【现象】
在聊天窗口发送消息时，界面顶部弹出黄色警告框：“登录已过期：您的登录会话已过期，请重新登录以继续使用”。

#### 【底层调用链路】
1. 用户在客户端界面发送对话消息；
2. Worker 运行时捕获请求并调用推理路由器；
3. 补丁路由器在提取请求密钥时，由于优先级写错，优先提取了客户端内存中的失效占位符 `"sk-cpa"`；
4. 路由器携带 `"Bearer sk-cpa"` 向局域网 CPA 发起请求；
5. CPA 校验失败，返回 `HTTP 401 {"error":"Invalid API key"}`；
6. Qoder 客户端接收到 401 状态码，在前端 UI 中统一渲染为黄色模态框：“登录已过期”。

#### 【修复方案】
- **修正优先级**：
  ```javascript
  // 修复前（错误优先使用了占位符）：
  let r = t?.parameters?.api_key || n.apiKey || e.apiKey || "sk-cpa";

  // 修复后（强制优先使用用户在配置文件/GUI 中配置的真实有效 Key）：
  let r = e.apiKey || n.apiKey || t?.parameters?.api_key || "sk-cpa";
  ```
- **GUI 支持可视化输入**：在图形界面左侧提供直观的 `API Key` 文本框和 `Test Conn` 测试按钮，一键校验上游连通性。

---

### 2. BYOK 前端组件状态与 Provider 可用性判定

#### 【现象】
打开“设置 → 模型”界面时：
1. 个人模型列表下方显示红色警告：“`可添加的 Provider 和模型暂时无法读取。检查网络或重新启动 Qoder 后重试；已有配置不会被覆盖。`”
2. 已添加的模型下方显示：“`Provider 已不可用`”。

#### 【前端源码逆向剖析】
通过对混淆后的 React UI 组件分析发现：
```javascript
// Qoder 前端 React 组件逻辑
let e = await A.getBYOKConfig();
if (e && e.enabled) {
  // 正常模式：渲染所有 Providers 并在下拉框中展示
  d(e);
} else {
  // 异常模式：提示无法读取，且所有依赖 Provider 的模型被标记为不可用
  D("可添加的 Provider 和模型暂时无法读取...");
}
```

#### 【修复方案】
1. 在 Worker 运行时的 `case "get_byok_config":` 中，将返回结构固定为 `{ enabled: true, providers: qcp }`。
2. 注入官方主流 Provider 基础 schema 作为离线兜底，消除“Provider 已不可用”状态。

---

### 3. 多维模型路由匹配与会话恢复容错

#### 【机制设计】
用户在 Qoder 中的模型选择状态可能来自多种来源：
1. **新建会话**：传入模型 ID `gemini-3.7-flash-high`；
2. **历史会话恢复**：可能记录为带命名空间的 `anthropic/gemini-3.7-flash-high`；
3. **设置中的自定义模型**：传入 `custom_model` 对象（内含 `provider` 与 `model`）；
4. **UI 显示名称**：传入 `Gemini 3.7 Flash High`。

#### 【鲁棒匹配实现】
```javascript
function qcv22target(A) {
  try {
    let e = qcv22cfg();
    if (!e || !Array.isArray(e.models)) return;
    let t = A?.custom_model, i = A?.model_config;
    let rawKey = t?.model || i?.key || A?.model || ("string" == typeof A ? A : "") || "";
    let k = "string" == typeof rawKey && rawKey.includes("/") ? rawKey.split("/").pop() : rawKey;
    let n = e.models.find(m => m && (m.id === rawKey || m.id === k || m.displayName === rawKey));
    if (!n) return;

    let r = e.apiKey || n.apiKey || t?.parameters?.api_key || "sk-cpa";
    let o = qcv22base(e.upstreamBaseUrl);
    if (!o) throw Error("QODER_CN_PATCH_UPSTREAM_URL_INVALID");

    return {
      providerId: "qoder-cn-patcher",
      adapter: "openai-compatible",
      baseUrl: o,
      apiKey: r,
      model: {
        modelId: n.id,
        displayName: n.displayName ?? n.id,
        contextWindow: n.maxInputTokens ?? 131072,
        maxOutputTokens: n.maxOutputTokens ?? 32768,
        capabilities: {
          tools: false !== n.tools,
          vision: true === n.vision,
          thinking: true === n.reasoning
        },
        maxTokensField: n.maxTokensField === "max_completion_tokens" ? "max_completion_tokens" : "max_tokens"
      },
      timeouts: {
        firstPayloadTimeoutMs: e.firstPayloadTimeoutMs ?? 60000,
        streamIdleTimeoutMs: e.streamIdleTimeoutMs ?? 0
      }
    };
  } catch (A) {
    return;
  }
}
```

---

## 🖥️ 新版 GUI (v2.2) 特性总结

- **纯原生透明架构**：基于 Windows PowerShell + WinForms，无需编译 EXE，彻底杜绝杀软误报。
- **可视化配置编辑**：支持 Display Name、Upstream Base URL、UI Base URL、API Key 实时编辑与保存。
- **模型勾选注入器 (Selective Injection)**：
  - 勾选哪些模型，就只把选中的模型注入到 Qoder 中。
  - 支持 `Select All`、`Uncheck All`、`Add Model...`、`Remove`。
- **一键测试与安装**：
  - `Test Conn`：直接测试上游 CPA 服务连通性与 Key 有效性。
  - `Install / Upgrade`：一键自动提权完成全套 9 个代码锚点的精准修补。
  - `Restore latest`：一键回退到原始官方备份。
