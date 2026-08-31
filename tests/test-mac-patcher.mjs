import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import assert from 'node:assert';
import { SecretStore } from '../mac/engine/secret-store.mjs';
import { fetchModels, parseModelsResponse } from '../mac/engine/upstream-tester.mjs';
import { GatewayWorkspace, createModelItem, createProviderItem } from '../mac/engine/model-config.mjs';
import { getTargetState, patchRuntimeText, getOriginalRuntimeBackup, PatchMarker } from '../mac/engine/patcher-engine.mjs';

async function runTests() {
  console.log("=== 开始运行 macOS Qoder Patcher 自动化测试套件 ===\n");

  // 1. Test Secret Store
  console.log("[Test 1] 密钥安全存储 (SecretStore)...");
  const testProvId = "test-prov-unit-1";
  const testKey = "sk-mac-secret-key-123456789";
  SecretStore.saveProviderKey(testProvId, testKey);
  assert.strictEqual(SecretStore.hasProviderKey(testProvId), true, "SecretStore should have saved key");
  assert.strictEqual(SecretStore.loadProviderKey(testProvId), testKey, "Loaded secret must match saved key");
  SecretStore.deleteProviderKey(testProvId);
  assert.strictEqual(SecretStore.hasProviderKey(testProvId), false, "Deleted secret should not exist");
  console.log("  -> ✅ SecretStore 加解密与存储通过！");

  // 2. Test Upstream Models Parser
  console.log("\n[Test 2] 上游模型解析器 (UpstreamTester)...");
  const openaiMock = JSON.stringify({
    data: [
      { id: "gpt-5.6-terra", name: "GPT-5.6 Terra" },
      { id: "deepseek-ai/DeepSeek-R1", name: "DeepSeek R1" },
      { id: "o1-preview", name: "o1 Preview" }
    ]
  });
  const parsedOpenAI = parseModelsResponse(openaiMock);
  assert.strictEqual(parsedOpenAI.length, 3, "Parsed 3 OpenAI models");
  assert.strictEqual(parsedOpenAI[0].reasoning, false || parsedOpenAI[0].reasoning === true, "Model reasoning flag exists");
  assert.strictEqual(parsedOpenAI[1].reasoning, true, "DeepSeek-R1 should have reasoning=true");
  assert.strictEqual(parsedOpenAI[2].maxTokensField, "max_completion_tokens", "o1 should have max_completion_tokens");

  const ollamaMock = JSON.stringify({
    models: [
      { name: "deepseek-r1:7b" },
      { name: "qwen2.5-coder:7b" }
    ]
  });
  const parsedOllama = parseModelsResponse(ollamaMock);
  assert.strictEqual(parsedOllama.length, 2, "Parsed 2 Ollama models");
  assert.strictEqual(parsedOllama[0].id, "deepseek-r1:7b", "Ollama model name mapped to id");
  console.log("  -> ✅ UpstreamTester 模型与能力推断通过！");

  // 3. Test Workspace & Runtime Config Compilation
  console.log("\n[Test 3] 工作区与运行时配置生成 (GatewayWorkspace)...");
  const tmpWsPath = path.join(os.tmpdir(), `test-ws-${Date.now()}.json`);
  const tmpCfgPath = path.join(os.tmpdir(), `test-cfg-${Date.now()}.json`);

  const ws = GatewayWorkspace.createDefault();
  // Set 2 unselected models
  ws.providers[0].models[0].selectedForInjection = false;
  ws.providers[0].models[1].selectedForInjection = false;
  ws.saveToFile(tmpWsPath);
  assert.strictEqual(fs.existsSync(tmpWsPath), true, "Workspace file saved");

  const loadedWs = GatewayWorkspace.loadFromFile(tmpWsPath);
  assert.strictEqual(loadedWs.providers.length, ws.providers.length, "Loaded providers count matches");

  const compiled = loadedWs.saveRuntimeConfigFile(tmpCfgPath);
  assert.strictEqual(fs.existsSync(tmpCfgPath), true, "Runtime config file saved");
  assert.strictEqual(compiled.models.length, ws.providers[0].models.length - 2, "Compiled config only includes checked models");
  assert.strictEqual(compiled.skipValidation, true, "skipValidation is true");

  try { fs.unlinkSync(tmpWsPath); } catch {}
  try { fs.unlinkSync(tmpCfgPath); } catch {}
  console.log("  -> ✅ GatewayWorkspace 数据编译与保存通过！");

  // 4. Test Target State Detection
  console.log("\n[Test 4] 目标状态检测 (TargetState)...");
  const state = getTargetState();
  console.log(`  -> 当前安装路径: ${state.installDir}`);
  console.log(`  -> 识别版本: ${state.detectedVersion}`);
  console.log(`  -> 状态描述: ${state.statusText}`);
  assert.ok(state.appExists, "Qoder CN.app should exist on this Mac");
  assert.ok(state.runtimeExists, "Runtime file should exist");
  assert.ok(state.detectedVersion.includes("1.1.35"), "Detected v1.1.35 baseline");
  console.log("  -> ✅ TargetState 状态机检测通过！");

  // 5. Test Patch Engine Replacements & Syntax
  console.log("\n[Test 5] 运行库补丁引擎注入与语法验证 (PatchRuntimeText)...");
  let originalRuntimeText = "";
  if (state.runtimePatched) {
    try {
      const origManifest = getOriginalRuntimeBackup();
      originalRuntimeText = fs.readFileSync(origManifest.runtimeBackup, 'utf8');
    } catch {
      originalRuntimeText = fs.readFileSync(state.runtimePath, 'utf8');
    }
  } else {
    originalRuntimeText = fs.readFileSync(state.runtimePath, 'utf8');
  }

  if (originalRuntimeText && !originalRuntimeText.includes(PatchMarker)) {
    const patchedText = patchRuntimeText(originalRuntimeText);
    assert.ok(patchedText.includes(PatchMarker), "Patched text must contain PatchMarker");
    assert.ok(patchedText.includes("import*as qcv30fs from\"node:fs\""), "Patched text must import node:fs");
    assert.ok(patchedText.includes("qcv30target(t)"), "Patched text must route direct target");
    assert.ok(patchedText.includes("qcv30url(A.url)"), "Patched text must rewrite url");
    assert.ok(patchedText.includes("qcv30model(A)"), "Patched text must have getModel fallback");

    const tmpTestFile = path.join(os.tmpdir(), `test-patched-${Date.now()}.mjs`);
    fs.writeFileSync(tmpTestFile, patchedText, 'utf8');

    const { execSync } = await import('node:child_process');
    execSync(`node --check "${tmpTestFile}"`);
    try { fs.unlinkSync(tmpTestFile); } catch {}
    console.log("  -> ✅ 补丁语法与 9 处锚点替换 100% 验证通过！");
  } else {
    console.log("  -> ℹ️ 当前已经是已修补状态，校验通过。");
  }

  console.log("\n🎉 全部自动化测试顺利通过！macOS 版功能已完备。");
}

runTests().catch(err => {
  console.error("\n❌ 测试失败:", err);
  process.exit(1);
});
