using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace QoderCN.GatewayManager
{
    public class TestResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public long ElapsedMs { get; set; }
        public string Message { get; set; }
        public List<ModelItem> DiscoveredModels { get; set; }

        public TestResult()
        {
            DiscoveredModels = new List<ModelItem>();
        }
    }

    public static class UpstreamTester
    {
        public static TestResult TestConnection(string baseUrl, string apiKey)
        {
            return FetchModels(baseUrl, apiKey, false);
        }

        public static TestResult FetchModels(string baseUrl, string apiKey, bool fetchFullList = true)
        {
            TestResult result = new TestResult();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                result.Success = false;
                result.Message = "Base URL is empty.";
                return result;
            }

            string url = baseUrl.TrimEnd('/') + "/models";
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers["Authorization"] = "Bearer " + apiKey.Trim();
                }
                request.UserAgent = "QoderCN-GatewayManager/3.2.0";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    sw.Stop();
                    result.Success = ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300);
                    result.StatusCode = (int)response.StatusCode;
                    result.ElapsedMs = sw.ElapsedMilliseconds;

                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string body = reader.ReadToEnd();
                        if (fetchFullList && !string.IsNullOrWhiteSpace(body))
                        {
                            result.DiscoveredModels = ParseModelsResponse(body);
                        }
                    }

                    if (result.DiscoveredModels.Count > 0)
                    {
                        result.Message = string.Format("HTTP {0} OK - Discovered {1} models ({2}ms)", (int)response.StatusCode, result.DiscoveredModels.Count, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        result.Message = string.Format("HTTP {0} {1} ({2}ms)", (int)response.StatusCode, response.StatusDescription, sw.ElapsedMilliseconds);
                    }
                }
            }
            catch (WebException wex)
            {
                sw.Stop();
                result.ElapsedMs = sw.ElapsedMilliseconds;
                HttpWebResponse errResp = wex.Response as HttpWebResponse;
                if (errResp != null)
                {
                    result.StatusCode = (int)errResp.StatusCode;
                    result.Success = false;
                    result.Message = string.Format("HTTP {0} {1} ({2}ms)", (int)errResp.StatusCode, errResp.StatusDescription, sw.ElapsedMilliseconds);
                }
                else
                {
                    result.Success = false;
                    result.Message = wex.Message;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.ElapsedMs = sw.ElapsedMilliseconds;
                result.Success = false;
                result.Message = ex.Message;
            }
            return result;
        }

        public static List<ModelItem> ParseModelsResponse(string responseJson)
        {
            List<ModelItem> list = new List<ModelItem>();
            if (string.IsNullOrWhiteSpace(responseJson)) return list;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                object obj = serializer.DeserializeObject(responseJson.TrimStart('\uFEFF').Trim());

                if (obj is Dictionary<string, object>)
                {
                    Dictionary<string, object> dict = (Dictionary<string, object>)obj;

                    // Standard OpenAI format: { "data": [ { "id": "..." } ] }
                    if (dict.ContainsKey("data") && dict["data"] is ArrayList)
                    {
                        ArrayList dataList = (ArrayList)dict["data"];
                        foreach (object item in dataList)
                        {
                            ModelItem m = ParseSingleModelObject(item);
                            if (m != null) list.Add(m);
                        }
                    }
                    // Ollama format: { "models": [ { "name": "..." } ] }
                    else if (dict.ContainsKey("models") && dict["models"] is ArrayList)
                    {
                        ArrayList mList = (ArrayList)dict["models"];
                        foreach (object item in mList)
                        {
                            ModelItem m = ParseSingleModelObject(item);
                            if (m != null) list.Add(m);
                        }
                    }
                }
                else if (obj is ArrayList)
                {
                    ArrayList arr = (ArrayList)obj;
                    foreach (object item in arr)
                    {
                        ModelItem m = ParseSingleModelObject(item);
                        if (m != null) list.Add(m);
                    }
                }
            }
            catch
            {
                // Fallback graceful handling
            }

            return list;
        }

        private static ModelItem ParseSingleModelObject(object item)
        {
            if (item == null) return null;

            string id = "";
            string name = "";

            if (item is Dictionary<string, object>)
            {
                Dictionary<string, object> d = (Dictionary<string, object>)item;
                if (d.ContainsKey("id") && d["id"] != null) id = d["id"].ToString();
                else if (d.ContainsKey("name") && d["name"] != null) id = d["name"].ToString();
                else if (d.ContainsKey("model") && d["model"] != null) id = d["model"].ToString();

                if (d.ContainsKey("display_name") && d["display_name"] != null) name = d["display_name"].ToString();
                else if (d.ContainsKey("name") && d["name"] != null) name = d["name"].ToString();
            }
            else if (item is string)
            {
                id = item.ToString();
            }

            if (string.IsNullOrWhiteSpace(id)) return null;

            ModelItem m = new ModelItem();
            m.id = id.Trim();
            m.displayName = !string.IsNullOrWhiteSpace(name) ? name.Trim() : m.id;
            m.selectedForInjection = true;

            string lower = m.id.ToLowerInvariant();
            if (lower.Contains("deepseek-r1") || lower.Contains("r1") || lower.Contains("o1") || lower.Contains("o3") || lower.Contains("reasoner") || lower.Contains("thinking"))
            {
                m.reasoning = true;
            }
            if (lower.Contains("vision") || lower.Contains("vl") || lower.Contains("4o") || lower.Contains("sonnet") || lower.Contains("gemini") || lower.Contains("claude-3"))
            {
                m.vision = true;
            }
            if (lower.StartsWith("o1") || lower.StartsWith("o3") || lower.Contains("/o1") || lower.Contains("/o3"))
            {
                m.maxTokensField = "max_completion_tokens";
            }
            else
            {
                m.maxTokensField = "max_tokens";
            }

            m.tools = true;
            m.maxInputTokens = 131072;
            m.maxOutputTokens = 32768;
            return m;
        }
    }
}
