using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Interop;

namespace PixOcrSearch
{
    public partial class App : Application
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;

        private static Mutex? _mutex;
        private NotifyIcon? _notifyIcon;
        private Window? _dummyHookWindow;
        private HotkeyHelper? _hotkeyHelper;
        private IntPtr _trayIconHandle = IntPtr.Zero;
        private bool _isCapturing = false;
        private readonly List<ScreenshotWindow> _screenshotWindows = new List<ScreenshotWindow>();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Set DLL search directory immediately to resolve native DLL dependencies
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libsDir = System.IO.Path.Combine(baseDir, "libs");
                SetDllDirectory(libsDir);
            }
            catch { }

            // 1. Single Instance check
            _mutex = new Mutex(true, "SnapFind-SingleInstance-Mutex-Key", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("SnapFind 已经在后台运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Load Configuration
            ConfigManager.Load();

            // Apply System Theme
            ApplyTheme();

            // Self-heal autostart path if enabled
            if (ConfigManager.Current.StartWithWindows)
            {
                HealAutoStartRegistry();
            }

            // Trim process working set on startup asynchronously after WPF loop starts
            Dispatcher.BeginInvoke(new Action(() => {
                OcrHelper.OptimizeMemory();
            }), System.Windows.Threading.DispatcherPriority.Background);

            // 2. Initialize a dummy hidden window to receive hotkey window messages
            _dummyHookWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Visibility = Visibility.Hidden
            };
            // Force creation of handle in background silently without showing or flashing it
            var helper = new WindowInteropHelper(_dummyHookWindow);
            helper.EnsureHandle();
            MainWindow = null;

            // 3. Initialize Tray Icon
            InitializeTrayIcon();

            // 4. Register Global Hotkey
            _hotkeyHelper = new HotkeyHelper();
            RegisterHotkey();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "SnapFind - 截图 OCR 搜索";
            
            // Generate icon
            Icon icon = CreateDynamicTrayIcon();
            _notifyIcon.Icon = icon;
            _notifyIcon.Visible = true;

            // Handle Right Click for native context menu
            _notifyIcon.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    ShowNativeContextMenu();
                }
            };

            // Double click to trigger screenshot
            _notifyIcon.DoubleClick += (s, e) => StartScreenshot();
        }

        private void ShowNativeContextMenu()
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            // Add standard items without emojis, completely plain text
            AppendMenu(hMenu, MF_STRING, 1, "截图 OCR 搜索");
            AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(hMenu, MF_STRING, 2, "设置");
            AppendMenu(hMenu, MF_STRING, 3, "退出");

            // We need a window handle to own the popup menu and receive commands.
            // We use our _dummyHookWindow handle.
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_dummyHookWindow).Handle;

            // Make sure the dummy window is active so the menu closes when clicking outside
            SetForegroundWindow(hwnd);

            GetCursorPos(out POINT pt);

            // Show the native context menu
            int cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, hwnd, IntPtr.Zero);
            
            DestroyMenu(hMenu);

            PostMessage(hwnd, 0x0000, IntPtr.Zero, IntPtr.Zero); // WM_NULL = 0x0000

            // Handle native menu click commands
            switch (cmd)
            {
                case 1:
                    StartScreenshot();
                    break;
                case 2:
                    OpenSettings();
                    break;
                case 3:
                    ExitApp();
                    break;
            }
        }

        private Icon CreateDynamicTrayIcon()
        {
            try
            {
                var sri = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
                if (sri != null)
                {
                    using (System.IO.Stream stream = sri.Stream)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch { }

            try
            {
                string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    Icon? icon = Icon.ExtractAssociatedIcon(exePath!);
                    if (icon != null)
                    {
                        return icon;
                    }
                }
            }
            catch { }

            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Draw circular background (accent blue)
                using var brush = new SolidBrush(Color.FromArgb(0, 120, 215));
                g.FillEllipse(brush, 2, 2, 28, 28);
                
                // Draw magnifying glass
                using var pen = new Pen(Color.White, 3);
                g.DrawEllipse(pen, 8, 8, 12, 12);
                g.DrawLine(pen, 17, 17, 24, 24);
            }
            _trayIconHandle = bmp.GetHicon();
            return Icon.FromHandle(_trayIconHandle);
        }

        private void RegisterHotkey()
        {
            if (_dummyHookWindow == null || _hotkeyHelper == null) return;

            string mods = ConfigManager.Current.HotkeyModifiers;
            string key = ConfigManager.Current.HotkeyKey;

            bool success = _hotkeyHelper.Register(_dummyHookWindow, mods, key, StartScreenshot);
            if (!success)
            {
                MessageBox.Show($"无法注册全局快捷键: {mods} + {key}。请在设置中修改，避免与其他程序冲突。", "快捷键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ReRegisterHotkey()
        {
            _hotkeyHelper?.Unregister();
            RegisterHotkey();
        }

        private void StartScreenshot()
        {
            if (_isCapturing) return;
            _isCapturing = true;

            // Start loading PaddleOCR in the background while user is selecting screenshot area
            OcrHelper.StartInitialize();

            _screenshotWindows.Clear();

            try
            {
                var screens = Screen.AllScreens;
                foreach (var screen in screens)
                {
                    var screenWin = new ScreenshotWindow(screen);
                    _screenshotWindows.Add(screenWin);

                    screenWin.OnScreenshotCompleted += async (bitmap, rect) =>
                    {
                        // Immediately close all screens to restore normal desktop view
                        CloseAllScreenshotWindows();

                        // Debug save to verify cropping alignment
                        try
                        {
                            string debugPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "debug_crop.png");
                            bitmap.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        catch { }

                        // Run OCR in background
                        string text = await OcrHelper.RecognizeTextAsync(bitmap);
                        bitmap.Dispose(); // Memory-only, release immediately

                        // Spawn edit window
                        var editWin = new EditWindow(text, rect);
                        editWin.Show();
                        editWin.Activate();
                    };

                    screenWin.Closed += (s, e) =>
                    {
                        // Clean up tracking when windows close
                        // If all screenshot windows are closed (or user canceled), reset capture lock
                        if (_screenshotWindows.All(w => !w.IsVisible))
                        {
                            _isCapturing = false;
                        }
                    };
                }

                // Show all windows simultaneously
                foreach (var win in _screenshotWindows)
                {
                    win.Show();
                    win.Activate();
                    win.Focus(); // Force keyboard focus to ensure Esc key cancels immediately
                }
            }
            catch (Exception ex)
            {
                CloseAllScreenshotWindows();
                MessageBox.Show("启动截图失败:\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseAllScreenshotWindows()
        {
            var windowsToClose = _screenshotWindows.ToList();
            _screenshotWindows.Clear();
            _isCapturing = false;

            foreach (var win in windowsToClose)
            {
                try
                {
                    win.Close();
                }
                catch { }
            }
        }

        private void OpenSettings()
        {
            // Ensure single SettingsWindow
            foreach (Window win in Current.Windows)
            {
                if (win is SettingsWindow)
                {
                    win.Activate();
                    return;
                }
            }

            var settingsWin = new SettingsWindow();
            settingsWin.Show();
        }

        private void ExitApp()
        {
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose OCR Engine
            OcrHelper.Dispose();

            // Unregister hotkey
            _hotkeyHelper?.Dispose();

            // Dispose Tray Icon
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            // Free Icon handle
            if (_trayIconHandle != IntPtr.Zero)
            {
                DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
            }

            // Close dummy window
            _dummyHookWindow?.Close();

            // Release Mutex
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();

            base.OnExit(e);
        }

        public static bool IsWindowsDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? value = key.GetValue("AppsUseLightTheme");
                    if (value is int intVal)
                    {
                        return intVal == 0;
                    }
                }
            }
            catch { }
            return false; // Default to Light Mode
        }

        public static void ApplyTheme()
        {
            bool isDark = IsWindowsDarkMode();
            var resources = Current.Resources;

            if (isDark)
            {
                resources["ThemeWindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 34)); // #1E1E22
                resources["ThemeWindowBorder"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)); // #3F3F46
                resources["ThemeText"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)); // #FFFFFF
                resources["ThemeSubText"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 142, 147)); // #8E8E93
                resources["ThemeInputBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 18, 21)); // #121215
                resources["ThemeInputFg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 241, 241)); // #F1F1F1
                resources["ThemeInputBorder"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 50)); // #2D2D32
                resources["ThemeBtnBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)); // #2D2D30
                resources["ThemeBtnFg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 225, 225)); // #E1E1E1
                resources["ThemeBtnBorder"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)); // #3F3F46
                resources["ThemeHoverBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)); // #3F3F46
            }
            else
            {
                resources["ThemeWindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 243, 243)); // #F3F3F3
                resources["ThemeWindowBorder"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 210, 210)); // #D2D2D2
                resources["ThemeText"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)); // #000000
                resources["ThemeSubText"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)); // #666666
                resources["ThemeInputBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)); // #FFFFFF
                resources["ThemeInputFg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)); // #000000
                resources["ThemeInputBorder"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204)); // #CCCCCC
                resources["ThemeBtnBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 225, 225)); // #E1E1E1
                resources["ThemeBtnFg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)); // #000000
                resources["ThemeBtnBorder"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204)); // #CCCCCC
                resources["ThemeHoverBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230)); // #E6E6E6
            }
        }

        private static void HealAutoStartRegistry()
        {
            try
            {
                string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    // Auto-heal path in registry in case the app was moved or renamed
                    key.SetValue("SnapFind", $"\"{exePath}\"");
                }
            }
            catch { }
        }
    }
}
