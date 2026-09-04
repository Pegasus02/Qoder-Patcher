import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import { execSync, spawn } from 'node:child_process';
import { SecretStore } from './secret-store.mjs';

export const PatchMarker = "QODER_CN_OAI_PATCH_V3_2_0";
export const PreviousPatchMarkers = [
  "QODER_CN_OAI_PATCH_MAC_V3_2_0",
  "QODER_CN_OAI_PATCH_V3_1_0",
  "QODER_CN_OAI_PATCH_V3_0_1",
  "QODER_CN_OAI_PATCH_V3_0",
  "QODER_CN_OAI_PATCH_V2_3",
  "QODER_CN_OAI_PATCH_V2_2",
  "QODER_CN_OAI_PATCH_V2_1",
  "QODER_CN_OAI_PATCH_V2"
];
export const LegacyPatchMarker = "QODER_CN_OAI_PATCH_V1";

export const RuntimeRelativePath = "Contents/Resources/app.asar.unpacked/node_modules/@qoder-ai/qoder-cn-agent-sdk/dist/_worker/qoder-worker-runtime.obf.mjs";
export const RuntimeInfoRelativePath = "Contents/Resources/app.asar.unpacked/node_modules/@qoder-ai/qoder-cn-agent-sdk/dist/_worker/runtime-info.json";
export const AsarRelativePath = "Contents/Resources/app.asar";

// Minified identifiers are re-mangled on every Qoder CN release, so each
// supported runtime needs its own row here. Newest first: detectProfile()
// returns the first match.
export const RuntimeProfiles = [
  {
    id: "1.1.40",
    label: "v1.1.40 (Qoder 0.1.6)",
    detect: "function GMA(A){",
    pristineSha256: "99584d619a93f37bf5fb084251a2c5a6852465c1f15d3865cfc567cadb8497c8",
    importStyle: "banner",
    converterFn: "GMA",
    converterSourceAnchor: 'max_output_tokens:32e3,source:"user"',
    converterSourceRepl: 'max_output_tokens:32e3,source:A.source||"system"',
    logger: "C",
    inferenceFn: "dOe",
    startupFn: "M$A",
    startupFetchFn: "e6e",
    modelListFn: "FCo",
    catalogStateVar: "i",
    validationLastArg: "i",
    emitPrefix: 'c(Gn(t,"success",{',
    emitSuffix: "}))"
  },
  {
    id: "1.1.35",
    label: "v1.1.35 (Qoder 0.1.3+)",
    detect: "function TvA(A){",
    pristineSha256: "c79c0cdcaba7f8eeacc5d8139add45d142c21f5354c03c0f4f1d8d2cfbc73150",
    importStyle: "banner",
    converterFn: "TvA",
    converterSourceAnchor: 'max_output_tokens:32e3,source:"user"',
    converterSourceRepl: 'max_output_tokens:32e3,source:A.source||"system"',
    logger: "C",
    inferenceFn: "bje",
    startupFn: "ree",
    startupFetchFn: "$Oe",
    modelListFn: "FIo",
    catalogStateVar: "i",
    validationLastArg: "i",
    emitPrefix: 'c(Di(t,"success",{',
    emitSuffix: "}))"
  },
  {
    id: "1.1.31",
    label: "v1.1.31 (Qoder 0.1.2)",
    detect: "function XxA(A){",
    pristineSha256: "7348879d488dc22cca1fc8138c3182233637f78bea210652701b3463b6d3f655",
    importStyle: "pathfs",
    converterFn: "XxA",
    converterSourceAnchor: 'max_output_tokens:32e3,source:"user"',
    converterSourceRepl: 'max_output_tokens:32e3,source:A.source||"system"',
    logger: "Q",
    inferenceFn: "ZVe",
    startupFn: "n$A",
    startupFetchFn: "y8e",
    modelListFn: "Zdo",
    catalogStateVar: "t",
    validationLastArg: "t",
    emitPrefix: 'B(pn(i,"success",{',
    emitSuffix: "}))"
  }
];

export function detectProfile(text) {
  if (!text) return null;
  for (const profile of RuntimeProfiles) {
    if (text.includes(profile.detect)) return profile;
  }
  return null;
}

