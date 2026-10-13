using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PixOcrSearch
{
    public partial class SettingsWindow : Window
    {
        private string _recordedModifiers = "";
        private string _recordedKey = "";
        private string _recordedControlPanelModifiers = "";
        private string _recordedControlPanelKey = "";
        
        // Updater fields
#if USE_RAPID_OCR
        private const string CurrentEdition = "Rapid";
        private const string OppositeEdition = "Paddle";
#else
        private const string CurrentEdition = "Paddle";
        private const string OppositeEdition = "Rapid";
#endif
        private GitHubRelease? _releaseInfo;
        private CancellationTokenSource? _cts;
        private bool _isDownloading = false;
        private string _currentActiveTab = "settings";
        private bool _isInitializing = true;

        public SettingsWindow() : this("settings", null)
        {
        }

        public SettingsWindow(string activeTab, GitHubRelease? releaseInfo = null)
        {
            InitializeComponent();
            _currentActiveTab = activeTab;
            _releaseInfo = releaseInfo;
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
                {
                    int preference = 2; // DWMWCP_ROUND
                    DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
                }

                // Disable Maximize Box and System Menu (to hide default caption buttons completely while keeping minimize animations)
                int value = GetWindowLong(hwnd, GWL_STYLE);
                const int WS_SYSMENU = 0x00080000;
                SetWindowLong(hwnd, GWL_STYLE, value & ~WS_MAXIMIZEBOX & ~WS_SYSMENU);
            }
            catch { }

            // Dynamically refresh system theme resources before rendering
            App.ApplyTheme();

            // Load current config settings
            LoadSettingsConfig();

            // Apply localization
            ApplyLocalization();

            // Select default tab
            SelectTab(_currentActiveTab);

            _isInitializing = false;
        }

        private void LoadSettingsConfig()
        {
            // Language
            string currentLang = ConfigManager.Current.Language ?? "zh-CN";
            bool langFound = false;
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (string.Equals(item.Tag?.ToString(), currentLang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageComboBox.SelectedItem = item;
                    langFound = true;
                    break;
                }
            }
            if (!langFound)
            {
                LanguageComboBox.SelectedIndex = 0;
            }

            // Result font size
            int currentFontSize = ConfigManager.Current.ResultFontSize > 0 ? ConfigManager.Current.ResultFontSize : 13;
            bool fontSizeFound = false;
            foreach (ComboBoxItem item in ResultFontSizeComboBox.Items)
            {
                if (int.TryParse(item.Tag?.ToString(), out int sz) && sz == currentFontSize)
                {
                    ResultFontSizeComboBox.SelectedItem = item;
                    fontSizeFound = true;
                    break;
                }
            }
            if (!fontSizeFound)
            {
                ResultFontSizeComboBox.SelectedIndex = 0;
            }

            // Search engine
            string currentUrl = ConfigManager.Current.SearchEngineUrl;
            bool found = false;
            foreach (ComboBoxItem item in SearchEngineComboBox.Items)
            {
                if (item.Tag?.ToString() == currentUrl)
                {
                    SearchEngineComboBox.SelectedItem = item;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                SearchEngineComboBox.SelectedIndex = 0;
            }

#if USE_RAPID_OCR
            OcrModelComboBox.Items.Clear();
            OcrModelComboBox.Items.Add(new ComboBoxItem
            {
                Content = Localization.OcrModelRapid,
                Tag = "Rapid_PP-OCRv4"
            });
            OcrModelComboBox.SelectedIndex = 0;
            OcrModelComboBox.IsEnabled = false;
#else
            // Detect OCR models
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string inferenceDir = Path.Combine(baseDir, "libs", "inference");

            bool tinyExists = Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_tiny_det_infer")) && Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_tiny_rec_infer"));
            bool smallExists = Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_small_det_infer")) && Directory.Exists(Path.Combine(inferenceDir, "PP-OCRv6_small_rec_infer"));

            var itemsToRemove = new System.Collections.Generic.List<ComboBoxItem>();
            foreach (ComboBoxItem item in OcrModelComboBox.Items)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (tag == "PP-OCRv6_tiny" && !tinyExists) itemsToRemove.Add(item);
                else if (tag == "PP-OCRv6_small" && !smallExists) itemsToRemove.Add(item);
            }
            foreach (var item in itemsToRemove)
            {
                OcrModelComboBox.Items.Remove(item);
            }

            string currentModel = ConfigManager.Current.OcrModel;
            bool modelFound = false;
            foreach (ComboBoxItem item in OcrModelComboBox.Items)
            {
                if (item.Tag?.ToString() == currentModel)
                {
                    OcrModelComboBox.SelectedItem = item;
                    modelFound = true;
                    break;
                }
            }
            if (!modelFound && OcrModelComboBox.Items.Count > 0)
            {
                OcrModelComboBox.SelectedIndex = 0;
            }

            if (OcrModelComboBox.Items.Count <= 1)
            {
                OcrModelComboBox.IsEnabled = false;
            }
#endif

            _recordedModifiers = ConfigManager.Current.HotkeyModifiers;
            _recordedKey = ConfigManager.Current.HotkeyKey;
            _recordedControlPanelModifiers = ConfigManager.Current.ControlPanelHotkeyModifiers;
            _recordedControlPanelKey = ConfigManager.Current.ControlPanelHotkeyKey;
            
            UpdateHotkeyTextBoxDisplay();
            UpdateControlPanelHotkeyTextBoxDisplay();
            AutoStartCheckBox.IsChecked = ConfigManager.Current.StartWithWindows;
            AutoCopyCheckBox.IsChecked = ConfigManager.Current.AutoCopyToClipboard;
            DoNotOpenEditWindowCheckBox.IsChecked = ConfigManager.Current.AutoCopyToClipboard && ConfigManager.Current.DoNotOpenEditWindow;
            DoNotOpenEditWindowCheckBox.IsEnabled = AutoCopyCheckBox.IsChecked == true;
            MultiRegionCheckBox.IsChecked = ConfigManager.Current.MultiRegionSelection;
            AutoTranslateOcrCheckBox.IsChecked = ConfigManager.Current.AutoTranslateOcrResult;
            UpdatePluginDependentUI();
        }

        /// <summary>
        /// 根据是否实际导入安装了翻译扩展插件，动态切换设置页中联动选项的显隐
        /// </summary>
        public void UpdatePluginDependentUI()
        {
            bool isAvailable = PluginManager.IsTranslationPluginAvailable;
            AutoTranslateOcrCheckBox.Visibility = isAvailable ? Visibility.Visible : Visibility.Collapsed;
            if (!isAvailable)
            {
                AutoTranslateOcrCheckBox.IsChecked = false;
                ConfigManager.Current.AutoTranslateOcrResult = false;
            }
        }

        public void ApplyLocalization()
        {
            WindowTitleText.Text = Localization.ControlCenterTitle;
            RadioSettings.Content = Localization.TabSettings;
            RadioPlugins.Content = Localization.TabPlugins;
            RadioNotifications.Content = Localization.TabUpdates;
            RadioAbout.Content = Localization.TabAbout;

            PluginsTitleText.Text = Localization.PluginsTitle;
            PluginsDescText.Text = Localization.PluginsDesc;
            BtnImportPlugin.Content = Localization.BtnImportPlugin;
            BtnOpenPluginFolder.Content = Localization.BtnOpenPluginFolder;
            BtnRefreshPlugins.Content = Localization.BtnRefreshPlugins;
            PluginsEmptyTitleText.Text = Localization.PluginsEmptyTitle;
            PluginsEmptyDescText.Text = Localization.PluginsEmptyDesc;
            PluginsCloseButton.Content = Localization.BtnClose;

            LabelLanguageText.Text = Localization.LabelLanguage;
            LabelSearchEngineText.Text = Localization.LabelSearchEngine;
            LabelOcrModelText.Text = Localization.LabelOcrModel;
            LabelScreenshotHotkeyText.Text = Localization.LabelScreenshotHotkey;
            LabelScreenshotHotkeyText.ToolTip = Localization.HotkeyTooltip;
            LabelControlPanelHotkeyText.Text = Localization.LabelControlPanelHotkey;
            LabelControlPanelHotkeyText.ToolTip = Localization.HotkeyTooltip;
            LabelResultFontSizeText.Text = Localization.LabelResultFontSize;

            AutoStartCheckBox.Content = Localization.CheckAutoStart;
            AutoCopyCheckBox.Content = Localization.CheckAutoCopy;
            DoNotOpenEditWindowCheckBox.Content = Localization.CheckDoNotOpenEditWindow;
            MultiRegionCheckBox.Content = Localization.CheckMultiRegion;
            AutoTranslateOcrCheckBox.Content = Localization.CheckAutoTranslateOcr;
            SettingsCancelButton.Content = Localization.BtnCancel;
            SettingsSaveButton.Content = Localization.BtnSave;

            // Result Font Size items
            if (ResultFontSizeComboBox.Items.Count >= 2)
            {
                if (ResultFontSizeComboBox.Items[0] is ComboBoxItem fontItem0) fontItem0.Content = Localization.FontSizeStandard;
                if (ResultFontSizeComboBox.Items[1] is ComboBoxItem fontItem1) fontItem1.Content = Localization.FontSizeExtraLarge;
            }

            // Search Engine items
            if (SearchEngineComboBox.Items.Count >= 2)
            {
                if (SearchEngineComboBox.Items[0] is ComboBoxItem item0) item0.Content = Localization.SearchGoogle;
                if (SearchEngineComboBox.Items[1] is ComboBoxItem item1) item1.Content = Localization.SearchBing;
            }

            // OCR Model items
#if USE_RAPID_OCR
            if (OcrModelComboBox.Items.Count > 0 && OcrModelComboBox.Items[0] is ComboBoxItem rapidItem)
            {
                rapidItem.Content = Localization.OcrModelRapid;
            }
#else
            foreach (ComboBoxItem item in OcrModelComboBox.Items)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (tag == "PP-OCRv6_tiny") item.Content = Localization.OcrModelTiny;
                else if (tag == "PP-OCRv6_small") item.Content = Localization.OcrModelSmall;
            }
