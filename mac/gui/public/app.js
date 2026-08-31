// State variables
let targetState = null;
let workspace = { providers: [], selectedModelKeys: [] };
let activeProviderFilter = 'all';
let searchKeyword = '';
let currentEditingProviderId = null;
let currentEditingModelKey = null;

// DOM Elements
const statusPill = document.getElementById('status-pill');
const statusPillText = document.getElementById('status-pill-text');
const btnPrimaryAction = document.getElementById('btn-primary-action');
const btnPrimaryText = document.getElementById('btn-primary-text');
const btnLaunchQoder = document.getElementById('btn-launch-qoder');
const btnRestore = document.getElementById('btn-restore');
const btnOpenDir = document.getElementById('btn-open-dir');
const btnBrowsePath = document.getElementById('btn-browse-path');
const installPathInput = document.getElementById('install-path-input');

const providersList = document.getElementById('providers-list');
const modelsTbody = document.getElementById('models-tbody');
const filterProvider = document.getElementById('filter-provider');
const searchModelInput = document.getElementById('search-model');
const modelStatsBadge = document.getElementById('model-selection-stats');

const tabProviderCount = document.getElementById('tab-provider-count');
const tabModelCount = document.getElementById('tab-model-count');
const logsConsole = document.getElementById('logs-console');

// Preset dropdown
const btnPresetMenu = document.getElementById('btn-preset-menu');
const presetDropdownMenu = document.getElementById('preset-dropdown-menu');

// Modals
const modalProvider = document.getElementById('modal-provider');
const modalModel = document.getElementById('modal-model');

// Logging helper
function appendLog(message, level = 'info') {
  const now = new Date().toLocaleTimeString();
  const line = document.createElement('div');
  line.className = `log-line log-${level}`;
  line.innerHTML = `<span class="log-time">[${now}]</span> [${level.toUpperCase()}] ${escapeHtml(message)}`;
  logsConsole.appendChild(line);
  logsConsole.scrollTop = logsConsole.scrollHeight;
}

function escapeHtml(str) {
  if (!str) return '';
  return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// API Helper
async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { 'Content-Type': 'application/json' },
    ...options
  });
  const data = await res.json();
  if (!res.ok) {
    throw new Error(data.error || data.message || `Request failed with status ${res.status}`);
  }
  return data;
}

// Initialize Application
async function init() {
  setupEventListeners();
  appendLog("Qoder CN Gateway Manager (macOS) 控制台已就绪", "info");
  await refreshAll();
  
  // Polling status
  setInterval(refreshStatusOnly, 4000);
}

async function refreshAll() {
  await Promise.all([refreshStatusOnly(), loadWorkspace()]);
}

async function refreshStatusOnly() {
  try {
    const installDir = installPathInput.value.trim();
    targetState = await api(`/api/status?installDir=${encodeURIComponent(installDir)}`);
    updateStatusUI();
    renderDiagnostics();
  } catch (err) {
    appendLog(`状态检测异常: ${err.message}`, "error");
  }
}

function updateStatusUI() {
  if (!targetState) return;

  statusPill.className = 'status-pill';
  
  if (targetState.runtimePatched) {
    statusPill.classList.add('status-green');
    statusPillText.textContent = targetState.isRunning ? '🟢 已修补 (Qoder 运行中)' : '🟢 已修补 (v3.2.0)';
    btnPrimaryText.textContent = targetState.isRunning ? '🔥 热重载配置' : '⚡ 启动 Qoder CN';
    btnPrimaryAction.className = targetState.isRunning ? 'btn btn-primary btn-large' : 'btn btn-success btn-large';
  } else if (targetState.previousRuntimePatched) {
    statusPill.classList.add('status-yellow');
    statusPillText.textContent = '🟡 可升级历史补丁';
    btnPrimaryText.textContent = '🚀 一键升级到 v3.2.0';
    btnPrimaryAction.className = 'btn btn-primary btn-large';
  } else if (targetState.legacyRuntimePatched) {
    statusPill.classList.add('status-red');
    statusPillText.textContent = '🔴 存在旧版 v1 补丁';
    btnPrimaryText.textContent = '🔄 需先恢复原版';
    btnPrimaryAction.className = 'btn btn-danger btn-large';
  } else if (targetState.detectedVersion !== 'unknown') {
    statusPill.classList.add('status-white');
    statusPillText.textContent = '⚪ 官方原版 (就绪)';
    btnPrimaryText.textContent = '🚀 一键安装修补';
    btnPrimaryAction.className = 'btn btn-primary btn-large';
  } else {
    statusPill.classList.add('status-red');
    statusPillText.textContent = targetState.statusText || '⚠️ 状态未知';
    btnPrimaryText.textContent = '⚠️ 无法操作';
    btnPrimaryAction.className = 'btn btn-secondary btn-large';
  }
}