export function readRuntimeVersion(installDir) {
  const infoPath = path.join(installDir || getDefaultInstallDir(), RuntimeInfoRelativePath);
  try {
    return JSON.parse(fs.readFileSync(infoPath, 'utf8')).version || null;
  } catch {
    return null;
  }
}

export function getDefaultInstallDir() {
  const custom = process.env.QODER_CN_INSTALL_DIR;
  if (custom && fs.existsSync(custom)) return path.resolve(custom);
  
  const standard = "/Applications/Qoder CN.app";
  if (fs.existsSync(standard)) return standard;

  const userApp = path.join(os.homedir(), "Applications/Qoder CN.app");
  if (fs.existsSync(userApp)) return userApp;

  return standard;
}

export function getDefaultBackupRoot() {
  const home = os.homedir() || process.env.HOME || '';
  return path.join(home, ".qoder-cn", "backups");
}

export function getDefaultConfigPath() {
  const home = os.homedir() || process.env.HOME || '';
  return path.join(home, ".qoder-cn", "custom-openai-provider-v3.2.0.json");
}

export function getFileSha256(filePath) {
  if (!fs.existsSync(filePath)) return "";
  const buf = fs.readFileSync(filePath);
  return crypto.createHash('sha256').update(buf).digest('hex');
}

export function countOccurrences(source, substr) {
  if (!source || !substr) return 0;
  let count = 0, pos = 0;
  while ((pos = source.indexOf(substr, pos)) !== -1) {
    count++;
    pos += substr.length;
  }
  return count;
}

export function replaceExactlyOnce(text, anchor, replacement, description) {
  const count = countOccurrences(text, anchor);
  if (count !== 1) {
    throw new Error(`Anchor for ${description} matched ${count} times (expected 1).`);
  }
  const idx = text.indexOf(anchor);
  return text.slice(0, idx) + replacement + text.slice(idx + anchor.length);
}