#endif

            UpdateHotkeyTextBoxDisplay();
            UpdateControlPanelHotkeyTextBoxDisplay();

            // About Panel
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            AboutVersionText.Text = Localization.AboutVersion($"{version.Major}.{version.Minor}.{version.Build}");
            AboutIntroTitleText.Text = Localization.AboutIntroTitle;
            AboutIntroContentText.Text = Localization.AboutIntroText;
            AboutUsageTitleText.Text = Localization.AboutUsageTitle;
            AboutUsageContentText.Text = Localization.AboutUsageText;
            AboutLinksTitleText.Text = Localization.AboutLinksTitle;
            AboutGithubRun.Text = Localization.AboutGithubLabel;
            AboutWebsiteRun.Text = Localization.AboutWebsiteLabel;
            AboutCheckUpdateButton.Content = Localization.BtnCheckUpdate;
            AboutCloseButton.Content = Localization.BtnClose;

            // Notifications / Updates Panel
            if (_releaseInfo != null)
            {
                DisplayReleaseInfo(_releaseInfo);
            }
            else
            {
                NotificationTitleText.Text = Localization.UpdateTitleLatest;
                DownloadButton.Content = Localization.BtnDownloadNow;
                IgnoreButton.Content = Localization.BtnIgnoreVersion;
                CancelNotifBtn.Content = Localization.BtnNotNow;
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string selectedLang = item.Tag.ToString() ?? "zh-CN";
                if (ConfigManager.Current.Language != selectedLang)
                {
                    ConfigManager.Current.Language = selectedLang;
                    ApplyLocalization();
                    if (Application.Current is App app)
                    {
                        app.UpdateTrayMenuLanguage();
                    }
                }
            }
        }

        public void SelectTab(string tabName)
        {
            // If download is in progress, lock navigation to avoid breaking state
            if (_isDownloading) return;

            _currentActiveTab = tabName;

            // Toggle Panels Visibility
            PanelNotifications.Visibility = tabName == "notifications" ? Visibility.Visible : Visibility.Collapsed;
            PanelSettings.Visibility = tabName == "settings" ? Visibility.Visible : Visibility.Collapsed;
            PanelPlugins.Visibility = tabName == "plugins" ? Visibility.Visible : Visibility.Collapsed;
            PanelAbout.Visibility = tabName == "about" ? Visibility.Visible : Visibility.Collapsed;

            // Highlight Sidebar Radio
            if (tabName == "notifications") RadioNotifications.IsChecked = true;
            else if (tabName == "settings") 
            {
                RadioSettings.IsChecked = true;
                UpdatePluginDependentUI();
            }
            else if (tabName == "plugins") RadioPlugins.IsChecked = true;
            else if (tabName == "about") RadioAbout.IsChecked = true;

            // Custom tab initialization
            if (tabName == "notifications")
            {
                if (_releaseInfo != null)
                {
                    DisplayReleaseInfo(_releaseInfo);
                }
                else
                {
                    FetchLatestReleaseAsync();
                }
            }
            else if (tabName == "plugins")
            {
                RefreshPluginsList();
            }
        }

        public void SetReleaseInfo(GitHubRelease releaseInfo)
        {
            _releaseInfo = releaseInfo;
            if (_currentActiveTab == "notifications")
            {
                DisplayReleaseInfo(_releaseInfo);
            }
        }

        private void Sidebar_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                // Prevent check change
                if (_currentActiveTab == "notifications") RadioNotifications.IsChecked = true;
                else if (_currentActiveTab == "settings") RadioSettings.IsChecked = true;
                else if (_currentActiveTab == "plugins") RadioPlugins.IsChecked = true;
                else if (_currentActiveTab == "about") RadioAbout.IsChecked = true;
                return;
            }

            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag != null)
            {
                SelectTab(rb.Tag.ToString() ?? "settings");
            }
        }

        // --- Settings Page Code ---

        private void UpdateHotkeyTextBoxDisplay()
        {
            if (string.IsNullOrEmpty(_recordedKey))
            {
                HotkeyTextBox.Text = Localization.NoHotkey;
                return;
            }
            string mods = _recordedModifiers.Replace(",", " + ");
            if (string.IsNullOrEmpty(mods))
            {
                HotkeyTextBox.Text = _recordedKey;
            }
            else
            {
                HotkeyTextBox.Text = mods + " + " + _recordedKey;
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            Key key = e.Key;
            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            var sb = new StringBuilder();
            var modifiers = Keyboard.Modifiers;
            if ((modifiers & ModifierKeys.Control) != 0) sb.Append("Control,");
            if ((modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt,");
            if ((modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift,");
            if ((modifiers & ModifierKeys.Windows) != 0) sb.Append("Windows,");

            string modStr = sb.ToString();
            if (modStr.EndsWith(","))
            {
                modStr = modStr.Substring(0, modStr.Length - 1);
            }

            _recordedModifiers = modStr;
            _recordedKey = key.ToString();

            UpdateHotkeyTextBoxDisplay();
        }

        private void UpdateControlPanelHotkeyTextBoxDisplay()
        {
            if (string.IsNullOrEmpty(_recordedControlPanelKey))
            {
                ControlPanelHotkeyTextBox.Text = Localization.NoHotkey;
                return;
            }
            string mods = _recordedControlPanelModifiers.Replace(",", " + ");
            if (string.IsNullOrEmpty(mods))
            {
                ControlPanelHotkeyTextBox.Text = _recordedControlPanelKey;
            }
            else
            {
                ControlPanelHotkeyTextBox.Text = mods + " + " + _recordedControlPanelKey;
            }
        }

        private void ControlPanelHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            Key key = e.Key;
            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            var sb = new StringBuilder();
            var modifiers = Keyboard.Modifiers;
            if ((modifiers & ModifierKeys.Control) != 0) sb.Append("Control,");
            if ((modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt,");
            if ((modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift,");
            if ((modifiers & ModifierKeys.Windows) != 0) sb.Append("Windows,");

            string modStr = sb.ToString();
            if (modStr.EndsWith(","))
            {
                modStr = modStr.Substring(0, modStr.Length - 1);
            }

            _recordedControlPanelModifiers = modStr;
            _recordedControlPanelKey = key.ToString();

            UpdateControlPanelHotkeyTextBoxDisplay();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_recordedModifiers))
            {
                MessageBox.Show(Localization.MsgModifierRequired, Localization.TitleModifierRequired, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_recordedControlPanelModifiers))
            {
                MessageBox.Show(Localization.MsgModifierRequired, Localization.TitleModifierRequired, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_recordedModifiers == _recordedControlPanelModifiers && _recordedKey == _recordedControlPanelKey)
            {
                MessageBox.Show(Localization.MsgHotkeyConflict, Localization.TitleHotkeyConflict, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string lang = "zh-CN";
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedLangItem)
            {
                lang = selectedLangItem.Tag?.ToString() ?? "zh-CN";
            }

            string url = "https://www.google.com/search?q=";
            if (SearchEngineComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                url = selectedItem.Tag?.ToString() ?? "https://www.google.com/search?q=";
            }

            string model = "PP-OCRv6_small";
            if (OcrModelComboBox.SelectedItem is ComboBoxItem selectedModelItem)
            {
                model = selectedModelItem.Tag?.ToString() ?? "PP-OCRv6_small";
            }

            bool modelChanged = ConfigManager.Current.OcrModel != model;

            ConfigManager.Current.Language = lang;
            ConfigManager.Current.SearchEngineUrl = url;
            ConfigManager.Current.HotkeyModifiers = _recordedModifiers;
            ConfigManager.Current.HotkeyKey = _recordedKey;
            ConfigManager.Current.ControlPanelHotkeyModifiers = _recordedControlPanelModifiers;
            ConfigManager.Current.ControlPanelHotkeyKey = _recordedControlPanelKey;
            ConfigManager.Current.OcrModel = model;
            
            int fontSize = 13;
            if (ResultFontSizeComboBox.SelectedItem is ComboBoxItem selectedFontItem &&
                int.TryParse(selectedFontItem.Tag?.ToString(), out int parsedFontSize))
            {
                fontSize = parsedFontSize;
            }
            ConfigManager.Current.ResultFontSize = fontSize;

            bool autoStart = AutoStartCheckBox.IsChecked == true;
            ConfigManager.Current.StartWithWindows = autoStart;
            ConfigManager.Current.AutoCopyToClipboard = AutoCopyCheckBox.IsChecked == true;
            ConfigManager.Current.DoNotOpenEditWindow = AutoCopyCheckBox.IsChecked == true && DoNotOpenEditWindowCheckBox.IsChecked == true;
            ConfigManager.Current.MultiRegionSelection = MultiRegionCheckBox.IsChecked == true;
            ConfigManager.Current.AutoTranslateOcrResult = AutoTranslateOcrCheckBox.Visibility == Visibility.Visible && AutoTranslateOcrCheckBox.IsChecked == true;

            ConfigManager.Save();

            if (modelChanged)
            {
                OcrHelper.Dispose();
            }

            SetAutoStart(autoStart);

            if (Application.Current is App app)
            {
                app.ReRegisterHotkey();
                app.UpdateTrayMenuLanguage();
            }

            Close();
        }

        private void AutoCopyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isAutoCopy = AutoCopyCheckBox.IsChecked == true;
            DoNotOpenEditWindowCheckBox.IsEnabled = isAutoCopy;
            if (!isAutoCopy)
            {
                DoNotOpenEditWindowCheckBox.IsChecked = false;
            }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SnapFind.exe");
                
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        key.SetValue("SnapFind", $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue("SnapFind", false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Localization.MsgAutoStartFailed + ex.Message, Localization.TitleWarning, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- About & Notifications / Updater Page Code ---

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
            e.Handled = true;
        }

        private async Task<bool> IsChinaIpAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(3000);
                client.DefaultRequestHeaders.Add("User-Agent", "SnapFind-Updater");
                string result = await client.GetStringAsync("https://cloudflare.com/cdn-cgi/trace");
                foreach (var line in result.Split('\n'))
                {
                    if (line.StartsWith("loc=", StringComparison.OrdinalIgnoreCase))
                    {
                        string country = line.Substring(4).Trim();
                        return country.Equals("CN", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(3000);
                client.DefaultRequestHeaders.Add("User-Agent", "SnapFind-Updater");
                string result = await client.GetStringAsync("http://ip-api.com/json/?fields=countryCode");
                using var doc = JsonDocument.Parse(result);
                if (doc.RootElement.TryGetProperty("countryCode", out var code))
                {
                    return code.GetString()?.Equals("CN", StringComparison.OrdinalIgnoreCase) == true;
                }
            }
            catch { }

            return true;
        }

        private async void AboutCheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            AboutCheckUpdateButton.IsEnabled = false;
            AboutCheckUpdateButton.Content = Localization.CheckingUpdates;
            AboutStatusText.Visibility = Visibility.Visible;
            AboutStatusText.Text = Localization.CheckingUpdatesStatus;

            try
            {
                var release = await GetLatestReleaseFromAllSourcesAsync();
                if (release != null)
                {
                    Version currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
                    string cleanTag = release.TagName.TrimStart('v', 'V');
                    
                    if (Version.TryParse(cleanTag, out Version? latestVer) && latestVer > currentVer)
                    {
                        AboutStatusText.Visibility = Visibility.Collapsed;
                        SetReleaseInfo(release);
                        SelectTab("notifications");
                        return;
                    }
                    else
                    {
                        AboutStatusText.Visibility = Visibility.Collapsed;
                        MessageBox.Show(Localization.MsgAlreadyLatest($"{currentVer.Major}.{currentVer.Minor}.{currentVer.Build}"), Localization.BtnCheckUpdate, MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                AboutStatusText.Visibility = Visibility.Collapsed;
                MessageBox.Show(Localization.MsgNoReleaseFound, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AboutStatusText.Visibility = Visibility.Collapsed;
                MessageBox.Show(ex.Message, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AboutCheckUpdateButton.IsEnabled = true;
                AboutCheckUpdateButton.Content = Localization.BtnCheckUpdate;
            }
        }

        private static string ExtractLocalizedChangelog(string rawBody)
        {
            if (string.IsNullOrEmpty(rawBody)) return "";

            bool isEn = Localization.IsEnglish;
            string content = rawBody;

            if (isEn)
            {
                int enIdx = rawBody.IndexOf("English", StringComparison.OrdinalIgnoreCase);
                if (enIdx != -1)
                {
                    int startDetails = rawBody.LastIndexOf("<details", enIdx, StringComparison.OrdinalIgnoreCase);
                    if (startDetails != -1)
                    {
                        int endDetails = rawBody.IndexOf("</details>", enIdx, StringComparison.OrdinalIgnoreCase);
                        if (endDetails != -1)
                        {
                            content = rawBody.Substring(startDetails, (endDetails + 10) - startDetails);
                        }
                    }
                }
            }
            else
            {
                int cnIdx = rawBody.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase);
                if (cnIdx == -1) cnIdx = rawBody.IndexOf("中文", StringComparison.OrdinalIgnoreCase);
                if (cnIdx != -1)
                {
                    int startDetails = rawBody.LastIndexOf("<details", cnIdx, StringComparison.OrdinalIgnoreCase);
                    if (startDetails != -1)
                    {
                        int endDetails = rawBody.IndexOf("</details>", cnIdx, StringComparison.OrdinalIgnoreCase);
                        if (endDetails != -1)
                        {
                            content = rawBody.Substring(startDetails, (endDetails + 10) - startDetails);
                        }
                    }
                }
            }

            int startSummary = content.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
            if (startSummary != -1)
            {
                int endSummary = content.IndexOf("</summary>", startSummary, StringComparison.OrdinalIgnoreCase);
                if (endSummary != -1)
                {
                    content = content.Remove(startSummary, (endSummary + 10) - startSummary);
                }
            }

            // Remove HTML comments <!-- ... -->
            content = Regex.Replace(content, @"<!--[\s\S]*?-->", "");

            content = content.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<p>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<ul>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</ul>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<li>", "- ", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase);

            // Strip any remaining html wrapper tags (like <details>, </details>, etc.)
            content = Regex.Replace(content, @"<[^>]+>", "");

            return content.Trim();
        }

        private void SetChangelogMarkdown(string markdown)
        {
            ChangelogPanel.Children.Clear();
            if (string.IsNullOrWhiteSpace(markdown)) return;

            bool isDark = App.IsWindowsDarkMode();
            var textColor = (System.Windows.Media.Brush)FindResource("ThemeText");
            var subTextColor = (System.Windows.Media.Brush)FindResource("ThemeSubText");
            var borderColor = (System.Windows.Media.Brush)FindResource("ThemeInputBorder");
            var codeBgColor = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(45, 45, 52) : System.Windows.Media.Color.FromRgb(235, 235, 238));
            var codeFgColor = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(255, 179, 102) : System.Windows.Media.Color.FromRgb(199, 37, 78));

            string[] lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            bool inList = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string trimmed = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    inList = false;
                    continue;
                }

                // 1. Horizontal Rule (---, ***, ___)
                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    inList = false;
                    var divider = new Border
                    {
                        Height = 1,
                        Background = borderColor,
                        Margin = new Thickness(0, 10, 0, 10),
                        Opacity = 0.6
                    };
                    ChangelogPanel.Children.Add(divider);
                    continue;
                }

                // 2. Headings (#, ##, ###, ####)
                if (trimmed.StartsWith('#'))
                {
                    inList = false;
                    int level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#') level++;
                    string titleText = trimmed.Substring(level).Trim();

                    double fontSize = level switch
                    {
                        1 => 15.0,
                        2 => 14.0,
                        3 => 13.0,
                        _ => 12.5
                    };
                    Thickness margin = level switch
                    {
                        1 => new Thickness(0, 10, 0, 4),
                        2 => new Thickness(0, 8, 0, 4),
                        3 => new Thickness(0, 6, 0, 3),
                        _ => new Thickness(0, 4, 0, 2)
                    };

                    var tb = new TextBlock
                    {
                        FontSize = fontSize,
                        FontWeight = FontWeights.Bold,
                        Foreground = textColor,
                        Margin = margin,
                        TextWrapping = TextWrapping.Wrap
                    };
                    AppendInlineMarkdown(tb.Inlines, titleText, codeBgColor, codeFgColor, isDark);
                    ChangelogPanel.Children.Add(tb);
                    continue;
                }

                // 3. Bullet List Items (- , * , • )
                bool isBullet = trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• ");
                if (isBullet)
                {
                    inList = true;
                    string itemText = trimmed.Substring(2).Trim();

                    var dock = new DockPanel
                    {
                        Margin = new Thickness(6, 2, 0, 2)
                    };

                    var bullet = new TextBlock
                    {
                        Text = "•",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = subTextColor,
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    DockPanel.SetDock(bullet, Dock.Left);
                    dock.Children.Add(bullet);

                    var contentTb = new TextBlock
                    {
                        FontSize = 12,
                        Foreground = textColor,
                        LineHeight = 18,
                        TextWrapping = TextWrapping.Wrap
                    };
                    AppendInlineMarkdown(contentTb.Inlines, itemText, codeBgColor, codeFgColor, isDark);
                    dock.Children.Add(contentTb);

                    ChangelogPanel.Children.Add(dock);
                    continue;
                }

                // 4. Regular Paragraph / Text
                var paraTb = new TextBlock
                {
                    FontSize = 12,
                    Foreground = textColor,
                    LineHeight = 18,
                    Margin = new Thickness(0, inList ? 2 : 3, 0, 3),
                    TextWrapping = TextWrapping.Wrap
                };
                AppendInlineMarkdown(paraTb.Inlines, trimmed, codeBgColor, codeFgColor, isDark);
                ChangelogPanel.Children.Add(paraTb);
            }
        }

        private static void AppendInlineMarkdown(InlineCollection inlines, string text, System.Windows.Media.Brush codeBg, System.Windows.Media.Brush codeFg, bool isDark)
        {
            if (string.IsNullOrEmpty(text)) return;

            var regex = new Regex(@"(\*\*(.+?)\*\*|`([^`]+)`)");
            int lastIndex = 0;

            foreach (Match match in regex.Matches(text))
            {
                if (match.Index > lastIndex)
                {
                    string normalText = text.Substring(lastIndex, match.Index - lastIndex);
                    inlines.Add(new Run(normalText));
                }

                string value = match.Value;
                if (value.StartsWith("**") && value.EndsWith("**") && value.Length >= 4)
                {
                    string boldContent = value.Substring(2, value.Length - 4);
                    var boldRun = new Bold();
                    if (boldContent.Contains('`'))
                    {
                        AppendInlineMarkdown(boldRun.Inlines, boldContent, codeBg, codeFg, isDark);
                    }
                    else
                    {
                        boldRun.Inlines.Add(new Run(boldContent));
                    }
                    inlines.Add(boldRun);
                }
                else if (value.StartsWith("`") && value.EndsWith("`") && value.Length >= 2)
                {
                    string codeContent = value.Substring(1, value.Length - 2);

                    var border = new Border
                    {
                        Background = codeBg,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 0, 4, 1),
                        Margin = new Thickness(1, 0, 1, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = codeContent,
                            FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                            FontSize = 11,
                            FontWeight = FontWeights.Normal,
                            Foreground = codeFg
                        }
                    };

                    var container = new InlineUIContainer(border)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    };
                    inlines.Add(container);
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }

        private async Task<GitHubRelease?> FetchReleaseFromGitHubAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            string response = "";
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(8000);
                client.DefaultRequestHeaders.Add("User-Agent", "SnapFind-Updater");
                response = await client.GetStringAsync("https://api.github.com/repos/Loecedas/SnapFind/releases/latest");

                stopwatch.Stop();
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string body = root.GetProperty("body").GetString() ?? "";
                string htmlUrl = root.GetProperty("html_url").GetString() ?? "";

                string downloadUrl = "";
                long size = 0;

                bool isInstalled = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unins000.exe"));
                string targetPattern = isInstalled ? $"SnapFindSetup_{CurrentEdition}_" : $"SnapFindPortable_{CurrentEdition}_";
                string targetExtension = isInstalled ? ".exe" : ".zip";

                if (root.TryGetProperty("assets", out var assetsVal) && assetsVal.ValueKind == JsonValueKind.Array)
                {
                    var assetList = new List<(string Name, string Url, long Size)>();
                    foreach (var asset in assetsVal.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                        long sz = asset.TryGetProperty("size", out var sp) ? sp.GetInt64() : 0;
                        assetList.Add((name, url, sz));
                    }

                    string? matchedName = UpdateHelper.SelectAsset(assetList.Select(a => a.Name), CurrentEdition, isInstalled);
                    if (!string.IsNullOrEmpty(matchedName))
                    {
                        var matched = assetList.FirstOrDefault(a => a.Name.Equals(matchedName, StringComparison.OrdinalIgnoreCase));
                        downloadUrl = matched.Url;
                        size = matched.Size;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = htmlUrl;
                }

                return new GitHubRelease
                {
                    TagName = tagName,
                    Body = body,
                    HtmlUrl = htmlUrl,
                    DownloadUrl = downloadUrl,
                    Size = size,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<GitHubRelease?> FetchReleaseFromGiteeAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(8000);
                client.DefaultRequestHeaders.Add("User-Agent", "SnapFind-Updater");

                string response = await client.GetStringAsync("https://gitee.com/api/v5/repos/loecedas/SnapFind/releases/latest");
                stopwatch.Stop();
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string body = root.GetProperty("body").GetString() ?? "";
                string htmlUrl = "https://gitee.com/loecedas/SnapFind/releases";

                string downloadUrl = "";
                long size = 0;

                bool isInstalled = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unins000.exe"));
                string targetPattern = isInstalled ? $"SnapFindSetup_{CurrentEdition}_" : $"SnapFindPortable_{CurrentEdition}_";
                string targetExtension = isInstalled ? ".exe" : ".zip";

                if (root.TryGetProperty("assets", out var assetsVal) && assetsVal.ValueKind == JsonValueKind.Array)
                {
                    var assetList = new List<(string Name, string Url, long Size)>();
                    foreach (var asset in assetsVal.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                        long sz = asset.TryGetProperty("size", out var sp) ? sp.GetInt64() : 0;
                        assetList.Add((name, url, sz));
                    }

                    string? matchedName = UpdateHelper.SelectAsset(assetList.Select(a => a.Name), CurrentEdition, isInstalled);
                    if (!string.IsNullOrEmpty(matchedName))
                    {
                        var matched = assetList.FirstOrDefault(a => a.Name.Equals(matchedName, StringComparison.OrdinalIgnoreCase));
                        downloadUrl = matched.Url;
                        size = matched.Size;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = htmlUrl;
                }

                return new GitHubRelease
                {
                    TagName = tagName,
                    Body = body,
                    HtmlUrl = htmlUrl,
                    DownloadUrl = downloadUrl,
                    Size = size,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<GitHubRelease?> GetLatestReleaseFromAllSourcesAsync()
        {
            bool isChinaIp = await IsChinaIpAsync();
            if (isChinaIp)
            {
                return await FetchReleaseFromGiteeAsync();
            }
            else
            {
                return await FetchReleaseFromGitHubAsync();
            }
        }

        private async void FetchLatestReleaseAsync()
        {
            SetChangelogMarkdown(Localization.IsEnglish ? "Fetching latest release notes..." : "正在获取最新的更新日志...");
            NotificationTitleText.Text = Localization.IsEnglish ? "Fetching latest version information..." : "正在获取最新版本信息...";

            try
            {
                var release = await GetLatestReleaseFromAllSourcesAsync();
                if (release != null)
                {
                    _releaseInfo = release;
                    _ = LastReleaseCache.SaveAsync(release.TagName, release.Body);
                    DisplayReleaseInfo(_releaseInfo);
                }
                else
                {
                    DisplayOfflineLog();
                }
            }
            catch
            {
                DisplayOfflineLog();
            }
        }

        private void DisplayReleaseInfo(GitHubRelease release)
        {
            Version currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            string cleanTag = release.TagName.TrimStart('v', 'V');
            
            SetChangelogMarkdown(ExtractLocalizedChangelog(release.Body));

            bool isIgnored = !string.IsNullOrEmpty(ConfigManager.Current.IgnoredVersion) && 
                             string.Equals(ConfigManager.Current.IgnoredVersion.TrimStart('v', 'V'), cleanTag, StringComparison.OrdinalIgnoreCase);

            if (Version.TryParse(cleanTag, out Version? latestVer) && latestVer > currentVer && !isIgnored)
            {
                NotificationTitleText.Text = Localization.UpdateTitleNew(release.TagName);
                DownloadButton.Visibility = Visibility.Visible;
                DownloadButton.Content = Localization.BtnDownloadNow;
                IgnoreButton.Visibility = Visibility.Visible;
                IgnoreButton.Content = Localization.BtnIgnoreVersion;
                CancelNotifBtn.Content = Localization.BtnNotNow;
            }
            else
            {
                NotificationTitleText.Text = Localization.UpdateTitleUpToDate;
                DownloadButton.Visibility = Visibility.Collapsed;
                IgnoreButton.Visibility = Visibility.Collapsed;
                CancelNotifBtn.Content = Localization.BtnClose;
            }
        }

        private void DisplayOfflineLog()
        {
            var cached = LastReleaseCache.Load();
            if (cached != null && !string.IsNullOrWhiteSpace(cached.Body))
            {
                NotificationTitleText.Text = Localization.IsEnglish ? $"Latest Release Notes ({cached.TagName} Offline)" : $"最新更新日志 ({cached.TagName} 离线)";
                SetChangelogMarkdown(ExtractLocalizedChangelog(cached.Body));
                return;
            }

            NotificationTitleText.Text = Localization.IsEnglish ? "Latest Release Notes (Offline)" : "最新更新日志 (离线)";
            SetChangelogMarkdown(Localization.IsEnglish
                ? "### 🚀 SnapFind v2.4.9 Changelog\n" +
                  "- **Resizable OCR Result Window**: The recognition result window can now be freely resized by dragging any edge or corner, with responsive layout adapting automatically.\n" +
                  "- **Result Font Size Setting**: Added font size configuration in Settings (Standard 13pt and Extra Large 17pt) with persistent preferences.\n" +
                  "- **Multi-Monitor Mixed DPI Fix**: Completely resolved the excessive scaling bug on secondary monitors under mixed DPI configurations (e.g., 225% primary and 175% secondary).\n" +
                  "- **Seamless 7z SFX Auto-Update**: Upgraded portable packages to 7z SFX self-extracting executables (.7z.exe) with fully automated silent in-place updates, matching Rapid and Paddle editions accurately.\n\n" +
                  "### 🚀 SnapFind v2.4.8 Changelog\n" +
                  "- **Multi-Monitor Cancel Fix**: Resolved an issue where right-clicking empty space to cancel OCR screenshot only dismissed the active monitor's overlay while leaving secondary screens masked.\n" +
                  "- **Unified Capture Cancellation**: Synchronized cancellation triggers (Right Click, Escape, Cancel Button, Empty Click) across all connected displays to instantly clear all overlays and unlock global hotkeys.\n\n" +
                  "### 🚀 SnapFind v2.4.7 Changelog\n" +
                  "- **Eliminated Multi-Clipboard Manager Freezes**: Completely resolved the 3~4s UI freeze during text copy when running alongside clipboard managers (e.g., ropy, zsclip) by adopting direct Win32 native clipboard I/O with millisecond-level backoff.\n" +
                  "- **Enhanced Clipboard Robustness**: Added zero-latency Win32 native format verification and automated high-concurrency unit testing.\n\n" +
                  "### 🚀 SnapFind v2.4.6 Changelog\n" +
                  "- **Packaging & Build System Fixes**: Resolved Inno Setup installer compilation path issues and ensured offline OCR runtime libraries are bundled in portable packages.\n" +
                  "- **Documentation & CI Enhancements**: Aligned README status badges with unified rounded styling; integrated unit testing into automated build workflows.\n\n" +
                  "### 🚀 SnapFind v2.4.4 Changelog\n" +
                  "- **Eliminated Copy/Close UI Freezes**: Asynchronously dispatched memory optimization and working set trimming to background threads.\n" +
                  "- **Refined Clipboard Retries**: Optimized backoff retry intervals in `ClipboardHelper` for faster clipboard operations.\n" +
                  "- **Markdown Changelog Support**: Native rich rendering for version release notes in Control Center.\n\n" +
                  "### 🚀 SnapFind v2.3.9 Changelog\n" +
                  "- **Bilingual Support**: Added full bilingual language switching support (Simplified Chinese & English) in Settings.\n" +
                  "- **Installer Fix**: Fixed English installer translation coverage in setup wizard."
                : "### 🚀 SnapFind v2.4.9 更新日志\n" +
                  "- **OCR 结果窗口自由缩放**: 识别结果编辑窗口支持通过拖动边缘或四角自由放大缩小，文本框与交互按钮自动自适应拉伸布局。\n" +
                  "- **结果字号配置**: 设置面板中新增结果框字号设置，支持“标准字号 (13pt - 默认)”与“特大号 (17pt - 放大两号)”。\n" +
                  "- **跨屏混合 DPI 乘积缩放修复**: 彻底解决主副屏不同 DPI 缩放比例（如主屏 225%、副屏 175%）下副屏截图被双重乘积放大的畸变 Bug。\n" +
                  "- **7z SFX 免解压无感自动更新**: 便携包全面升级为 7z 自解压程序 (.7z.exe)，软件内更新实现后台静默原地解压替换与双版本严格匹配隔离。\n\n" +
                  "### 🚀 SnapFind v2.4.8 更新日志\n" +
                  "- **多显示器取消截图修复**: 彻底解决在多显示器环境下快捷键呼出 OCR 截图时，鼠标右键点击空白处仅取消当前屏幕、其他副屏仍残留全屏遮罩的问题。\n" +
                  "- **全屏幕取消与解锁同步**: 统一全屏遮罩取消事件广播机制（右键空白、ESC 按键、取消按钮及空白点击），保证在任一屏幕取消均立即关闭所有显示器蒙版并释放快捷键占用。\n\n" +
                  "### 🚀 SnapFind v2.4.7 更新日志\n" +
                  "- **消除多剪贴板软件共存卡顿**: 全面重构剪贴板核心通道为纯 Win32 原生调用与毫秒级智能退避重试，彻底解决同时开启 ropy、zsclip 等剪贴板管理器时复制卡顿 3~4 秒的问题。\n" +
                  "- **提升剪贴板高可用与鲁棒性**: 增加零延迟原生假失败校验与高并发多监听器单元测试，杜绝 OLE 锁争用死锁。\n\n" +
                  "### 🚀 SnapFind v2.4.6 更新日志\n" +
                  "- **打包与构建体系修复**: 彻底修复 Inno Setup 安装包编译器路径错误，并确保便携 ZIP 发布包完整携带离线 OCR 原生依赖库。\n" +
                  "- **文档与徽标优化**: 统一 README 状态徽标为标准圆角样式；将单元测试集成至 CI 自动化工作流。\n\n" +
                  "### 🚀 SnapFind v2.4.4 更新日志\n" +
                  "- **消除复制与关闭卡顿**: 优化内存裁剪与垃圾回收调度，放入后台异步线程执行，彻底解决点击复制或快捷键卡顿。\n" +
                  "- **优化剪贴板响应**: 优化 `ClipboardHelper` 重试退避间隔，提升高并发剪贴板监听环境下的写入响应速度。\n" +
                  "- **支持 Markdown 日志渲染**: 控制中心版本页面原生支持 Markdown 语法高亮、代码块、列表与分割线。\n\n" +
                  "### 🚀 SnapFind v2.3.9 更新日志\n" +
                  "- **双语支持**: 设置面板新增界面语言切换选项（支持简体中文与 English）。\n" +
                  "- **安装包修复**: 修复英文安装向导界面残留中文的问题，实现全英文覆盖。");
            
            DownloadButton.Visibility = Visibility.Collapsed;
            IgnoreButton.Visibility = Visibility.Collapsed;
            CancelNotifBtn.Content = Localization.BtnClose;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                CancelDownload();
                return;
            }

            if (_releaseInfo == null || string.IsNullOrEmpty(_releaseInfo.DownloadUrl))
            {
                string fallbackUrl = _releaseInfo?.HtmlUrl ?? "https://github.com/Loecedas/SnapFind/releases/latest";
                MessageBox.Show(Localization.IsEnglish ? "Failed to retrieve download link. Please visit the release page manually." : "获取下载链接失败，请尝试手动访问发布页面获取更新。", Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                try { Process.Start(new ProcessStartInfo(fallbackUrl) { UseShellExecute = true }); } catch { }
                return;
            }

            string fileName = Path.GetFileName(_releaseInfo.DownloadUrl);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"SnapFindUpdate_{_releaseInfo.TagName}.exe";
            }
            string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
            string filePath = Path.Combine(cacheDir, fileName);

            _isDownloading = true;
            DownloadButton.Content = Localization.BtnCancel;
            IgnoreButton.IsEnabled = false;
            CancelNotifBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            ProgressPercentText.Text = Localization.UpdateProgressPercent(0);
            ProgressBytesText.Text = Localization.UpdateConnecting;

            // Disable sidebar radio buttons to prevent switching tabs mid-download
            RadioNotifications.IsEnabled = false;
            RadioSettings.IsEnabled = false;
            RadioAbout.IsEnabled = false;

            await StartDownloadAsync(_releaseInfo.DownloadUrl, filePath);
        }

        private async Task StartDownloadAsync(string url, string filePath)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            string downloadUrl = url;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                if (downloadUrl.Contains("gitee.com"))
                {
                    client.DefaultRequestHeaders.Add("Referer", "https://gitee.com/loecedas/SnapFind/releases");
                }

                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                using var contentStream = await response.Content.ReadAsStreamAsync(token);

                string dir = Path.GetDirectoryName(filePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                byte[] buffer = new byte[8192];
                long totalReadBytes = 0;
                int readBytes;

                while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, readBytes, token);
                    totalReadBytes += readBytes;

                    if (totalBytes.HasValue)
                    {
                        double progress = (double)totalReadBytes / totalBytes.Value * 100;
                        
                        Dispatcher.Invoke(() =>
                        {
                            DownloadProgressBar.Value = progress;
                            ProgressPercentText.Text = Localization.UpdateProgressPercent((int)progress);
                            
                            double readMb = (double)totalReadBytes / (1024 * 1024);
                            double totalMb = (double)totalBytes.Value / (1024 * 1024);
                            ProgressBytesText.Text = $"{readMb:F2} MB / {totalMb:F2} MB";
                        });
                    }
                }

                await fileStream.FlushAsync(token);
                fileStream.Close();

                // Run installer or deploy portable zip, then exit
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        bool isPortable = filePath.EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase) ||
                                          filePath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                          filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

                        if (isPortable)
                        {
                            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                            string cacheDir = Path.Combine(currentDir, "cache");
                            string currentExe = Process.GetCurrentProcess().MainModule?.FileName 
                                ?? Path.Combine(currentDir, "SnapFind.exe");
                            string tempUpdater = Path.Combine(cacheDir, "temp_updater.exe");

                            if (!Directory.Exists(cacheDir))
                            {
                                Directory.CreateDirectory(cacheDir);
                            }

                            File.Copy(currentExe, tempUpdater, true);

                            int currentPid = Process.GetCurrentProcess().Id;
                            string safeDest = currentDir.TrimEnd('\\');
                            string safePackage = filePath.TrimEnd('\\');

                            Process.Start(new ProcessStartInfo(tempUpdater)
                            {
                                Arguments = $"--update-mode --pid {currentPid} --package \"{safePackage}\" --zip \"{safePackage}\" --dest \"{safeDest}\"",
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            Process.Start(new ProcessStartInfo(filePath) 
                            { 
                                Arguments = "/SILENT /SUPPRESSMSGBBOXES /NORESTART /SP-",
                                UseShellExecute = true 
                            });
                        }
                        Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        bool isPortable = filePath.EndsWith(".7z.exe", StringComparison.OrdinalIgnoreCase) ||
                                          filePath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                          filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                        string mode = isPortable
                            ? (Localization.IsEnglish ? "Auto deploy portable package" : "自动部署便携包") 
                            : (Localization.IsEnglish ? "Launch installer" : "启动安装程序");
                        MessageBox.Show($"{mode} {(Localization.IsEnglish ? "failed" : "失败")}: {ex.Message}\n{(Localization.IsEnglish ? "Please operate file manually" : "请手动操作文件")}: {filePath}", Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
                MessageBox.Show($"{Localization.UpdateDownloadFailed}{ex.Message}", Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _isDownloading = false;
                
                Dispatcher.Invoke(() =>
                {
                    ResetUiAfterDownload();
                });
            }
        }

        private void CancelDownload()
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }
        }

        private void ResetUiAfterDownload()
        {
            DownloadButton.Content = Localization.BtnDownloadNow;
            IgnoreButton.IsEnabled = true;
            CancelNotifBtn.IsEnabled = true;
            ProgressPanel.Visibility = Visibility.Collapsed;

            // Re-enable sidebar
            RadioNotifications.IsEnabled = true;
            RadioSettings.IsEnabled = true;
            RadioAbout.IsEnabled = true;
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (_releaseInfo != null)
            {
                ConfigManager.Current.IgnoredVersion = _releaseInfo.TagName;
                ConfigManager.Save();
            }
            Close();
        }

        // --- Window general events ---

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                CancelDownload();
            }
            else
            {
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isDownloading && e.OriginalSource != SearchEngineComboBox && e.OriginalSource != OcrModelComboBox && e.OriginalSource != LanguageComboBox)
            {
                DragMove();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !_isDownloading)
            {
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            OcrHelper.OptimizeMemory();
        }

        // --- Plugins Page Code ---

        private string GetPluginsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "plugins");
        }

        private void BtnImportPlugin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = Localization.IsEnglish ? "Select Plugin Package" : "选择插件安装包",
                    Filter = Localization.PluginsFilterText
                };

                if (dlg.ShowDialog() == true)
                {
                    string pluginsDir = GetPluginsDirectory();
                    if (!Directory.Exists(pluginsDir))
                    {
                        Directory.CreateDirectory(pluginsDir);
                    }

                    string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    string detectedPluginName = Path.GetFileNameWithoutExtension(dlg.FileName);

                    if (ext == ".zip")
                    {
                        string tempExtract = Path.Combine(Path.GetTempPath(), "SnapFind_Plugin_Ext_" + Guid.NewGuid().ToString("N"));
                        try
                        {
                            System.IO.Compression.ZipFile.ExtractToDirectory(dlg.FileName, tempExtract);

                            // 分析解压后的目录结构
                            var topDirs = Directory.GetDirectories(tempExtract);
                            var topFiles = Directory.GetFiles(tempExtract);

                            string targetPluginDir;
                            if (topDirs.Length == 1 && topFiles.Length == 0)
                            {
                                string folderName = Path.GetFileName(topDirs[0]);
                                targetPluginDir = Path.Combine(pluginsDir, folderName);
                                if (Directory.Exists(targetPluginDir))
                                {
                                    try { Directory.Delete(targetPluginDir, true); } catch { }
                                }
                                Directory.CreateDirectory(targetPluginDir);
                                CopyDirectoryRecursive(topDirs[0], targetPluginDir);
                                detectedPluginName = folderName;
                            }
                            else
                            {
                                string cleanName = detectedPluginName.Split('_')[0];
                                targetPluginDir = Path.Combine(pluginsDir, cleanName);
                                if (Directory.Exists(targetPluginDir))
                                {
                                    try { Directory.Delete(targetPluginDir, true); } catch { }
                                }
                                Directory.CreateDirectory(targetPluginDir);
                                CopyDirectoryRecursive(tempExtract, targetPluginDir);
                                detectedPluginName = cleanName;
                            }
                        }
                        finally
                        {
                            try
                            {
                                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        string destFile = Path.Combine(pluginsDir, Path.GetFileName(dlg.FileName));
                        File.Copy(dlg.FileName, destFile, true);
                    }

                    // 自动激活对应插件并生效
                    if (detectedPluginName.Contains("Translation", StringComparison.OrdinalIgnoreCase))
                    {
                        PluginManager.SetPluginActive("SnapFind.Plugin.Translation", true);
                    }
                    else
                    {
                        PluginManager.SetPluginActive(detectedPluginName, true);
                    }

                    if (Application.Current is App app)
                    {
                        app.ReRegisterHotkey();
                        app.UpdateTrayMenuLanguage();
                    }

                    RefreshPluginsList();

                    string hotkeyDesc = ConfigManager.Current.TranslateHotkeyModifiers.Replace(",", " + ") + " + " + ConfigManager.Current.TranslateHotkeyKey;
                    MessageBox.Show(
                        Localization.MsgPluginImportSuccessWithHotkey(detectedPluginName, hotkeyDesc),
                        Localization.TitleInfo,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Localization.PluginsImportFailed + ex.Message, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, targetSubDir);
            }
        }

        private void BtnOpenPluginFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pluginsDir = GetPluginsDirectory();
                if (!Directory.Exists(pluginsDir))
                {
                    Directory.CreateDirectory(pluginsDir);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = pluginsDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRefreshPlugins_Click(object sender, RoutedEventArgs e)
        {
            RefreshPluginsList();
        }

        private void RefreshPluginsList()
        {
            try
            {
                PluginsListPanel.Children.Clear();

                var plugins = PluginManager.GetInstalledPlugins();

                if (plugins.Count == 0)
                {
                    PluginsEmptyStateBorder.Visibility = Visibility.Visible;
                    PluginsStatusText.Text = Localization.PluginsLoadedCount(0, 0);
                    return;
                }

                PluginsEmptyStateBorder.Visibility = Visibility.Collapsed;

                int activeCount = 0;
                foreach (var plugin in plugins)
                {
                    if (plugin.IsActive) activeCount++;
                    AddPluginCard(plugin);
                }

                PluginsStatusText.Text = Localization.PluginsLoadedCount(plugins.Count, activeCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshPluginsList Exception: {ex}");
                PluginsEmptyStateBorder.Visibility = Visibility.Visible;
                PluginsStatusText.Text = Localization.PluginsLoadedCount(0, 0);
            }
            finally
            {
                UpdatePluginDependentUI();
            }
        }

        private void AddPluginCard(PluginInfo plugin)
        {
            var card = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("ThemeInputBg"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("ThemeWindowBorder"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var mainStack = new StackPanel();

            // Top Row: Info + Actions
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: Information Panel
            var leftPanel = new DockPanel { LastChildFill = true };

            // Plugin Icon
            var iconBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(6),
                Background = plugin.IsActive 
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(28, 0, 120, 215))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 128, 134, 139)),
                BorderBrush = plugin.IsActive
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 120, 215))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 128, 134, 139)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 2, 12, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            DockPanel.SetDock(iconBorder, Dock.Left);

            var iconText = new TextBlock
            {
                Text = plugin.Id == "SnapFind.Plugin.Translation" ? "🌐" : "🧩",
                FontSize = 18,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
            leftPanel.Children.Add(iconBorder);

            var infoStack = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Center };

            // Title & Badges
            var titlePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = System.Windows.VerticalAlignment.Center };

            var titleBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(plugin.Name) ? plugin.Id : plugin.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("ThemeText"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            titlePanel.Children.Add(titleBlock);

            // Version Badge
            var verBorder = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("ThemeHoverBg"),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            verBorder.Child = new TextBlock
            {
                Text = $"v{plugin.Version}",
                FontSize = 10,
                Foreground = (System.Windows.Media.Brush)FindResource("ThemeSubText")
            };
            titlePanel.Children.Add(verBorder);

            // Type Badge
            var typeBadgeBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(28, 0, 120, 215)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 0, 120, 215)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            typeBadgeBorder.Child = new TextBlock
            {
                Text = plugin.TypeName,
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215))
            };
            titlePanel.Children.Add(typeBadgeBorder);

            // Status Badge (Active / Disabled)
            var statusBadgeBorder = new Border
            {
                Background = plugin.IsActive 
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 46, 125, 50))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 128, 134, 139)),
                BorderBrush = plugin.IsActive
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 46, 125, 50))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(70, 128, 134, 139)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBadgeBorder.Child = new TextBlock
            {
                Text = plugin.IsActive ? Localization.PluginStatusActive : Localization.PluginStatusDisabled,
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = plugin.IsActive
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 134, 139))
            };
            titlePanel.Children.Add(statusBadgeBorder);

            infoStack.Children.Add(titlePanel);


            leftPanel.Children.Add(infoStack);
            Grid.SetColumn(leftPanel, 0);
            topGrid.Children.Add(leftPanel);

            // Right: Actions Panel
            var actionsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            // 1. Enable / Disable Toggle Button
            var toggleBtn = new System.Windows.Controls.Button
            {
                Content = plugin.IsActive ? Localization.BtnDisablePlugin : Localization.BtnEnablePlugin,
                Style = plugin.IsActive ? (Style)FindResource("ModernButton") : (Style)FindResource("PrimaryButton"),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = Localization.TipPluginToggle(plugin.IsActive)
            };
            toggleBtn.Click += (s, ev) =>
            {
                PluginManager.SetPluginActive(plugin.Id, !plugin.IsActive);
                RefreshPluginsList();
            };
            actionsPanel.Children.Add(toggleBtn);

            // 2. Test Launch Button (if available)
            if (plugin.Id == "SnapFind.Plugin.Translation" && plugin.IsActive)
            {
                var testBtn = new System.Windows.Controls.Button
                {
                    Content = Localization.IsEnglish ? "Test" : "测试",
                    Style = (Style)FindResource("PrimaryButton"),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    ToolTip = Localization.TipPluginTestTranslate
                };
                testBtn.Click += (s, ev) =>
                {
                    PluginManager.ShowTranslationWindow("Hello SnapFind! 这是一个翻译测试。", this);
                };
                actionsPanel.Children.Add(testBtn);
            }

            // 3. Location Button
            var openFolderBtn = new System.Windows.Controls.Button
            {
                Content = Localization.IsEnglish ? "Folder" : "目录",
                Style = (Style)FindResource("ModernButton"),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = Localization.TipPluginOpenFolder
            };
            openFolderBtn.Click += (s, ev) =>
            {
                try
                {
                    string target = Directory.Exists(plugin.Location) ? plugin.Location : (Path.GetDirectoryName(plugin.Location) ?? GetPluginsDirectory());
                    if (!Directory.Exists(target)) target = GetPluginsDirectory();
                    Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                }
                catch { }
            };
            actionsPanel.Children.Add(openFolderBtn);

            // 4. Delete Plugin Button
            var deleteBtn = new System.Windows.Controls.Button
            {
                Content = Localization.BtnDeletePlugin,
                Style = (Style)FindResource("DangerButton"),
                Padding = new Thickness(8, 4, 8, 4),
                ToolTip = Localization.TipPluginDelete
            };
            deleteBtn.Click += (s, ev) =>
            {
                string displayName = string.IsNullOrWhiteSpace(plugin.Name) ? plugin.Id : plugin.Name;
                var confirm = MessageBox.Show(
                    Localization.MsgConfirmDeletePlugin(displayName),
                    Localization.BtnDeletePlugin,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    var (ok, msg) = PluginManager.DeletePlugin(plugin.Id);
                    RefreshPluginsList();
                    if (!ok)
                    {
                        MessageBox.Show(msg, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show(msg, Localization.TitleInfo, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            };
            actionsPanel.Children.Add(deleteBtn);

            Grid.SetColumn(actionsPanel, 1);
            topGrid.Children.Add(actionsPanel);
            mainStack.Children.Add(topGrid);
            // Description (Full card width, left aligned, stable)
            var descBlock = new TextBlock
            {
                Text = plugin.Description,
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource("ThemeSubText"),
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            mainStack.Children.Add(descBlock);

            // Features badges (if any)
            if (plugin.Features != null && plugin.Features.Count > 0)
            {
                var featPanel = new WrapPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                foreach (var feat in plugin.Features)
                {
                    var fBorder = new Border
                    {
                        Background = (System.Windows.Media.Brush)FindResource("ThemeHoverBg"),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 6, 4)
                    };
                    fBorder.Child = new TextBlock
                    {
                        Text = feat,
                        FontSize = 10,
                        Foreground = (System.Windows.Media.Brush)FindResource("ThemeSubText")
                    };
                    featPanel.Children.Add(fBorder);
                }
                mainStack.Children.Add(featPanel);
            }

            // Hotkey Configuration Bar
            if (plugin.HasHotkey || plugin.Id == "SnapFind.Plugin.Translation")
            {
                var hotkeyCard = new Border
                {
                    Background = (System.Windows.Media.Brush)FindResource("ThemeHoverBg"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 10, 0, 0),
                    IsEnabled = plugin.IsActive
                };

                var configStack = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical
                };

                // Row 1: Hotkey Settings
                var hotkeyRow = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Hotkey Label
                var hotkeyLabel = new TextBlock
                {
                    Text = Localization.LabelPluginLaunchHotkey,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = (System.Windows.Media.Brush)FindResource("ThemeText"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                hotkeyRow.Children.Add(hotkeyLabel);

                // Hotkey Input Box
                string curMods = plugin.Id == "SnapFind.Plugin.Translation" ? ConfigManager.Current.TranslateHotkeyModifiers : plugin.HotkeyModifiers;
                string curKey = plugin.Id == "SnapFind.Plugin.Translation" ? ConfigManager.Current.TranslateHotkeyKey : plugin.HotkeyKey;
                if (string.IsNullOrEmpty(curMods)) curMods = "Control,Alt";
                if (string.IsNullOrEmpty(curKey)) curKey = "T";

                var hotkeyBox = new System.Windows.Controls.TextBox
                {
                    Text = curMods.Replace(",", " + ") + " + " + curKey,
                    Width = 120,
                    Height = 26,
                    IsReadOnly = true,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.Medium,
                    FontSize = 11,
                    Background = (System.Windows.Media.Brush)FindResource("ThemeInputBg"),
                    Foreground = (System.Windows.Media.Brush)FindResource("ThemeInputFg"),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("ThemeInputBorder"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4, 2, 4, 2),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    ToolTip = Localization.TipPluginHotkeyRecording,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                hotkeyBox.PreviewKeyDown += (s, ev) =>
                {
                    ev.Handled = true;
                    Key key = ev.Key == Key.System ? ev.SystemKey : ev.Key;
                    if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftAlt || key == Key.RightAlt ||
                        key == Key.LeftShift || key == Key.RightShift ||
                        key == Key.LWin || key == Key.RWin) return;

                    var sb = new StringBuilder();
                    var modifiers = Keyboard.Modifiers;
                    if ((modifiers & ModifierKeys.Control) != 0) sb.Append("Control,");
                    if ((modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt,");
                    if ((modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift,");
                    if ((modifiers & ModifierKeys.Windows) != 0) sb.Append("Windows,");

                    string modStr = sb.ToString().TrimEnd(',');
                    if (string.IsNullOrEmpty(modStr))
                    {
                        MessageBox.Show(Localization.MsgModifierRequired, Localization.TitleModifierRequired, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (plugin.Id == "SnapFind.Plugin.Translation")
                    {
                        ConfigManager.Current.TranslateHotkeyModifiers = modStr;
                        ConfigManager.Current.TranslateHotkeyKey = key.ToString();
                        ConfigManager.Save();
                        if (Application.Current is App app)
                        {
                            app.ReRegisterHotkey();
                        }
                    }

                    hotkeyBox.Text = modStr.Replace(",", " + ") + " + " + key.ToString();
                };
                hotkeyRow.Children.Add(hotkeyBox);

                // Reset Hotkey Button
                var resetBtn = new System.Windows.Controls.Button
                {
                    Content = Localization.BtnResetHotkey,
                    Style = (Style)FindResource("ModernButton"),
                    Padding = new Thickness(6, 1, 6, 1),
                    Height = 24,
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = Localization.TipResetDefaultHotkey
                };
                resetBtn.Click += (s, ev) =>
                {
                    if (plugin.Id == "SnapFind.Plugin.Translation")
                    {
                        ConfigManager.Current.TranslateHotkeyModifiers = "Control,Alt";
                        ConfigManager.Current.TranslateHotkeyKey = "T";
                        ConfigManager.Save();
                        if (Application.Current is App app)
                        {
                            app.ReRegisterHotkey();
                        }
                    }
                    hotkeyBox.Text = "Control + Alt + T";
                };
                hotkeyRow.Children.Add(resetBtn);

                // Tip text
                var tipText = new TextBlock
                {
                    Text = Localization.TipPluginHotkeyRecording,
                    FontSize = 10,
                    Foreground = (System.Windows.Media.Brush)FindResource("ThemeSubText"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                hotkeyRow.Children.Add(tipText);

                configStack.Children.Add(hotkeyRow);

                // Row 2: Target Language Option (for Translation Plugin)
                if (plugin.Id == "SnapFind.Plugin.Translation")
                {
                    var langRow = new StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 0)
                    };

                    var langLabel = new TextBlock
                    {
                        Text = Localization.LabelDefaultTargetLang,
                        FontSize = 11,
                        Foreground = (System.Windows.Media.Brush)FindResource("ThemeText"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    langRow.Children.Add(langLabel);

                    var langCombo = new System.Windows.Controls.ComboBox
                    {
                        Height = 26,
                        Width = 120,
                        Background = (System.Windows.Media.Brush)FindResource("ThemeInputBg"),
                        Foreground = (System.Windows.Media.Brush)FindResource("ThemeInputFg"),
                        BorderBrush = (System.Windows.Media.Brush)FindResource("ThemeInputBorder"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(6, 0, 0, 0),
                        FontSize = 11,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                        Template = (ControlTemplate)FindResource("ThemeComboBoxTemplate"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    langCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = Localization.LangChinese, Tag = "zh-CN" });
                    langCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = Localization.LangTraditionalChinese, Tag = "zh-TW" });
                    langCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "English", Tag = "en" });
                    langCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = Localization.LangJapanese, Tag = "ja" });
                    langCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = Localization.LangKorean, Tag = "ko" });

                    string curTarget = ConfigManager.Current.DefaultTargetLanguage;
                    int selIdx = 0;
                    for (int i = 0; i < langCombo.Items.Count; i++)
                    {
                        if ((langCombo.Items[i] as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() == curTarget)
                        {
                            selIdx = i;
                            break;
                        }
                    }
                    langCombo.SelectedIndex = selIdx;
                    langCombo.SelectionChanged += (s, ev) =>
                    {
                        if (langCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                        {
                            ConfigManager.Current.DefaultTargetLanguage = item.Tag?.ToString() ?? "zh-CN";
                            ConfigManager.Save();
                        }
                    };
                    langRow.Children.Add(langCombo);

                    configStack.Children.Add(langRow);
                }

                hotkeyCard.Child = configStack;
                mainStack.Children.Add(hotkeyCard);
            }

            card.Child = mainStack;
            PluginsListPanel.Children.Add(card);
        }
    }
}