async function loadWorkspace() {
  try {
    workspace = await api('/api/workspace');
    renderProviders();
    renderProviderFilterOptions();
    renderModels();
  } catch (err) {
    appendLog(`加载工作区配置失败: ${err.message}`, "error");
  }
}

function renderProviders() {
  providersList.innerHTML = '';
  tabProviderCount.textContent = workspace.providers.length;

  if (workspace.providers.length === 0) {
    providersList.innerHTML = `<div class="empty-hint" style="padding: 30px; text-align: center; color: var(--text-muted); grid-column: 1/-1;">暂未添加任何 AI 提供方，请点击左上方“添加提供方”或“快速载入预设”。</div>`;
    return;
  }

  workspace.providers.forEach(p => {
    const card = document.createElement('div');
    card.className = `provider-card ${p.enabled ? '' : 'disabled'}`;
    
    const keyBadge = p.hasApiKey 
      ? `<span class="badge badge-green">🔐 API Key 已加密</span>` 
      : `<span class="badge badge-yellow">⚠️ 未设置 Key</span>`;

    const statusBadge = p.enabled 
      ? `<span class="badge badge-blue">🟢 已启用</span>` 
      : `<span class="badge badge-yellow">⚪ 已禁用</span>`;

    card.innerHTML = `
      <div class="prov-header">
        <div class="prov-title">
          <span>${escapeHtml(p.name)}</span>
        </div>
        <div class="prov-badges">
          ${statusBadge}
          ${keyBadge}
        </div>
      </div>
      <div class="prov-details">
        <div class="prov-detail-row">
          <span>上游 Base URL:</span>
          <span class="prov-detail-val font-mono">${escapeHtml(p.baseUrl)}</span>
        </div>
        <div class="prov-detail-row">
          <span>UI 占位 URL:</span>
          <span class="prov-detail-val font-mono">${escapeHtml(p.uiBaseUrl || '-')}</span>
        </div>
        <div class="prov-detail-row">
          <span>替换 Provider 槽位:</span>
          <span class="prov-detail-val">${escapeHtml(p.replaceProviderKey)}</span>
        </div>
        <div class="prov-detail-row">
          <span>包含模型数量:</span>
          <span class="prov-detail-val">${(p.models || []).length} 个</span>
        </div>
      </div>
      <div class="prov-actions">
        <div>
          <button class="btn btn-secondary btn-sm btn-probe-prov" data-id="${p.id}">📡 探测模型</button>
          <button class="btn btn-secondary btn-sm btn-edit-prov" data-id="${p.id}">✏️ 编辑</button>
        </div>
        <button class="btn btn-danger btn-sm btn-del-prov" data-id="${p.id}">🗑️ 删除</button>
      </div>
    `;

    providersList.appendChild(card);
  });

  // Attach event listeners
  document.querySelectorAll('.btn-edit-prov').forEach(b => {
    b.onclick = () => openProviderModal(b.dataset.id);
  });
  document.querySelectorAll('.btn-del-prov').forEach(b => {
    b.onclick = () => deleteProvider(b.dataset.id);
  });
  document.querySelectorAll('.btn-probe-prov').forEach(b => {
    b.onclick = () => probeSingleProvider(b.dataset.id);
  });
}

function renderProviderFilterOptions() {
  const current = filterProvider.value;
  filterProvider.innerHTML = `<option value="all">全部提供方 (${workspace.providers.length})</option>`;
  workspace.providers.forEach(p => {
    const opt = document.createElement('option');
    opt.value = p.id;
    opt.textContent = `${p.name} (${(p.models || []).length})`;
    filterProvider.appendChild(opt);
  });
  if (current && Array.from(filterProvider.options).some(o => o.value === current)) {
    filterProvider.value = current;
  }
}

function getAllModelsWithProvider() {
  const list = [];
  workspace.providers.forEach(p => {
    if (!p.models) return;
    p.models.forEach(m => {
      list.push({
        ...m,
        providerId: p.id,
        providerName: p.name,
        providerEnabled: p.enabled
      });
    });
  });
  return list;
}

