using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace QoderCN.Patcher
{
    static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        static int Main(string[] args)
        {
            // 命令行支持模式（供测试与自动化运维调用）
            if (args != null && args.Length > 0)
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                return RunCommandLine(args);
            }

            // GUI 模式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show("发生未捕获异常: " + e.Exception.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
            return 0;
        }

        private static int RunCommandLine(string[] args)
        {
            try
            {
                string cmd = args[0].ToLowerInvariant();
                string installDir = PatcherCore.GetDefaultInstallDir();
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDir, @"configs\cpa-192.168.50.241.json");
                if (!File.Exists(configPath))
                {
                    configPath = Path.Combine(baseDir, @"..\configs\cpa-192.168.50.241.json");
                }
                configPath = Path.GetFullPath(configPath);

                if (args.Length > 1 && !string.IsNullOrEmpty(args[1]))
                {
                    installDir = args[1];
                }
                if (args.Length > 2 && !string.IsNullOrEmpty(args[2]))
                {
                    configPath = args[2];
                }

                if (cmd == "--self-test" || cmd == "/selftest" || cmd == "-selftest")
                {
                    Console.WriteLine("[INFO] 执行 PatcherCore 纯 C# 原生自检...");
                    string sampleSource = PatcherCore.GetOriginalSource(installDir);
                    string transformed = PatcherCore.PerformPatchTransform(sampleSource);
                    if (!transformed.Contains(PatcherCore.PatchMarker))
                    {
                        Console.Error.WriteLine("[ERROR] 自检失败：转换结果中未发现 PatchMarker。");
                        return 1;
                    }
                    Console.WriteLine(string.Format("[OK] 自检通过！转换输出长度: {0} 字符", transformed.Length));
                    return 0;
                }
                else if (cmd == "--inspect" || cmd == "/inspect")
                {
                    var res = PatcherCore.Inspect(installDir);
                    Console.WriteLine(string.Format("State: {0}", res.State));
                    Console.WriteLine(string.Format("Message: {0}", res.Message));
                    Console.WriteLine(string.Format("RuntimeSha256: {0}", res.RuntimeSha256));
                    return 0;
                }
                else if (cmd == "--dry-run" || cmd == "/dryrun")
                {
                    string output = PatcherCore.DryRun(installDir, configPath);
                    Console.WriteLine(output);
                    return 0;
                }
                else
                {
                    Console.WriteLine("用法: QoderCN-Patcher.exe [--inspect|--dry-run|--self-test] [installDir] [configPath]");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ERROR] " + ex.Message);
                return 1;
            }
        }
    }
}
