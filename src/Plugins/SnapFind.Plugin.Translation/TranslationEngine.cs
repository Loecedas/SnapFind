using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 翻译核心引擎，提供在线翻译与本地繁简转换能力
    /// </summary>
    public static class TranslationEngine
    {
        private static HttpClient _httpClient;
        private static readonly object _lock = new object();

        public static readonly List<LanguageInfo> SupportedLanguages = new List<LanguageInfo>
        {
            new LanguageInfo { DisplayName = "自动检测", Code = "auto", MicrosoftCode = "" },
            new LanguageInfo { DisplayName = "简体中文", Code = "zh-CN", MicrosoftCode = "zh-Hans" },
            new LanguageInfo { DisplayName = "繁体中文", Code = "zh-TW", MicrosoftCode = "zh-Hant" },
            new LanguageInfo { DisplayName = "繁体中文 (本地)", Code = "s2t", MicrosoftCode = "s2t" },
            new LanguageInfo { DisplayName = "简体中文 (本地)", Code = "t2s", MicrosoftCode = "t2s" },
            new LanguageInfo { DisplayName = "英语", Code = "en", MicrosoftCode = "en" },
            new LanguageInfo { DisplayName = "日语", Code = "ja", MicrosoftCode = "ja" },
            new LanguageInfo { DisplayName = "韩语", Code = "ko", MicrosoftCode = "ko" },
            new LanguageInfo { DisplayName = "法语", Code = "fr", MicrosoftCode = "fr" },
            new LanguageInfo { DisplayName = "德语", Code = "de", MicrosoftCode = "de" },
            new LanguageInfo { DisplayName = "俄语", Code = "ru", MicrosoftCode = "ru" },
            new LanguageInfo { DisplayName = "西班牙语", Code = "es", MicrosoftCode = "es" },
            new LanguageInfo { DisplayName = "意大利语", Code = "it", MicrosoftCode = "it" },
            new LanguageInfo { DisplayName = "葡萄牙语", Code = "pt", MicrosoftCode = "pt" },
            new LanguageInfo { DisplayName = "阿拉伯语", Code = "ar", MicrosoftCode = "ar" },
            new LanguageInfo { DisplayName = "越南语", Code = "vi", MicrosoftCode = "vi" },
            new LanguageInfo { DisplayName = "泰语", Code = "th", MicrosoftCode = "th" }
        };

        static TranslationEngine()
        {
            _httpClient = CreateHttpClient();
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                Proxy = new DynamicSystemWebProxy(),
                UseProxy = true,
                PooledConnectionLifetime = TimeSpan.FromSeconds(2) // 2 秒连接回收，敏捷响应系统 VPN 与代理路由变化
            };
            var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return client;
        }

        private static void RecreateHttpClient(HttpClient failedClient)
        {
            lock (_lock)
            {
                if (ReferenceEquals(_httpClient, failedClient))
                {
                    try
                    {
                        _httpClient.Dispose();
                    }
                    catch { }
                    _httpClient = CreateHttpClient();
                }
            }
        }

        /// <summary>
        /// 异步执行文本翻译
        /// </summary>
        /// <param name="text">待翻译文本</param>
        /// <param name="provider">翻译服务提供商（默认为 "Microsoft"）</param>
        /// <param name="fromLang">源语言代码（默认 "auto"）</param>
        /// <param name="toLang">目标语言代码（默认 "zh-CN"）</param>
        /// <returns>翻译后的文本</returns>
        public static async Task<string> TranslateAsync(string text, string provider = "Microsoft", string fromLang = "auto", string toLang = "zh-CN")
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            if (toLang == "auto")
            {
                // 智能检测：包含中文字符则目标语言为英语，否则为简体中文
                bool containsChinese = Regex.IsMatch(text, @"[\u4e00-\u9fa5]");
                toLang = containsChinese ? "en" : "zh-CN";
            }

            if (toLang == "s2t")
            {
                return ChineseConverter.ToTraditional(text);
            }
            if (toLang == "t2s")
            {
                return ChineseConverter.ToSimplified(text);
            }

            try
            {
                var fromInfo = SupportedLanguages.Find(l => l.Code == fromLang) ?? SupportedLanguages[0];
                var toInfo = SupportedLanguages.Find(l => l.Code == toLang) ?? SupportedLanguages[1]; // fallback to zh-CN

                string fromCode = fromInfo.MicrosoftCode;
                string toCode = toInfo.MicrosoftCode;
                return await TranslateMicrosoftAsync(text, fromCode, toCode);
            }
            catch (Exception ex)
            {
                return $"[翻译失败：{ex.Message}]";
            }
        }

        private static async Task<string> TranslateMicrosoftAsync(string text, string fromCode, string toCode)
        {
            string url = $"https://edge.microsoft.com/translate/translatetext?from={fromCode}&to={toCode}&isEnterpriseClient=false";
            var requestBody = new[] { text };
            string jsonBody = JsonSerializer.Serialize(requestBody);

            HttpClient client = _httpClient;
            try
            {
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await ParseJsonResponseAsync(response);
            }
            catch
            {
                // 重建 HttpClient 并刷新代理配置进行重试
                RecreateHttpClient(client);

                client = _httpClient;
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await ParseJsonResponseAsync(response);
            }
        }

        private static async Task<string> ParseJsonResponseAsync(HttpResponseMessage response)
        {
            string jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var firstElement = root[0];
                if (firstElement.TryGetProperty("translations", out var translationsVal) &&
                    translationsVal.ValueKind == JsonValueKind.Array &&
                    translationsVal.GetArrayLength() > 0)
                {
                    return translationsVal[0].GetProperty("text").GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