function renderModels() {
  modelsTbody.innerHTML = '';
  const all = getAllModelsWithProvider();
  tabModelCount.textContent = all.length;

  const filtered = all.filter(m => {
    if (activeProviderFilter !== 'all' && m.providerId !== activeProviderFilter) return false;
    if (searchKeyword) {
      const q = searchKeyword.toLowerCase();
      const matchId = (m.id || '').toLowerCase().includes(q);
      const matchName = (m.displayName || '').toLowerCase().includes(q);
      if (!matchId && !matchName) return false;
    }
    return true;
  });

  let selectedCount = 0;
  all.forEach(m => {
    if (m.selectedForInjection) selectedCount++;
  });
  modelStatsBadge.textContent = `已勾选 ${selectedCount} / ${all.length} 个模型`;

  if (filtered.length === 0) {
    modelsTbody.innerHTML = `<tr><td colspan="8" style="text-align: center; color: var(--text-muted); padding: 30px;">没有匹配的模型</td></tr>`;
    return;
  }

  filtered.forEach(m => {
    const tr = document.createElement('tr');
    
    const caps = [];
    if (m.reasoning) caps.push(`<span class="badge badge-purple">🧠 思考</span>`);
    if (m.tools) caps.push(`<span class="badge badge-blue">🛠️ 工具</span>`);
    if (m.vision) caps.push(`<span class="badge badge-green">👁️ 视觉</span>`);
    if (m.maxTokensField === 'max_completion_tokens') caps.push(`<span class="badge badge-yellow">o-series</span>`);

    tr.innerHTML = `
      <td style="text-align: center;">
        <input type="checkbox" class="model-check" data-prov="${m.providerId}" data-id="${m.id}" ${m.selectedForInjection ? 'checked' : ''}>
      </td>
      <td style="font-weight: 500;">${escapeHtml(m.displayName || m.id)}</td>
      <td class="font-mono" style="color: var(--text-secondary);">${escapeHtml(m.id)}</td>
      <td><span class="badge badge-blue">${escapeHtml(m.providerName)}</span></td>
      <td><div class="caps-badges">${caps.join('')}</div></td>
      <td class="font-mono">${Number(m.maxInputTokens || 131072).toLocaleString()}</td>
      <td class="font-mono">${Number(m.maxOutputTokens || 32768).toLocaleString()}</td>
      <td>
        <button class="btn btn-secondary btn-sm btn-edit-model" data-prov="${m.providerId}" data-id="${m.id}">✏️ 编辑</button>
      </td>
    `;
    modelsTbody.appendChild(tr);
  });

  // Attach event listeners
  document.querySelectorAll('.model-check').forEach(cb => {
    cb.onchange = () => {
      const p = workspace.providers.find(x => x.id === cb.dataset.prov);
      if (p && p.models) {
        const m = p.models.find(x => x.id === cb.dataset.id);
        if (m) {
          m.selectedForInjection = cb.checked;
          updateModelStatsOnly();
        }
      }
    };
  });

  document.querySelectorAll('.btn-edit-model').forEach(b => {
    b.onclick = () => openModelModal(b.dataset.prov, b.dataset.id);
  });
}

function updateModelStatsOnly() {
  const all = getAllModelsWithProvider();
  let selectedCount = 0;
  all.forEach(m => {
    if (m.selectedForInjection) selectedCount++;
  });
  modelStatsBadge.textContent = `已勾选 ${selectedCount} / ${all.length} 个模型`;
}

function renderDiagnostics() {
  if (!targetState) return;
  document.getElementById('diag-app-path').textContent = targetState.installDir || '-';
  document.getElementById('diag-runtime-sha').textContent = targetState.runtimeSha256 || '-';
  document.getElementById('diag-asar-sha').textContent = targetState.asarSha256 || '-';
  document.getElementById('diag-version').textContent = targetState.detectedVersion || '-';
  document.getElementById('diag-secret-dir').textContent = targetState.secretStoreDir || '-';
  document.getElementById('diag-config-path').textContent = targetState.configPath || '-';
}