export function isQoderRunning(installDir) {
  try {
    const out = execSync('pgrep -f "Qoder CN"', { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
    return Boolean(out && out.trim().length > 0);
  } catch {
    return false;
  }
}

export function closeQoder(force = false) {
  try {
    if (force) {
      execSync('pkill -9 -f "Qoder CN"', { stdio: 'ignore' });
    } else {
      execSync('pkill -f "Qoder CN"', { stdio: 'ignore' });
    }
    return true;
  } catch {
    return false;
  }
}

export function getTargetState(installDir) {
  const dir = installDir || getDefaultInstallDir();
  const runtimePath = path.join(dir, RuntimeRelativePath);
  const asarPath = path.join(dir, AsarRelativePath);

  const state = {
    installDir: dir,
    appExists: fs.existsSync(dir),
    runtimePath,
    asarPath,
    runtimeExists: fs.existsSync(runtimePath),
    asarExists: fs.existsSync(asarPath),
    runtimeSha256: "",
    asarSha256: "",
    runtimePatched: false,
    previousRuntimePatched: false,
    legacyRuntimePatched: false,
    isRunning: isQoderRunning(dir),
    runtimeVersion: readRuntimeVersion(dir),
    profileId: null,
    detectedVersion: "",
    canApply: false,
    statusText: ""
  };

  if (state.asarExists) {
    state.asarSha256 = getFileSha256(asarPath);
  }

  if (state.runtimeExists) {
    state.runtimeSha256 = getFileSha256(runtimePath);
    const text = fs.readFileSync(runtimePath, 'utf8');

    state.runtimePatched = text.includes(PatchMarker);

    let prev = false;
    for (const m of PreviousPatchMarkers) {
      if (!state.runtimePatched && text.includes(m)) {
        prev = true;
        break;
      }
    }
    state.previousRuntimePatched = prev;
    state.legacyRuntimePatched = text.includes(LegacyPatchMarker);

    // Profile detection
    const profile = detectProfile(text);
    state.profileId = profile ? profile.id : null;
    state.detectedVersion = profile ? profile.label : "unknown";

    if (state.runtimePatched) {
      state.statusText = "🟢 已修补 (v3.2.0)";
    } else if (state.previousRuntimePatched) {
      state.statusText = "🟡 可升级历史补丁";
      state.canApply = true;
    } else if (state.legacyRuntimePatched) {
      state.statusText = "🔴 存在旧版 v1 补丁 (需先还原)";
    } else if (profile) {
      state.statusText = "⚪ 官方原版 (可一键修补)";
      state.canApply = true;
    } else if (state.runtimeVersion) {
      state.statusText = `⚠️ 检测到 Qoder CN 运行时 ${state.runtimeVersion}，Patcher 尚未适配该版本`;
    } else {
      state.statusText = "⚠️ 未知版本运行库";
    }
  } else {
    state.statusText = "❌ 未找到 Qoder CN 运行库";
  }

  return state;
}

export function patchRuntimeText(originalText) {
  if (originalText.includes(PatchMarker)) {
    throw new Error("The v3.2.0 runtime patch is already installed.");
  }
  for (const m of PreviousPatchMarkers) {
    if (originalText.includes(m)) {
      throw new Error("An older runtime patch is present. Upgrade must start from its verified original backup.");
    }
  }
  if (originalText.includes(LegacyPatchMarker)) {
    throw new Error("The legacy v1 runtime patch is present. Restore v1 before applying v3.2.0.");
  }

  let text = originalText;

  const profile = detectProfile(text);
  if (!profile) {
    const supported = RuntimeProfiles.map(p => p.id).join(", ");
    throw new Error(`Target file does not match a known Qoder runtime version (supported: ${supported}).`);
  }

  // 1. Import node:fs
  const importAnchor = profile.importStyle === "banner"
    ? "import{createRequire as __banner_createRequire}"
    : "import*as P8e from\"node:path\";import*as qxA from\"node:fs/promises\";";
  text = replaceExactlyOnce(
    text,
    importAnchor,
    `import*as qcv30fs from"node:fs";${importAnchor}`,
    "node:fs import"
  );

  // 2. Helpers and Model Converter
  const helperFunctions = `function qcv30cfg(){try{let h=process.env.HOME||process.env.USERPROFILE||"";let p=process.env.QODER_CN_CUSTOM_PROVIDER_CONFIG||(h?h+"/.qoder-cn/custom-openai-provider-v3.2.0.json":"");if(!p||!qcv30fs.existsSync(p)){let a0000=h?h+"/.qoder-cn/custom-openai-provider-v3.1.0.json":"";let a000=h?h+"/.qoder-cn/custom-openai-provider-v3.0.1.json":"";let a00=h?h+"/.qoder-cn/custom-openai-provider-v3.0.json":"";let a0=h?h+"/.qoder-cn/custom-openai-provider-v2.3.json":"";let a1=h?h+"/.qoder-cn/custom-openai-provider-v2.2.json":"";let a2=h?h+"/.qoder-cn/custom-openai-provider-v2.1.json":"";let a3=h?h+"/.qoder-cn/custom-openai-provider.json":"";p=a0000&&qcv30fs.existsSync(a0000)?a0000:a000&&qcv30fs.existsSync(a000)?a000:a00&&qcv30fs.existsSync(a00)?a00:a0&&qcv30fs.existsSync(a0)?a0:a1&&qcv30fs.existsSync(a1)?a1:a2&&qcv30fs.existsSync(a2)?a2:a3&&qcv30fs.existsSync(a3)?a3:p}let txt=p&&qcv30fs.existsSync(p)?qcv30fs.readFileSync(p,"utf8").replace(/^\\uFEFF/,"").trim():"";return txt?JSON.parse(txt):null}catch(A){return null}}function qcv30base(A){try{let e=new URL(A);if("http:"!==e.protocol&&"https:"!==e.protocol||e.username||e.password||e.search||e.hash)return;let t=e.pathname.replace(/\\/+$/g,"");return t.endsWith("/chat/completions")&&(t=t.slice(0,-17)),e.pathname=t||"/",e.toString().replace(/\\/$/,"")}catch{}}function qcv30url(A){try{let e=qcv30cfg();if(e&&"string"==typeof A){if(e.uiBaseUrl&&new URL(e.uiBaseUrl).toString()===new URL(A).toString())return qcv30base(e.upstreamBaseUrl)??A;if(Array.isArray(e.models)){let m=e.models.find(x=>x&&x.uiBaseUrl&&new URL(x.uiBaseUrl).toString()===new URL(A).toString());if(m)return qcv30base(m.upstreamBaseUrl||e.upstreamBaseUrl)??A}}}catch{}return A}function qcv30model(A){try{let e=qcv30cfg();if(e&&Array.isArray(e.models)){let p=e.replaceProviderKey||"bailian",k="string"==typeof A&&A.includes("/")?A.split("/").pop():A,m=e.models.find(e=>e&&(e.id===A||e.id===k||e.displayName===A));if(m)return ${profile.converterFn}({key:m.id,source:"system",display_name:m.displayName??m.id,provider:p,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:m.uiBaseUrl||e.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled})}}catch(A){}}function qcv30target(A){let e=qcv30cfg();if(!e||!Array.isArray(e.models))return;let t=A?.custom_model,i=A?.model_config;let rawKey=t?.model||i?.key||A?.model||("string"==typeof A?A:"")||"";let k="string"==typeof rawKey&&rawKey.includes("/")?rawKey.split("/").pop():rawKey;let n=e.models.find(m=>m&&(m.id===rawKey||m.id===k||m.displayName===rawKey));if(!n)return;let r=process.env["QODER_CN_KEY_"+(n.providerId||"")]||process.env.QODER_CN_CUSTOM_PROVIDER_API_KEY||t?.parameters?.api_key||t?.api_key||n.apiKey||e.apiKey;let o=qcv30base(n.upstreamBaseUrl||e.upstreamBaseUrl);if("string"!=typeof r||!r.trim())throw Error("QODER_CN_PATCH_API_KEY_MISSING");if(!o)throw Error("QODER_CN_PATCH_UPSTREAM_URL_INVALID");let s=Number.isInteger(e.firstPayloadTimeoutMs)&&e.firstPayloadTimeoutMs>0?e.firstPayloadTimeoutMs:6e4,a=Number.isInteger(e.streamIdleTimeoutMs)&&e.streamIdleTimeoutMs>=0?e.streamIdleTimeoutMs:0,g=Number.isInteger(n.maxInputTokens)&&n.maxInputTokens>0?n.maxInputTokens:131072,l=Number.isInteger(n.maxOutputTokens)&&n.maxOutputTokens>0?n.maxOutputTokens:32768;return{providerId:"qoder-cn-patcher",adapter:"openai-compatible",baseUrl:o,apiKey:r,model:{modelId:n.id,displayName:n.displayName??n.id,contextWindow:g,maxOutputTokens:l,capabilities:{tools:!1!==n.tools,vision:!0===n.vision,thinking:!0===n.reasoning},maxTokensField:"max_completion_tokens"===n.maxTokensField?"max_completion_tokens":"max_tokens"},timeouts:{firstPayloadTimeoutMs:s,...a>0?{streamIdleTimeoutMs:a}:{}}}}`;

  const converterAnchor = `function ${profile.converterFn}(A){`;
  text = replaceExactlyOnce(
    text,
    converterAnchor,
    helperFunctions + converterAnchor + "/*" + PatchMarker + "*/\n",
    "BYOK model converter"
  );

  // 2b. Converter model source (prevent lNe filter in desktop main process from dropping models)
  if (profile.converterSourceAnchor && text.includes(profile.converterSourceAnchor)) {
    text = replaceExactlyOnce(
      text,
      profile.converterSourceAnchor,
      profile.converterSourceRepl,
      "converter model source"
    );
  }

  // 3. Model URL
  text = replaceExactlyOnce(
    text,
    "url:A.url,model:A.model,provider:A.provider",
    "url:qcv30url(A.url),model:A.model,provider:A.provider",
    "BYOK inference URL"
  );

  // 4. Catalog replacement
  const catalogPayload = `if(qcp.length===0){qcp=[{key:"bailian",display_name:"Alibaba Cloud Model Studio",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"bailian",display_name:"Alibaba Cloud Model Studio",style:"bailian",models:[]}]},{key:"deepseek",display_name:"DeepSeek",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"deepseek",display_name:"DeepSeek",style:"deepseek",models:[]}]},{key:"anthropic",display_name:"Anthropic",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"anthropic",display_name:"Anthropic",style:"anthropic",models:[]}]},{key:"openai",display_name:"OpenAI",fields:[{key:"api_key",display_name:"API Key",type:"free_input",mandatory:true}],types:[{key:"openai",display_name:"OpenAI",style:"openai",models:[]}]}]}try{let qcc=qcv30cfg();if(qcc){let p=qcc.replaceProviderKey||"bailian",name=qcc.replaceProviderDisplayName||qcc.displayName||"Custom OpenAI Compatible";let targetP=qcp.find(x=>x&&x.key===p);if(!targetP&&qcp.length>0)targetP=qcp[0];if(targetP){targetP.display_name=name;if(Array.isArray(targetP.types)){if(targetP.types.length===0){targetP.types=[{key:p,display_name:name,style:p,models:[]}]}for(let t of targetP.types){if(t){t.key=t.key||p;t.display_name=name;if(Array.isArray(qcc.models)){t.models=qcc.models.map(m=>({key:m.id,display_name:m.displayName??m.id,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled}))}}}}}}}catch(qce){${profile.logger}.warn("[qoder-cn-openai-patch-v3.2.0] catalog replacement failed:",qce)}`;

  const catalogHead = `n=await e().getBYOKConfig(),r=${profile.catalogStateVar};`;
  const catalogAnchor = `${catalogHead}${profile.emitPrefix}providers:n?.providers.map(A)??[]${profile.emitSuffix}`;
  const catalogRepl = `${catalogHead}let qcp=n?.providers.map(A)??[];${catalogPayload}${profile.emitPrefix}providers:qcp${profile.emitSuffix}`;
  text = replaceExactlyOnce(text, catalogAnchor, catalogRepl, "BYOK catalog");

  // 5. Validation bypass
  const validationBypassCode = `let modelMatch=Array.isArray(qcv.models)&&qcv.models.some(m=>m&&(m.id===n.model||m.displayName===n.model));let providerMatch=n.provider===(qcv.replaceProviderKey||"bailian")||"custom"===n.provider;let urlMatch=false;if(qcv.uiBaseUrl&&A&&typeof A==="string"){try{urlMatch=new URL(qcv.uiBaseUrl).origin===new URL(A).origin}catch{}}if(modelMatch||providerMatch||urlMatch)return true`;

  const validationCall = `r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,${profile.validationLastArg})`;
  const validationAnchor = `o=await ${validationCall};${profile.emitPrefix}success:o${profile.emitSuffix}`;
  const validationRepl = `o=await(async()=>{try{let qcv=qcv30cfg();if(qcv&&qcv.skipValidation!==false){${validationBypassCode}}}catch(qce){${profile.logger}.warn("[qoder-cn-openai-patch-v3.2.0] validation bypass error:",qce)}return ${validationCall}})();${profile.emitPrefix}success:o${profile.emitSuffix}`;
  text = replaceExactlyOnce(text, validationAnchor, validationRepl, "BYOK validation");

  // 6. Direct Inference Route
  const inferenceCall = `${profile.inferenceFn}(t.model_config,()=>S?.getExternalProviderRegistry())`;
  text = replaceExactlyOnce(
    text,
    `let A=${inferenceCall};for(;;)`,
    `let A=qcv30target(t)??${inferenceCall};for(;;)`,
    "direct inference route"
  );

  // 7. Startup BYOK injection
  const startupInjection = `try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||"bailian";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,source:"system",display_name:m.displayName??m.id,provider:qpk,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:qcc.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};n.has(m.id)||n.set(m.id,item)}}}}catch(qce){${profile.logger}.warn("[qoder-cn-openai-patch-v3.2.0] startup custom models injection failed:",qce)}let r=[...n.values()];`;

  const startupHead = `async function ${profile.startupFn}(A,e){let t=e?await ${profile.startupFetchFn}(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);`;
  text = replaceExactlyOnce(
    text,
    startupHead + "let r=[...n.values()];",
    startupHead + startupInjection,
    "startup BYOK injection"
  );

  // 8. Model list injection
  const modelListInjection = `try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||"bailian";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,source:"system",display_name:m.displayName??m.id,provider:qpk,model:m.id,type:"openai-compatible",parameters:{api_key:""},url:qcc.uiBaseUrl,format:"openai",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};i.some(x=>x.key===m.id)||i.push(item)}}}}catch(qce){${profile.logger}.warn("[qoder-cn-openai-patch-v3.2.0] model list injection failed:",qce)}`;

  const modelListAnchor = `function ${profile.modelListFn}(A,e,t){let i="function"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];`;
  text = replaceExactlyOnce(text, modelListAnchor, modelListAnchor + modelListInjection, "model list injection");

  // 9. getModel resolution fallback
  const getModelAnchor = "getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)}";
  const getModelRepl = "getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)??qcv30model(A)}";
  text = replaceExactlyOnce(text, getModelAnchor, getModelRepl, "catalog getModel resolution");

  return text;
}

export function newBackup(installDir, backupRoot = getDefaultBackupRoot()) {
  const runtimePath = path.join(installDir, RuntimeRelativePath);
  const asarPath = path.join(installDir, AsarRelativePath);

  const now = new Date();
  const dateStr = now.toISOString().replace(/[-:T]/g, '').slice(0, 14);
  const id = `${dateStr}-${crypto.randomBytes(4).toString('hex')}`;
  const dir = path.join(backupRoot, id);

  fs.mkdirSync(dir, { recursive: true });

  const runtimeBackup = path.join(dir, "qoder-worker-runtime.obf.mjs");
  fs.copyFileSync(runtimePath, runtimeBackup);

  const runtimeSha256 = getFileSha256(runtimePath);
  const backupProfile = RuntimeProfiles.find(p => p.pristineSha256 === runtimeSha256.toLowerCase());

  const manifest = {
    backupId: id,
    createdAt: now.toISOString(),
    installDir: path.resolve(installDir),
    runtimePath: path.resolve(runtimePath),
    runtimeBackup: path.resolve(runtimeBackup),
    runtimeSha256,
    runtimeVersion: readRuntimeVersion(installDir) || (backupProfile ? backupProfile.id : null),
    profileId: backupProfile ? backupProfile.id : null,
    appAsarPath: path.resolve(asarPath),
    appAsarSha256: getFileSha256(asarPath),
    patchVersion: "3.2.0"
  };

  fs.writeFileSync(path.join(dir, "manifest.json"), JSON.stringify(manifest, null, 2), 'utf8');
  return manifest;
}

export function getLatestBackup(backupRoot = getDefaultBackupRoot(), installDir = getDefaultInstallDir(), specificBackupId = null) {
  if (!fs.existsSync(backupRoot)) {
    throw new Error(`No backups found in: ${backupRoot}`);
  }

  if (specificBackupId) {
    const manifestPath = path.join(backupRoot, specificBackupId, "manifest.json");
    if (!fs.existsSync(manifestPath)) {
      throw new Error(`Backup not found: ${specificBackupId}`);
    }
    return JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  }

  const entries = fs.readdirSync(backupRoot, { withFileTypes: true })
    .filter(e => e.isDirectory())
    .map(e => path.join(backupRoot, e.name, "manifest.json"))
    .filter(p => fs.existsSync(p))
    .map(p => {
      try {
        return JSON.parse(fs.readFileSync(p, 'utf8'));
      } catch {
        return null;
      }
    })
    .filter(m => m && path.resolve(m.installDir) === path.resolve(installDir))
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

  if (entries.length === 0) {
    throw new Error(`No backup belongs to this installation: ${installDir}`);
  }
  return entries[0];
}

export function getOriginalRuntimeBackup(backupRoot = getDefaultBackupRoot(), installDir = getDefaultInstallDir()) {
  const latest = getLatestBackup(backupRoot, installDir);
  return latest;
}

export function resolveBackupVersion(manifest) {
  if (manifest.runtimeVersion) return manifest.runtimeVersion;
  const profile = RuntimeProfiles.find(p => p.pristineSha256 === (manifest.runtimeSha256 || "").toLowerCase());
  return profile ? profile.id : null;
}

// Qoder CN updates the runtime file in place, so writing back a backup taken
// from a different release leaves the app unable to start.
export function assertBackupMatchesInstall(manifest, installDir, context, force = false) {
  if (force) return;
  const backupVersion = resolveBackupVersion(manifest);
  const currentVersion = readRuntimeVersion(installDir);
  if (!backupVersion || !currentVersion || backupVersion === currentVersion) return;
  throw new Error(
    `${context}：备份来自 Qoder CN 运行时 ${backupVersion}，但当前安装是 ${currentVersion}，` +
    `跨版本写回会导致 Qoder CN 无法启动。请先重新执行修补以生成对应当前版本的备份；` +
    `确需强制写回请加 --force。`
  );
}

function writeRuntimeAtomically(runtimePath, text, rollbackSource) {
  const temp = `${runtimePath}.tmp-${crypto.randomBytes(4).toString('hex')}`;
  try {
    fs.writeFileSync(temp, text, 'utf8');
    fs.renameSync(temp, runtimePath);
  } catch (err) {
    if (rollbackSource && fs.existsSync(rollbackSource)) {
      try { fs.copyFileSync(rollbackSource, runtimePath); } catch {}
    }
    throw err;
  } finally {
    if (fs.existsSync(temp)) {
      try { fs.unlinkSync(temp); } catch {}
    }
  }
}

export function applyPatch(installDir = getDefaultInstallDir(), backupRoot = getDefaultBackupRoot(), force = false) {
  if (isQoderRunning(installDir)) {
    throw new Error("Qoder CN is currently running. Please close it before patching.");
  }

  const runtimePath = path.join(installDir, RuntimeRelativePath);
  const state = getTargetState(installDir);

  if (state.runtimePatched || state.previousRuntimePatched) {
    const origManifest = getOriginalRuntimeBackup(backupRoot, installDir);
    assertBackupMatchesInstall(origManifest, installDir, "升级已中止", force);
    const upgradeOriginalText = fs.readFileSync(origManifest.runtimeBackup, 'utf8');
    const upgradedText = patchRuntimeText(upgradeOriginalText);
    writeRuntimeAtomically(runtimePath, upgradedText, origManifest.runtimeBackup);
    return { upgraded: true, backupId: origManifest.backupId };
  }

  if (state.legacyRuntimePatched) {
    throw new Error("The legacy v1 runtime patch is present. Restore it before applying v3.2.0.");
  }

  if (!state.runtimeExists) {
    throw new Error(`Runtime file does not exist: ${runtimePath}`);
  }

  const backup = newBackup(installDir, backupRoot);
  const originalText = fs.readFileSync(runtimePath, 'utf8');
  const patchedText = patchRuntimeText(originalText);
  writeRuntimeAtomically(runtimePath, patchedText, backup.runtimeBackup);
  return { upgraded: false, backupId: backup.backupId };
}

export function restorePatch(installDir = getDefaultInstallDir(), backupRoot = getDefaultBackupRoot(), specificBackupId = null, force = false) {
  if (isQoderRunning(installDir)) {
    throw new Error("Qoder CN is currently running. Please close it before restoring.");
  }

  const runtimePath = path.join(installDir, RuntimeRelativePath);
  const manifest = getLatestBackup(backupRoot, installDir, specificBackupId);

  if (!fs.existsSync(manifest.runtimeBackup)) {
    throw new Error(`Runtime backup file is missing: ${manifest.runtimeBackup}`);
  }

  const backupHash = getFileSha256(manifest.runtimeBackup);
  if (backupHash.toLowerCase() !== manifest.runtimeSha256.toLowerCase()) {
    throw new Error("Runtime backup hash verification failed.");
  }

  assertBackupMatchesInstall(manifest, installDir, "还原已中止", force);

  const restoreText = fs.readFileSync(manifest.runtimeBackup, 'utf8');
  writeRuntimeAtomically(runtimePath, restoreText, null);
  return manifest;
}

export function launchQoder(installDir = getDefaultInstallDir(), extraEnv = {}) {
  const env = {
    ...process.env,
    ...extraEnv
  };

  const child = spawn('open', ['-a', installDir], {
    env,
    detached: true,
    stdio: 'ignore'
  });
  child.unref();
  return true;
}
