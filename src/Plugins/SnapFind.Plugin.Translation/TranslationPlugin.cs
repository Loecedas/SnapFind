using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SnapFind.Plugin.Abstractions;

namespace SnapFind.Plugin.Translation
{
    public class TranslationPlugin : ITranslationPlugin, IPlugin, IUiContribution
    {
        private static readonly Lazy<TranslationPlugin> _lazyInstance = new(() => new TranslationPlugin());
        public static TranslationPlugin Instance => _lazyInstance.Value;

        public string Name => "SnapFind.Plugin.Translation";
        public string Version => "1.0.0";

        private TranslationOptions _options = new();
        private TranslateWindow? _activeWindow;
        private IHostServices? _host;

        public PluginManifest Manifest { get; } = new PluginManifest
        {
            Id = "SnapFind.Plugin.Translation",
            Name = "多源智能翻译扩展",
            NameEn = "Smart Translation Extension",
            Version = "1.0.0",
            Description = "提供 OCR 结果自动同步翻译、独立互译浮窗、简繁转换与智能代理支持。",
            DescriptionEn = "Provides OCR auto-translation, floating translator, Chinese script conversion and proxy support.",
            Author = "SnapFind",
            ApiVersion = 1,
            Entry = "SnapFind.Plugin.Translation.dll",
            EntryType = "SnapFind.Plugin.Translation.TranslationPlugin",
            HasHotkey = true,
            DefaultHotkey = "Control,Alt,T",
            Features = new List<string> { "同步翻译", "独立浮窗", "简繁转换" },
            FeaturesEn = new List<string> { "Sync Translate", "Float Window", "Script Conversion" }
        };

        public Task InitializeAsync(IHostServices host)
        {
            _host = host;
            return Task.CompletedTask;
        }

        public Task ActivateAsync()
        {
            return Task.CompletedTask;
        }

        public Task DeactivateAsync()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_activeWindow != null && _activeWindow.IsLoaded)
                {
                    _activeWindow.Close();
                    _activeWindow = null;
                }
            });
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DeactivateAsync().GetAwaiter().GetResult();
        }

        public FrameworkElement? CreateOcrPanel(System.Windows.Controls.TextBox resultTextBox)
        {
            var panel = new OcrTranslationPanel();
            panel.Attach(resultTextBox, _options.DefaultTargetLanguage);
            return panel;
        }

        public FrameworkElement? CreateSettingsSection()
        {
            return new TranslationSettingsSection();
        }

        public IReadOnlyList<LanguageInfo> SupportedLanguages => TranslationEngine.SupportedLanguages;

        public void Configure(TranslationOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_activeWindow != null && _activeWindow.IsLoaded)
                {
                    _activeWindow.SetDefaultTargetLanguage(_options.DefaultTargetLanguage);
                }
            });
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
        public static Task<string> Translate(string text, string fromLang = "auto", string toLang = "zh-CN", string provider = "Microsoft")
        {
            return Instance.TranslateAsync(text, provider, fromLang, toLang);
        }

        public static void ShowWindow(string initialText = "", Window? owner = null)
        {
            Instance.OpenTranslateWindow(initialText, owner);
        }

        public static string ConvertToTraditional(string text)
        {
            return Instance.ToTraditional(text);
        }

        public static string ConvertToSimplified(string text)
        {
            return Instance.ToSimplified(text);
        }
        #endregion
    }
}

