import { startServer } from './gui/server.mjs';
import { exec } from 'node:child_process';

async function main() {
  console.log("==================================================");
  console.log("  Qoder CN Gateway Manager (macOS v3.2.0)");
  console.log("==================================================");

  try {
    const { url, port } = await startServer(8399);
    console.log(`[OK] 本地控制台服务已启动: ${url}`);
    console.log(`[OK] 正在使用默认浏览器打开管理控制台...`);
    
    // Automatically open in default browser
    exec(`open "${url}"`);

    console.log("\n提示: 按 Ctrl+C 可停止本地管理服务。\n");
  } catch (err) {
    console.error("[ERROR] 启动管理服务失败:", err.message);
    process.exit(1);
  }
}

main();