// Provider Modal Operations
function openProviderModal(providerId = null) {
  currentEditingProviderId = providerId;
  const modalTitle = document.getElementById('modal-provider-title');
  const provName = document.getElementById('prov-name');
  const provEnabled = document.getElementById('prov-enabled');
  const provBaseUrl = document.getElementById('prov-base-url');
  const provUiUrl = document.getElementById('prov-ui-url');
  const provApiKey = document.getElementById('prov-api-key');
  const provReplaceKey = document.getElementById('prov-replace-key');
  const provFirstTimeout = document.getElementById('prov-first-timeout');
  const provStreamTimeout = document.getElementById('prov-stream-timeout');
  const keyHint = document.getElementById('prov-key-status-hint');
  const probeStatus = document.getElementById('probe-status-message');

  provApiKey.value = '';
  probeStatus.textContent = '';

  if (providerId) {
    modalTitle.textContent = "编辑 AI 提供方";
    const p = workspace.providers.find(x => x.id === providerId);
    if (!p) return;
    provName.value = p.name || '';
    provEnabled.value = p.enabled !== false ? 'true' : 'false';
    provBaseUrl.value = p.baseUrl || '';
    provUiUrl.value = p.uiBaseUrl || '';
    provReplaceKey.value = p.replaceProviderKey || 'bailian';
    provFirstTimeout.value = p.firstPayloadTimeoutMs || 180000;
    provStreamTimeout.value = p.streamIdleTimeoutMs || 300000;

    keyHint.textContent = p.hasApiKey 
      ? "🔐 当前已存在加密 API Key。若无需更换，可直接留空。" 
      : "🔐 密钥将使用 AES-256 加密保存于本地，不写入任何配置文件。";
  } else {
    modalTitle.textContent = "添加 AI 提供方";
    provName.value = '';
    provEnabled.value = 'true';
    provBaseUrl.value = 'http://127.0.0.1:11434/v1';
    provUiUrl.value = '';
    provReplaceKey.value = 'bailian';
    provFirstTimeout.value = 180000;
    provStreamTimeout.value = 300000;
    keyHint.textContent = "🔐 密钥将使用 AES-256 加密保存于本地，不写入任何配置文件。";
  }

  modalProvider.style.display = 'flex';
}

function closeProviderModal() {
  modalProvider.style.display = 'none';
  currentEditingProviderId = null;
}

async function saveProviderFromModal() {
  const name = document.getElementById('prov-name').value.trim();
  const baseUrl = document.getElementById('prov-base-url').value.trim();
  const uiBaseUrl = document.getElementById('prov-ui-url').value.trim();
  const enabled = document.getElementById('prov-enabled').value === 'true';
  const replaceKey = document.getElementById('prov-replace-key').value;
  const apiKey = document.getElementById('prov-api-key').value;
  const firstTimeout = parseInt(document.getElementById('prov-first-timeout').value, 10) || 180000;
  const streamTimeout = parseInt(document.getElementById('prov-stream-timeout').value, 10) || 300000;

  if (!name) {
    alert("请输入提供方名称");
    return;
  }
  if (!baseUrl) {
    alert("请输入上游 Base URL");
    return;
  }

  if (currentEditingProviderId) {
    const p = workspace.providers.find(x => x.id === currentEditingProviderId);
    if (p) {
      p.name = name;
      p.baseUrl = baseUrl;
      p.uiBaseUrl = uiBaseUrl;
      p.enabled = enabled;
      p.replaceProviderKey = replaceKey;
      p.firstPayloadTimeoutMs = firstTimeout;
      p.streamIdleTimeoutMs = streamTimeout;
      if (apiKey.trim().length > 0) {
        p.apiKey = apiKey.trim();
        p.hasApiKey = true;
      }
    }
  } else {
    const newId = `p-${Date.now().toString(36)}`;
    const newP = {
      id: newId,
      name,
      baseUrl,
      uiBaseUrl,
      enabled,
      replaceProviderKey: replaceKey,
      firstPayloadTimeoutMs: firstTimeout,
      streamIdleTimeoutMs: streamTimeout,
      models: [],
      apiKey: apiKey.trim().length > 0 ? apiKey.trim() : '',
      hasApiKey: apiKey.trim().length > 0
    };
    workspace.providers.push(newP);
  }

  closeProviderModal();
  await saveWorkspaceToServer(true);
  appendLog(`已保存提供方: ${name}`, "info");
}

