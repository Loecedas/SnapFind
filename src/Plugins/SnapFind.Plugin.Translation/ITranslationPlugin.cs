using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 翻译插件接口规范
    /// </summary>
    public interface ITranslationPlugin
    {
        string Name { get; }
        string Version { get; }

        /// <summary>
        /// 异步翻译文本
        /// </summary>
        Task<string> TranslateAsync(string text, string provider = "Microsoft", string fromLang = "auto", string toLang = "zh-CN");

        /// <summary>
        /// 打开独立的翻译交互窗口
        /// </summary>
        void OpenTranslateWindow(string initialText = "", Window? owner = null);

        /// <summary>
        /// 本地转换为繁体中文
        /// </summary>
        string ToTraditional(string text);

        /// <summary>
        /// 本地转换为简体中文
        /// </summary>
        string ToSimplified(string text);

        /// <summary>
        /// 支持的语言列表
        /// </summary>
        IReadOnlyList<LanguageInfo> SupportedLanguages { get; }

        /// <summary>
        /// 更新插件配置
        /// </summary>
        void Configure(TranslationOptions options);
    }
}
