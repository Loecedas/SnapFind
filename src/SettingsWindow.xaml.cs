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

            // Setup Version Text in About Page
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            AboutVersionText.Text = $"版本: v{version.Major}.{version.Minor}.{version.Build}";

            // Select default tab
            SelectTab(_currentActiveTab);
        }

        private void LoadSettingsConfig()
        {
            string currentUrl = ConfigManager.Current.SearchEngineUrl;
            bool found = false;
            foreach (System.Windows.Controls.ComboBoxItem item in SearchEngineComboBox.Items)
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

            var itemsToRemove = new System.Collections.Generic.List<System.Windows.Controls.ComboBoxItem>();
            foreach (System.Windows.Controls.ComboBoxItem item in OcrModelComboBox.Items)
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
            foreach (System.Windows.Controls.ComboBoxItem item in OcrModelComboBox.Items)
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
                HotkeyTextBox.Text = "无快捷键";
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
                ControlPanelHotkeyTextBox.Text = "无快捷键";
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
                MessageBox.Show("快捷键必须包含修饰键 (如 Ctrl, Alt, Shift 等)！以防键盘普通按键被系统全局拦截冲突。", "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_recordedControlPanelModifiers))
            {
                MessageBox.Show("控制面板快捷键必须包含修饰键 (如 Ctrl, Alt, Shift 等)！以防键盘普通按键被系统全局拦截冲突。", "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_recordedModifiers == _recordedControlPanelModifiers && _recordedKey == _recordedControlPanelKey)
            {
                MessageBox.Show("截图快捷键与控制面板快捷键不能相同！请设置不同的快捷键组合以防冲突。", "快捷键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string url = "https://www.google.com/search?q=";
            if (SearchEngineComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                url = selectedItem.Tag?.ToString() ?? "https://www.google.com/search?q=";
            }

            string model = "PP-OCRv6_small";
            if (OcrModelComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedModelItem)
            {
                model = selectedModelItem.Tag?.ToString() ?? "PP-OCRv6_small";
            }

            bool modelChanged = ConfigManager.Current.OcrModel != model;

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
                MessageBox.Show("设置开机启动失败:\n" + ex.Message, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            // 使用 Cloudflare 全球 Anycast 接口检测出口 IP 归属地
            // 此接口会跟随系统代理（VPN）走：开了全局VPN返回境外IP，未开返回真实国内IP
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(3000);
                client.DefaultRequestHeaders.Add("User-Agent", "SnapFind-Updater");
                string result = await client.GetStringAsync("https://cloudflare.com/cdn-cgi/trace");
                // 查找 loc= 字段，格式如 loc=CN 或 loc=US
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

            // Cloudflare 探测失败时用备用接口 ip-api.com
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

            // 两个接口均失败时，保守默认为中国用户（走 Gitee 更稳妥）
            return true;
        }

        private async void AboutCheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            AboutCheckUpdateButton.IsEnabled = false;
            AboutCheckUpdateButton.Content = "正在检查...";
            AboutStatusText.Visibility = Visibility.Visible;
            AboutStatusText.Text = "正在检查更新...";

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
                        MessageBox.Show($"您当前已是最新版本 (v{currentVer.Major}.{currentVer.Minor}.{currentVer.Build})，无需更新。", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                AboutStatusText.Visibility = Visibility.Collapsed;
                MessageBox.Show("未能在 GitHub 或 Gitee 检测到发布版本，请检查您的网络连接或稍后再试。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AboutStatusText.Visibility = Visibility.Collapsed;
                MessageBox.Show($"检查更新发生异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AboutCheckUpdateButton.IsEnabled = true;
                AboutCheckUpdateButton.Content = "检查更新";
            }
        }

        private static string ExtractChineseChangelog(string rawBody)
        {
            if (string.IsNullOrEmpty(rawBody)) return "";

            string cnContent = rawBody;
            
            int startDetails = rawBody.IndexOf("<details", StringComparison.OrdinalIgnoreCase);
            if (startDetails != -1)
            {
                int endDetails = rawBody.IndexOf("</details>", startDetails, StringComparison.OrdinalIgnoreCase);
                if (endDetails != -1)
                {
                    cnContent = rawBody.Substring(startDetails, endDetails - startDetails);
                }
            }

            int startSummary = cnContent.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
            if (startSummary != -1)
            {
                int endSummary = cnContent.IndexOf("</summary>", startSummary, StringComparison.OrdinalIgnoreCase);
                if (endSummary != -1)
                {
                    cnContent = cnContent.Remove(startSummary, (endSummary + 10) - startSummary);
                }
            }

            cnContent = cnContent.Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<p>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<ul>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</ul>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<li>", "• ", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<b>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</b>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<strong>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</strong>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<h3>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</h3>", "\n", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<h2>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</h2>", "\n", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<h1>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</h1>", "\n", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("<code>", "", StringComparison.OrdinalIgnoreCase);
            cnContent = cnContent.Replace("</code>", "", StringComparison.OrdinalIgnoreCase);

            cnContent = System.Text.RegularExpressions.Regex.Replace(cnContent, @"<[^>]+>", "");

            var lines = cnContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
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

                bool isInstalled = System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unins000.exe"));
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
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

                bool isInstalled = System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unins000.exe"));
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
            // 通过出口 IP 精准判断：中国 IP 走 Gitee，境外 IP 走 GitHub
            // Cloudflare trace 接口会跟随 VPN 走，开了全局 VPN 自动切 GitHub
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
            ChangelogTextBlock.Text = "正在获取最新的更新日志...";
            NotificationTitleText.Text = "正在获取最新版本信息...";

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
            
            ChangelogTextBlock.Text = ExtractChineseChangelog(release.Body);

            if (Version.TryParse(cleanTag, out Version? latestVer) && latestVer > currentVer)
            {
                NotificationTitleText.Text = $"发现新版本: {release.TagName}";
                DownloadButton.Visibility = Visibility.Visible;
                IgnoreButton.Visibility = Visibility.Visible;
                CancelNotifBtn.Content = "暂不更新";
            }
            else
            {
                NotificationTitleText.Text = $"当前已是最新版本 (v{currentVer.Major}.{currentVer.Minor}.{currentVer.Build})";
                DownloadButton.Visibility = Visibility.Collapsed;
                IgnoreButton.Visibility = Visibility.Collapsed;
                CancelNotifBtn.Content = "关闭";
            }
        }

        private void DisplayOfflineLog()
        {
            NotificationTitleText.Text = "最新更新日志 (离线)";
            ChangelogTextBlock.Text = "v2.3.0 更新日志\n" +
                               "• 控制中心支持拖拽拉伸调整窗口尺寸，并原生适配任务栏最小化缩回操作。\n" +
                               "• 移除了结果展示窗口（EditWindow）醒目的蓝色边框线，改为系统主题自适应灰色。\n\n" +
                               "v2.2.0 更新日志\n" +
                               "• 控制面板整合侧栏切换，移除了冗余的独立关于和通知窗口。\n" +
                               "• 新增全局控制中心呼出热键（默认 Ctrl+Alt+C），自带快捷键冲突防重校验机制。\n" +
                               "• 精简系统托盘右键选项为：截图 OCR 搜索、控制面板、退出。\n" +
                               "• 引入 6 像素极细半透明 Fluent 滚动条，悬停时自动渐显。\n\n" +
                               "v2.1.0 更新日志\n" +
                               "• 移除了 OCR 结果窗口底部“设置”、“复制”和“搜索”按钮的 Emoji 图标。\n" +
                               "• 支持检查更新及本地直接下载更新包。";
            
            DownloadButton.Visibility = Visibility.Collapsed;
            IgnoreButton.Visibility = Visibility.Collapsed;
            CancelNotifBtn.Content = "关闭";
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
                MessageBox.Show("获取下载链接失败，请尝试手动访问发布页面获取更新。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fallbackUrl) { UseShellExecute = true }); } catch { }
                return;
            }



            string ext = Path.GetExtension(_releaseInfo.DownloadUrl);
            if (string.IsNullOrEmpty(ext)) ext = ".exe";
            string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
            string prefix = ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ? "SnapFindPortable_" : "SnapFindSetup_";
            string fileName = $"{prefix}{_releaseInfo.TagName}{ext}";
            string filePath = Path.Combine(cacheDir, fileName);

            _isDownloading = true;
            DownloadButton.Content = "取消下载";
            IgnoreButton.IsEnabled = false;
            CancelNotifBtn.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            ProgressPercentText.Text = "下载进度: 0%";
            ProgressBytesText.Text = "正在连接服务器...";

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

            // 如果是中国国内用户，且链接包含 github.com，直接采用国内高速 CDN 代理，不再进行无意义的官方直连测速以防止因 Header 误判卡在慢速通道
            try
            {
                using var client = new HttpClient();
                // 伪装成高拟真浏览器请求头以确保通过 Gitee 防盗链/防机器人人机验证检测
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
                            ProgressPercentText.Text = $"下载进度: {(int)progress}%";
                            
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
                            // Portable ZIP Update Flow via Self-Copy
                            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                            string cacheDir = Path.Combine(currentDir, "cache");
                            string currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                                ?? Path.Combine(currentDir, "SnapFind.exe");
                            string tempUpdater = Path.Combine(cacheDir, "temp_updater.exe");

                            if (!Directory.Exists(cacheDir))
                            {
                                Directory.CreateDirectory(cacheDir);
                            }

                            // 强力复制主程序自身为临时更新器
                            File.Copy(currentExe, tempUpdater, true);

                            int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;

                            string safeDest = currentDir.TrimEnd('\\');
                            string safeZip = filePath.TrimEnd('\\');

                            // 启动临时更新器并传入更新任务的控制参数
                            Process.Start(new ProcessStartInfo(tempUpdater)
                            {
                                Arguments = $"--update-mode --pid {currentPid} --zip \"{safeZip}\" --dest \"{safeDest}\"",
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            // Installer Exe Flow
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
                        string mode = filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? "自动部署便携包" : "启动安装程序";
                        MessageBox.Show($"{mode}失败: {ex.Message}\n请手动操作文件: {filePath}", "更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"下载更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            DownloadButton.Content = "立即下载";
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
            if (!_isDownloading && e.OriginalSource != SearchEngineComboBox && e.OriginalSource != OcrModelComboBox)
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
