using System;
using System.IO;
using System.Text.Json;

namespace PixOcrSearch
{
    public class AppConfig
    {
        public string SearchEngineUrl { get; set; } = "https://www.google.com/search?q=";
        public string HotkeyModifiers { get; set; } = "Control,Alt"; // Comma-separated modifiers
        public string HotkeyKey { get; set; } = "S";                // Main key
        public bool StartWithWindows { get; set; } = false;
        public string OcrModel { get; set; } = "PP-OCRv6_tiny";
        public string IgnoredVersion { get; set; } = "";
        public string ControlPanelHotkeyModifiers { get; set; } = "Control,Alt";
        public string ControlPanelHotkeyKey { get; set; } = "C";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "config.json");
        public static AppConfig Current { get; private set; } = new AppConfig();

        static ConfigManager()
        {
            try
            {
                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }
            }
            catch { }

            Load();
        }

        private static readonly object ConfigLock = new object();

        private static bool CheckRegistryAutoStart()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("SnapFind") != null;
            }
            catch
            {
                return false;
            }
        }

        public static void Load()
        {
            lock (ConfigLock)
            {
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        string json = File.ReadAllText(ConfigPath);
                        var config = JsonSerializer.Deserialize<AppConfig>(json);
                        if (config != null)
                        {
                            Current = config;
                            Current.StartWithWindows = CheckRegistryAutoStart();
                            return;
                        }
                    }
                }
                catch
                {
                    // Fallback to default config on error
                }
                Current = new AppConfig();
                Current.StartWithWindows = CheckRegistryAutoStart();
            }
        }

        public static void Save()
        {
            lock (ConfigLock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                    string tempPath = ConfigPath + ".tmp";
                    File.WriteAllText(tempPath, json);
                    if (File.Exists(ConfigPath))
                    {
                        File.Delete(ConfigPath);
                    }
                    File.Move(tempPath, ConfigPath);
                }
                catch
                {
                    // Ignore save error
                }
            }
        }
    }
}
