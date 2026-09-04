#!/usr/bin/env node

import path from 'node:path';
import fs from 'node:fs';
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
} from './engine/patcher-engine.mjs';
import { 
  GatewayWorkspace, 
  getDefaultWorkspacePath, 
  getDefaultRuntimeConfigPath 
} from './engine/model-config.mjs';
import { SecretStore } from './engine/secret-store.mjs';
import { fetchModels } from './engine/upstream-tester.mjs';
import { startServer } from './gui/server.mjs';
import { exec } from 'node:child_process';

const args = process.argv.slice(2);
const command = args[0] ? args[0].toLowerCase() : 'help';

function getArgVal(flag, def = null) {
  const idx = args.indexOf(flag);
  if (idx !== -1 && idx + 1 < args.length) {
    return args[idx + 1];
  }
  return def;
}

const installDir = getArgVal('--install-dir', getDefaultInstallDir());

async function run() {
  switch (command) {
    case 'inspect':
    case 'status': {
      console.log("\n=== Qoder CN macOS 状态检测 ===");
      const state = getTargetState(installDir);
      console.log(`安装路径: ${state.installDir}`);
      console.log(`应用存在: ${state.appExists ? '✅ 是' : '❌ 否'}`);
      console.log(`运行库文件: ${state.runtimePath}`);
      console.log(`运行库存在: ${state.runtimeExists ? '✅ 是' : '❌ 否'}`);
      console.log(`运行库 SHA256: ${state.runtimeSha256 || 'N/A'}`);
      console.log(`ASAR SHA256: ${state.asarSha256 || 'N/A'}`);
      console.log(`运行时版本: ${state.runtimeVersion || 'N/A'}`);
      console.log(`补丁状态: ${state.statusText}`);
      console.log(`识别版本: ${state.detectedVersion}`);
      console.log(`Qoder 运行中: ${state.isRunning ? '⚡ 是' : '⚪ 否'}`);
      console.log(`配置文件路径: ${getDefaultConfigPath()}`);
      console.log(`密钥存储目录: ${SecretStore.getStoreDirectory()}`);
      break;
    }

    case 'apply':
    case 'patch': {
      console.log("\n=== 执行 Qoder CN 运行库修补 ===");
      const ws = GatewayWorkspace.loadFromFile();
      ws.saveRuntimeConfigFile();
      console.log(`[1/2] 运行时配置已写入: ${getDefaultConfigPath()}`);

      console.log(`[2/2] 正在修补运行库并创建备份...`);
      const res = applyPatch(installDir, getDefaultBackupRoot(), args.includes('--force'));
      if (res.upgraded) {
        console.log(`[SUCCESS] 升级成功！已基于历史备份 (${res.backupId}) 升级至 v3.2.0。`);
      } else {
        console.log(`[SUCCESS] 修补成功！已创建备份 (${res.backupId}) 并应用 v3.2.0。`);
      }
      break;
    }

    case 'restore': {
      const backupId = getArgVal('--backup-id', null);
      console.log(`\n=== 恢复 Qoder CN 官方原版运行库 ===`);
      const manifest = restorePatch(installDir, getDefaultBackupRoot(), backupId, args.includes('--force'));
      console.log(`[SUCCESS] 还原成功！已恢复官方未修改版本 (来自备份 ${manifest.backupId})。`);
      break;
    }

    case 'hot-reload':
    case 'reload': {
      console.log("\n=== 热重载 Qoder CN 模型配置 ===");
      const ws = GatewayWorkspace.loadFromFile();
      const cfg = ws.saveRuntimeConfigFile();
      console.log(`[SUCCESS] 配置已热重载！已更新 ${cfg.models.length} 个注入模型至:`);
      console.log(`  ${getDefaultConfigPath()}`);
      break;
    }

    case 'launch': {
      console.log("\n=== 启动 Qoder CN 桌面版 ===");
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
      console.log("[SUCCESS] Qoder CN 已拉起！");
      break;
    }

    case 'close': {
      const force = args.includes('--force');
      const closed = closeQoder(force);
      console.log(closed ? "[SUCCESS] Qoder CN 进程已关闭。" : "[INFO] 未发现运行中的 Qoder CN 进程。");
      break;
    }

    case 'gui': {
      const port = parseInt(getArgVal('--port', '8399'), 10);
      const { url } = await startServer(port);
      console.log(`[OK] Qoder CN Patcher GUI 服务已启动: ${url}`);
      exec(`open "${url}"`);
      break;
    }

    case 'probe': {
      const url = getArgVal('--url', 'http://127.0.0.1:8317/v1');
      const key = getArgVal('--key', '');
      console.log(`\n=== 正在探测上游模型接口 (${url}) ===`);
      const res = await fetchModels(url, key);
      console.log(`结果: ${res.message}`);
      if (res.models && res.models.length > 0) {
        console.log(`模型列表 (${res.models.length} 个):`);
        res.models.forEach(m => {
          const tags = [];
          if (m.reasoning) tags.push('Thinking');
          if (m.tools) tags.push('Tools');
          if (m.vision) tags.push('Vision');
          console.log(`  - ${m.id} (${m.displayName}) [${tags.join(', ')}]`);
        });
      }
      break;
    }

    default: {
      console.log(`
Qoder CN OpenAI-Compatible Gateway Manager (macOS v3.2.0)

用法:
  node mac/cli.mjs <command> [options]
  ./qoder-patcher-mac.sh <command> [options]

常用命令:
  inspect                     检测 Qoder CN 安装状态与运行库哈希
  apply                       全自动备份并应用 v3.2.0 补丁
  restore [--backup-id <id>]  一键恢复至官方原版
  hot-reload                  热重载已勾选的模型配置 (无需重启 Qoder)
  launch                      启动 Qoder CN 并注入环境变量
  close [--force]             关闭 Qoder CN 进程
  gui [--port <port>]         启动本地图形化 Web 控制台并打开浏览器
  probe --url <url>           测试上游 /models 接口并列出可用模型

选项:
  --install-dir <path>        指定 Qoder CN.app 路径 (默认: /Applications/Qoder CN.app)
  --backup-id <id>            指定恢复特定备份编号
      `);
      break;
    }
  }
}

run().catch(err => {
  console.error("\n[ERROR]", err.message);
  process.exit(1);
});
