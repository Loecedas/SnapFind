namespace SnapFind.Plugin.Translation
{
    public class LanguageInfo
    {
        public string DisplayName { get; set; } = "";
        public string Code { get; set; } = ""; // Unified code (e.g. "auto", "zh-CN", "en", "ja")
        public string MicrosoftCode { get; set; } = "";
    }
}
