export function parseSingleModelObject(item) {
  if (!item) return null;

  let id = "";
  let name = "";

  if (typeof item === 'object' && item !== null) {
    id = item.id || item.name || item.model || "";
    name = item.display_name || item.name || item.id || "";
  } else if (typeof item === 'string') {
    id = item;
    name = item;
  }

  if (!id || !id.trim()) return null;

  id = id.trim();
  name = name && name.trim() ? name.trim() : id;

  const lower = id.toLowerCase();
  let reasoning = false;
  let vision = false;
  let maxTokensField = "max_tokens";

  if (
    lower.includes("deepseek-r1") ||
    lower.includes("r1") ||
    lower.includes("o1") ||
    lower.includes("o3") ||
    lower.includes("reasoner") ||
    lower.includes("thinking")
  ) {
    reasoning = true;
  }

  if (
    lower.includes("vision") ||
    lower.includes("vl") ||
    lower.includes("4o") ||
    lower.includes("sonnet") ||
    lower.includes("gemini") ||
    lower.includes("claude-3")
  ) {
    vision = true;
  }

  if (
    lower.startsWith("o1") ||
    lower.startsWith("o3") ||
    lower.includes("/o1") ||
    lower.includes("/o3")
  ) {
    maxTokensField = "max_completion_tokens";
  }

  return {
    id,
    displayName: name,
    vision,
    reasoning,
    tools: true,
    maxInputTokens: 131072,
    maxOutputTokens: 32768,
    maxTokensField,
    efforts: [],
    supportsDisabled: null,
    selectedForInjection: true
  };
}

export function parseModelsResponse(responseJson) {
  const list = [];
  if (!responseJson) return list;

  try {
    const data = typeof responseJson === 'string'
      ? JSON.parse(responseJson.replace(/^\uFEFF/, '').trim())
      : responseJson;

    if (data && typeof data === 'object') {
      if (Array.isArray(data.data)) {
        for (const item of data.data) {
          const m = parseSingleModelObject(item);
          if (m) list.push(m);
        }
      } else if (Array.isArray(data.models)) {
        for (const item of data.models) {
          const m = parseSingleModelObject(item);
          if (m) list.push(m);
        }
      } else if (Array.isArray(data)) {
        for (const item of data) {
          const m = parseSingleModelObject(item);
          if (m) list.push(m);
        }
      }
    }
  } catch (err) {
    // Graceful error handling
  }

  return list;
}

export async function fetchModels(baseUrl, apiKey, fetchFullList = true) {
  if (!baseUrl || !baseUrl.trim()) {
    return {
      success: false,
      statusCode: 0,
      elapsedMs: 0,
      message: "Base URL is empty.",
      models: []
    };
  }

  let cleanBase = baseUrl.trim().replace(/\/+$/, '');
  let url = cleanBase.endsWith('/models') ? cleanBase : `${cleanBase}/models`;

  const startTime = Date.now();
  const headers = {
    'User-Agent': 'QoderCN-GatewayManager-Mac/3.2.0'
  };
  if (apiKey && apiKey.trim()) {
    headers['Authorization'] = `Bearer ${apiKey.trim()}`;
  }

  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 15000);

    const response = await fetch(url, {
      method: 'GET',
      headers,
      signal: controller.signal
    });
    clearTimeout(timer);

    const elapsedMs = Date.now() - startTime;
    const isOk = response.status >= 200 && response.status < 300;

    let discoveredModels = [];
    if (fetchFullList && isOk) {
      const text = await response.text();
      discoveredModels = parseModelsResponse(text);
    }

    let message = "";
    if (isOk) {
      message = `HTTP ${response.status} OK - 成功探测到 ${discoveredModels.length} 个可用模型 (${elapsedMs}ms)`;
    } else {
      message = `HTTP ${response.status} ${response.statusText} (${elapsedMs}ms)`;
    }

    return {
      success: isOk,
      statusCode: response.status,
      elapsedMs,
      message,
      models: discoveredModels
    };
  } catch (err) {
    const elapsedMs = Date.now() - startTime;
    return {
      success: false,
      statusCode: 0,
      elapsedMs,
      message: err.name === 'AbortError' ? `请求超时 (15000ms)` : err.message,
      models: []
    };
  }
}
