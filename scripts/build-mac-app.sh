#!/bin/bash
set -e

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_NAME="QoderCN-Patcher"
APP_BUNDLE="$PROJECT_ROOT/bin/$APP_NAME.app"
DMG_PATH="$PROJECT_ROOT/bin/$APP_NAME-macOS-v3.2.0.dmg"

echo "=== 正在构建 macOS 原生应用: $APP_NAME.app ==="

# 1. Prepare bundle directories
rm -rf "$APP_BUNDLE" "$DMG_PATH"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"
mkdir -p "$PROJECT_ROOT/bin"

# 2. Copy application assets into Resources
cp -R "$PROJECT_ROOT/mac" "$APP_BUNDLE/Contents/Resources/mac"
mkdir -p "$APP_BUNDLE/Contents/Resources/configs"
cp -R "$PROJECT_ROOT/configs/"* "$APP_BUNDLE/Contents/Resources/configs/" 2>/dev/null || true

# 3. Create App Icon (.icns) from app.ico
if command -v sips >/dev/null 2>&1 && [ -f "$PROJECT_ROOT/src-native/app.ico" ]; then
  mkdir -p "$PROJECT_ROOT/bin/icon.iconset"
  sips -s format png "$PROJECT_ROOT/src-native/app.ico" --out "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" >/dev/null 2>&1 || true
  if [ -f "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" ]; then
    sips -z 16 16     "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_16x16.png" >/dev/null 2>&1
    sips -z 32 32     "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_16x16@2x.png" >/dev/null 2>&1
    sips -z 32 32     "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_32x32.png" >/dev/null 2>&1
    sips -z 64 64     "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_32x32@2x.png" >/dev/null 2>&1
    sips -z 128 128   "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_128x128.png" >/dev/null 2>&1
    sips -z 256 256   "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_128x128@2x.png" >/dev/null 2>&1
    sips -z 256 256   "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_256x256.png" >/dev/null 2>&1
    sips -z 512 512   "$PROJECT_ROOT/bin/icon.iconset/icon_512x512.png" --out "$PROJECT_ROOT/bin/icon.iconset/icon_256x256@2x.png" >/dev/null 2>&1
    iconutil -c icns "$PROJECT_ROOT/bin/icon.iconset" -o "$APP_BUNDLE/Contents/Resources/AppIcon.icns" >/dev/null 2>&1 || true
    rm -rf "$PROJECT_ROOT/bin/icon.iconset"
  fi
fi

# 4. Create Info.plist
cat << 'PLIST' > "$APP_BUNDLE/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>QoderCN-Patcher</string>
    <key>CFBundleDisplayName</key>
    <string>Qoder CN Patcher</string>
    <key>CFBundleIdentifier</key>
    <string>ai.qoder.patcher.mac</string>
    <key>CFBundleVersion</key>
    <string>3.2.0</string>
    <key>CFBundleShortVersionString</key>
    <string>3.2.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>QoderCN-Patcher</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSAppTransportSecurity</key>
    <dict>
        <key>NSAllowsArbitraryLoads</key>
        <true/>
        <key>NSAllowsLocalNetworking</key>
        <true/>
    </dict>
</dict>
</plist>
PLIST

# 5. Write Swift native window wrapper
cat << 'SWIFT' > "$PROJECT_ROOT/bin/main.swift"
import Cocoa
import WebKit

