using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace QoderCN.GatewayManager
{
    public class PatcherEngine
    {
        public const string PatchMarker = "QODER_CN_OAI_PATCH_V3_2_0";
        public static readonly string[] PreviousPatchMarkers = new string[] {
            "QODER_CN_OAI_PATCH_MAC_V3_2_0",
            "QODER_CN_OAI_PATCH_V3_1_0",
            "QODER_CN_OAI_PATCH_V3_0_1",
            "QODER_CN_OAI_PATCH_V3_0",
            "QODER_CN_OAI_PATCH_V2_3",
            "QODER_CN_OAI_PATCH_V2_2",
            "QODER_CN_OAI_PATCH_V2_1",
            "QODER_CN_OAI_PATCH_V2"
        };
        public const string LegacyPatchMarker = "QODER_CN_OAI_PATCH_V1";
        public const string RuntimeRelativePath = @"resources\app.asar.unpacked\node_modules\@qoder-ai\qoder-cn-agent-sdk\dist\_worker\qoder-worker-runtime.obf.mjs";
        public const string AsarRelativePath = @"resources\app.asar";

        public static readonly string[] SupportedRuntimeSha256List = new string[] {
            "c79c0cdcaba7f8eeacc5d8139add45d142c21f5354c03c0f4f1d8d2cfbc73150", // v1.1.35 (Qoder 0.1.3+)
            "7348879d488dc22cca1fc8138c3182233637f78bea210652701b3463b6d3f655"  // v1.1.31 (Qoder 0.1.2)
        };
        public static readonly string[] SupportedAsarSha256List = new string[] {
            "f51f8b148ab29dfeb56716349ca660b2706cc18563a26a42ceae33185f191bef", // Qoder 0.1.3+
            "8f7429f5e0efd4850663fae438cf1340feda7e86ec2392d0d7820ee22699a941"  // Qoder 0.1.2
        };

        public const string SupportedRuntimeSha256 = "c79c0cdcaba7f8eeacc5d8139add45d142c21f5354c03c0f4f1d8d2cfbc73150";
        public const string SupportedAsarSha256 = "f51f8b148ab29dfeb56716349ca660b2706cc18563a26a42ceae33185f191bef";

        // v1.1.35+ (Qoder 0.1.3+) Anchors & Replacements
        public const string ImportAnchor_v1135 = "import{createRequire as __banner_createRequire}from\"node:module\";";
        public const string ImportReplacement_v1135 = "import*as qcv30fs from\"node:fs\";import{createRequire as __banner_createRequire}from\"node:module\";";
        public const string ConverterAnchor_v1135 = "function TvA(A){";
        public const string CatalogAnchor_v1135 = "n=await e().getBYOKConfig(),r=i;c(Di(t,\"success\",{providers:n?.providers.map(A)??[]}))";
        public const string CatalogReplacement_v1135 = "n=await e().getBYOKConfig(),r=i;let qcp=n?.providers.map(A)??[];if(qcp.length===0){qcp=[{key:\"bailian\",display_name:\"Alibaba Cloud Model Studio\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"bailian\",display_name:\"Alibaba Cloud Model Studio\",style:\"bailian\",models:[]}]},{key:\"deepseek\",display_name:\"DeepSeek\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"deepseek\",display_name:\"DeepSeek\",style:\"deepseek\",models:[]}]},{key:\"moonshot\",display_name:\"Kimi\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"moonshot\",display_name:\"Kimi\",style:\"moonshot\",models:[]}]},{key:\"zhipu\",display_name:\"Z.ai\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"zhipu\",display_name:\"Z.ai\",style:\"zhipu\",models:[]}]},{key:\"minimax\",display_name:\"MiniMax\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"minimax\",display_name:\"MiniMax\",style:\"minimax\",models:[]}]},{key:\"qwen\",display_name:\"QwenCloud-China\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"qwen\",display_name:\"QwenCloud-China\",style:\"qwen\",models:[]}]},{key:\"xiaomi\",display_name:\"Xiaomi MIMO\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"xiaomi\",display_name:\"Xiaomi MIMO\",style:\"xiaomi\",models:[]}]}];}try{let qcc=qcv30cfg();if(qcc){let qpk=qcc.replaceProviderKey||\"anthropic\";let qci=qcp.findIndex(q=>q.key===qpk||q.display_name===qcc.replaceProviderDisplayName);if(qci<0&&Number.isInteger(qcc.replaceProviderIndex))qci=qcc.replaceProviderIndex;let qcb=qci>=0&&qci<qcp.length?qcp[qci]:null;let qcm=(qcc.models??[]).map(q=>({key:q.id,display_name:q.displayName??q.id,is_vl:q.vision===true,is_reasoning:q.reasoning===true,format:\"openai\",max_input_tokens:Number.isInteger(q.maxInputTokens)&&q.maxInputTokens>0?q.maxInputTokens:131072,efforts:Array.isArray(q.efforts)?q.efforts:[],supports_disabled:q.supportsDisabled===true}));if(qcm.length>0){let customProvider={...qcb,key:qpk,display_name:qcc.displayName??\"Local OpenAI Compatible\",api_key_url:\"\",url:qcc.uiBaseUrl,fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"openai-compatible\",display_name:\"OpenAI Compatible\",style:\"openai\",models:qcm}]};if(qci>=0&&qci<qcp.length){qcp[qci]=customProvider}else{qcp.unshift(customProvider)}}}}catch(qce){C.warn(\"[qoder-cn-openai-patch-v3.2.0] custom provider not loaded:\",qce)}c(Di(t,\"success\",{enabled:true,providers:qcp}))";
        public const string ValidationAnchor_v1135 = "o=await r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,i);c(Di(t,\"success\",{success:o}))";
        public const string ValidationReplacement_v1135 = "o=await(async()=>{try{let qcv=qcv30cfg();if(qcv&&qcv.skipValidation!==false){let modelMatch=Array.isArray(qcv.models)&&qcv.models.some(m=>m&&m.id===n.model);let providerMatch=n.provider===(qcv.replaceProviderKey||\"anthropic\");let urlMatch=false;if(A&&typeof A===\"string\"&&qcv.uiBaseUrl){try{urlMatch=new URL(qcv.uiBaseUrl).toString()===new URL(A).toString()}catch{}}if(modelMatch||providerMatch||urlMatch)return true}}catch(qce){C.warn(\"[qoder-cn-openai-patch-v3.2.0] validation override unavailable:\",qce)}return r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,i)})();c(Di(t,\"success\",{success:o}))";
        public const string InferenceRouteAnchor_v1135 = "let A=bje(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)";
        public const string InferenceRouteReplacement_v1135 = "let A=qcv30target(t)??bje(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)";
        public const string StartupBYOKAnchor_v1135 = "async function ree(A,e){let t=e?await $Oe(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);let r=[...n.values()];";
        public const string StartupBYOKReplacement_v1135 = "async function ree(A,e){let t=e?await $Oe(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||\"anthropic\";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:\"openai-compatible\",parameters:{api_key:\"\"},url:qcc.uiBaseUrl,format:\"openai\",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};n.has(m.id)||n.set(m.id,item)}}}}catch(qce){C.warn(\"[qoder-cn-openai-patch-v3.2.0] startup custom models injection failed:\",qce)}let r=[...n.values()];";
        public const string ModelListAnchor_v1135 = "function FIo(A,e,t){let i=\"function\"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];";
        public const string ModelListReplacement_v1135 = "function FIo(A,e,t){let i=\"function\"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||\"anthropic\";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:\"openai-compatible\",parameters:{api_key:\"\"},url:qcc.uiBaseUrl,format:\"openai\",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};i.some(x=>x.key===m.id)||i.push(item)}}}}catch(qce){C.warn(\"[qoder-cn-openai-patch-v3.2.0] FIo injection failed:\",qce)}";

        // v1.1.31 (Qoder 0.1.2) Anchors & Replacements
        public const string ImportAnchor_v1131 = "import*as P8e from\"node:path\";import*as qxA from\"node:fs/promises\";";
        public const string ImportReplacement_v1131 = "import*as qcv30fs from\"node:fs\";import*as P8e from\"node:path\";import*as qxA from\"node:fs/promises\";";
        public const string ConverterAnchor_v1131 = "function XxA(A){";
        public const string CatalogAnchor_v1131 = "n=await e().getBYOKConfig(),r=t;B(pn(i,\"success\",{providers:n?.providers.map(A)??[]}))";
        public const string CatalogReplacement_v1131 = "n=await e().getBYOKConfig(),r=t;let qcp=n?.providers.map(A)??[];if(qcp.length===0){qcp=[{key:\"bailian\",display_name:\"Alibaba Cloud Model Studio\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"bailian\",display_name:\"Alibaba Cloud Model Studio\",style:\"bailian\",models:[]}]},{key:\"deepseek\",display_name:\"DeepSeek\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"deepseek\",display_name:\"DeepSeek\",style:\"deepseek\",models:[]}]},{key:\"moonshot\",display_name:\"Kimi\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"moonshot\",display_name:\"Kimi\",style:\"moonshot\",models:[]}]},{key:\"zhipu\",display_name:\"Z.ai\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"zhipu\",display_name:\"Z.ai\",style:\"zhipu\",models:[]}]},{key:\"minimax\",display_name:\"MiniMax\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"minimax\",display_name:\"MiniMax\",style:\"minimax\",models:[]}]},{key:\"qwen\",display_name:\"QwenCloud-China\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"qwen\",display_name:\"QwenCloud-China\",style:\"qwen\",models:[]}]},{key:\"xiaomi\",display_name:\"Xiaomi MIMO\",fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"xiaomi\",display_name:\"Xiaomi MIMO\",style:\"xiaomi\",models:[]}]}];}try{let qcc=qcv30cfg();if(qcc){let qpk=qcc.replaceProviderKey||\"anthropic\";let qci=qcp.findIndex(q=>q.key===qpk||q.display_name===qcc.replaceProviderDisplayName);if(qci<0&&Number.isInteger(qcc.replaceProviderIndex))qci=qcc.replaceProviderIndex;let qcb=qci>=0&&qci<qcp.length?qcp[qci]:null;let qcm=(qcc.models??[]).map(q=>({key:q.id,display_name:q.displayName??q.id,is_vl:q.vision===true,is_reasoning:q.reasoning===true,format:\"openai\",max_input_tokens:Number.isInteger(q.maxInputTokens)&&q.maxInputTokens>0?q.maxInputTokens:131072,efforts:Array.isArray(q.efforts)?q.efforts:[],supports_disabled:q.supportsDisabled===true}));if(qcm.length>0){let customProvider={...qcb,key:qpk,display_name:qcc.displayName??\"Local OpenAI Compatible\",api_key_url:\"\",url:qcc.uiBaseUrl,fields:[{key:\"api_key\",display_name:\"API Key\",type:\"free_input\",mandatory:true}],types:[{key:\"openai-compatible\",display_name:\"OpenAI Compatible\",style:\"openai\",models:qcm}]};if(qci>=0&&qci<qcp.length){qcp[qci]=customProvider}else{qcp.unshift(customProvider)}}}}catch(qce){Q.warn(\"[qoder-cn-openai-patch-v3.1.0] custom provider not loaded:\",qce)}B(pn(i,\"success\",{enabled:true,providers:qcp}))";
        public const string ValidationAnchor_v1131 = "o=await r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t);B(pn(i,\"success\",{success:o}))";
        public const string ValidationReplacement_v1131 = "o=await(async()=>{try{let qcv=qcv30cfg();if(qcv&&qcv.skipValidation!==false){let modelMatch=Array.isArray(qcv.models)&&qcv.models.some(m=>m&&m.id===n.model);let providerMatch=n.provider===(qcv.replaceProviderKey||\"anthropic\");let urlMatch=false;if(A&&typeof A===\"string\"&&qcv.uiBaseUrl){try{urlMatch=new URL(qcv.uiBaseUrl).toString()===new URL(A).toString()}catch{}}if(modelMatch||providerMatch||urlMatch)return true}}catch(qce){Q.warn(\"[qoder-cn-openai-patch-v3.1.0] validation override unavailable:\",qce)}return r().checkBYOKModel(n.provider,n.model,{api_key:n.api_key},A,e,t)})();B(pn(i,\"success\",{success:o}))";
        public const string InferenceRouteAnchor_v1131 = "let A=ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)";
        public const string InferenceRouteReplacement_v1131 = "let A=qcv30target(t)??ZVe(t.model_config,()=>S?.getExternalProviderRegistry());for(;;)";
        public const string StartupBYOKAnchor_v1131 = "async function n$A(A,e){let t=e?await y8e(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);let r=[...n.values()];";
        public const string StartupBYOKReplacement_v1131 = "async function n$A(A,e){let t=e?await y8e(e):[],i=A.getSettingsBYOKModels(),n=new Map(t.map(A=>[A.key,A]));for(let A of i)n.set(A.key,A);try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||\"anthropic\";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:\"openai-compatible\",parameters:{api_key:\"\"},url:qcc.uiBaseUrl,format:\"openai\",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};n.has(m.id)||n.set(m.id,item)}}}}catch(qce){Q.warn(\"[qoder-cn-openai-patch-v3.1.0] startup custom models injection failed:\",qce)}let r=[...n.values()];";
        public const string ModelListAnchor_v1131 = "function Zdo(A,e,t){let i=\"function\"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];";
        public const string ModelListReplacement_v1131 = "function Zdo(A,e,t){let i=\"function\"==typeof t.getBYOKCustomModels?t.getBYOKCustomModels()??[]:[];try{let qcc=qcv30cfg();if(qcc&&Array.isArray(qcc.models)){let qpk=qcc.replaceProviderKey||\"anthropic\";for(let m of qcc.models){if(m&&m.id){let item={key:m.id,display_name:m.displayName??m.id,provider:qpk,model:m.id,type:\"openai-compatible\",parameters:{api_key:\"\"},url:qcc.uiBaseUrl,format:\"openai\",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled};i.some(x=>x.key===m.id)||i.push(item)}}}}catch(qce){Q.warn(\"[qoder-cn-openai-patch-v3.1.0] Zdo injection failed:\",qce)}";

        // Shared Anchors
        public const string ModelUrlAnchor = "url:A.url,model:A.model,provider:A.provider";
        public const string ModelUrlReplacement = "url:qcv30url(A.url),model:A.model,provider:A.provider";

        public const string GetModelAnchor = "getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)}";
        public const string GetModelReplacement = "getModel(A){return this.allModels.get(A)??this.getModelCaseInsensitive(A)??this.getModelByDisplayName(A)??qcv30model(A)}";

        // Default public aliases matching latest baseline
        public const string ImportAnchor = ImportAnchor_v1135;
        public const string ImportReplacement = ImportReplacement_v1135;
        public const string ConverterAnchor = ConverterAnchor_v1135;
        public const string CatalogAnchor = CatalogAnchor_v1135;
        public const string CatalogReplacement = CatalogReplacement_v1135;
        public const string ValidationAnchor = ValidationAnchor_v1135;
        public const string ValidationReplacement = ValidationReplacement_v1135;
        public const string InferenceRouteAnchor = InferenceRouteAnchor_v1135;
        public const string InferenceRouteReplacement = InferenceRouteReplacement_v1135;
        public const string StartupBYOKAnchor = StartupBYOKAnchor_v1135;
        public const string StartupBYOKReplacement = StartupBYOKReplacement_v1135;
        public const string ModelListAnchor = ModelListAnchor_v1135;
        public const string ModelListReplacement = ModelListReplacement_v1135;

        public static bool IsSupportedRuntimeSha(string sha)
        {
            if (string.IsNullOrEmpty(sha)) return false;
            foreach (string s in SupportedRuntimeSha256List)
            {
                if (string.Equals(sha, s, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsSupportedAsarSha(string sha)
        {
            if (string.IsNullOrEmpty(sha)) return false;
            foreach (string s in SupportedAsarSha256List)
            {
                if (string.Equals(sha, s, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string GetDefaultInstallDir()
        {
            string envPath = Environment.GetEnvironmentVariable("QODER_CN_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
            {
                return Path.GetFullPath(envPath);
            }

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                string userDir = Path.Combine(localAppData, "Programs", "Qoder CN");
                if (Directory.Exists(userDir) && File.Exists(Path.Combine(userDir, AsarRelativePath)))
                {
                    return userDir;
                }
            }

            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(progFiles))
            {
                string pf1 = Path.Combine(progFiles, "Qoder", "Qoder CN");
                if (Directory.Exists(pf1) && File.Exists(Path.Combine(pf1, AsarRelativePath)))
                {
                    return pf1;
                }
                string pf2 = Path.Combine(progFiles, "Qoder CN");
                if (Directory.Exists(pf2) && File.Exists(Path.Combine(pf2, AsarRelativePath)))
                {
                    return pf2;
                }
            }

            if (!string.IsNullOrEmpty(localAppData))
            {
                string userDir = Path.Combine(localAppData, "Programs", "Qoder CN");
                if (Directory.Exists(userDir)) return userDir;
            }

            return @"C:\Program Files\Qoder\Qoder CN";
        }

        public static string BuildConverterReplacement(bool isV1135)
        {
            string funcName = isV1135 ? "TvA" : "XxA";
            return "function qcv30cfg(){try{let h=process.env.USERPROFILE||process.env.HOME||\"\";let p=process.env.QODER_CN_CUSTOM_PROVIDER_CONFIG||(h?h+\"/.qoder-cn/custom-openai-provider-v3.2.0.json\":\"\");if(!p||!qcv30fs.existsSync(p)){let a0000=h?h+\"/.qoder-cn/custom-openai-provider-v3.1.0.json\":\"\";let a000=h?h+\"/.qoder-cn/custom-openai-provider-v3.0.1.json\":\"\";let a00=h?h+\"/.qoder-cn/custom-openai-provider-v3.0.json\":\"\";let a0=h?h+\"/.qoder-cn/custom-openai-provider-v2.3.json\":\"\";let a1=h?h+\"/.qoder-cn/custom-openai-provider-v2.2.json\":\"\";let a2=h?h+\"/.qoder-cn/custom-openai-provider-v2.1.json\":\"\";let a3=h?h+\"/.qoder-cn/custom-openai-provider.json\":\"\";p=a0000&&qcv30fs.existsSync(a0000)?a0000:a000&&qcv30fs.existsSync(a000)?a000:a00&&qcv30fs.existsSync(a00)?a00:a0&&qcv30fs.existsSync(a0)?a0:a1&&qcv30fs.existsSync(a1)?a1:a2&&qcv30fs.existsSync(a2)?a2:a3&&qcv30fs.existsSync(a3)?a3:p}let txt=p&&qcv30fs.existsSync(p)?qcv30fs.readFileSync(p,\"utf8\").replace(/^\\uFEFF/,\"\").trim():\"\";return txt?JSON.parse(txt):null}catch(A){return null}}function qcv30base(A){try{let e=new URL(A);if(\"http:\"!==e.protocol&&\"https:\"!==e.protocol||e.username||e.password||e.search||e.hash)return;let t=e.pathname.replace(/\\/+$/g,\"\");return t.endsWith(\"/chat/completions\")&&(t=t.slice(0,-17)),e.pathname=t||\"/\",e.toString().replace(/\\/$/,\"\")}catch{}}function qcv30url(A){try{let e=qcv30cfg();if(e&&\"string\"==typeof A){if(e.uiBaseUrl&&new URL(e.uiBaseUrl).toString()===new URL(A).toString())return qcv30base(e.upstreamBaseUrl)??A;if(Array.isArray(e.models)){let m=e.models.find(x=>x&&x.uiBaseUrl&&new URL(x.uiBaseUrl).toString()===new URL(A).toString());if(m)return qcv30base(m.upstreamBaseUrl||e.upstreamBaseUrl)??A}}}catch{}return A}function qcv30model(A){try{let e=qcv30cfg();if(e&&Array.isArray(e.models)){let p=e.replaceProviderKey||\"anthropic\",k=\"string\"==typeof A&&A.includes(\"/\")?A.split(\"/\").pop():A,m=e.models.find(e=>e&&(e.id===A||e.id===k||e.displayName===A));if(m)return " + funcName + "({key:m.id,display_name:m.displayName??m.id,provider:p,model:m.id,type:\"openai-compatible\",parameters:{api_key:\"\"},url:m.uiBaseUrl||e.uiBaseUrl,format:\"openai\",is_vl:!0===m.vision,is_reasoning:!0===m.reasoning,max_input_tokens:Number.isInteger(m.maxInputTokens)&&m.maxInputTokens>0?m.maxInputTokens:131072,efforts:Array.isArray(m.efforts)?m.efforts:[],supports_disabled:!0===m.supportsDisabled})}}catch(A){}}function qcv30target(A){let e=qcv30cfg();if(!e||!Array.isArray(e.models))return;let t=A?.custom_model,i=A?.model_config;let rawKey=t?.model||i?.key||A?.model||(\"string\"==typeof A?A:\"\")||\"\";let k=\"string\"==typeof rawKey&&rawKey.includes(\"/\")?rawKey.split(\"/\").pop():rawKey;let n=e.models.find(m=>m&&(m.id===rawKey||m.id===k||m.displayName===rawKey));if(!n)return;let r=process.env[\"QODER_CN_KEY_\"+(n.providerId||\"\")]||process.env.QODER_CN_CUSTOM_PROVIDER_API_KEY||t?.parameters?.api_key;let o=qcv30base(n.upstreamBaseUrl||e.upstreamBaseUrl);if(\"string\"!=typeof r||!r.trim())throw Error(\"QODER_CN_PATCH_API_KEY_MISSING\");if(!o)throw Error(\"QODER_CN_PATCH_UPSTREAM_URL_INVALID\");let s=Number.isInteger(e.firstPayloadTimeoutMs)&&e.firstPayloadTimeoutMs>0?e.firstPayloadTimeoutMs:6e4,a=Number.isInteger(e.streamIdleTimeoutMs)&&e.streamIdleTimeoutMs>=0?e.streamIdleTimeoutMs:0,g=Number.isInteger(n.maxInputTokens)&&n.maxInputTokens>0?n.maxInputTokens:131072,l=Number.isInteger(n.maxOutputTokens)&&n.maxOutputTokens>0?n.maxOutputTokens:32768;return{providerId:\"qoder-cn-patcher\",adapter:\"openai-compatible\",baseUrl:o,apiKey:r,model:{modelId:n.id,displayName:n.displayName??n.id,contextWindow:g,maxOutputTokens:l,capabilities:{tools:!1!==n.tools,vision:!0===n.vision,thinking:!0===n.reasoning},maxTokensField:\"max_completion_tokens\"===n.maxTokensField?\"max_completion_tokens\":\"max_tokens\"},timeouts:{firstPayloadTimeoutMs:s,...a>0?{streamIdleTimeoutMs:a}:{}}}}function " + funcName + "(A){/*" + PatchMarker + "*/\r\n";
        }

        public static string GetFileSha256(string filePath)
        {
            if (!File.Exists(filePath)) return "";
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static int CountOccurrences(string source, string substring)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(substring)) return 0;
            int count = 0, n = 0;
            while ((n = source.IndexOf(substring, n, StringComparison.Ordinal)) != -1)
            {
                n += substring.Length;
                count++;
            }
            return count;
        }

        public static string ReplaceExactlyOnce(string text, string anchor, string replacement, string description)
        {
            int count = CountOccurrences(text, anchor);
            if (count != 1)
            {
                throw new InvalidOperationException(string.Format("Anchor for {0} matched {1} times (expected 1).", description, count));
            }
            int index = text.IndexOf(anchor, StringComparison.Ordinal);
            return text.Substring(0, index) + replacement + text.Substring(index + anchor.Length);
        }

        private static string NormalizeDirectory(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool ManifestMatchesInstall(BackupManifest manifest, string installDir)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.installDir)) return false;
            return string.Equals(NormalizeDirectory(manifest.installDir), NormalizeDirectory(installDir), StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteRuntimeAtomically(string runtimePath, string text, string rollbackSource)
        {
            string temp = runtimePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                File.Replace(temp, runtimePath, null, true);
            }
            catch
            {
                if (!string.IsNullOrEmpty(rollbackSource) && File.Exists(rollbackSource))
                {
                    File.Copy(rollbackSource, runtimePath, true);
                }
                throw;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public static bool IsQoderRunning(string installDir)
        {
            Process[] procs = Process.GetProcesses();
            string fullInstall = Path.GetFullPath(installDir).TrimEnd('\\');
            foreach (Process p in procs)
            {
                try
                {
                    string name = p.ProcessName.ToLowerInvariant();
                    if (name.Contains("qoder"))
                    {
                        try
                        {
                            string path = p.MainModule.FileName;
                            if (path.StartsWith(fullInstall, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return false;
        }

        public static TargetState GetTargetState(string installDir)
        {
            string runtimePath = Path.Combine(installDir, RuntimeRelativePath);
            string asarPath = Path.Combine(installDir, AsarRelativePath);

            TargetState state = new TargetState();
            if (File.Exists(asarPath))
            {
                state.AsarSha256 = GetFileSha256(asarPath);
                state.AppAsarUnmodified = IsSupportedAsarSha(state.AsarSha256);
            }

            if (File.Exists(runtimePath))
            {
                state.RuntimeSha256 = GetFileSha256(runtimePath);
                string text = File.ReadAllText(runtimePath, Encoding.UTF8);
                state.RuntimePatched = text.IndexOf(PatchMarker, StringComparison.Ordinal) >= 0;

                bool prev = false;
                foreach (string m in PreviousPatchMarkers)
                {
                    if (!state.RuntimePatched && text.IndexOf(m, StringComparison.Ordinal) >= 0) { prev = true; break; }
                }
                state.PreviousRuntimePatched = prev;
                state.LegacyRuntimePatched = text.IndexOf(LegacyPatchMarker, StringComparison.Ordinal) >= 0;

                bool isV1135 = text.IndexOf("function TvA(A){", StringComparison.Ordinal) >= 0;
                bool isV1131 = text.IndexOf("function XxA(A){", StringComparison.Ordinal) >= 0;

                if (isV1135)
                {
                    state.DetectedVersion = "v1.1.35+ (Qoder 0.1.3+)";
                    state.ImportAnchorCount = CountOccurrences(text, ImportAnchor_v1135);
                    state.ConverterAnchorCount = CountOccurrences(text, ConverterAnchor_v1135);
                    state.ModelUrlAnchorCount = CountOccurrences(text, ModelUrlAnchor);
                    state.CatalogAnchorCount = CountOccurrences(text, CatalogAnchor_v1135);
                    state.ValidationAnchorCount = CountOccurrences(text, ValidationAnchor_v1135);
                    state.InferenceRouteAnchorCount = CountOccurrences(text, InferenceRouteAnchor_v1135);
                    state.StartupBYOKAnchorCount = CountOccurrences(text, StartupBYOKAnchor_v1135);
                    state.ModelListAnchorCount = CountOccurrences(text, ModelListAnchor_v1135);
                    state.GetModelAnchorCount = CountOccurrences(text, GetModelAnchor);
                }
                else if (isV1131)
                {
                    state.DetectedVersion = "v1.1.31 (Qoder 0.1.2)";
                    state.ImportAnchorCount = CountOccurrences(text, ImportAnchor_v1131);
                    state.ConverterAnchorCount = CountOccurrences(text, ConverterAnchor_v1131);
                    state.ModelUrlAnchorCount = CountOccurrences(text, ModelUrlAnchor);
                    state.CatalogAnchorCount = CountOccurrences(text, CatalogAnchor_v1131);
                    state.ValidationAnchorCount = CountOccurrences(text, ValidationAnchor_v1131);
                    state.InferenceRouteAnchorCount = CountOccurrences(text, InferenceRouteAnchor_v1131);
                    state.StartupBYOKAnchorCount = CountOccurrences(text, StartupBYOKAnchor_v1131);
                    state.ModelListAnchorCount = CountOccurrences(text, ModelListAnchor_v1131);
                    state.GetModelAnchorCount = CountOccurrences(text, GetModelAnchor);
                }
                else
                {
                    state.DetectedVersion = "unknown";
                    state.ImportAnchorCount = CountOccurrences(text, ImportAnchor_v1135);
                    state.ConverterAnchorCount = CountOccurrences(text, ConverterAnchor_v1135);
                    state.ModelUrlAnchorCount = CountOccurrences(text, ModelUrlAnchor);
                    state.CatalogAnchorCount = CountOccurrences(text, CatalogAnchor_v1135);
                    state.ValidationAnchorCount = CountOccurrences(text, ValidationAnchor_v1135);
                    state.InferenceRouteAnchorCount = CountOccurrences(text, InferenceRouteAnchor_v1135);
                    state.StartupBYOKAnchorCount = CountOccurrences(text, StartupBYOKAnchor_v1135);
                    state.ModelListAnchorCount = CountOccurrences(text, ModelListAnchor_v1135);
                    state.GetModelAnchorCount = CountOccurrences(text, GetModelAnchor);
                }
            }
            return state;
        }

        public static string PatchRuntimeText(string text)
        {
            if (text.IndexOf(PatchMarker, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("The v3.2.0 runtime patch is already installed.");
            }
            foreach (string m in PreviousPatchMarkers)
            {
                if (text.IndexOf(m, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException("An older runtime patch is present. Upgrade must start from its verified original backup.");
                }
            }
            if (text.IndexOf(LegacyPatchMarker, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("The legacy v1 runtime patch is present. Restore v1 before applying v3.2.0.");
            }

            bool isV1135 = text.IndexOf("function TvA(A){", StringComparison.Ordinal) >= 0;
            bool isV1131 = text.IndexOf("function XxA(A){", StringComparison.Ordinal) >= 0;

            if (!isV1135 && !isV1131)
            {
                throw new InvalidOperationException("Target file does not match known Qoder runtime versions (1.1.35 or 1.1.31).");
            }

            if (isV1135)
            {
                text = ReplaceExactlyOnce(text, ImportAnchor_v1135, ImportReplacement_v1135, "node:fs import");
                text = ReplaceExactlyOnce(text, ConverterAnchor_v1135, BuildConverterReplacement(true), "BYOK model converter");
                text = ReplaceExactlyOnce(text, ModelUrlAnchor, ModelUrlReplacement, "BYOK inference URL");
                text = ReplaceExactlyOnce(text, CatalogAnchor_v1135, CatalogReplacement_v1135, "BYOK catalog");
                text = ReplaceExactlyOnce(text, ValidationAnchor_v1135, ValidationReplacement_v1135, "BYOK validation");
                text = ReplaceExactlyOnce(text, InferenceRouteAnchor_v1135, InferenceRouteReplacement_v1135, "direct inference route");
                text = ReplaceExactlyOnce(text, StartupBYOKAnchor_v1135, StartupBYOKReplacement_v1135, "startup BYOK injection");
                text = ReplaceExactlyOnce(text, ModelListAnchor_v1135, ModelListReplacement_v1135, "model list injection");
                text = ReplaceExactlyOnce(text, GetModelAnchor, GetModelReplacement, "catalog getModel resolution");
            }
            else
            {
                text = ReplaceExactlyOnce(text, ImportAnchor_v1131, ImportReplacement_v1131, "node:fs import");
                text = ReplaceExactlyOnce(text, ConverterAnchor_v1131, BuildConverterReplacement(false), "BYOK model converter");
                text = ReplaceExactlyOnce(text, ModelUrlAnchor, ModelUrlReplacement, "BYOK inference URL");
                text = ReplaceExactlyOnce(text, CatalogAnchor_v1131, CatalogReplacement_v1131, "BYOK catalog");
                text = ReplaceExactlyOnce(text, ValidationAnchor_v1131, ValidationReplacement_v1131, "BYOK validation");
                text = ReplaceExactlyOnce(text, InferenceRouteAnchor_v1131, InferenceRouteReplacement_v1131, "direct inference route");
                text = ReplaceExactlyOnce(text, StartupBYOKAnchor_v1131, StartupBYOKReplacement_v1131, "startup BYOK injection");
                text = ReplaceExactlyOnce(text, ModelListAnchor_v1131, ModelListReplacement_v1131, "model list injection");
                text = ReplaceExactlyOnce(text, GetModelAnchor, GetModelReplacement, "catalog getModel resolution");
            }
            return text;
        }

        public static BackupManifest NewBackup(string installDir, string backupRoot)
        {
            string runtimePath = Path.Combine(installDir, RuntimeRelativePath);
            string asarPath = Path.Combine(installDir, AsarRelativePath);

            string id = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string dir = Path.Combine(backupRoot, id);
            Directory.CreateDirectory(dir);

            string runtimeBackup = Path.Combine(dir, "qoder-worker-runtime.obf.mjs");
            File.Copy(runtimePath, runtimeBackup, true);

            BackupManifest manifest = new BackupManifest
            {
                backupId = id,
                createdAt = DateTime.UtcNow.ToString("o"),
                installDir = Path.GetFullPath(installDir),
                runtimePath = Path.GetFullPath(runtimePath),
                runtimeBackup = runtimeBackup,
                runtimeSha256 = GetFileSha256(runtimePath),
                appAsarPath = Path.GetFullPath(asarPath),
                appAsarSha256 = GetFileSha256(asarPath),
                patchVersion = "3.2.0"
            };

            manifest.Save(Path.Combine(dir, "manifest.json"));
            return manifest;
        }

        public static BackupManifest GetOriginalRuntimeBackup(string backupRoot, string installDir)
        {
            if (!Directory.Exists(backupRoot))
            {
                throw new DirectoryNotFoundException("No runtime backups found in: " + backupRoot);
            }

            string[] manifestFiles = Directory.GetFiles(backupRoot, "manifest.json", SearchOption.AllDirectories);
            Array.Sort(manifestFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

            foreach (string mf in manifestFiles)
            {
                try
                {
                    BackupManifest m = BackupManifest.Load(mf);
                    if (ManifestMatchesInstall(m, installDir) && File.Exists(m.runtimeBackup) &&
                        IsSupportedRuntimeSha(GetFileSha256(m.runtimeBackup)))
                    {
                        return m;
                    }
                }
                catch { }
            }
            throw new InvalidOperationException("No verified original runtime backup is available for upgrade.");
        }

        public static BackupManifest GetLatestBackup(string backupRoot, string installDir, string specificBackupId = null)
        {
            if (!Directory.Exists(backupRoot))
            {
                throw new DirectoryNotFoundException("No backups found in: " + backupRoot);
            }

            if (!string.IsNullOrEmpty(specificBackupId))
            {
                if (!string.Equals(Path.GetFileName(specificBackupId), specificBackupId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid backup identifier.");
                string path = Path.Combine(Path.Combine(backupRoot, specificBackupId), "manifest.json");
                if (!File.Exists(path)) throw new FileNotFoundException("Backup not found: " + specificBackupId);
                BackupManifest specific = BackupManifest.Load(path);
                if (!ManifestMatchesInstall(specific, installDir))
                    throw new InvalidOperationException("The selected backup belongs to a different Qoder CN installation.");
                return specific;
            }

            string[] manifestFiles = Directory.GetFiles(backupRoot, "manifest.json", SearchOption.AllDirectories);
            if (manifestFiles.Length == 0) throw new FileNotFoundException("No backup manifests found in: " + backupRoot);
            Array.Sort(manifestFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
            foreach (string manifestFile in manifestFiles)
            {
                try
                {
                    BackupManifest manifest = BackupManifest.Load(manifestFile);
                    if (ManifestMatchesInstall(manifest, installDir)) return manifest;
                }
                catch { }
            }
            throw new FileNotFoundException("No backup belongs to this Qoder CN installation: " + NormalizeDirectory(installDir));
        }

        public static void ApplyPatch(string installDir, string backupRoot)
        {
            if (IsQoderRunning(installDir))
            {
                throw new InvalidOperationException("Qoder CN is currently running. Please close it before patching.");
            }

            string runtimePath = Path.Combine(installDir, RuntimeRelativePath);
            string asarPath = Path.Combine(installDir, AsarRelativePath);
            TargetState state = GetTargetState(installDir);

            if (state.RuntimePatched || state.PreviousRuntimePatched)
            {
                if (!state.AppAsarUnmodified)
                {
                    throw new InvalidOperationException("app.asar no longer matches the tested build; refusing to upgrade.");
                }
                BackupManifest origManifest = GetOriginalRuntimeBackup(backupRoot, installDir);
                string upgradeOriginalText = File.ReadAllText(origManifest.runtimeBackup, Encoding.UTF8);
                string upgradedText = PatchRuntimeText(upgradeOriginalText);
                WriteRuntimeAtomically(runtimePath, upgradedText, origManifest.runtimeBackup);
                return;
            }

            if (state.LegacyRuntimePatched)
            {
                throw new InvalidOperationException("The legacy v1 runtime patch is present. Restore it before applying v3.2.0.");
            }

            if (!IsSupportedRuntimeSha(state.RuntimeSha256) || !state.AppAsarUnmodified)
            {
                throw new InvalidOperationException("This Qoder CN build does not match any tested baseline.");
            }

            BackupManifest backup = NewBackup(installDir, backupRoot);
            string originalText = File.ReadAllText(runtimePath, Encoding.UTF8);
            string patchedText = PatchRuntimeText(originalText);
            WriteRuntimeAtomically(runtimePath, patchedText, backup.runtimeBackup);
        }

        public static void RestorePatch(string installDir, string backupRoot, string specificBackupId = null)
        {
            if (IsQoderRunning(installDir))
            {
                throw new InvalidOperationException("Qoder CN is currently running. Please close it before restoring.");
            }

            string runtimePath = Path.Combine(installDir, RuntimeRelativePath);
            BackupManifest manifest = GetLatestBackup(backupRoot, installDir, specificBackupId);

            if (!File.Exists(manifest.runtimeBackup))
            {
                throw new FileNotFoundException("Runtime backup file is missing: " + manifest.runtimeBackup);
            }

            string backupHash = GetFileSha256(manifest.runtimeBackup);
            if (!string.Equals(backupHash, manifest.runtimeSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Runtime backup hash verification failed.");
            }
            if (string.IsNullOrWhiteSpace(manifest.runtimePath) ||
                !string.Equals(Path.GetFullPath(manifest.runtimePath), Path.GetFullPath(runtimePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backup manifest runtime path does not match the selected installation.");
            }

            string restoreText = File.ReadAllText(manifest.runtimeBackup, Encoding.UTF8);
            WriteRuntimeAtomically(runtimePath, restoreText, null);
        }
    }
}
