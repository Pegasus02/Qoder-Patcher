import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { exec } from 'node:child_process';
import { 
  getTargetState, 
  applyPatch, 
  restorePatch, 
  isQoderRunning, 
  closeQoder, 
  launchQoder,
  getDefaultInstallDir,
  getDefaultBackupRoot,
  getDefaultConfigPath
} from '../engine/patcher-engine.mjs';
import { 
  GatewayWorkspace, 
  createProviderItem, 
  createModelItem,
  getDefaultWorkspacePath,
  getDefaultRuntimeConfigPath
} from '../engine/model-config.mjs';
import { SecretStore } from '../engine/secret-store.mjs';
import { fetchModels } from '../engine/upstream-tester.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const PUBLIC_DIR = path.join(__dirname, 'public');

function sendJson(res, statusCode, data) {
  res.writeHead(statusCode, {
    'Content-Type': 'application/json; charset=utf-8',
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type'
  });
  res.end(JSON.stringify(data));
}

function sendFile(res, filePath, contentType) {
  if (!fs.existsSync(filePath)) {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('404 Not Found');
    return;
  }
  res.writeHead(200, { 'Content-Type': contentType });
  fs.createReadStream(filePath).pipe(res);
}

function parseJsonBody(req) {
  return new Promise((resolve, reject) => {
    let body = '';
    req.on('data', chunk => {
      body += chunk;
      if (body.length > 5 * 1024 * 1024) { // 5MB limit
        req.destroy();
        reject(new Error('Payload too large'));
      }
    });
    req.on('end', () => {
      if (!body.trim()) return resolve({});
      try {
        resolve(JSON.parse(body));
      } catch (err) {
        reject(new Error('Invalid JSON payload'));
      }
    });
    req.on('error', reject);
  });
}

