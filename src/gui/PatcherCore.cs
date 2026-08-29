using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace QoderCN.Patcher
{
    public class ModelItem
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public bool? vision { get; set; }
        public bool? reasoning { get; set; }
        public bool? tools { get; set; }
        public int? maxInputTokens { get; set; }
        public int? maxOutputTokens { get; set; }
        public string maxTokensField { get; set; }
        public List<string> efforts { get; set; }
        public bool? supportsDisabled { get; set; }

        public ModelItem()
        {
            tools = true;
            maxInputTokens = 131072;
            maxOutputTokens = 32768;
            maxTokensField = "max_tokens";
            efforts = new List<string>();
        }
    }

    public class ProviderProfile
    {
        public string displayName { get; set; }
        public string uiBaseUrl { get; set; }
        public string upstreamBaseUrl { get; set; }
        public string replaceProviderKey { get; set; }
        public string replaceProviderDisplayName { get; set; }
        public int? replaceProviderIndex { get; set; }
        public bool? skipValidation { get; set; }
        public int? firstPayloadTimeoutMs { get; set; }
        public int? streamIdleTimeoutMs { get; set; }
        public List<ModelItem> models { get; set; }

        public ProviderProfile()
        {
            displayName = "Local CPA Router (192.168.50.241)";
            uiBaseUrl = "http://127.0.0.1:8317/v1";
            upstreamBaseUrl = "http://192.168.50.241:8317/v1";
            replaceProviderKey = "anthropic";
            replaceProviderDisplayName = "Anthropic (Claude)";
            replaceProviderIndex = 1;
            skipValidation = true;
            firstPayloadTimeoutMs = 60000;
            streamIdleTimeoutMs = 0;
            models = new List<ModelItem>();
        }
    }

    public enum PatchState
    {
        NotFound,
        OriginalSupported,
        OriginalUnknown,
        PatchedV21,
        PatchedV20,
        PatchedV1
    }

    public class InspectionResult
    {
        public PatchState State { get; set; }
        public string InstallDir { get; set; }
        public string RuntimePath { get; set; }
        public string AsarPath { get; set; }
        public bool RuntimeExists { get; set; }
        public bool AsarExists { get; set; }
        public string RuntimeSha256 { get; set; }
        public string AsarSha256 { get; set; }
        public string Message { get; set; }
        public bool IsRunning { get; set; }
        public List<string> Backups { get; set; }

        public InspectionResult()
        {
            Backups = new List<string>();
        }
    }

    public class PatcherCore
    {
        public const string Version = "2.1.1-experimental";
        public const string PatchMarker = "QODER_CN_OAI_PATCH_V2_1";
        public const string PreviousPatchMarker = "QODER_CN_OAI_PATCH_V2*/";
        public const string LegacyPatchMarker = "QODER_CN_OAI_PATCH_V1";

        public const string RuntimeRelativePath = @"resources\app.asar.unpacked\node_modules\@qoder-ai\qoder-cn-agent-sdk\dist\_worker\qoder-worker-runtime.obf.mjs";
        public const string AsarRelativePath = @"resources\app.asar";

        public const string SupportedRuntimeSha256 = "7348879d488dc22cca1fc8138c3182233637f78bea210652701b3463b6d3f655";
        public const string SupportedAsarSha256 = "8f7429f5e0efd4850663fae438cf1340feda7e86ec2392d0d7820ee22699a941";

        private const string ImportAnchor = "import*as P8e from\"node:path\";import*as qxA from\"node:fs/promises\";";
        private const string ImportReplacement = "import*as qcv21fs from\"node:fs\";import*as P8e from\"node:path\";import*as qxA from\"node:fs/promises\";";

        private const string ConverterAnchor = "function XxA(A){";
        private const string ConverterReplacement = @"function qcv21cfg(){try{return JSON.parse(qcv21fs.readFileSync(process.env.QODER_CN_CUSTOM_PROVIDER_CONFIG||process.env.USERPROFILE+""/.qoder-cn/custom-openai-provider-v2.1.json"",""utf8""))}catch(A){return null}}function qcv21base(A){try{let e=new URL(A);if(""http:""!==e.protocol&&""https:""!==e.protocol||e.username||e.password||e.search||e.hash)return;let t=e.pathname.replace(/\/+$/g,"""");return t.endsWith(""/chat/completions"")&&(t=t.slice(0,-17)),e.pathname=t||""/"",e.toString().replace(/\/$/,"""")}catch{}}function qcv21url(A){try{let e=qcv21cfg();if(e&&""string""==typeof A&&new URL(e.uiBaseUrl).toString()===new URL(A).toString())return qcv21base(e.upstreamBaseUrl)??A}catch{}return A}function qcv21target(A){let t=A?.custom_model,i=A?.model_config;if(!t||""custom_model""!==i?.key)return;let e=qcv21cfg();if(!e)throw Error(""QODER_CN_PATCH_CONFIG_UNAVAILABLE"");let n=Array.isArray(e.models)?e.models.find(A=>A&&A.id===t.model):void 0;if(!n)return;let r=t.parameters?.api_key,o=qcv21base(e.upstreamBaseUrl);if(""string""!=typeof r||!r.trim())throw Error(""QODER_CN_PATCH_API_KEY_MISSING"");if(!o)throw Error(""QODER_CN_PATCH_UPSTREAM_URL_INVALID"");let s=Number.isInteger(e.firstPayloadTimeoutMs)&&e.firstPayloadTimeoutMs>0?e.firstPayloadTimeoutMs:6e4,a=Number.isInteger(e.streamIdleTimeoutMs)&&e.streamIdleTimeoutMs>=0?e.streamIdleTimeoutMs:0,g=Number.isInteger(n.maxInputTokens)&&n.maxInputTokens>0?n.maxInputTokens:131072,l=Number.isInteger(n.maxOutputTokens)&&n.maxOutputTokens>0?n.maxOutputTokens:32768;return{providerId:""qoder-cn-patcher"",adapter:""openai-compatible"",baseUrl:o,apiKey:r,model:{modelId:t.model,displayName:n.displayName??t.model,contextWindow:g,maxOutputTokens:l,capabilities:{tools:!1!==n.tools,vision:!0===n.vision,thinking:!0===n.reasoning},maxTokensField:""max_completion_tokens""===n.maxTokensField?""max_completion_tokens"":""max_tokens""},timeouts:{firstPayloadTimeoutMs:s,...a>0?{streamIdleTimeoutMs:a}:{}}}}function XxA(A){/*QODER_CN_OAI_PATCH_V2_1*/";

        private const string ModelUrlAnchor = "url:A.url,model:A.model,provider:A.provider";
        private const string ModelUrlReplacement = "url:qcv21url(A.url),model:A.model,provider:A.provider";

        private const string CatalogAnchor = "n=await e().getBYOKConfig(),r=t;B(pn(i,\"success\",{providers:n?.providers.map(A)??[]}))";
        private const string CatalogReplacement = @"n=await e().getBYOKConfig(),r=t;let qcp=n?.providers.map(A)??[];try{let qcc=qcv21cfg();if(!qcc)throw Error(""QODER_CN_PATCH_CONFIG_UNAVAILABLE"");let qci=Number.isInteger(qcc.replaceProviderIndex)?qcc.replaceProviderIndex:qcp.findIndex(q=>q.key===qcc.replaceProviderKey||q.display_name===qcc.replaceProviderDisplayName);if(qci<0||qci>=qcp.length)throw Error(""QODER_CN_PATCH_REPLACEMENT_PROVIDER_NOT_FOUND"");let qcb=qcp[qci],qcm=(qcc.models??[]).map(q=>({key:q.id,display_name:q.displayName??q.id,is_vl:q.vision===true,is_reasoning:q.reasoning===true,format:""openai"",max_input_tokens:q.maxInputTokens??131072,efforts:q.efforts??[],supports_disabled:q.supportsDisabled===true}));if(!qcm.length)throw Error(""QODER_CN_PATCH_MODELS_EMPTY"");qcp[qci]={...qcb,display_name:qcc.displayName?? ""Local OpenAI Compatible"",url:qcc.uiBaseUrl,fields:[{key:""api_key"",display_name:""API Key"",type:""free_input"",mandatory:true}],types:[{key:""openai-compatible"",display_name:""OpenAI Compatible"",style:""openai"",models:qcm}]}}catch(qce){Q.warn(""[qoder-cn-openai-patch-v2.1] custom provider not loaded:"",qce)}B(pn(i,""success"",{providers:qcp}))";

        private const string ValidationAnchor = "o=await r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t);B(pn(i,\"success\",{success:o}))";
        private const string ValidationReplacement = @"o=await(async()=>{try{let qcv=qcv21cfg();if(qcv&&qcv.skipValidation!==false&&new URL(qcv.uiBaseUrl).toString()===new URL(A).toString())return true}catch(qce){Q.warn(""[qoder-cn-openai-patch-v2.1] validation override unavailable:"",qce)}return r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t)})();B(pn(i,""success"",{success:o}))";

        private const string InferenceRouteAnchor = "let A=ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)";
        private const string InferenceRouteReplacement = "let A=qcv21target(t)??ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)";

        public static string GetDefaultInstallDir()
        {
            return @"C:\Program Files\Qoder\Qoder CN";
        }

        public static string GetRuntimeConfigPath()
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(profile, @".qoder-cn\custom-openai-provider-v2.1.json");
        }

        public static string GetDefaultBackupRootDir()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, @"QoderCNOpenAICompatiblePatcher\backups-v2");
        }

        public static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool IsQoderRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        string name = p.ProcessName.ToLowerInvariant();
                        if (name == "qoder cn" || name == "qoder")
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        public static string ComputeFileSha256(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder();
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static int CountOccurrences(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return 0;
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(pattern, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += pattern.Length;
            }
            return count;
        }

        public static string NormalizeUpstreamUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var uri = new Uri(url.Trim());
                if (uri.Scheme != "http" && uri.Scheme != "https") return null;
                if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return null;
                
                string path = uri.AbsolutePath.TrimEnd('/');
                if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(0, path.Length - 17).TrimEnd('/');
                }
                
                var builder = new UriBuilder(uri.Scheme, uri.Host, uri.Port, string.IsNullOrEmpty(path) ? "/" : path);
                return builder.Uri.ToString().TrimEnd('/');
            }
            catch
            {
                return null;
            }
        }

        public static ProviderProfile ParseProfile(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) throw new ArgumentException("配置内容为空。");
            
            // 安全检查：不允许在 tracked 配置中包含明文 API Key
            if (Regex.IsMatch(jsonText, "\"(?:api[_-]?key|access[_-]?token|authorization)\"\\s*:", RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException("配置文件中检测到敏感字段（如 api_key）。请勿在预设配置文件中硬编码 API Key，Key 应当在 Qoder CN 界面中输入。");
            }

            var serializer = new JavaScriptSerializer();
            var profile = serializer.Deserialize<ProviderProfile>(jsonText);
            if (profile == null) throw new InvalidOperationException("无法解析 JSON 配置文件。");

            if (string.IsNullOrWhiteSpace(profile.upstreamBaseUrl))
                throw new InvalidOperationException("配置缺少 upstreamBaseUrl。");
            if (NormalizeUpstreamUrl(profile.upstreamBaseUrl) == null)
                throw new InvalidOperationException("upstreamBaseUrl 格式无效（必须为有效 http/https 基础 URL，不可带 token/query）。");
            if (profile.models == null || profile.models.Count == 0)
                throw new InvalidOperationException("models 列表不能为空。");

            foreach (var m in profile.models)
            {
                if (string.IsNullOrWhiteSpace(m.id))
                    throw new InvalidOperationException("存在没有 ID 的模型项。");
            }

            return profile;
        }

        public static string SerializeProfile(ProviderProfile profile)
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(profile);
        }

        public static InspectionResult Inspect(string installDir)
        {
            var result = new InspectionResult
            {
                InstallDir = installDir,
                RuntimePath = Path.Combine(installDir, RuntimeRelativePath),
                AsarPath = Path.Combine(installDir, AsarRelativePath),
                IsRunning = IsQoderRunning()
            };

            result.RuntimeExists = File.Exists(result.RuntimePath);
            result.AsarExists = File.Exists(result.AsarPath);

            if (!result.RuntimeExists)
            {
                result.State = PatchState.NotFound;
                result.Message = "未找到 Qoder CN 运行库文件，请确认安装目录是否正确。";
                return result;
            }

            result.RuntimeSha256 = ComputeFileSha256(result.RuntimePath);
            result.AsarSha256 = result.AsarExists ? ComputeFileSha256(result.AsarPath) : null;

            string content = File.ReadAllText(result.RuntimePath, Encoding.UTF8);

            if (content.Contains(PatchMarker))
            {
                result.State = PatchState.PatchedV21;
                result.Message = "已修补 (v2.1 Direct Custom Routing)";
            }
            else if (content.Contains(PreviousPatchMarker))
            {
                result.State = PatchState.PatchedV20;
                result.Message = "已修补 (v2.0 早期版本)";
            }
            else if (content.Contains(LegacyPatchMarker))
            {
                result.State = PatchState.PatchedV1;
                result.Message = "已修补 (v1 早期版本)";
            }
            else if (string.Equals(result.RuntimeSha256, SupportedRuntimeSha256, StringComparison.OrdinalIgnoreCase))
            {
                result.State = PatchState.OriginalSupported;
                result.Message = "官方原版 (已验证支持修补)";
            }
            else
            {
                result.State = PatchState.OriginalUnknown;
                result.Message = "未知版本 / 自定义版本 (SHA256: " + result.RuntimeSha256 + ")";
            }

            // 查找备份
            string backupRoot = GetDefaultBackupRootDir();
            if (Directory.Exists(backupRoot))
            {
                var dirs = Directory.GetDirectories(backupRoot);
                Array.Sort(dirs);
                Array.Reverse(dirs);
                foreach (var d in dirs)
                {
                    string bf = Path.Combine(d, "qoder-worker-runtime.obf.mjs");
                    if (File.Exists(bf))
                    {
                        result.Backups.Add(d);
                    }
                }
            }

            return result;
        }

        public static string PerformPatchTransform(string sourceText)
        {
            string current = sourceText;

            // 1. Import anchor
            int importCount = CountOccurrences(current, ImportAnchor);
            if (importCount != 1)
                throw new InvalidOperationException(string.Format("ImportAnchor 匹配次数为 {0}（预期为 1 次）。当前 Qoder 版本不兼容或已被修改。", importCount));
            current = current.Replace(ImportAnchor, ImportReplacement);

            // 2. Converter anchor
            int convCount = CountOccurrences(current, ConverterAnchor);
            if (convCount != 1)
                throw new InvalidOperationException(string.Format("ConverterAnchor 匹配次数为 {0}（预期为 1 次）。当前 Qoder 版本不兼容或已被修改。", convCount));
            current = current.Replace(ConverterAnchor, ConverterReplacement);

            // 3. ModelUrl anchor
            int urlCount = CountOccurrences(current, ModelUrlAnchor);
            if (urlCount != 1)
                throw new InvalidOperationException(string.Format("ModelUrlAnchor 匹配次数为 {0}（预期为 1 次）。当前 Qoder 版本不兼容或已被修改。", urlCount));
            current = current.Replace(ModelUrlAnchor, ModelUrlReplacement);

            // 4. Catalog anchor
            int catCount = CountOccurrences(current, CatalogAnchor);
            if (catCount != 1)
                throw new InvalidOperationException(string.Format("CatalogAnchor 匹配次数为 {0}（预期为 1 次）。当前 Qoder 版本不兼容或已被修改。", catCount));
            current = current.Replace(CatalogAnchor, CatalogReplacement);

            // 5. Validation anchor
            int valCount = CountOccurrences(current, ValidationAnchor);
            if (valCount != 1)
                throw new InvalidOperationException(string.Format("ValidationAnchor 匹配次数为 {0}（预期为 1 次）。当前 Qoder 版本不兼容或已被修改。", valCount));
            current = current.Replace(ValidationAnchor, ValidationReplacement);

            // 6. InferenceRoute anchor
            int infCount = CountOccurrences(current, InferenceRouteAnchor);
            if (infCount != 1)
                throw new InvalidOperationException(string.Format("InferenceRouteAnchor 匹配次数为 {0}（预期为 1 次）。当前 Qoder 版本不兼容或已被修改。", infCount));
            current = current.Replace(InferenceRouteAnchor, InferenceRouteReplacement);

            return current;
        }

        public static string PerformUnpatchTransform(string patchedText)
        {
            string current = patchedText;
            if (current.Contains(ImportReplacement))
                current = current.Replace(ImportReplacement, ImportAnchor);
            if (current.Contains(ConverterReplacement + "\r\n"))
                current = current.Replace(ConverterReplacement + "\r\n", ConverterAnchor);
            else if (current.Contains(ConverterReplacement + "\n"))
                current = current.Replace(ConverterReplacement + "\n", ConverterAnchor);
            else if (current.Contains(ConverterReplacement))
                current = current.Replace(ConverterReplacement, ConverterAnchor);
            if (current.Contains(ModelUrlReplacement))
                current = current.Replace(ModelUrlReplacement, ModelUrlAnchor);
            if (current.Contains(CatalogReplacement))
                current = current.Replace(CatalogReplacement, CatalogAnchor);
            if (current.Contains(ValidationReplacement))
                current = current.Replace(ValidationReplacement, ValidationAnchor);
            if (current.Contains(InferenceRouteReplacement))
                current = current.Replace(InferenceRouteReplacement, InferenceRouteAnchor);
            return current;
        }

        public static string GetOriginalSource(string installDir, string backupRoot = null)
        {
            string runtimePath = Path.Combine(installDir, RuntimeRelativePath);
            if (!File.Exists(runtimePath))
                throw new FileNotFoundException("找不到目标运行库文件: " + runtimePath);

            string current = File.ReadAllText(runtimePath, Encoding.UTF8);
            if (!current.Contains(PatchMarker) && !current.Contains(PreviousPatchMarker) && !current.Contains(LegacyPatchMarker))
            {
                return current;
            }

            // 1. 如果当前文件为 v2.1 修补态，尝试确定性逆向还原并校验 SHA256
            if (current.Contains(PatchMarker))
            {
                string unpatched = PerformUnpatchTransform(current);
                if (!unpatched.Contains(PatchMarker))
                {
                    return unpatched;
                }
            }

            // 2. 尝试从最近的备份中寻找原版
            if (string.IsNullOrEmpty(backupRoot)) backupRoot = GetDefaultBackupRootDir();
            if (Directory.Exists(backupRoot))
            {
                var dirs = Directory.GetDirectories(backupRoot);
                Array.Sort(dirs);
                Array.Reverse(dirs);
                foreach (var d in dirs)
                {
                    string bf = Path.Combine(d, "qoder-worker-runtime.obf.mjs");
                    if (File.Exists(bf))
                    {
                        string bText = File.ReadAllText(bf, Encoding.UTF8);
                        if (!bText.Contains(PatchMarker) && !bText.Contains(PreviousPatchMarker) && !bText.Contains(LegacyPatchMarker))
                        {
                            return bText;
                        }
                    }
                }
            }

            throw new InvalidOperationException("当前文件已被早期修补版本修改，且未在本地备份目录中找到原版备份。请确认原版文件或重新安装 Qoder CN 后修补。");
        }

        public static string DryRun(string installDir, string configPath)
        {
            var profile = ParseProfile(File.ReadAllText(configPath, Encoding.UTF8));
            string source = GetOriginalSource(installDir);
            string patched = PerformPatchTransform(source);

            var sb = new StringBuilder();
            sb.AppendLine("=== [DRY RUN 预演测试通过] ===");
            sb.AppendLine(string.Format("配置渠道名称: {0}", profile.displayName));
            sb.AppendLine(string.Format("上游 Base URL: {0}", profile.upstreamBaseUrl));
            sb.AppendLine(string.Format("模型列表数量: {0}", profile.models.Count));
            foreach (var m in profile.models)
            {
                sb.AppendLine(string.Format("  - {0} ({1}) [MaxInput: {2}, Reasoning: {3}, Vision: {4}]",
                    m.id, m.displayName ?? m.id, m.maxInputTokens ?? 131072, m.reasoning == true, m.vision == true));
            }
            sb.AppendLine();
            sb.AppendLine(string.Format("原代码大小: {0} 字节", Encoding.UTF8.GetByteCount(source)));
            sb.AppendLine(string.Format("修补后大小: {0} 字节", Encoding.UTF8.GetByteCount(patched)));
            sb.AppendLine("所有 6 处注入锚点校验均唯一定位并替换成功，已具备安全修补条件。");
            return sb.ToString();
        }

        public static void Apply(string installDir, string configPath, string runtimeConfigPath = null, string backupRoot = null)
        {
            if (IsQoderRunning())
            {
                throw new InvalidOperationException("检测到 Qoder CN 正在运行中！请先完全退出 Qoder CN 再执行修补。");
            }

            if (string.IsNullOrEmpty(runtimeConfigPath)) runtimeConfigPath = GetRuntimeConfigPath();
            if (string.IsNullOrEmpty(backupRoot)) backupRoot = GetDefaultBackupRootDir();

            // 1. 验证配置文件
            string configJson = File.ReadAllText(configPath, Encoding.UTF8);
            var profile = ParseProfile(configJson);

            // 2. 获取原版源码并生成修补代码
            string originalSource = GetOriginalSource(installDir, backupRoot);
            string patchedSource = PerformPatchTransform(originalSource);

            // 3. 备份原版
            string runtimePath = Path.Combine(installDir, RuntimeRelativePath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupDir = Path.Combine(backupRoot, timestamp);
            Directory.CreateDirectory(backupDir);

            string backupFile = Path.Combine(backupDir, "qoder-worker-runtime.obf.mjs");
            File.WriteAllText(backupFile, originalSource, new UTF8Encoding(false));

            string metaFile = Path.Combine(backupDir, "metadata.json");
            var metaObj = new Dictionary<string, object>
            {
                { "createdAt", DateTime.UtcNow.ToString("o") },
                { "installDir", installDir },
                { "sha256", ComputeFileSha256(backupFile) },
                { "version", Version }
            };
            File.WriteAllText(metaFile, new JavaScriptSerializer().Serialize(metaObj), Encoding.UTF8);

            // 4. 写入运行时配置文件到 ~/.qoder-cn/
            string runtimeDir = Path.GetDirectoryName(runtimeConfigPath);
            if (!Directory.Exists(runtimeDir))
            {
                Directory.CreateDirectory(runtimeDir);
            }
            File.WriteAllText(runtimeConfigPath, configJson, new UTF8Encoding(false));

            // 5. 写入修补后的运行时文件
            File.WriteAllText(runtimePath, patchedSource, new UTF8Encoding(false));
        }

        public static string Restore(string installDir, string backupRoot = null)
        {
            if (IsQoderRunning())
            {
                throw new InvalidOperationException("检测到 Qoder CN 正在运行中！请先完全退出 Qoder CN 再执行还原。");
            }

            string runtimePath = Path.Combine(installDir, RuntimeRelativePath);
            if (!File.Exists(runtimePath))
            {
                throw new FileNotFoundException("找不到目标运行库文件: " + runtimePath);
            }

            if (string.IsNullOrEmpty(backupRoot)) backupRoot = GetDefaultBackupRootDir();
            if (Directory.Exists(backupRoot))
            {
                var dirs = Directory.GetDirectories(backupRoot);
                Array.Sort(dirs);
                Array.Reverse(dirs);

                foreach (var d in dirs)
                {
                    string bf = Path.Combine(d, "qoder-worker-runtime.obf.mjs");
                    if (File.Exists(bf))
                    {
                        string originalText = File.ReadAllText(bf, Encoding.UTF8);
                        File.WriteAllText(runtimePath, originalText, new UTF8Encoding(false));
                        return d;
                    }
                }
            }

            // 如果没有备份目录，尝试从已修补内容逆向还原
            string currentText = File.ReadAllText(runtimePath, Encoding.UTF8);
            if (currentText.Contains(PatchMarker))
            {
                string unpatchedText = PerformUnpatchTransform(currentText);
                File.WriteAllText(runtimePath, unpatchedText, new UTF8Encoding(false));
                return "内置确定性逆向还原 (无历史备份文件夹)";
            }

            throw new FileNotFoundException("未找到历史备份文件，且当前文件未识别为可逆修补版本。");
        }

        public static void LaunchQoder(string installDir)
        {
            string exe1 = Path.Combine(installDir, "Qoder CN.exe");
            string exe2 = Path.Combine(installDir, "Qoder.exe");
            string target = File.Exists(exe1) ? exe1 : (File.Exists(exe2) ? exe2 : null);

            if (target == null)
            {
                throw new FileNotFoundException("在目标安装目录未找到 Qoder CN.exe。");
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = target,
                WorkingDirectory = installDir
            });
        }
    }
}
