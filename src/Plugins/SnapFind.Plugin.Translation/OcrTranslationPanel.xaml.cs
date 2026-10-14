using System.Text.RegularExpressions;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SnapFind.Plugin.Translation
{
    public partial class OcrTranslationPanel : UserControl
    {
        private readonly DispatcherTimer _debounceTimer;
        private TextBox? _attachedTextBox;
        private string _currentTargetLang = "zh-CN";
        private string _lastSourceText = "";

        public string TranslatedText => TranslateTextBox.Text;

        public string TargetLanguage
        {
            get => _currentTargetLang;
            set
            {
                _currentTargetLang = value;
                SelectLanguageInComboBox(value);
            }
        }

        public OcrTranslationPanel()
        {
            InitializeComponent();

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

            Loaded += OcrTranslationPanel_Loaded;
        }

        private void OcrTranslationPanel_Loaded(object sender, RoutedEventArgs e)
        {
            PluginThemeHelper.ApplyTheme(Resources);
            PopulateLanguages();
        }

        private void PopulateLanguages()
        {
            if (TargetLangComboBox.Items.Count > 0) return;

            int selectedIndex = 0;
            int idx = 0;

            foreach (var lang in TranslationEngine.SupportedLanguages)
            {
                TargetLangComboBox.Items.Add(new ComboBoxItem
                {
                    Content = lang.DisplayName,
                    Tag = lang.Code
                });

                if (lang.Code == _currentTargetLang)
                {
                    selectedIndex = idx;
                }
                idx++;
            }

            if (TargetLangComboBox.Items.Count > 0)
            {
                TargetLangComboBox.SelectedIndex = selectedIndex;
            }
        }

        private void SelectLanguageInComboBox(string langCode)
        {
            for (int i = 0; i < TargetLangComboBox.Items.Count; i++)
            {
                if (TargetLangComboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == langCode)
                {
                    TargetLangComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// 绑定源 OCR 文本框，自动监听其键入变动并执行防抖同步翻译
        /// </summary>
        public void Attach(TextBox sourceTextBox, string defaultTargetLanguage = "zh-CN")
        {
            if (_attachedTextBox != null)
            {
                _attachedTextBox.TextChanged -= AttachedTextBox_TextChanged;
            }

            _attachedTextBox = sourceTextBox;
            _currentTargetLang = string.IsNullOrEmpty(defaultTargetLanguage) ? "zh-CN" : defaultTargetLanguage;

            if (_attachedTextBox != null)
            {
                _attachedTextBox.TextChanged += AttachedTextBox_TextChanged;
                if (!string.IsNullOrWhiteSpace(_attachedTextBox.Text))
                {
                    Translate(_attachedTextBox.Text);
                }
            }
        }

        /// <summary>
        /// 手动触发翻译指定文本
        /// </summary>
        public void Translate(string text)
        {
            _debounceTimer.Stop();
            _lastSourceText = text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_lastSourceText))
            {
                TranslateTextBox.Text = string.Empty;
                return;
            }

            TranslateTextBox.Text = "正在翻译中...";

            string targetLang = _currentTargetLang;
            if (targetLang == "auto")
            {
                targetLang = Regex.IsMatch(_lastSourceText, @"[\u4e00-\u9fa5]") ? "en" : "zh-CN";
            }
            string source = _lastSourceText;

            Task.Run(async () =>
            {
                string result = await TranslationEngine.TranslateAsync(source, "Microsoft", "auto", targetLang);
                Dispatcher.Invoke(() =>
                {
                    if (_lastSourceText == source)
                    {
                        TranslateTextBox.Text = result;
                    }
                });
            });
        }

        private void AttachedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _debounceTimer.Stop();

            if (_attachedTextBox == null || string.IsNullOrWhiteSpace(_attachedTextBox.Text))
            {
                TranslateTextBox.Text = string.Empty;
                return;
            }

            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (_attachedTextBox != null)
            {
                Translate(_attachedTextBox.Text);
            }
        }

        private void TargetLangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLangComboBox.SelectedItem is ComboBoxItem item)
            {
                _currentTargetLang = item.Tag?.ToString() ?? "zh-CN";
                if (!string.IsNullOrWhiteSpace(_lastSourceText))
                {
                    Translate(_lastSourceText);
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = TranslateTextBox.Text;
            if (!string.IsNullOrEmpty(text) && !text.StartsWith("[翻译失败") && text != "正在翻译中...")
            {
                PluginClipboard.SetText(text);
            }
        }
    }
}
