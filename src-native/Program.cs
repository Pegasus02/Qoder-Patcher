using System;
using System.Windows.Forms;

namespace QoderCN.GatewayManager
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args != null && args.Length == 2 && string.Equals(args[0], "--elevated-action", StringComparison.OrdinalIgnoreCase))
            {
                return ElevationProtocol.RunElevatedRequest(args[1]);
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
