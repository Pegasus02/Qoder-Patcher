import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import { SecretStore } from './secret-store.mjs';

export function getDefaultWorkspacePath() {
  const home = os.homedir() || process.env.HOME || '';
  return path.join(home, ".qoder-cn", "workspace.json");
}

export function getDefaultRuntimeConfigPath() {
  const home = os.homedir() || process.env.HOME || '';
  return path.join(home, ".qoder-cn", "custom-openai-provider-v3.2.0.json");
}

export function createModelItem(partial = {}) {
  const id = (partial.id || "").trim();
  const displayName = partial.displayName && partial.displayName.trim() ? partial.displayName.trim() : id;
  const isSelected = partial.selectedForInjection === undefined ? true : Boolean(partial.selectedForInjection);
  
  return {
    id,
    displayName,
    vision: Boolean(partial.vision),
    reasoning: partial.reasoning !== false,
    tools: partial.tools !== false,
    maxInputTokens: Number.isInteger(partial.maxInputTokens) && partial.maxInputTokens > 0 ? partial.maxInputTokens : 131072,
    maxOutputTokens: Number.isInteger(partial.maxOutputTokens) && partial.maxOutputTokens > 0 ? partial.maxOutputTokens : 32768,
    maxTokensField: partial.maxTokensField === "max_completion_tokens" ? "max_completion_tokens" : "max_tokens",
    efforts: Array.isArray(partial.efforts) ? partial.efforts : [],
    supportsDisabled: partial.supportsDisabled ?? null,
    providerId: partial.providerId || "",
    providerName: partial.providerName || "",
    upstreamBaseUrl: partial.upstreamBaseUrl || "",
    uiBaseUrl: partial.uiBaseUrl || "",
    selectedForInjection: isSelected
  };
}

export function createProviderItem(partial = {}) {
  const id = partial.id || `p-${crypto.randomBytes(4).toString('hex')}`;
  const baseUrl = (partial.baseUrl || "http://127.0.0.1:11434/v1").trim();
  
  let uiBaseUrl = (partial.uiBaseUrl || "").trim();
  if (!uiBaseUrl && baseUrl) {
    if (baseUrl.toLowerCase().startsWith("http://")) {
      uiBaseUrl = "https://" + baseUrl.slice(7);
    } else {
      uiBaseUrl = baseUrl;
    }
  }

  const models = Array.isArray(partial.models) 
    ? partial.models.map(m => createModelItem({ 
        ...m, 
        providerId: id, 
        providerName: partial.name || "Provider", 
        upstreamBaseUrl: baseUrl, 
        uiBaseUrl 
      }))
    : [];

  return {
    id,
    name: partial.name || "New Provider",
    baseUrl,
    uiBaseUrl,
    replaceProviderKey: partial.replaceProviderKey || "bailian",
    replaceProviderDisplayName: partial.replaceProviderDisplayName || "Alibaba Cloud Model Studio",
    replaceProviderIndex: Number.isInteger(partial.replaceProviderIndex) ? partial.replaceProviderIndex : 0,
    firstPayloadTimeoutMs: Number.isInteger(partial.firstPayloadTimeoutMs) ? partial.firstPayloadTimeoutMs : 180000,
    streamIdleTimeoutMs: Number.isInteger(partial.streamIdleTimeoutMs) ? partial.streamIdleTimeoutMs : 300000,
    enabled: partial.enabled !== false,
    models
  };
}

export class GatewayWorkspace {
  constructor() {
    this.providers = [];
    this.selectedModelKeys = [];
  }

