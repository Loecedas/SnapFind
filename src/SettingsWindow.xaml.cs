using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

            _recordedModifiers = ConfigManager.Current.HotkeyModifiers;
            _recordedKey = ConfigManager.Current.HotkeyKey;
            _recordedControlPanelModifiers = ConfigManager.Current.ControlPanelHotkeyModifiers;
            _recordedControlPanelKey = ConfigManager.Current.ControlPanelHotkeyKey;
            
            UpdateHotkeyTextBoxDisplay();
            UpdateControlPanelHotkeyTextBoxDisplay();
            AutoStartCheckBox.IsChecked = ConfigManager.Current.StartWithWindows;
            AutoCopyCheckBox.IsChecked = ConfigManager.Current.AutoCopyToClipboard;
        }

        public void ApplyLocalization()
        {
            WindowTitleText.Text = Localization.ControlCenterTitle;
            RadioSettings.Content = Localization.TabSettings;
            RadioNotifications.Content = Localization.TabUpdates;
            RadioAbout.Content = Localization.TabAbout;

            LabelLanguageText.Text = Localization.LabelLanguage;
            LabelSearchEngineText.Text = Localization.LabelSearchEngine;
            LabelOcrModelText.Text = Localization.LabelOcrModel;
            LabelScreenshotHotkeyText.Text = Localization.LabelScreenshotHotkey;
            LabelScreenshotHotkeyText.ToolTip = Localization.HotkeyTooltip;
            LabelControlPanelHotkeyText.Text = Localization.LabelControlPanelHotkey;
            LabelControlPanelHotkeyText.ToolTip = Localization.HotkeyTooltip;

            AutoStartCheckBox.Content = Localization.CheckAutoStart;
            AutoCopyCheckBox.Content = Localization.CheckAutoCopy;
            SettingsCancelButton.Content = Localization.BtnCancel;
            SettingsSaveButton.Content = Localization.BtnSave;

            // Search Engine items
            if (SearchEngineComboBox.Items.Count >= 2)
            {
                if (SearchEngineComboBox.Items[0] is ComboBoxItem item0) item0.Content = Localization.SearchGoogle;
                if (SearchEngineComboBox.Items[1] is ComboBoxItem item1) item1.Content = Localization.SearchBing;
            }

            // OCR Model items
            foreach (ComboBoxItem item in OcrModelComboBox.Items)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (tag == "PP-OCRv6_tiny") item.Content = Localization.OcrModelTiny;
                else if (tag == "PP-OCRv6_small") item.Content = Localization.OcrModelSmall;
            }

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
            PanelAbout.Visibility = tabName == "about" ? Visibility.Visible : Visibility.Collapsed;

            // Highlight Sidebar Radio
            if (tabName == "notifications") RadioNotifications.IsChecked = true;
            else if (tabName == "settings") RadioSettings.IsChecked = true;
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
            
            bool autoStart = AutoStartCheckBox.IsChecked == true;
            ConfigManager.Current.StartWithWindows = autoStart;
            ConfigManager.Current.AutoCopyToClipboard = AutoCopyCheckBox.IsChecked == true;

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

            content = content.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<p>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<ul>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</ul>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<li>", "• ", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<b>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</b>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<strong>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</strong>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<h3>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</h3>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<h2>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</h2>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<h1>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</h1>", "\n", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("<code>", "", StringComparison.OrdinalIgnoreCase);
            content = content.Replace("</code>", "", StringComparison.OrdinalIgnoreCase);

            content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", "");

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    trimmed = trimmed.TrimStart('#').Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        sb.AppendLine(trimmed);
                    }
                }
            }

            return sb.ToString().Trim();
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
                string targetPattern = isInstalled ? "SnapFindSetup_" : "SnapFindPortable_";
                string targetExtension = isInstalled ? ".exe" : ".zip";

                if (root.TryGetProperty("assets", out var assetsVal) && assetsVal.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsVal.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.StartsWith(targetPattern, StringComparison.OrdinalIgnoreCase) && name.EndsWith(targetExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            size = asset.GetProperty("size").GetInt64();
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        foreach (var asset in assetsVal.EnumerateArray())
                        {
                            string name = asset.GetProperty("name").GetString() ?? "";
                            if (name.EndsWith(targetExtension, StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                                size = asset.GetProperty("size").GetInt64();
                                break;
                            }
                        }
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
                string targetPattern = isInstalled ? "SnapFindSetup_" : "SnapFindPortable_";
                string targetExtension = isInstalled ? ".exe" : ".zip";

                if (root.TryGetProperty("assets", out var assetsVal) && assetsVal.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsVal.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.StartsWith(targetPattern, StringComparison.OrdinalIgnoreCase) && name.EndsWith(targetExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            if (asset.TryGetProperty("size", out var sizeProp))
                            {
                                size = sizeProp.GetInt64();
                            }
                            break;
                        }
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
            ChangelogTextBlock.Text = Localization.IsEnglish ? "Fetching latest release notes..." : "正在获取最新的更新日志...";
            NotificationTitleText.Text = Localization.IsEnglish ? "Fetching latest version information..." : "正在获取最新版本信息...";

            try
            {
                var release = await GetLatestReleaseFromAllSourcesAsync();
                if (release != null)
                {
                    _releaseInfo = release;
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
            
            ChangelogTextBlock.Text = ExtractLocalizedChangelog(release.Body);

            if (Version.TryParse(cleanTag, out Version? latestVer) && latestVer > currentVer)
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
            NotificationTitleText.Text = Localization.IsEnglish ? "Latest Release Notes (Offline)" : "最新更新日志 (离线)";
            ChangelogTextBlock.Text = Localization.IsEnglish
                ? "v2.3.9 Changelog\n" +
                  "• Added full bilingual language switching support (Simplified Chinese & English) in Settings.\n" +
                  "• Fixed English installer translation coverage in setup wizard.\n\n" +
                  "v2.3.0 Changelog\n" +
                  "• Control Center window now supports drag resizing and native taskbar minimize animations.\n" +
                  "• Removed bright blue border around EditWindow, replaced with adaptive system theme border.\n\n" +
                  "v2.2.0 Changelog\n" +
                  "• Unified Control Center navigation sidebar.\n" +
                  "• Added global hotkey for Control Center (default Ctrl+Alt+C).\n" +
                  "• Streamlined tray context menu."
                : "v2.3.9 更新日志\n" +
                  "• 设置面板新增界面语言切换选项（支持简体中文与 English）。\n" +
                  "• 修复英文安装向导界面残留中文的问题，实现全英文覆盖。\n\n" +
                  "v2.3.0 更新日志\n" +
                  "• 控制中心支持拖拽拉伸调整窗口尺寸，并原生适配任务栏最小化缩回操作。\n" +
                  "• 移除了结果展示窗口（EditWindow）醒目的蓝色边框线，改为系统主题自适应灰色。\n\n" +
                  "v2.2.0 更新日志\n" +
                  "• 控制面板整合侧栏切换，移除了冗余的独立关于和通知窗口。\n" +
                  "• 新增全局控制中心呼出热键（默认 Ctrl+Alt+C），自带快捷键冲突防重校验机制。\n" +
                  "• 精简系统托盘右键选项为：截图 OCR 搜索、控制面板、退出。";
            
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

            string ext = Path.GetExtension(_releaseInfo.DownloadUrl);
            if (string.IsNullOrEmpty(ext)) ext = ".exe";
            string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
            string prefix = ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ? "SnapFindPortable_" : "SnapFindSetup_";
            string fileName = $"{prefix}{_releaseInfo.TagName}{ext}";
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
                        if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
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
                            string safeZip = filePath.TrimEnd('\\');

                            Process.Start(new ProcessStartInfo(tempUpdater)
                            {
                                Arguments = $"--update-mode --pid {currentPid} --zip \"{safeZip}\" --dest \"{safeDest}\"",
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
                        string mode = filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) 
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
    }
}
