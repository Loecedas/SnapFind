using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 翻译插件主入口与门面门面单例类
    /// </summary>
    public class TranslationPlugin : ITranslationPlugin
    {
        private static readonly Lazy<TranslationPlugin> _lazyInstance = new(() => new TranslationPlugin());
        public static TranslationPlugin Instance => _lazyInstance.Value;

        public string Name => "SnapFind.Plugin.Translation";
        public string Version => "1.0.0";

        private TranslationOptions _options = new();
        private TranslateWindow? _activeWindow;

        public IReadOnlyList<LanguageInfo> SupportedLanguages => TranslationEngine.SupportedLanguages;

        public void Configure(TranslationOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<string> TranslateAsync(string text, string provider = "Microsoft", string fromLang = "auto", string toLang = "zh-CN")
        {
            return TranslationEngine.TranslateAsync(text, provider, fromLang, toLang);
        }

        public string ToTraditional(string text)
        {
            return ChineseConverter.ToTraditional(text);
        }

        public string ToSimplified(string text)
        {
            return ChineseConverter.ToSimplified(text);
        }

        public void OpenTranslateWindow(string initialText = "", Window? owner = null)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_activeWindow != null && _activeWindow.IsLoaded && _activeWindow.IsVisible)
                {
                    // 若无传入新文本（即快捷键呼出），且窗口已在前台打开，则再次按下快捷键关闭窗口
                    if (string.IsNullOrEmpty(initialText) && _activeWindow.IsActive)
                    {
                        _activeWindow.Close();
                        _activeWindow = null;
                        return;
                    }

                    if (!string.IsNullOrEmpty(initialText))
                    {
                        _activeWindow.SetInputText(initialText);
                    }
                    if (_activeWindow.WindowState == WindowState.Minimized)
                    {
                        _activeWindow.WindowState = WindowState.Normal;
                    }
                    _activeWindow.Activate();
                    return;
                }

                _activeWindow = new TranslateWindow(initialText, _options.DefaultTargetLanguage);
                if (owner != null)
                {
                    _activeWindow.Owner = owner;
                }
                _activeWindow.Closed += (_, _) => _activeWindow = null;
                _activeWindow.Show();
                _activeWindow.Activate();
            });
        }

        #region Static Convenience Methods

        /// <summary>
        /// 便捷静态调用：异步翻译文本
        /// </summary>
        public static Task<string> Translate(string text, string fromLang = "auto", string toLang = "zh-CN", string provider = "Microsoft")
        {
            return Instance.TranslateAsync(text, provider, fromLang, toLang);
        }

        /// <summary>
        /// 便捷静态调用：打开翻译窗口
        /// </summary>
        public static void ShowWindow(string initialText = "", Window? owner = null)
        {
            Instance.OpenTranslateWindow(initialText, owner);
        }

        /// <summary>
        /// 便捷静态调用：繁体字转换
        /// </summary>
        public static string ConvertToTraditional(string text)
        {
            return Instance.ToTraditional(text);
        }

        /// <summary>
        /// 便捷静态调用：简体字转换
        /// </summary>
        public static string ConvertToSimplified(string text)
        {
            return Instance.ToSimplified(text);
        }

        #endregion
    }
}
