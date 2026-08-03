using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PixOcrSearch
{
    public partial class SettingsWindow : Window
    {
        private string _recordedModifiers = "";
        private string _recordedKey = "";

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Dynamically refresh system theme resources before rendering
            App.ApplyTheme();

            // Load current configs
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
                // Default to Google if not matched
                SearchEngineComboBox.SelectedIndex = 0;
            }

            _recordedModifiers = ConfigManager.Current.HotkeyModifiers;
            _recordedKey = ConfigManager.Current.HotkeyKey;
            
            UpdateHotkeyTextBoxDisplay();
            AutoStartCheckBox.IsChecked = ConfigManager.Current.StartWithWindows;
        }

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
            // Handle System key (Alt is sent as System)
            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            // Skip if only modifier is pressed
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            // Build modifier string
            var sb = new StringBuilder();
            var modifiers = Keyboard.Modifiers;
            if ((modifiers & ModifierKeys.Control) != 0) sb.Append("Control,");
            if ((modifiers & ModifierKeys.Alt) != 0) sb.Append("Alt,");
            if ((modifiers & ModifierKeys.Shift) != 0) sb.Append("Shift,");
            if ((modifiers & ModifierKeys.Windows) != 0) sb.Append("Windows,");

            // Strip trailing comma
            string modStr = sb.ToString();
            if (modStr.EndsWith(","))
            {
                modStr = modStr.Substring(0, modStr.Length - 1);
            }

            _recordedModifiers = modStr;
            _recordedKey = key.ToString();

            UpdateHotkeyTextBoxDisplay();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_recordedModifiers))
            {
                MessageBox.Show("快捷键必须包含修饰键 (如 Ctrl, Alt, Shift 等)！以防键盘普通按键被系统全局拦截冲突。", "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string url = "https://www.google.com/search?q=";
            if (SearchEngineComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                url = selectedItem.Tag?.ToString() ?? "https://www.google.com/search?q=";
            }

            ConfigManager.Current.SearchEngineUrl = url;
            ConfigManager.Current.HotkeyModifiers = _recordedModifiers;
            ConfigManager.Current.HotkeyKey = _recordedKey;
            
            bool autoStart = AutoStartCheckBox.IsChecked == true;
            ConfigManager.Current.StartWithWindows = autoStart;

            ConfigManager.Save();

            // Handle registry for autostart
            SetAutoStart(autoStart);

            // Trigger Hotkey Re-registration in App
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
                // Dynamically fetch the current running executable name (supports renaming)
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource != SearchEngineComboBox)
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
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            OcrHelper.OptimizeMemory();
        }
    }
}
