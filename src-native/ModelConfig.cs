using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace QoderCN.GatewayManager
{
    public class ModelItem
    {
        public string id { get; set; }
        public string displayName { get; set; }
        public bool vision { get; set; }
        public bool reasoning { get; set; }
        public bool tools { get; set; }
        public int maxInputTokens { get; set; }
        public int maxOutputTokens { get; set; }
        public string maxTokensField { get; set; }
        public string[] efforts { get; set; }
        public bool? supportsDisabled { get; set; }
        public string providerId { get; set; }
        public string providerName { get; set; }
        public string upstreamBaseUrl { get; set; }
        public string uiBaseUrl { get; set; }
        public bool selectedForInjection { get; set; }

        public ModelItem()
        {
            id = "";
            displayName = "";
            vision = false;
            reasoning = true;
            tools = true;
            maxInputTokens = 131072;
            maxOutputTokens = 32768;
            maxTokensField = "max_tokens";
            efforts = new string[0];
            supportsDisabled = null;
            providerId = "";
            providerName = "";
            upstreamBaseUrl = "";
            uiBaseUrl = "";
            selectedForInjection = true;
        }

        public override string ToString()
        {
            string disp = string.IsNullOrWhiteSpace(displayName) || displayName == id ? id : string.Format("{0} ({1})", id, displayName);
            List<string> tags = new List<string>();
            if (!string.IsNullOrWhiteSpace(providerName)) tags.Add(providerName);
            if (reasoning) tags.Add("Thinking");
            if (tools) tags.Add("Tools");
            if (vision) tags.Add("Vision");
            if (maxTokensField == "max_completion_tokens") tags.Add("o-series");

            if (tags.Count > 0)
            {
                return string.Format("{0}  [{1}]", disp, string.Join(", ", tags.ToArray()));
            }
            return disp;
        }
    }

    public class ProviderItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string baseUrl { get; set; }
        public string uiBaseUrl { get; set; }
        public string replaceProviderKey { get; set; }
        public string replaceProviderDisplayName { get; set; }
        public int replaceProviderIndex { get; set; }
        public int firstPayloadTimeoutMs { get; set; }
        public int streamIdleTimeoutMs { get; set; }
        public bool enabled { get; set; }
        public List<ModelItem> models { get; set; }

        public ProviderItem()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            name = "New Provider";
            baseUrl = "http://127.0.0.1:11434/v1";
            uiBaseUrl = "";
            replaceProviderKey = "anthropic";
            replaceProviderDisplayName = "Anthropic (Claude)";
            replaceProviderIndex = 0;
            firstPayloadTimeoutMs = 180000;
            streamIdleTimeoutMs = 300000;
            enabled = true;
            models = new List<ModelItem>();
        }

        public string GetEffectiveUiUrl()
        {
            if (!string.IsNullOrWhiteSpace(uiBaseUrl)) return uiBaseUrl.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl)) return "http://127.0.0.1:11434/v1";
            string b = baseUrl.Trim();
            if (b.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + b.Substring(7);
            }
            return b;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1} models) - {2}", name, models != null ? models.Count : 0, baseUrl);
        }
    }

    public class GatewayWorkspace
    {
        public List<ProviderItem> providers { get; set; }
        public List<string> selectedModelKeys { get; set; }

        public GatewayWorkspace()
        {
            providers = new List<ProviderItem>();
            selectedModelKeys = new List<string>();
        }

        public static GatewayWorkspace LoadFromFile(string path)
        {
            GatewayWorkspace ws = new GatewayWorkspace();
            if (!File.Exists(path)) return ws;

            string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF').Trim();
            if (string.IsNullOrEmpty(text)) return ws;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(text);
            if (dict == null) return ws;

            if (dict.ContainsKey("providers") && dict["providers"] is ArrayList)
            {
                ArrayList pList = (ArrayList)dict["providers"];
                foreach (object pObj in pList)
                {
                    if (pObj is Dictionary<string, object>)
                    {
                        Dictionary<string, object> pDict = (Dictionary<string, object>)pObj;
                        ProviderItem p = new ProviderItem();
                        if (pDict.ContainsKey("id") && pDict["id"] != null) p.id = pDict["id"].ToString();
                        if (pDict.ContainsKey("name") && pDict["name"] != null) p.name = pDict["name"].ToString();
                        if (pDict.ContainsKey("baseUrl") && pDict["baseUrl"] != null) p.baseUrl = pDict["baseUrl"].ToString();
                        if (pDict.ContainsKey("uiBaseUrl") && pDict["uiBaseUrl"] != null) p.uiBaseUrl = pDict["uiBaseUrl"].ToString();
                        if (pDict.ContainsKey("replaceProviderKey") && pDict["replaceProviderKey"] != null) p.replaceProviderKey = pDict["replaceProviderKey"].ToString();
                        if (pDict.ContainsKey("replaceProviderDisplayName") && pDict["replaceProviderDisplayName"] != null) p.replaceProviderDisplayName = pDict["replaceProviderDisplayName"].ToString();
                        if (pDict.ContainsKey("replaceProviderIndex") && pDict["replaceProviderIndex"] != null) p.replaceProviderIndex = Convert.ToInt32(pDict["replaceProviderIndex"]);
                        if (pDict.ContainsKey("firstPayloadTimeoutMs") && pDict["firstPayloadTimeoutMs"] != null) p.firstPayloadTimeoutMs = Convert.ToInt32(pDict["firstPayloadTimeoutMs"]);
                        if (pDict.ContainsKey("streamIdleTimeoutMs") && pDict["streamIdleTimeoutMs"] != null) p.streamIdleTimeoutMs = Convert.ToInt32(pDict["streamIdleTimeoutMs"]);
                        if (pDict.ContainsKey("enabled") && pDict["enabled"] != null) p.enabled = Convert.ToBoolean(pDict["enabled"]);

                        if (pDict.ContainsKey("models") && pDict["models"] is ArrayList)
                        {
                            ArrayList mList = (ArrayList)pDict["models"];
                            foreach (object mObj in mList)
                            {
                                if (mObj is Dictionary<string, object>)
                                {
                                    Dictionary<string, object> mDict = (Dictionary<string, object>)mObj;
                                    ModelItem m = new ModelItem();
                                    m.providerId = p.id;
                                    m.providerName = p.name;
                                    m.upstreamBaseUrl = p.baseUrl;
                                    m.uiBaseUrl = p.GetEffectiveUiUrl();
                                    if (mDict.ContainsKey("id") && mDict["id"] != null) m.id = mDict["id"].ToString();
                                    if (mDict.ContainsKey("displayName") && mDict["displayName"] != null) m.displayName = mDict["displayName"].ToString();
                                    if (mDict.ContainsKey("vision") && mDict["vision"] != null) m.vision = Convert.ToBoolean(mDict["vision"]);
                                    if (mDict.ContainsKey("reasoning") && mDict["reasoning"] != null) m.reasoning = Convert.ToBoolean(mDict["reasoning"]);
                                    if (mDict.ContainsKey("tools") && mDict["tools"] != null) m.tools = Convert.ToBoolean(mDict["tools"]);
                                    if (mDict.ContainsKey("maxInputTokens") && mDict["maxInputTokens"] != null) m.maxInputTokens = Convert.ToInt32(mDict["maxInputTokens"]);
                                    if (mDict.ContainsKey("maxOutputTokens") && mDict["maxOutputTokens"] != null) m.maxOutputTokens = Convert.ToInt32(mDict["maxOutputTokens"]);
                                    if (mDict.ContainsKey("maxTokensField") && mDict["maxTokensField"] != null) m.maxTokensField = mDict["maxTokensField"].ToString();
                                    if (mDict.ContainsKey("selectedForInjection") && mDict["selectedForInjection"] != null) m.selectedForInjection = Convert.ToBoolean(mDict["selectedForInjection"]);
                                    if (mDict.ContainsKey("efforts") && mDict["efforts"] is ArrayList)
                                    {
                                        ArrayList effortList = (ArrayList)mDict["efforts"];
                                        List<string> efforts = new List<string>();
                                        foreach (object effort in effortList)
                                        {
                                            if (effort != null && !string.IsNullOrWhiteSpace(effort.ToString())) efforts.Add(effort.ToString());
                                        }
                                        m.efforts = efforts.ToArray();
                                    }
                                    if (mDict.ContainsKey("supportsDisabled") && mDict["supportsDisabled"] != null)
                                    {
                                        m.supportsDisabled = Convert.ToBoolean(mDict["supportsDisabled"]);
                                    }
                                    p.models.Add(m);
                                }
                            }
                        }
                        ws.providers.Add(p);
                    }
                }
            }

            if (dict.ContainsKey("selectedModelKeys") && dict["selectedModelKeys"] is ArrayList)
            {
                ArrayList sList = (ArrayList)dict["selectedModelKeys"];
                foreach (object s in sList)
                {
                    if (s != null && !string.IsNullOrWhiteSpace(s.ToString()))
                    {
                        ws.selectedModelKeys.Add(s.ToString());
                    }
                }
            }

            return ws;
        }

        public void SaveToFile(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Dictionary<string, object> dict = new Dictionary<string, object>();
            List<Dictionary<string, object>> pList = new List<Dictionary<string, object>>();
            foreach (ProviderItem p in providers)
            {
                Dictionary<string, object> pDict = new Dictionary<string, object>();
                pDict["id"] = p.id;
                pDict["name"] = p.name;
                pDict["baseUrl"] = p.baseUrl;
                pDict["uiBaseUrl"] = p.uiBaseUrl;
                pDict["replaceProviderKey"] = p.replaceProviderKey;
                pDict["replaceProviderDisplayName"] = p.replaceProviderDisplayName;
                pDict["replaceProviderIndex"] = p.replaceProviderIndex;
                pDict["firstPayloadTimeoutMs"] = p.firstPayloadTimeoutMs;
                pDict["streamIdleTimeoutMs"] = p.streamIdleTimeoutMs;
                pDict["enabled"] = p.enabled;

                List<Dictionary<string, object>> mList = new List<Dictionary<string, object>>();
                foreach (ModelItem m in p.models)
                {
                    Dictionary<string, object> mDict = new Dictionary<string, object>();
                    mDict["id"] = m.id;
                    mDict["displayName"] = string.IsNullOrEmpty(m.displayName) ? m.id : m.displayName;
                    mDict["vision"] = m.vision;
                    mDict["reasoning"] = m.reasoning;
                    mDict["tools"] = m.tools;
                    mDict["maxInputTokens"] = m.maxInputTokens;
                    mDict["maxOutputTokens"] = m.maxOutputTokens;
                    mDict["maxTokensField"] = m.maxTokensField;
                    mDict["selectedForInjection"] = m.selectedForInjection;
                    mDict["efforts"] = m.efforts ?? new string[0];
                    mDict["supportsDisabled"] = m.supportsDisabled;
                    mList.Add(mDict);
                }
                pDict["models"] = mList;
                pList.Add(pDict);
            }
            dict["providers"] = pList;
            dict["selectedModelKeys"] = selectedModelKeys ?? new List<string>();

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(dict);
            string formatted = FormatJson(json);
            File.WriteAllText(path, formatted, new UTF8Encoding(false));
        }

        public List<ModelItem> GetAllModels()
        {
            List<ModelItem> all = new List<ModelItem>();
            foreach (ProviderItem p in providers)
            {
                if (p.models == null) continue;
                foreach (ModelItem m in p.models)
                {
                    m.providerId = p.id;
                    m.providerName = p.name;
                    m.upstreamBaseUrl = p.baseUrl;
                    m.uiBaseUrl = p.GetEffectiveUiUrl();
                    all.Add(m);
                }
            }
            return all;
        }

        public ProviderConfig CompileToRuntimeConfig(HashSet<string> activeModelIds)
        {
            ProviderConfig cfg = new ProviderConfig();
            List<ModelItem> injected = new List<ModelItem>();

            ProviderItem primaryProvider = null;
            foreach (ProviderItem p in providers)
            {
                if (!p.enabled) continue;
                if (primaryProvider == null) primaryProvider = p;

                if (p.models == null) continue;
                foreach (ModelItem m in p.models)
                {
                    string uniqueKey = string.Format("{0}::{1}", p.id, m.id);
                    bool isSelected = activeModelIds.Contains(m.id) || activeModelIds.Contains(uniqueKey) || m.selectedForInjection;
                    if (isSelected)
                    {
                        ModelItem copy = new ModelItem();
                        copy.id = m.id;
                        copy.displayName = m.displayName;
                        copy.vision = m.vision;
                        copy.reasoning = m.reasoning;
                        copy.tools = m.tools;
                        copy.maxInputTokens = m.maxInputTokens;
                        copy.maxOutputTokens = m.maxOutputTokens;
                        copy.maxTokensField = m.maxTokensField;
                        copy.efforts = m.efforts;
                        copy.supportsDisabled = m.supportsDisabled;
                        copy.providerId = p.id;
                        copy.providerName = p.name;
                        copy.upstreamBaseUrl = p.baseUrl;
                        copy.uiBaseUrl = p.GetEffectiveUiUrl();
                        copy.selectedForInjection = true;
                        injected.Add(copy);
                    }
                }
            }

            if (primaryProvider != null)
            {
                cfg.displayName = primaryProvider.name;
                cfg.uiBaseUrl = primaryProvider.GetEffectiveUiUrl();
                cfg.upstreamBaseUrl = primaryProvider.baseUrl;
                cfg.replaceProviderKey = primaryProvider.replaceProviderKey;
                cfg.replaceProviderDisplayName = primaryProvider.replaceProviderDisplayName;
                cfg.replaceProviderIndex = primaryProvider.replaceProviderIndex;
                cfg.firstPayloadTimeoutMs = primaryProvider.firstPayloadTimeoutMs;
                cfg.streamIdleTimeoutMs = primaryProvider.streamIdleTimeoutMs;
            }

            cfg.models = injected;
            return cfg;
        }

        public static GatewayWorkspace FromSingleProfile(ProviderConfig single, string providerName)
        {
            GatewayWorkspace ws = new GatewayWorkspace();
            ProviderItem p = new ProviderItem();
            p.id = "p-default";
            p.name = !string.IsNullOrWhiteSpace(providerName) ? providerName : single.displayName;
            p.baseUrl = single.upstreamBaseUrl;
            p.uiBaseUrl = single.uiBaseUrl;
            p.replaceProviderKey = single.replaceProviderKey;
            p.replaceProviderDisplayName = single.replaceProviderDisplayName;
            p.replaceProviderIndex = single.replaceProviderIndex;
            p.firstPayloadTimeoutMs = single.firstPayloadTimeoutMs;
            p.streamIdleTimeoutMs = single.streamIdleTimeoutMs;
            p.enabled = true;

            foreach (ModelItem m in single.models)
            {
                m.providerId = p.id;
                m.providerName = p.name;
                m.upstreamBaseUrl = p.baseUrl;
                m.uiBaseUrl = p.GetEffectiveUiUrl();
                m.selectedForInjection = true;
                p.models.Add(m);
                ws.selectedModelKeys.Add(m.id);
            }

            ws.providers.Add(p);
            return ws;
        }

        private static string FormatJson(string json)
        {
            StringBuilder sb = new StringBuilder();
            int indent = 0;
            bool quoted = false;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '\"')
                {
                    bool isEscaped = false;
                    for (int j = i - 1; j >= 0 && json[j] == '\\'; j--) isEscaped = !isEscaped;
                    if (!isEscaped) quoted = !quoted;
                }

                if (quoted)
                {
                    sb.Append(ch);
                    continue;
                }

                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        sb.AppendLine();
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        sb.AppendLine();
                        indent--;
                        sb.Append(new string(' ', Math.Max(0, indent * 2)));
                        sb.Append(ch);
                        break;
                    case ',':
                    case ':':
                        if (ch == ',')
                        {
                            sb.Append(ch);
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                        }
                        else
                        {
                            sb.Append(": ");
                        }
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    public class ProviderConfig
    {
        public string displayName { get; set; }
        public string uiBaseUrl { get; set; }
        public string upstreamBaseUrl { get; set; }
        public string replaceProviderKey { get; set; }
        public string replaceProviderDisplayName { get; set; }
        public int replaceProviderIndex { get; set; }
        public bool skipValidation { get; set; }
        public int firstPayloadTimeoutMs { get; set; }
        public int streamIdleTimeoutMs { get; set; }
        public string apiKey { get; set; }
        public List<ModelItem> models { get; set; }

        public ProviderConfig()
        {
            displayName = "CPA @ 192.168.50.241";
            uiBaseUrl = "https://192.168.50.241:8317/v1";
            upstreamBaseUrl = "http://192.168.50.241:8317/v1";
            replaceProviderKey = "anthropic";
            replaceProviderDisplayName = "Anthropic (Claude)";
            replaceProviderIndex = 0;
            skipValidation = true;
            firstPayloadTimeoutMs = 180000;
            streamIdleTimeoutMs = 300000;
            apiKey = "";
            models = new List<ModelItem>();
        }

        public static ProviderConfig LoadFromFile(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF').Trim();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(text);

            ProviderConfig config = new ProviderConfig();
            if (dict.ContainsKey("displayName") && dict["displayName"] != null) config.displayName = dict["displayName"].ToString();
            if (dict.ContainsKey("uiBaseUrl") && dict["uiBaseUrl"] != null) config.uiBaseUrl = dict["uiBaseUrl"].ToString();
            if (dict.ContainsKey("upstreamBaseUrl") && dict["upstreamBaseUrl"] != null) config.upstreamBaseUrl = dict["upstreamBaseUrl"].ToString();
            if (dict.ContainsKey("replaceProviderKey") && dict["replaceProviderKey"] != null) config.replaceProviderKey = dict["replaceProviderKey"].ToString();
            if (dict.ContainsKey("replaceProviderDisplayName") && dict["replaceProviderDisplayName"] != null) config.replaceProviderDisplayName = dict["replaceProviderDisplayName"].ToString();
            if (dict.ContainsKey("replaceProviderIndex") && dict["replaceProviderIndex"] != null) config.replaceProviderIndex = Convert.ToInt32(dict["replaceProviderIndex"]);
            if (dict.ContainsKey("skipValidation") && dict["skipValidation"] != null) config.skipValidation = Convert.ToBoolean(dict["skipValidation"]);
            if (dict.ContainsKey("firstPayloadTimeoutMs") && dict["firstPayloadTimeoutMs"] != null) config.firstPayloadTimeoutMs = Convert.ToInt32(dict["firstPayloadTimeoutMs"]);
            if (dict.ContainsKey("streamIdleTimeoutMs") && dict["streamIdleTimeoutMs"] != null) config.streamIdleTimeoutMs = Convert.ToInt32(dict["streamIdleTimeoutMs"]);
            if (dict.ContainsKey("apiKey") && dict["apiKey"] != null) config.apiKey = dict["apiKey"].ToString();

            if (dict.ContainsKey("models") && dict["models"] is ArrayList)
            {
                ArrayList mList = (ArrayList)dict["models"];
                foreach (object item in mList)
                {
                    if (item is Dictionary<string, object>)
                    {
                        Dictionary<string, object> mDict = (Dictionary<string, object>)item;
                        ModelItem m = new ModelItem();
                        if (mDict.ContainsKey("id") && mDict["id"] != null) m.id = mDict["id"].ToString();
                        if (mDict.ContainsKey("displayName") && mDict["displayName"] != null) m.displayName = mDict["displayName"].ToString();
                        if (mDict.ContainsKey("vision") && mDict["vision"] != null) m.vision = Convert.ToBoolean(mDict["vision"]);
                        if (mDict.ContainsKey("reasoning") && mDict["reasoning"] != null) m.reasoning = Convert.ToBoolean(mDict["reasoning"]);
                        if (mDict.ContainsKey("tools") && mDict["tools"] != null) m.tools = Convert.ToBoolean(mDict["tools"]);
                        if (mDict.ContainsKey("maxInputTokens") && mDict["maxInputTokens"] != null) m.maxInputTokens = Convert.ToInt32(mDict["maxInputTokens"]);
                        if (mDict.ContainsKey("maxOutputTokens") && mDict["maxOutputTokens"] != null) m.maxOutputTokens = Convert.ToInt32(mDict["maxOutputTokens"]);
                        if (mDict.ContainsKey("maxTokensField") && mDict["maxTokensField"] != null) m.maxTokensField = mDict["maxTokensField"].ToString();
                        if (mDict.ContainsKey("upstreamBaseUrl") && mDict["upstreamBaseUrl"] != null) m.upstreamBaseUrl = mDict["upstreamBaseUrl"].ToString();
                        if (mDict.ContainsKey("uiBaseUrl") && mDict["uiBaseUrl"] != null) m.uiBaseUrl = mDict["uiBaseUrl"].ToString();
                        if (mDict.ContainsKey("providerId") && mDict["providerId"] != null) m.providerId = mDict["providerId"].ToString();
                        if (mDict.ContainsKey("providerName") && mDict["providerName"] != null) m.providerName = mDict["providerName"].ToString();
                        if (mDict.ContainsKey("selectedForInjection") && mDict["selectedForInjection"] != null) m.selectedForInjection = Convert.ToBoolean(mDict["selectedForInjection"]);
                        if (mDict.ContainsKey("efforts") && mDict["efforts"] is ArrayList)
                        {
                            ArrayList effortList = (ArrayList)mDict["efforts"];
                            List<string> efforts = new List<string>();
                            foreach (object effort in effortList)
                            {
                                if (effort != null && !string.IsNullOrWhiteSpace(effort.ToString())) efforts.Add(effort.ToString());
                            }
                            m.efforts = efforts.ToArray();
                        }
                        if (mDict.ContainsKey("supportsDisabled") && mDict["supportsDisabled"] != null)
                        {
                            m.supportsDisabled = Convert.ToBoolean(mDict["supportsDisabled"]);
                        }
                        config.models.Add(m);
                    }
                }
            }
            return config;
        }

        public void SaveToFile(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict["displayName"] = displayName;
            dict["uiBaseUrl"] = uiBaseUrl;
            dict["upstreamBaseUrl"] = upstreamBaseUrl;
            dict["replaceProviderKey"] = replaceProviderKey;
            dict["replaceProviderDisplayName"] = replaceProviderDisplayName;
            dict["replaceProviderIndex"] = replaceProviderIndex;
            dict["skipValidation"] = skipValidation;
            dict["firstPayloadTimeoutMs"] = firstPayloadTimeoutMs;
            dict["streamIdleTimeoutMs"] = streamIdleTimeoutMs;
            List<Dictionary<string, object>> mList = new List<Dictionary<string, object>>();
            foreach (ModelItem m in models)
            {
                Dictionary<string, object> mDict = new Dictionary<string, object>();
                mDict["id"] = m.id;
                mDict["displayName"] = string.IsNullOrEmpty(m.displayName) ? m.id : m.displayName;
                mDict["vision"] = m.vision;
                mDict["reasoning"] = m.reasoning;
                mDict["tools"] = m.tools;
                mDict["maxInputTokens"] = m.maxInputTokens;
                mDict["maxOutputTokens"] = m.maxOutputTokens;
                mDict["maxTokensField"] = m.maxTokensField;
                if (!string.IsNullOrWhiteSpace(m.upstreamBaseUrl)) mDict["upstreamBaseUrl"] = m.upstreamBaseUrl;
                if (!string.IsNullOrWhiteSpace(m.uiBaseUrl)) mDict["uiBaseUrl"] = m.uiBaseUrl;
                if (!string.IsNullOrWhiteSpace(m.providerId)) mDict["providerId"] = m.providerId;
                if (!string.IsNullOrWhiteSpace(m.providerName)) mDict["providerName"] = m.providerName;
                mDict["efforts"] = m.efforts ?? new string[0];
                mDict["supportsDisabled"] = m.supportsDisabled;
                mList.Add(mDict);
            }
            dict["models"] = mList;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(dict);
            string formatted = FormatJson(json);
            File.WriteAllText(path, formatted, new UTF8Encoding(false));
        }

        private static string FormatJson(string json)
        {
            StringBuilder sb = new StringBuilder();
            int indent = 0;
            bool quoted = false;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (ch == '\"')
                {
                    bool isEscaped = false;
                    for (int j = i - 1; j >= 0 && json[j] == '\\'; j--) isEscaped = !isEscaped;
                    if (!isEscaped) quoted = !quoted;
                }

                if (quoted)
                {
                    sb.Append(ch);
                    continue;
                }

                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        sb.AppendLine();
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        sb.AppendLine();
                        indent--;
                        sb.Append(new string(' ', Math.Max(0, indent * 2)));
                        sb.Append(ch);
                        break;
                    case ',':
                    case ':':
                        if (ch == ',')
                        {
                            sb.Append(ch);
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                        }
                        else
                        {
                            sb.Append(": ");
                        }
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    public class BackupManifest
    {
        public string backupId { get; set; }
        public string createdAt { get; set; }
        public string installDir { get; set; }
        public string runtimePath { get; set; }
        public string runtimeBackup { get; set; }
        public string runtimeSha256 { get; set; }
        public string appAsarPath { get; set; }
        public string appAsarSha256 { get; set; }
        public string patchVersion { get; set; }

        public static BackupManifest Load(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Deserialize<BackupManifest>(text);
        }

        public void Save(string path)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(this);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
    }

    public class TargetState
    {
        public bool RuntimePatched { get; set; }
        public bool PreviousRuntimePatched { get; set; }
        public bool LegacyRuntimePatched { get; set; }
        public bool AppAsarUnmodified { get; set; }
        public string RuntimeSha256 { get; set; }
        public string AsarSha256 { get; set; }
        public string DetectedVersion { get; set; }
        public int ImportAnchorCount { get; set; }
        public int ConverterAnchorCount { get; set; }
        public int ModelUrlAnchorCount { get; set; }
        public int CatalogAnchorCount { get; set; }
        public int ValidationAnchorCount { get; set; }
        public int InferenceRouteAnchorCount { get; set; }
        public int StartupBYOKAnchorCount { get; set; }
        public int ModelListAnchorCount { get; set; }
        public int GetModelAnchorCount { get; set; }
    }
}