  static createDefault() {
    const ws = new GatewayWorkspace();

    // Default CPA Provider
    const cpa = createProviderItem({
      id: "p-cpa",
      name: "CPA Gateway",
      baseUrl: "http://127.0.0.1:8317/v1",
      uiBaseUrl: "https://127.0.0.1:8317/v1",
      replaceProviderKey: "bailian",
      replaceProviderDisplayName: "CPA Local Gateway",
      enabled: true,
      models: [
        { id: "gpt-5.6-terra", displayName: "GPT-5.6 Terra", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "claude-opus-4-6-thinking", displayName: "Claude Opus 4.6 Thinking", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "claude-sonnet-4-6", displayName: "Claude Sonnet 4.6", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "gemini-3.7-flash-high", displayName: "Gemini 3.7 Flash High", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "gpt-5.6-sol", displayName: "GPT-5.6 Sol", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "gpt-5.6-luna", displayName: "GPT-5.6 Luna", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "deepseek-reasoner", displayName: "DeepSeek R1", reasoning: true, vision: false, tools: true, selectedForInjection: true },
        { id: "deepseek-chat", displayName: "DeepSeek V3", reasoning: false, vision: false, tools: true, selectedForInjection: true }
      ]
    });

    // Default Ollama Provider
    const ollama = createProviderItem({
      id: "p-ollama",
      name: "Ollama (Local)",
      baseUrl: "http://127.0.0.1:11434/v1",
      uiBaseUrl: "https://127.0.0.1:11434/v1",
      replaceProviderKey: "bailian",
      replaceProviderDisplayName: "Ollama Local",
      enabled: false,
      models: [
        { id: "deepseek-r1:7b", displayName: "DeepSeek R1 7B", reasoning: true, vision: false, tools: true, selectedForInjection: true },
        { id: "qwen2.5-coder:7b", displayName: "Qwen 2.5 Coder 7B", reasoning: false, vision: false, tools: true, selectedForInjection: true },
        { id: "llama3.3:70b", displayName: "Llama 3.3 70B", reasoning: false, vision: false, tools: true, selectedForInjection: true }
      ]
    });

    // Default SiliconFlow Provider
    const silicon = createProviderItem({
      id: "p-siliconflow",
      name: "SiliconFlow (硅基流动)",
      baseUrl: "https://api.siliconflow.cn/v1",
      uiBaseUrl: "https://api.siliconflow.cn/v1",
      replaceProviderKey: "deepseek",
      replaceProviderDisplayName: "SiliconFlow",
      enabled: false,
      models: [
        { id: "deepseek-ai/DeepSeek-R1", displayName: "SiliconFlow DeepSeek R1", reasoning: true, vision: false, tools: true, selectedForInjection: true },
        { id: "deepseek-ai/DeepSeek-V3", displayName: "SiliconFlow DeepSeek V3", reasoning: false, vision: false, tools: true, selectedForInjection: true },
        { id: "Qwen/Qwen2.5-Coder-32B-Instruct", displayName: "Qwen 2.5 Coder 32B", reasoning: false, vision: false, tools: true, selectedForInjection: true }
      ]
    });

    ws.providers = [cpa, ollama, silicon];
    ws.syncSelectedModelKeys();
    return ws;
  }

  static loadFromFile(filePath = getDefaultWorkspacePath()) {
    if (!fs.existsSync(filePath)) {
      const def = GatewayWorkspace.createDefault();
      def.saveToFile(filePath);
      return def;
    }

    try {
      const text = fs.readFileSync(filePath, 'utf8').replace(/^\uFEFF/, '').trim();
      if (!text) return GatewayWorkspace.createDefault();

      const data = JSON.parse(text);
      const ws = new GatewayWorkspace();

      if (Array.isArray(data.providers)) {
        ws.providers = data.providers.map(p => createProviderItem(p));
      } else {
        ws.providers = [];
      }

      ws.syncSelectedModelKeys();
      return ws;
    } catch {
      return GatewayWorkspace.createDefault();
    }
  }

  syncSelectedModelKeys() {
    this.selectedModelKeys = [];
    for (const p of this.providers) {
      if (!p.models) continue;
      for (const m of p.models) {
        if (m.selectedForInjection) {
          this.selectedModelKeys.push(m.id);
        }
      }
    }
  }

  saveToFile(filePath = getDefaultWorkspacePath()) {
    const dir = path.dirname(filePath);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }

    this.syncSelectedModelKeys();

    const data = {
      providers: this.providers,
      selectedModelKeys: this.selectedModelKeys
    };