async function deleteProvider(id) {
  const p = workspace.providers.find(x => x.id === id);
  if (!p) return;
  if (!confirm(`确定要删除提供方 "${p.name}" 及其包含的所有模型吗？`)) return;

  workspace.providers = workspace.providers.filter(x => x.id !== id);
  await saveWorkspaceToServer(true);
  appendLog(`已删除提供方: ${p.name}`, "warn");
}

async function probeSingleProvider(id) {
  const p = workspace.providers.find(x => x.id === id);
  if (!p) return;

  appendLog(`正在探测提供方 "${p.name}" (${p.baseUrl})...`, "info");
  try {
    const res = await api('/api/test-upstream', {
      method: 'POST',
      body: JSON.stringify({
        baseUrl: p.baseUrl,
        providerId: p.id
      })
    });

    if (res.success) {
      appendLog(res.message, "success");
      if (res.models && res.models.length > 0) {
        const existingMap = new Map((p.models || []).map(m => [m.id, m]));
        res.models.forEach(m => {
          if (!existingMap.has(m.id)) {
            existingMap.set(m.id, m);
          }
        });
        p.models = Array.from(existingMap.values());
        await saveWorkspaceToServer(false);
        appendLog(`已自动同步 ${res.models.length} 个模型到 "${p.name}" 模型池`, "info");
      }
    } else {
      appendLog(`探测失败: ${res.message}`, "error");
    }
  } catch (err) {
    appendLog(`探测请求异常: ${err.message}`, "error");
  }
}

// Model Modal Operations
function openModelModal(providerId, modelId = null) {
  currentEditingProviderId = providerId;
  currentEditingModelKey = modelId;

  const provSelect = document.getElementById('model-provider-select');
  provSelect.innerHTML = '';
  workspace.providers.forEach(p => {
    const opt = document.createElement('option');
    opt.value = p.id;
    opt.textContent = p.name;
    if (p.id === providerId) opt.selected = true;
    provSelect.appendChild(opt);
  });

  const mIdInput = document.getElementById('model-id');
  const mNameInput = document.getElementById('model-display-name');
  const mCapReasoning = document.getElementById('model-cap-reasoning');
  const mCapTools = document.getElementById('model-cap-tools');
  const mCapVision = document.getElementById('model-cap-vision');
  const mMaxInput = document.getElementById('model-max-input');
  const mMaxOutput = document.getElementById('model-max-output');
  const mTokensField = document.getElementById('model-tokens-field');

  if (modelId) {
    document.getElementById('modal-model-title').textContent = "编辑模型属性";
    const p = workspace.providers.find(x => x.id === providerId);
    const m = p && p.models ? p.models.find(x => x.id === modelId) : null;
    if (!m) return;

    mIdInput.value = m.id || '';
    mNameInput.value = m.displayName || '';
    mCapReasoning.checked = Boolean(m.reasoning);
    mCapTools.checked = m.tools !== false;
    mCapVision.checked = Boolean(m.vision);
    mMaxInput.value = m.maxInputTokens || 131072;
    mMaxOutput.value = m.maxOutputTokens || 32768;
    mTokensField.value = m.maxTokensField || 'max_tokens';
  } else {
    document.getElementById('modal-model-title').textContent = "添加自定义模型";
    mIdInput.value = '';
    mNameInput.value = '';
    mCapReasoning.checked = true;
    mCapTools.checked = true;
    mCapVision.checked = false;
    mMaxInput.value = 131072;
    mMaxOutput.value = 32768;
    mTokensField.value = 'max_tokens';
  }

  modalModel.style.display = 'flex';
}

function closeModelModal() {
  modalModel.style.display = 'none';
  currentEditingProviderId = null;
  currentEditingModelKey = null;
}

async function saveModelFromModal() {
  const provId = document.getElementById('model-provider-select').value;
  const id = document.getElementById('model-id').value.trim();
  const displayName = document.getElementById('model-display-name').value.trim();
  const reasoning = document.getElementById('model-cap-reasoning').checked;
  const tools = document.getElementById('model-cap-tools').checked;
  const vision = document.getElementById('model-cap-vision').checked;
  const maxInput = parseInt(document.getElementById('model-max-input').value, 10) || 131072;
  const maxOutput = parseInt(document.getElementById('model-max-output').value, 10) || 32768;
  const tokensField = document.getElementById('model-tokens-field').value;

  if (!id) {
    alert("请输入模型 ID");
    return;
  }

  const p = workspace.providers.find(x => x.id === provId);
  if (!p) return;
  if (!p.models) p.models = [];

  const existingModel = p.models.find(x => x.id === (currentEditingModelKey || id));
  if (existingModel) {
    existingModel.id = id;
    existingModel.displayName = displayName || id;
    existingModel.reasoning = reasoning;
    existingModel.tools = tools;
    existingModel.vision = vision;
    existingModel.maxInputTokens = maxInput;
    existingModel.maxOutputTokens = maxOutput;
    existingModel.maxTokensField = tokensField;
  } else {
    p.models.push({
      id,
      displayName: displayName || id,
      reasoning,
      tools,
      vision,
      maxInputTokens: maxInput,
      maxOutputTokens: maxOutput,
      maxTokensField: tokensField,
      selectedForInjection: true
    });
  }

  closeModelModal();
  await saveWorkspaceToServer(true);
  appendLog(`已更新模型配置: ${displayName || id}`, "info");
}

