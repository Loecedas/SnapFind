using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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
        private readonly List<SelectedRegionItem> _multiSessionRegions = new List<SelectedRegionItem>();
        private MultiSessionBarWindow? _multiSessionBarWindow;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 优先拦截更新部署模式，以防启动 WPF 和 Mutex 实例冲突
            if (e.Args.Length > 0 && e.Args[0] == "--update-mode")
            {
                RunUpdateDeploymentFlow(e.Args);
                return;
            }

            // Set DLL search directory immediately to resolve native DLL dependencies
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string libsDir = System.IO.Path.Combine(baseDir, "libs");
                SetDllDirectory(libsDir);
            }
            catch { }

            // 异步自动清理上一次更新遗留的临时更新器、PowerShell 脚本和临时解压目录
            Task.Run(() =>
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string cacheDir = System.IO.Path.Combine(baseDir, "cache");
                    string updaterExe = System.IO.Path.Combine(cacheDir, "temp_updater.exe");
                    
                    // 等待最多 5 秒，直到 temp_updater.exe 进程完全退出并释放文件锁
                    for (int i = 0; i < 10; i++)
                    {
                        if (!System.IO.File.Exists(updaterExe)) break;
                        try
                        {
                            System.IO.File.Delete(updaterExe);
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(500);
                        }
                    }

                    string scriptPath = System.IO.Path.Combine(cacheDir, "update.ps1");
                    if (System.IO.File.Exists(scriptPath))
                    {
                        try { System.IO.File.Delete(scriptPath); } catch { }
                    }
                    string logPath = System.IO.Path.Combine(cacheDir, "update.log");
                    if (System.IO.File.Exists(logPath))
                    {
                        try { System.IO.File.Delete(logPath); } catch { }
                    }
                    string tempDir = System.IO.Path.Combine(cacheDir, "temp_update");
                    if (System.IO.Directory.Exists(tempDir))
                    {
                        try { System.IO.Directory.Delete(tempDir, true); } catch { }
                    }

                    // 清理安装版更新完成后遗留在 cache 目录下的安装包 (SnapFindSetup_*.exe)
                    if (System.IO.Directory.Exists(cacheDir))
                    {
                        string[] setupFiles = System.IO.Directory.GetFiles(cacheDir, "SnapFindSetup_*.exe");
                        foreach (string setupFile in setupFiles)
                        {
                            try { System.IO.File.Delete(setupFile); } catch { }
                        }
                    }
                }
                catch { }
            });

            // 1. Single Instance check
            _mutex = new Mutex(true, "SnapFind-SingleInstance-Mutex-Key", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show(Localization.MsgAlreadyRunning, Localization.TitleInfo, MessageBoxButton.OK, MessageBoxImage.Information);
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

            // 5. Warm up OCR engine in background to make the very first screenshot instant
            Task.Run(() => OcrHelper.StartInitialize());
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = Localization.TrayToolTip;
            
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

        public void UpdateTrayMenuLanguage()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = Localization.TrayToolTip;
            }
        }

        private void ShowNativeContextMenu()
        {
            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            // Add standard items without emojis, completely plain text
            AppendMenu(hMenu, MF_STRING, 1, Localization.TrayMenuScreenshot);
            AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(hMenu, MF_STRING, 2, Localization.TrayMenuControlPanel);
            AppendMenu(hMenu, MF_STRING, 3, Localization.TrayMenuExit);

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

            string cpMods = ConfigManager.Current.ControlPanelHotkeyModifiers;
            string cpKey = ConfigManager.Current.ControlPanelHotkeyKey;

            bool success = _hotkeyHelper.Register(_dummyHookWindow, 
                mods, key, StartScreenshot,
                cpMods, cpKey, ToggleSettings);

            if (!success)
            {
                MessageBox.Show(Localization.MsgHotkeyRegisterFailed(mods, key, cpMods, cpKey), Localization.TitleHotkeyConflict, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ReRegisterHotkey()
        {
            _hotkeyHelper?.Unregister();
            RegisterHotkey();
        }

        private string GetScreenshotHotkeyDisplay()
        {
            string mods = ConfigManager.Current.HotkeyModifiers.Replace(",", "+");
            string key = ConfigManager.Current.HotkeyKey;
            return $"{mods}+{key}";
        }

        private void ClearMultiSession()
        {
            foreach (var item in _multiSessionRegions)
            {
                item.Bitmap?.Dispose();
            }
            _multiSessionRegions.Clear();
        }

        private async void ProcessMultiRegionsAndOpenEditWindow(List<SelectedRegionItem> regions)
        {
            if (regions == null || regions.Count == 0) return;

            var textList = new List<string>();
            Rect boundingUnion = regions[0].AbsoluteRect;

            foreach (var region in regions)
            {
                boundingUnion.Union(region.AbsoluteRect);
                string text = await OcrHelper.RecognizeTextAsync(region.Bitmap);
                region.Bitmap.Dispose();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textList.Add(text.Trim());
                }
            }

            string mergedText = string.Join("\n", textList);

            // Spawn edit window
            var editWin = new EditWindow(mergedText, boundingUnion);
            editWin.Show();
            editWin.Activate();
        }

        private void StartScreenshot()
        {
            if (_isCapturing) return;
            _isCapturing = true;

            // Close floating session bar if active
            _multiSessionBarWindow?.Close();
            _multiSessionBarWindow = null;

            _screenshotWindows.Clear();

            try
            {
                var screens = Screen.AllScreens;
                foreach (var screen in screens)
                {
                    var screenWin = new ScreenshotWindow(screen, _multiSessionRegions.Count);
                    _screenshotWindows.Add(screenWin);

                    screenWin.OnScreenshotCompleted += async (bitmap, rect) =>
                    {
                        // Immediately close all screens to restore normal desktop view
                        CloseAllScreenshotWindows();
                        ClearMultiSession();

                        // Run OCR in background
                        string text = await OcrHelper.RecognizeTextAsync(bitmap);
                        bitmap.Dispose(); // Memory-only, release immediately

                        // Spawn edit window
                        var editWin = new EditWindow(text, rect);
                        editWin.Show();
                        editWin.Activate();
                    };

                    screenWin.OnMultiScreenshotCompleted += (regions) =>
                    {
                        // Immediately close all screens to restore normal desktop view
                        CloseAllScreenshotWindows();
                        _multiSessionBarWindow?.Close();
                        _multiSessionBarWindow = null;

                        var allRegions = new List<SelectedRegionItem>(_multiSessionRegions);
                        if (regions != null && regions.Count > 0)
                        {
                            allRegions.AddRange(regions);
                        }
                        _multiSessionRegions.Clear();

                        ProcessMultiRegionsAndOpenEditWindow(allRegions);
                    };

                    screenWin.OnSwitchWindowRequested += (regions) =>
                    {
                        // Close screenshot overlay to let user interact with other windows
                        CloseAllScreenshotWindows();

                        if (regions != null && regions.Count > 0)
                        {
                            _multiSessionRegions.AddRange(regions);
                        }

                        if (_multiSessionRegions.Count > 0)
                        {
                            _multiSessionBarWindow = new MultiSessionBarWindow(_multiSessionRegions.Count, GetScreenshotHotkeyDisplay());
                            _multiSessionBarWindow.OnContinueRequested += () =>
                            {
                                _multiSessionBarWindow?.Close();
                                _multiSessionBarWindow = null;
                                StartScreenshot();
                            };
                            _multiSessionBarWindow.OnCompleteRequested += () =>
                            {
                                _multiSessionBarWindow?.Close();
                                _multiSessionBarWindow = null;
                                var all = new List<SelectedRegionItem>(_multiSessionRegions);
                                _multiSessionRegions.Clear();
                                ProcessMultiRegionsAndOpenEditWindow(all);
                            };
                            _multiSessionBarWindow.OnCancelRequested += () =>
                            {
                                _multiSessionBarWindow?.Close();
                                _multiSessionBarWindow = null;
                                ClearMultiSession();
                            };
                            _multiSessionBarWindow.Show();
                        }
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

                // Start loading PaddleOCR in the background AFTER the screenshot windows are shown and rendered.
                // This prevents disk and CPU initialization contention from stalling the UI thread's window rendering.
                Task.Run(() => OcrHelper.StartInitialize());
            }
            catch (Exception ex)
            {
                CloseAllScreenshotWindows();
                ClearMultiSession();
                MessageBox.Show(Localization.MsgScreenshotFailed + ex.Message, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
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
            foreach (Window win in Current.Windows)
            {
                if (win is SettingsWindow activeWin)
                {
                    activeWin.SelectTab("settings");
                    activeWin.Activate();
                    return;
                }
            }

            var settingsWin = new SettingsWindow("settings");
            settingsWin.Show();
        }

        private void ToggleSettings()
        {
            foreach (Window win in Current.Windows)
            {
                if (win is SettingsWindow activeWin)
                {
                    activeWin.Close();
                    return;
                }
            }

            var settingsWin = new SettingsWindow("settings");
            settingsWin.Show();
            settingsWin.Activate();
        }

        public void OpenAbout()
        {
            foreach (Window win in Current.Windows)
            {
                if (win is SettingsWindow activeWin)
                {
                    activeWin.SelectTab("about");
                    activeWin.Activate();
                    return;
                }
            }

            var settingsWin = new SettingsWindow("about");
            settingsWin.Show();
        }

        public void OpenNotifications(GitHubRelease? releaseInfo = null)
        {
            foreach (Window win in Current.Windows)
            {
                if (win is SettingsWindow activeWin)
                {
                    if (releaseInfo != null)
                    {
                        activeWin.SetReleaseInfo(releaseInfo);
                    }
                    activeWin.SelectTab("notifications");
                    activeWin.Activate();
                    return;
                }
            }

            var settingsWin = new SettingsWindow("notifications", releaseInfo);
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

        private void RunUpdateDeploymentFlow(string[] args)
        {
            int pid = 0;
            string zipPath = "";
            string destDir = "";

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--pid" && i + 1 < args.Length) pid = int.Parse(args[++i]);
                else if (args[i] == "--zip" && i + 1 < args.Length) zipPath = args[++i];
                else if (args[i] == "--dest" && i + 1 < args.Length) destDir = args[++i];
            }

            try
            {
                if (pid > 0)
                {
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById(pid);
                        process.WaitForExit(10000);
                    }
                    catch { }
                }

                string tempExtractDir = Path.Combine(destDir, "cache", "temp_update");
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, true);
                }
                Directory.CreateDirectory(tempExtractDir);

                // 解压缩 zip 包
                ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

                // 寻找源目录
                string sourceDir = tempExtractDir;
                string nestedDir = Path.Combine(tempExtractDir, "SnapFind");
                if (File.Exists(Path.Combine(nestedDir, "SnapFind.exe")))
                {
                    sourceDir = nestedDir;
                }

                // 递归覆盖拷贝
                CopyDirectoryRecursive(sourceDir, destDir);

                try { Directory.Delete(tempExtractDir, true); } catch { }
                try { File.Delete(zipPath); } catch { }

                // 重启新版本
                string mainExe = Path.Combine(destDir, "SnapFind.exe");
                System.Diagnostics.Process.Start(new ProcessStartInfo(mainExe) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SnapFind 自动更新覆盖失败：{ex.Message}\n\n请尝试手动解压 cache 目录下的压缩包进行覆盖。", "更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
                try
                {
                    string mainExe = Path.Combine(destDir, "SnapFind.exe");
                    System.Diagnostics.Process.Start(new ProcessStartInfo(mainExe) { UseShellExecute = true });
                }
                catch { }
            }
            finally
            {
                Shutdown();
            }
        }

        private void CopyDirectoryRecursive(string source, string target)
        {
            foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(source, target));
            }

            foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
            {
                string destFile = newPath.Replace(source, target);
                bool copied = false;
                string lastError = "";
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        File.Copy(newPath, destFile, true);
                        copied = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        System.Threading.Thread.Sleep(500);
                    }
                }
                if (!copied)
                {
                    throw new IOException($"无法写入文件，可能被占用：{destFile}。详细错误：{lastError}");
                }
            }
        }
    }

    public class GitHubRelease
    {
        public string TagName { get; set; } = "";
        public string Body { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public long Size { get; set; }
        public long DurationMs { get; set; }
    }
}
