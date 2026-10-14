using System.Text.RegularExpressions;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SnapFind.Plugin.Translation
{
    public partial class TranslateWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newLong);

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x00080000;

        private readonly DispatcherTimer _autoTranslateTimer;
        private readonly string _initialText;
        private readonly string _defaultTargetLanguage;

        public TranslateWindow(string initialText = "", string defaultTargetLanguage = "zh-CN")
        {
            _initialText = initialText;
            _defaultTargetLanguage = string.IsNullOrEmpty(defaultTargetLanguage) ? "zh-CN" : defaultTargetLanguage;

            InitializeComponent();

            _autoTranslateTimer = new DispatcherTimer();
            _autoTranslateTimer.Interval = TimeSpan.FromMilliseconds(600);
            _autoTranslateTimer.Tick += AutoTranslateTimer_Tick;
            Closed += (s, e) => _autoTranslateTimer.Stop();

            // 尝试继承主程序图标，若未提供则安全忽略
            TryLoadHostIcon();
        }

        private void TryLoadHostIcon()
        {
            try
            {
                if (Application.Current?.MainWindow?.Icon != null)
                {
                    Icon = Application.Current.MainWindow.Icon;
                }
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
                {
                    int preference = 2; // DWMWCP_ROUND (Win11 圆角)
                    DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
                }

                // 移除系统菜单以保持扁平现代化无边框风格
                int value = GetWindowLong(hwnd, GWL_STYLE);
                SetWindowLong(hwnd, GWL_STYLE, value & ~WS_SYSMENU);
            }
            catch { }

            // 动态适配系统深色/浅色模式或宿主 App 主题
            PluginThemeHelper.ApplyTheme(Resources);

            InitializeLanguages();

            if (!string.IsNullOrEmpty(_initialText))
            {
                InputTextBox.Text = _initialText;
                PerformTranslation();
            }

            InputTextBox.Focus();
        }

        private void InitializeLanguages()
        {
            // 初始化源语言下拉框
            SourceLangComboBox.Items.Clear();
            foreach (var lang in TranslationEngine.SupportedLanguages)
            {
                SourceLangComboBox.Items.Add(new ComboBoxItem
                {
                    Content = lang.DisplayName,
                    Tag = lang.Code
                });
            }
            SourceLangComboBox.SelectedIndex = 0; // 默认自动检测

            // 初始化目标语言下拉框
            TargetLangComboBox.Items.Clear();
            int defaultTargetIndex = 0;
            int index = 0;

            foreach (var lang in TranslationEngine.SupportedLanguages)
            {
                TargetLangComboBox.Items.Add(new ComboBoxItem
                {
                    Content = lang.DisplayName,
                    Tag = lang.Code
                });

                if (lang.Code == _defaultTargetLanguage)
                {
                    defaultTargetIndex = index;
                }
                index++;
            }

            if (TargetLangComboBox.Items.Count > 0)
            {
                TargetLangComboBox.SelectedIndex = defaultTargetIndex;
            }
        }

        public void SetDefaultTargetLanguage(string defaultTargetLanguage)
        {
            if (string.IsNullOrEmpty(defaultTargetLanguage)) return;
            SetComboBoxSelectedTag(TargetLangComboBox, defaultTargetLanguage);
        }

        public void SetInputText(string text)
        {
            InputTextBox.Text = text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                PerformTranslation();
            }
        }

        private void PerformTranslation()
        {
            string text = InputTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                LocalConvertTextBox.Text = string.Empty;
                MicrosoftTextBox.Text = string.Empty;
                return;
            }

            string fromLang = "auto";
            string toLang = "zh-CN";

            if (SourceLangComboBox.SelectedItem is ComboBoxItem sourceItem)
            {
                fromLang = sourceItem.Tag?.ToString() ?? "auto";
            }
            if (TargetLangComboBox.SelectedItem is ComboBoxItem targetItem)
            {
                toLang = targetItem.Tag?.ToString() ?? "zh-CN";
            }

            if (toLang == "auto")
            {
                toLang = Regex.IsMatch(text, @"[\u4e00-\u9fa5]") ? "en" : "zh-CN";
            }

            if (toLang == "s2t" || toLang == "t2s")
            {
                // 本地离线转换
                LocalConvertCard.Visibility = Visibility.Visible;
                MicrosoftTranslateCard.Visibility = Visibility.Collapsed;

                string converted = toLang == "s2t" ? ChineseConverter.ToTraditional(text) : ChineseConverter.ToSimplified(text);
                LocalConvertTextBox.Text = converted;
            }
            else
            {
                // 在线网络翻译
                LocalConvertCard.Visibility = Visibility.Collapsed;
                MicrosoftTranslateCard.Visibility = Visibility.Visible;

                MicrosoftTextBox.Text = "正在翻译中...";

                Task.Run(async () =>
                {
                    string res = await TranslationEngine.TranslateAsync(text, "Microsoft", fromLang, toLang);
                    Dispatcher.Invoke(() =>
                    {
                        if (InputTextBox.Text == text)
                        {
                            MicrosoftTextBox.Text = res;
                        }
                    });
                });
            }
        }

        private void AutoTranslateTimer_Tick(object? sender, EventArgs e)
        {
            _autoTranslateTimer.Stop();
            PerformTranslation();
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _autoTranslateTimer.Stop();

            if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                _autoTranslateTimer.Start();
            }
            else
            {
                LocalConvertTextBox.Text = string.Empty;
                MicrosoftTextBox.Text = string.Empty;
            }
        }

        private void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && !string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                PerformTranslation();
            }
        }

        private void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            _autoTranslateTimer.Stop();
            PerformTranslation();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputTextBox.Text = string.Empty;
            LocalConvertTextBox.Text = string.Empty;
            MicrosoftTextBox.Text = string.Empty;
            InputTextBox.Focus();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string textToCopy = string.Empty;
            if (LocalConvertCard.Visibility == Visibility.Visible)
            {
                textToCopy = LocalConvertTextBox.Text;
            }
            else
            {
                textToCopy = MicrosoftTextBox.Text;
            }

            if (!string.IsNullOrEmpty(textToCopy) && !textToCopy.StartsWith("[翻译失败"))
            {
                PluginClipboard.SetText(textToCopy);
            }
        }

        private void SwapLanguagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (SourceLangComboBox.SelectedItem is ComboBoxItem sourceItem &&
                TargetLangComboBox.SelectedItem is ComboBoxItem targetItem)
            {
                string sourceTag = sourceItem.Tag?.ToString() ?? "auto";
                string targetTag = targetItem.Tag?.ToString() ?? "zh-CN";
                if (sourceTag != "auto" && targetTag != "s2t" && targetTag != "t2s")
                {
                    SetComboBoxSelectedTag(SourceLangComboBox, targetTag);
                    SetComboBoxSelectedTag(TargetLangComboBox, sourceTag);
                }
            }
        }

        private void SetComboBoxSelectedTag(ComboBox cb, string tag)
        {
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is ComboBoxItem item && item.Tag?.ToString() == tag)
                {
                    cb.SelectedIndex = i;
                    break;
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource != InputTextBox && 
                e.OriginalSource != LocalConvertTextBox && 
                e.OriginalSource != MicrosoftTextBox)
            {
                DragMove();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _autoTranslateTimer.Stop();
                PerformTranslation();
                e.Handled = true;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