// Workspace Server Sync
async function saveWorkspaceToServer(showToast = true) {
  try {
    const res = await api('/api/workspace', {
      method: 'POST',
      body: JSON.stringify(workspace)
    });
    renderProviders();
    renderProviderFilterOptions();
    renderModels();
    if (showToast) {
      appendLog(`工作区配置已保存，包含 ${res.modelsCount} 个已注入模型`, "success");
    }
  } catch (err) {
    appendLog(`保存配置失败: ${err.message}`, "error");
  }
}

// Primary Action Dispatcher
async function handlePrimaryAction() {
  if (!targetState) return;

  if (targetState.runtimePatched) {
    if (targetState.isRunning) {
      await hotReloadConfig();
    } else {
      await launchQoder();
    }
    return;
  }

  // Need install or upgrade
  await applyPatchAction();
}

async function applyPatchAction() {
  appendLog("开始执行 Qoder CN 运行库修补...", "info");
  btnPrimaryAction.disabled = true;
  try {
    const installDir = installPathInput.value.trim();
    const res = await api('/api/patch/apply', {
      method: 'POST',
      body: JSON.stringify({ installDir })
    });
    appendLog(res.message, "success");
    await refreshAll();
  } catch (err) {
    appendLog(`修补失败: ${err.message}`, "error");
    alert(`修补失败: ${err.message}`);
  } finally {
    btnPrimaryAction.disabled = false;
  }
}

async function restorePatchAction() {
  if (!confirm("确定要将 Qoder CN 运行库恢复为官方未修改原版吗？")) return;
  appendLog("正在还原官方原版运行库...", "info");
  btnRestore.disabled = true;
  try {
    const installDir = installPathInput.value.trim();
    const res = await api('/api/patch/restore', {
      method: 'POST',
      body: JSON.stringify({ installDir })
    });
    appendLog(res.message, "success");
    await refreshAll();
  } catch (err) {
    appendLog(`恢复失败: ${err.message}`, "error");
    alert(`恢复失败: ${err.message}`);
  } finally {
    btnRestore.disabled = false;
  }
}

async function hotReloadConfig() {
  appendLog("正在热重载最新模型配置...", "info");
  try {
    await saveWorkspaceToServer(false);
    const res = await api('/api/patch/hot-reload', { method: 'POST' });
    appendLog(res.message, "success");
    alert(`✅ ${res.message}`);
  } catch (err) {
    appendLog(`热重载失败: ${err.message}`, "error");
    alert(`❌ 热重载失败: ${err.message}`);
  }
}

async function launchQoder() {
  appendLog("正在启动 Qoder CN 桌面客户端...", "info");
  try {
    const installDir = installPathInput.value.trim();
    const res = await api('/api/qoder/launch', {
      method: 'POST',
      body: JSON.stringify({ installDir })
    });
    appendLog(res.message, "success");
    setTimeout(refreshStatusOnly, 1500);
  } catch (err) {
    appendLog(`启动失败: ${err.message}`, "error");
  }
}

async function openConfigDir() {
  try {
    await api('/api/open-dir', { method: 'POST' });
    appendLog("已在 Finder 中打开配置目录", "info");
  } catch (err) {
    appendLog(`打开目录失败: ${err.message}`, "error");
  }
}