export function createServer() {
  return http.createServer(async (req, res) => {
    // Handle CORS preflight
    if (req.method === 'OPTIONS') {
      res.writeHead(204, {
        'Access-Control-Allow-Origin': '*',
        'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
        'Access-Control-Allow-Headers': 'Content-Type'
      });
      res.end();
      return;
    }

    const url = new URL(req.url, `http://${req.headers.host}`);
    const pathname = url.pathname;

    try {
      // API Routes
      if (pathname === '/api/status' && req.method === 'GET') {
        const installDir = url.searchParams.get('installDir') || getDefaultInstallDir();
        const state = getTargetState(installDir);
        const configPath = getDefaultConfigPath();
        const configExists = fs.existsSync(configPath);
        return sendJson(res, 200, {
          ...state,
          configPath,
          configExists,
          secretStoreDir: SecretStore.getStoreDirectory()
        });
      }

      if (pathname === '/api/workspace' && req.method === 'GET') {
        const ws = GatewayWorkspace.loadFromFile();
        const providersWithSecretInfo = ws.providers.map(p => ({
          ...p,
          hasApiKey: SecretStore.hasProviderKey(p.id)
        }));
        return sendJson(res, 200, {
          providers: providersWithSecretInfo,
          selectedModelKeys: ws.selectedModelKeys
        });
      }

      if (pathname === '/api/workspace' && req.method === 'POST') {
        const body = await parseJsonBody(req);
        const ws = new GatewayWorkspace();
        if (Array.isArray(body.providers)) {
          ws.providers = body.providers.map(p => {
            // Save API key if provided
            if (p.apiKey !== undefined && p.apiKey !== null) {
              if (p.apiKey.trim().length > 0) {
                SecretStore.saveProviderKey(p.id, p.apiKey.trim());
              } else if (p.clearApiKey) {
                SecretStore.deleteProviderKey(p.id);
              }
            }
            const cleanP = createProviderItem(p);
            return cleanP;
          });
        }
        if (Array.isArray(body.selectedModelKeys)) {
          ws.selectedModelKeys = body.selectedModelKeys;
        }

        ws.saveToFile();
        ws.saveRuntimeConfigFile();

        return sendJson(res, 200, {
          success: true,
          message: "Workspace and runtime config saved successfully."
        });
      }

      if (pathname === '/api/test-upstream' && req.method === 'POST') {
        const body = await parseJsonBody(req);
        let apiKey = body.apiKey || '';
        if (!apiKey && body.providerId) {
          apiKey = SecretStore.loadProviderKey(body.providerId);
        }
        const result = await fetchModels(body.baseUrl, apiKey, body.fetchFullList !== false);
        return sendJson(res, 200, result);
      }

      if (pathname === '/api/patch/apply' && req.method === 'POST') {
        const body = await parseJsonBody(req);
        const installDir = body.installDir || getDefaultInstallDir();
        
        // Auto compile and save runtime config before patch
        const ws = GatewayWorkspace.loadFromFile();
        ws.saveRuntimeConfigFile();

        const result = applyPatch(installDir);
        return sendJson(res, 200, {
          success: true,
          upgraded: result.upgraded,
          backupId: result.backupId,
          message: result.upgraded 
            ? `升级成功！已基于历史备份 (${result.backupId}) 成功升级补丁 v3.2.0。`
            : `修补成功！已创建备份 (${result.backupId}) 并应用 v3.2.0 补丁。`
        });
      }

      if (pathname === '/api/patch/restore' && req.method === 'POST') {
        const body = await parseJsonBody(req);
        const installDir = body.installDir || getDefaultInstallDir();
        const specificBackupId = body.backupId || null;

        const manifest = restorePatch(installDir, getDefaultBackupRoot(), specificBackupId);
        return sendJson(res, 200, {
          success: true,
          backupId: manifest.backupId,
          message: `恢复成功！已还原至官方原版 (备份编号: ${manifest.backupId})。`
        });
      }

      if (pathname === '/api/patch/hot-reload' && req.method === 'POST') {
        const ws = GatewayWorkspace.loadFromFile();
        const cfg = ws.saveRuntimeConfigFile();
        return sendJson(res, 200, {
          success: true,
          modelsCount: cfg.models.length,
          message: `配置已热重载！已更新 ${cfg.models.length} 个注入模型到 ${getDefaultConfigPath()}`
        });
      }

      if (pathname === '/api/qoder/launch' && req.method === 'POST') {
        const body = await parseJsonBody(req);
        const installDir = body.installDir || getDefaultInstallDir();
        
        // Prepare environment variables for providers
        const ws = GatewayWorkspace.loadFromFile();
        const extraEnv = {
          QODER_CN_CUSTOM_PROVIDER_CONFIG: getDefaultConfigPath()
        };

        for (const p of ws.providers) {
          const key = SecretStore.loadProviderKey(p.id);
          if (key) {
            extraEnv[`QODER_CN_KEY_${p.id}`] = key;
            if (p.enabled && !extraEnv.QODER_CN_CUSTOM_PROVIDER_API_KEY) {
              extraEnv.QODER_CN_CUSTOM_PROVIDER_API_KEY = key;
            }
          }
        }

        launchQoder(installDir, extraEnv);
        return sendJson(res, 200, {
          success: true,
          message: "Qoder CN 已启动！"
        });
      }

      if (pathname === '/api/qoder/close' && req.method === 'POST') {
        const body = await parseJsonBody(req);
        const success = closeQoder(Boolean(body.force));
        return sendJson(res, 200, {
          success,
          message: success ? "Qoder CN 进程已关闭。" : "未发现正在运行的 Qoder CN 进程。"
        });
      }

      if (pathname === '/api/backups' && req.method === 'GET') {
        const backupRoot = getDefaultBackupRoot();
        const list = [];
        if (fs.existsSync(backupRoot)) {
          const entries = fs.readdirSync(backupRoot, { withFileTypes: true });
          for (const ent of entries) {
            if (ent.isDirectory()) {
              const mf = path.join(backupRoot, ent.name, "manifest.json");
              if (fs.existsSync(mf)) {
                try {
                  list.push(JSON.parse(fs.readFileSync(mf, 'utf8')));
                } catch {}
              }
            }
          }
        }
        list.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
        return sendJson(res, 200, { backups: list });
      }

      if (pathname === '/api/open-dir' && req.method === 'POST') {
        const dir = path.dirname(getDefaultConfigPath());
        if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
        exec(`open "${dir}"`);
        return sendJson(res, 200, { success: true, path: dir });
      }

      if (pathname === '/api/presets' && req.method === 'GET') {
        const def = GatewayWorkspace.createDefault();
        return sendJson(res, 200, {
          presets: def.providers
        });
      }

      // Static file serving
      let reqPath = pathname === '/' ? '/index.html' : pathname;
      const safePath = path.normalize(reqPath).replace(/^(\.\.[\/\\])+/, '');
      const filePath = path.join(PUBLIC_DIR, safePath);

      const ext = path.extname(filePath).toLowerCase();
      const mimeTypes = {
        '.html': 'text/html; charset=utf-8',
        '.js': 'application/javascript; charset=utf-8',
        '.css': 'text/css; charset=utf-8',
        '.json': 'application/json; charset=utf-8',
        '.png': 'image/png',
        '.jpg': 'image/jpeg',
        '.svg': 'image/svg+xml',
        '.ico': 'image/x-icon'
      };

      const contentType = mimeTypes[ext] || 'application/octet-stream';
      return sendFile(res, filePath, contentType);

    } catch (err) {
      console.error("API Error:", err);
      return sendJson(res, 500, {
        error: err.message || "Internal Server Error"
      });
    }
  });
}

export function startServer(port = 8399) {
  const server = createServer();
  return new Promise((resolve, reject) => {
    server.on('error', (err) => {
      if (err.code === 'EADDRINUSE') {
        // Retry on next port
        server.listen(port + 1, '127.0.0.1');
      } else {
        reject(err);
      }
    });

    server.listen(port, '127.0.0.1', () => {
      const address = server.address();
      const actualPort = typeof address === 'object' ? address.port : port;
      resolve({ server, port: actualPort, url: `http://127.0.0.1:${actualPort}` });
    });
  });
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  startServer().then(({ url }) => {
    console.log(`Qoder CN Patcher macOS Server running at: ${url}`);
  }).catch(console.error);
}
