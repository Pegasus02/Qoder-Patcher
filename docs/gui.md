# GUI 指南 (Native EXE & PowerShell)

## 推荐方式：原生桌面程序 (`QoderCN-Patcher.exe` 或 `bin\QoderCN-Patcher.exe`)

本项目提供**单文件免安装原生桌面 EXE 应用程序**。应用程序清单已集成管理员提权（UAC Shield），双击即可直接以管理员权限运行：

```text
QoderCN-Patcher.exe
或
bin\QoderCN-Patcher.exe
```

### 构建与编译 (零依赖一键生成)
如果需要重新构建或修改源码后编译，只需运行：
- 双击根目录下的 `build.cmd`
- 或在 PowerShell 中执行 `.\build.ps1`

构建脚本自动调用 Windows 内置的 .NET Framework C# 编译器（`csc.exe`），无需安装 Visual Studio 或 .NET SDK。

### 界面功能介绍

1. **安装与运行状态卡片**
   - 自动探测 Qoder CN 默认安装目录 `C:\Program Files\Qoder\Qoder CN`（支持自定义浏览）。
   - 状态指示灯：
     - 🟢 **已修补 (v2.1 原生直连 Direct Custom Routing)**
     - 🟡 **官方原版 (已就绪，可随时修补)**
     - 🔴 **未找到运行库文件**
   - 实时检测 Qoder 运行进程，防止运行中修补造成冲突。

2. **上游渠道与可视化模型管理**
   - 预设配置下拉框：自动加载 `configs/*.json`，并支持【导入 JSON...】、【新建配置】、【打开目录】。
   - 渠道参数编辑：直观修改渠道显示名称与上游 Base URL，并提供【测试连接】按钮验证上游服务是否可达。
   - 模型列表表格：直观管理模型 ID、显示名称、思考/推理 (Reasoning)、视觉 (Vision)、工具调用 (Tools)，支持【+ 添加模型】与【- 删除所选】，点击【💾 保存配置】即可保存。

3. **核心操作栏**
   - 【🚀 一键安装 / 更新修补】：写入运行时配置并执行补丁，如果当前不是管理员权限且遇到 Program Files 写保护，将自动弹出 UAC 提权引导。
   - 【🔄 恢复官方原版】：支持从历史备份或确定性逆向还原（Unpatch）安全还原官方原版。
   - 【🔍 预演检测 (Dry Run)】：模拟检测修补锚点与 JSON 配置，不修改系统文件。
   - 【⚡ 启动 Qoder CN】：一键拉起 Qoder CN 客户端。

4. **实时彩色诊断日志**
   - 底部黑色控制台卡片，按时间戳分色显示 Info (蓝)、OK (绿)、Warn (橙)、Error (红) 详细反馈。

---

## 备用方式：PowerShell WinForms 脚本

如需在轻量命令行环境下运行或排查问题，可双击根目录下的 `Launch-QoderCN-Patcher-GUI.cmd` 启动备用的 PowerShell GUI。