// Preset Loader
function loadPreset(presetKey) {
  presetDropdownMenu.classList.remove('show');
  
  const presets = {
    cpa: {
      name: "CPA Gateway",
      baseUrl: "http://127.0.0.1:8317/v1",
      uiBaseUrl: "https://127.0.0.1:8317/v1",
      replaceProviderKey: "bailian",
      models: [
        { id: "gpt-5.6-terra", displayName: "GPT-5.6 Terra", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "claude-opus-4-6-thinking", displayName: "Claude Opus 4.6 Thinking", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "claude-sonnet-4-6", displayName: "Claude Sonnet 4.6", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "gemini-3.7-flash-high", displayName: "Gemini 3.7 Flash High", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "gpt-5.6-sol", displayName: "GPT-5.6 Sol", reasoning: true, vision: true, tools: true, selectedForInjection: true },
        { id: "deepseek-reasoner", displayName: "DeepSeek R1", reasoning: true, vision: false, tools: true, selectedForInjection: true }
      ]
    },
    ollama: {
      name: "Ollama Local",
      baseUrl: "http://127.0.0.1:11434/v1",
      uiBaseUrl: "https://127.0.0.1:11434/v1",
      replaceProviderKey: "bailian",
      models: [
        { id: "deepseek-r1:7b", displayName: "DeepSeek R1 7B", reasoning: true, vision: false, tools: true, selectedForInjection: true },
        { id: "qwen2.5-coder:7b", displayName: "Qwen 2.5 Coder 7B", reasoning: false, vision: false, tools: true, selectedForInjection: true },
        { id: "llama3.3:70b", displayName: "Llama 3.3 70B", reasoning: false, vision: false, tools: true, selectedForInjection: true }
      ]
    },
    siliconflow: {
      name: "SiliconFlow (硅基流动)",
      baseUrl: "https://api.siliconflow.cn/v1",
      uiBaseUrl: "https://api.siliconflow.cn/v1",
      replaceProviderKey: "deepseek",
      models: [
        { id: "deepseek-ai/DeepSeek-R1", displayName: "DeepSeek R1", reasoning: true, vision: false, tools: true, selectedForInjection: true },
        { id: "deepseek-ai/DeepSeek-V3", displayName: "DeepSeek V3", reasoning: false, vision: false, tools: true, selectedForInjection: true },
        { id: "Qwen/Qwen2.5-Coder-32B-Instruct", displayName: "Qwen 2.5 Coder 32B", reasoning: false, vision: false, tools: true, selectedForInjection: true }
      ]
    },
    deepseek: {
      name: "DeepSeek Official",
      baseUrl: "https://api.deepseek.com/v1",
      uiBaseUrl: "https://api.deepseek.com/v1",
      replaceProviderKey: "deepseek",
      models: [
        { id: "deepseek-reasoner", displayName: "DeepSeek R1", reasoning: true, vision: false, tools: true, selectedForInjection: true },
        { id: "deepseek-chat", displayName: "DeepSeek V3", reasoning: false, vision: false, tools: true, selectedForInjection: true }
      ]
    },
    openai: {
      name: "OpenAI Official",
      baseUrl: "https://api.openai.com/v1",
      uiBaseUrl: "https://api.openai.com/v1",
      replaceProviderKey: "openai",
      models: [
        { id: "gpt-4o", displayName: "GPT-4o", reasoning: false, vision: true, tools: true, selectedForInjection: true },
        { id: "o1", displayName: "o1", reasoning: true, vision: true, tools: true, maxTokensField: "max_completion_tokens", selectedForInjection: true },
        { id: "o3-mini", displayName: "o3-mini", reasoning: true, vision: false, tools: true, maxTokensField: "max_completion_tokens", selectedForInjection: true }
      ]
    }
  };

  const pData = presets[presetKey];
  if (!pData) return;

  const existing = workspace.providers.find(x => x.name === pData.name || x.baseUrl === pData.baseUrl);
  if (existing) {
    alert(`提供方 "${pData.name}" 已存在。`);
    return;
  }

  const newP = {
    id: `p-${presetKey}-${Date.now().toString(36)}`,
    ...pData,
    enabled: true,
    firstPayloadTimeoutMs: 180000,
    streamIdleTimeoutMs: 300000,
    apiKey: '',
    hasApiKey: false
  };

  workspace.providers.push(newP);
  saveWorkspaceToServer(true);
  appendLog(`已载入预设提供方: ${pData.name}`, "info");
}