class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    var window: NSWindow!
    var webView: WKWebView!
    var serverProcess: Process?
    var serverPort: Int = 8399

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Find Node executable
        let nodePath = findNodeExecutable()
        guard let node = nodePath else {
            let alert = NSAlert()
            alert.messageText = "未检测到 Node.js 运行环境"
            alert.informativeText = "Qoder CN Patcher 需要系统安装 Node.js (推荐 v18+)。请先安装 Node.js 后重试。"
            alert.alertStyle = .critical
            alert.runModal()
            NSApp.terminate(nil)
            return
        }

        // Locate server.mjs inside app bundle or current dir
        let bundlePath = Bundle.main.bundlePath
        let resourcesPath = (bundlePath as NSString).appendingPathComponent("Contents/Resources/mac/gui/server.mjs")
        var serverScript = resourcesPath
        if !FileManager.default.fileExists(atPath: serverScript) {
            // Fallback for dev mode
            serverScript = URL(fileURLWithPath: #file).deletingLastPathComponent().deletingLastPathComponent().appendingPathComponent("mac/gui/server.mjs").path
        }

        // Start server process
        serverProcess = Process()
        serverProcess?.executableURL = URL(fileURLWithPath: node)
        serverProcess?.arguments = [serverScript]
        
        var env = ProcessInfo.processInfo.environment
        env["PATH"] = (env["PATH"] ?? "") + ":/usr/local/bin:/opt/homebrew/bin:~/.nvm/versions/node/$(ls ~/.nvm/versions/node 2>/dev/null | tail -n 1)/bin"
        serverProcess?.environment = env

        do {
            try serverProcess?.run()
        } catch {
            print("Failed to start server: \(error)")
        }

        // Wait brief moment for server to bind
        Thread.sleep(forTimeInterval: 0.4)

        // Setup Main Window
        let rect = NSRect(x: 0, y: 0, width: 1100, height: 740)
        window = NSWindow(
            contentRect: rect,
            styleMask: [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        window.center()
        window.title = "Qoder CN OpenAI-Compatible Gateway Manager (v3.2.0)"
        window.titlebarAppearsTransparent = true
        window.titleVisibility = .visible
        window.delegate = self
        window.minSize = NSSize(width: 900, height: 600)

        // Setup WebKit
        let config = WKWebViewConfiguration()
        config.preferences.setValue(true, forKey: "developerExtrasEnabled")
        webView = WKWebView(frame: window.contentView!.bounds, configuration: config)
        webView.autoresizingMask = [.width, .height]
        window.contentView?.addSubview(webView)

        if let url = URL(string: "http://127.0.0.1:8399") {
            webView.load(URLRequest(url: url))
        }

        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    func windowShouldClose(_ sender: NSWindow) -> Bool {
        NSApp.terminate(nil)
        return true
    }

    func applicationWillTerminate(_ notification: Notification) {
        if let proc = serverProcess, proc.isRunning {
            proc.terminate()
        }
    }

    private func findNodeExecutable() -> String? {
        let candidates = [
            "/usr/local/bin/node",
            "/opt/homebrew/bin/node",
            "/usr/bin/node",
            NSString(string: "~/.nvm/current/bin/node").expandingTildeInPath
        ]
        for path in candidates {
            if FileManager.default.isExecutableFile(atPath: path) {
                return path
            }
        }
        
        // Try `which node`
        let p = Process()
        p.executableURL = URL(fileURLWithPath: "/bin/zsh")
        p.arguments = ["-lc", "which node"]
        let pipe = Pipe()
        p.standardOutput = pipe
        try? p.run()
        p.waitUntilExit()
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        if let str = String(data: data, encoding: .utf8)?.trimmingCharacters(in: .whitespacesAndNewlines), !str.isEmpty, FileManager.default.isExecutableFile(atPath: str) {
            return str
        }
        return nil
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
SWIFT

# 6. Compile with swiftc
echo "编译 Swift 原生窗口外壳..."
swiftc "$PROJECT_ROOT/bin/main.swift" \
  -o "$APP_BUNDLE/Contents/MacOS/QoderCN-Patcher" \
  -framework Cocoa \
  -framework WebKit \
  -O

rm -f "$PROJECT_ROOT/bin/main.swift"
chmod +x "$APP_BUNDLE/Contents/MacOS/QoderCN-Patcher"
echo "✅ 已生成原生应用: $APP_BUNDLE"

# 7. Create DMG installer
echo "制作 DMG 安装镜像..."
DMG_TMP="$PROJECT_ROOT/bin/dmg_temp"
rm -rf "$DMG_TMP"
mkdir -p "$DMG_TMP"
cp -R "$APP_BUNDLE" "$DMG_TMP/"
ln -s /Applications "$DMG_TMP/Applications"

hdiutil create -volname "Qoder CN Patcher" \
  -srcfolder "$DMG_TMP" \
  -ov -format UDZO \
  "$DMG_PATH" >/dev/null 2>&1

rm -rf "$DMG_TMP"
echo "✅ 已生成 DMG 镜像: $DMG_PATH"
