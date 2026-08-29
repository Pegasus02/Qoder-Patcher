using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace QoderCN.GatewayManager
{
    public class ElevatedRequest
    {
        public string action { get; set; }
        public string installDir { get; set; }
        public string backupRoot { get; set; }
        public string responsePath { get; set; }
    }

    public class ElevatedResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
    }

    public static class ElevationProtocol
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void Execute(ElevatedRequest request)
        {
            if (string.Equals(request.action, "apply", StringComparison.OrdinalIgnoreCase))
            {
                PatcherEngine.ApplyPatch(request.installDir, request.backupRoot);
                return;
            }
            if (string.Equals(request.action, "restore", StringComparison.OrdinalIgnoreCase))
            {
                PatcherEngine.RestorePatch(request.installDir, request.backupRoot);
                return;
            }
            throw new InvalidOperationException("Unsupported elevated action: " + request.action);
        }

        public static int RunElevatedRequest(string encodedRequest)
        {
            ElevatedRequest request = null;
            ElevatedResponse response = new ElevatedResponse();
            try
            {
                if (!IsAdministrator()) throw new UnauthorizedAccessException("The patch operation was not elevated.");
                string requestJson = Encoding.UTF8.GetString(Convert.FromBase64String(encodedRequest));
                request = Serializer.Deserialize<ElevatedRequest>(requestJson);
                Execute(request);
                response.success = true;
                response.message = "OK";
            }
            catch (Exception ex)
            {
                response.success = false;
                response.message = ex.Message;
            }

            if (request != null && !string.IsNullOrWhiteSpace(request.responsePath))
            {
                File.WriteAllText(request.responsePath, Serializer.Serialize(response), new UTF8Encoding(false));
            }
            return response.success ? 0 : 1;
        }

        public static void Invoke(string action, string installDir, string backupRoot)
        {
            ElevatedRequest request = new ElevatedRequest
            {
                action = action,
                installDir = Path.GetFullPath(installDir),
                backupRoot = Path.GetFullPath(backupRoot)
            };

            if (IsAdministrator())
            {
                Execute(request);
                return;
            }

            string token = Guid.NewGuid().ToString("N");
            string responsePath = Path.Combine(Path.GetTempPath(), "qoder-patcher-elevated-" + token + ".response.json");
            request.responsePath = responsePath;

            try
            {
                string encodedRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(Serializer.Serialize(request)));
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = "--elevated-action " + encodedRequest,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();
                }

                if (!File.Exists(responsePath)) throw new InvalidOperationException("The elevated helper did not return a result.");
                ElevatedResponse response = Serializer.Deserialize<ElevatedResponse>(File.ReadAllText(responsePath, Encoding.UTF8));
                if (response == null || !response.success)
                {
                    throw new InvalidOperationException(response == null ? "The elevated helper returned an invalid result." : response.message);
                }
            }
            finally
            {
                if (File.Exists(responsePath)) File.Delete(responsePath);
            }
        }
    }
}