    fs.writeFileSync(filePath, JSON.stringify(data, null, 2), 'utf8');
  }

  getAllModels() {
    const list = [];
    for (const p of this.providers) {
      if (!p.models) continue;
      for (const m of p.models) {
        list.push({
          ...m,
          providerId: p.id,
          providerName: p.name,
          upstreamBaseUrl: p.baseUrl,
          uiBaseUrl: p.uiBaseUrl || p.baseUrl
        });
      }
    }
    return list;
  }

  compileToRuntimeConfig(activeModelIds = null) {
    const injectedModels = [];
    let primaryProvider = null;

    for (const p of this.providers) {
      if (!p.enabled) continue;
      if (!primaryProvider) primaryProvider = p;

      if (!p.models) continue;
      for (const m of p.models) {
        let isSelected = false;
        if (activeModelIds && Array.isArray(activeModelIds)) {
          const set = new Set(activeModelIds);
          isSelected = set.has(m.id) || set.has(`${p.id}::${m.id}`);
        } else {
          isSelected = Boolean(m.selectedForInjection);
        }

        if (isSelected) {
          injectedModels.push({
            id: m.id,
            displayName: m.displayName || m.id,
            vision: Boolean(m.vision),
            reasoning: Boolean(m.reasoning),
            tools: m.tools !== false,
            maxInputTokens: m.maxInputTokens || 131072,
            maxOutputTokens: m.maxOutputTokens || 32768,
            maxTokensField: m.maxTokensField || "max_tokens",
            efforts: m.efforts || [],
            supportsDisabled: m.supportsDisabled ?? null,
            providerId: p.id,
            providerName: p.name,
            upstreamBaseUrl: p.baseUrl,
            uiBaseUrl: p.uiBaseUrl || p.baseUrl,
            selectedForInjection: true
          });
        }
      }
    }

    const cfg = {
      displayName: primaryProvider ? primaryProvider.name : "Custom OpenAI Compatible",
      uiBaseUrl: primaryProvider ? primaryProvider.uiBaseUrl : "https://127.0.0.1:8317/v1",
      upstreamBaseUrl: primaryProvider ? primaryProvider.baseUrl : "http://127.0.0.1:8317/v1",
      replaceProviderKey: primaryProvider ? primaryProvider.replaceProviderKey : "bailian",
      replaceProviderDisplayName: primaryProvider ? (primaryProvider.replaceProviderDisplayName || primaryProvider.name) : "Alibaba Cloud Model Studio",
      replaceProviderIndex: primaryProvider ? primaryProvider.replaceProviderIndex : 0,
      skipValidation: true,
      firstPayloadTimeoutMs: primaryProvider ? primaryProvider.firstPayloadTimeoutMs : 180000,
      streamIdleTimeoutMs: primaryProvider ? primaryProvider.streamIdleTimeoutMs : 300000,
      models: injectedModels
    };

    return cfg;
  }

  saveRuntimeConfigFile(runtimePath = getDefaultRuntimeConfigPath(), activeModelIds = null) {
    const dir = path.dirname(runtimePath);
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }

    const cfg = this.compileToRuntimeConfig(activeModelIds);
    fs.writeFileSync(runtimePath, JSON.stringify(cfg, null, 2), 'utf8');
    return cfg;
  }

  static fromSingleProfile(profile, providerName = "") {
    const ws = new GatewayWorkspace();
    const p = createProviderItem({
      id: "p-imported",
      name: providerName || profile.displayName || "Imported Provider",
      baseUrl: profile.upstreamBaseUrl || profile.baseUrl || "http://127.0.0.1:8317/v1",
      uiBaseUrl: profile.uiBaseUrl || profile.upstreamBaseUrl || "https://127.0.0.1:8317/v1",
      replaceProviderKey: profile.replaceProviderKey || "bailian",
      replaceProviderDisplayName: profile.replaceProviderDisplayName || profile.displayName,
      replaceProviderIndex: profile.replaceProviderIndex || 0,
      firstPayloadTimeoutMs: profile.firstPayloadTimeoutMs || 180000,
      streamIdleTimeoutMs: profile.streamIdleTimeoutMs || 300000,
      enabled: true,
      models: Array.isArray(profile.models) ? profile.models : []
    });

    ws.providers = [p];
    ws.syncSelectedModelKeys();
    return ws;
  }
}
