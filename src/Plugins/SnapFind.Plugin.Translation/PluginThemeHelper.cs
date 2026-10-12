using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 主题支持助手，支持跟随 Windows 深色/浅色模式或自定义注入
    /// </summary>
    public static class PluginThemeHelper
    {
        public static bool IsWindowsDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0;
                    }
                }
            }
            catch { }
            return true; // 默认深色
        }

        public static void ApplyTheme(ResourceDictionary resources, bool? forceDarkMode = null)
        {
            if (resources == null) return;

            bool isDark = forceDarkMode ?? IsWindowsDarkMode();

            if (isDark)
            {
                resources["ThemeWindowBg"] = new SolidColorBrush(Color.FromRgb(30, 30, 34)); // #1E1E22
                resources["ThemeWindowBorder"] = new SolidColorBrush(Color.FromRgb(63, 63, 70)); // #3F3F46
                resources["ThemeText"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // #FFFFFF
                resources["ThemeSubText"] = new SolidColorBrush(Color.FromRgb(142, 142, 147)); // #8E8E93
                resources["ThemeInputBg"] = new SolidColorBrush(Color.FromRgb(18, 18, 21)); // #121215
                resources["ThemeInputFg"] = new SolidColorBrush(Color.FromRgb(241, 241, 241)); // #F1F1F1
                resources["ThemeInputBorder"] = new SolidColorBrush(Color.FromRgb(45, 45, 50)); // #2D2D32
                resources["ThemeBtnBg"] = new SolidColorBrush(Color.FromRgb(45, 45, 48)); // #2D2D30
                resources["ThemeBtnFg"] = new SolidColorBrush(Color.FromRgb(225, 225, 225)); // #E1E1E1
                resources["ThemeBtnBorder"] = new SolidColorBrush(Color.FromRgb(63, 63, 70)); // #3F3F46
                resources["ThemeHoverBg"] = new SolidColorBrush(Color.FromRgb(63, 63, 70)); // #3F3F46
            }
            else
            {
                resources["ThemeWindowBg"] = new SolidColorBrush(Color.FromRgb(243, 243, 243)); // #F3F3F3
                resources["ThemeWindowBorder"] = new SolidColorBrush(Color.FromRgb(210, 210, 210)); // #D2D2D2
                resources["ThemeText"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // #000000
                resources["ThemeSubText"] = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // #666666
                resources["ThemeInputBg"] = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // #FFFFFF
                resources["ThemeInputFg"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // #000000
                resources["ThemeInputBorder"] = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                resources["ThemeBtnBg"] = new SolidColorBrush(Color.FromRgb(225, 225, 225)); // #E1E1E1
                resources["ThemeBtnFg"] = new SolidColorBrush(Color.FromRgb(0, 0, 0)); // #000000
                resources["ThemeBtnBorder"] = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                resources["ThemeHoverBg"] = new SolidColorBrush(Color.FromRgb(230, 230, 230)); // #E6E6E6
            }
        }
    }
}
