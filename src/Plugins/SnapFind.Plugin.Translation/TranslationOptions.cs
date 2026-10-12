namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 翻译插件配置选项
    /// </summary>
    public class TranslationOptions
    {
        /// <summary>
        /// 翻译提供商（默认 Microsoft）
        /// </summary>
        public string Provider { get; set; } = "Microsoft";

        /// <summary>
        /// 默认目标语言代码（默认 zh-CN）
        /// </summary>
        public string DefaultTargetLanguage { get; set; } = "zh-CN";

        /// <summary>
        /// 默认源语言代码（默认 auto）
        /// </summary>
        public string DefaultSourceLanguage { get; set; } = "auto";

        /// <summary>
        /// 键入防抖延迟毫秒（默认 600ms）
        /// </summary>
        public int DebounceDelayMs { get; set; } = 600;
    }
}