// Event Listeners setup
function setupEventListeners() {
  // Tabs
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.onclick = () => {
      document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
      btn.classList.add('active');
      document.getElementById(btn.dataset.tab).classList.add('active');
    };
  });

  // Main Actions
  btnPrimaryAction.onclick = handlePrimaryAction;
  btnLaunchQoder.onclick = launchQoder;
  btnRestore.onclick = restorePatchAction;
  btnOpenDir.onclick = openConfigDir;
  btnBrowsePath.onclick = refreshAll;

  // Save HotReload
  document.getElementById('btn-save-hotreload').onclick = hotReloadConfig;
  document.getElementById('btn-save-models-hotreload').onclick = hotReloadConfig;

  // Add Provider
  document.getElementById('btn-add-provider').onclick = () => openProviderModal(null);
  document.getElementById('btn-close-provider-modal').onclick = closeProviderModal;
  document.getElementById('btn-cancel-provider').onclick = closeProviderModal;
  document.getElementById('btn-save-provider').onclick = saveProviderFromModal;

  // Modal Probe button
  document.getElementById('btn-modal-probe').onclick = async () => {
    const baseUrl = document.getElementById('prov-base-url').value.trim();
    const apiKey = document.getElementById('prov-api-key').value;
    const msgEl = document.getElementById('probe-status-message');
    msgEl.textContent = "正在探测 /models 接口...";

    try {
      const res = await api('/api/test-upstream', {
        method: 'POST',
        body: JSON.stringify({
          baseUrl,
          apiKey: apiKey.trim(),
          providerId: currentEditingProviderId
        })
      });

      if (res.success) {
        msgEl.textContent = `✅ ${res.message}`;
      } else {
        msgEl.textContent = `❌ ${res.message}`;
      }
    } catch (err) {
      msgEl.textContent = `❌ 请求错误: ${err.message}`;
    }
  };

  // Add Custom Model
  document.getElementById('btn-add-custom-model').onclick = () => {
    if (workspace.providers.length === 0) {
      alert("请先添加至少一个提供方！");
      return;
    }
    openModelModal(workspace.providers[0].id, null);
  };
  document.getElementById('btn-close-model-modal').onclick = closeModelModal;
  document.getElementById('btn-cancel-model').onclick = closeModelModal;
  document.getElementById('btn-save-model').onclick = saveModelFromModal;

  // Preset dropdown toggle
  btnPresetMenu.onclick = (e) => {
    e.stopPropagation();
    presetDropdownMenu.classList.toggle('show');
  };
  document.addEventListener('click', () => {
    presetDropdownMenu.classList.remove('show');
  });
  document.querySelectorAll('#preset-dropdown-menu a').forEach(a => {
    a.onclick = (e) => {
      e.preventDefault();
      loadPreset(a.dataset.preset);
    };
  });

  // Filter & Search
  filterProvider.onchange = () => {
    activeProviderFilter = filterProvider.value;
    renderModels();
  };
  searchModelInput.oninput = () => {
    searchKeyword = searchModelInput.value.trim();
    renderModels();
  };

  // Select all / Deselect all
  document.getElementById('btn-select-all').onclick = () => {
    const all = getAllModelsWithProvider();
    all.forEach(m => {
      if (activeProviderFilter === 'all' || m.providerId === activeProviderFilter) {
        const p = workspace.providers.find(x => x.id === m.providerId);
        const item = p && p.models ? p.models.find(x => x.id === m.id) : null;
        if (item) item.selectedForInjection = true;
      }
    });
    renderModels();
  };
  document.getElementById('btn-deselect-all').onclick = () => {
    const all = getAllModelsWithProvider();
    all.forEach(m => {
      if (activeProviderFilter === 'all' || m.providerId === activeProviderFilter) {
        const p = workspace.providers.find(x => x.id === m.providerId);
        const item = p && p.models ? p.models.find(x => x.id === m.id) : null;
        if (item) item.selectedForInjection = false;
      }
    });
    renderModels();
  };

  // Logs
  document.getElementById('btn-clear-logs').onclick = () => {
    logsConsole.innerHTML = '';
  };
  document.getElementById('btn-copy-logs').onclick = () => {
    navigator.clipboard.writeText(logsConsole.innerText).then(() => {
      alert("日志已复制到剪贴板！");
    });
  };

  // Toggle password visibility
  document.getElementById('btn-toggle-pwd').onclick = () => {
    const inp = document.getElementById('prov-api-key');
    inp.type = inp.type === 'password' ? 'text' : 'password';
  };
}

// Start app
window.addEventListener('DOMContentLoaded', init);
