using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 基于 Windows 原生 Win32 API 的离线快速繁简中文转换器
    /// </summary>
    public static class ChineseConverter
    {
        private const int LCMAP_SIMPLIFIED_CHINESE = 0x02000000;
        private const int LCMAP_TRADITIONAL_CHINESE = 0x04000000;
        private const int LOCALE_SYSTEM_DEFAULT = 0x0800;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int LCMapString(int Locale, int dwMapFlags, string lpSrcStr, int cchSrc, StringBuilder lpDestStr, int cchDest);

        /// <summary>
        /// 转换为繁体中文
        /// </summary>
        public static string ToTraditional(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            int len = LCMapString(LOCALE_SYSTEM_DEFAULT, LCMAP_TRADITIONAL_CHINESE, source, -1, null!, 0);
            if (len <= 0) return source;

            StringBuilder target = new StringBuilder(len);
            int result = LCMapString(LOCALE_SYSTEM_DEFAULT, LCMAP_TRADITIONAL_CHINESE, source, -1, target, len);
            return result > 0 ? target.ToString().TrimEnd('\0') : source;
        }

        /// <summary>
        /// 转换为简体中文
        /// </summary>
        public static string ToSimplified(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;

            int len = LCMapString(LOCALE_SYSTEM_DEFAULT, LCMAP_SIMPLIFIED_CHINESE, source, -1, null!, 0);
            if (len <= 0) return source;

            StringBuilder target = new StringBuilder(len);
            int result = LCMapString(LOCALE_SYSTEM_DEFAULT, LCMAP_SIMPLIFIED_CHINESE, source, -1, target, len);
            return result > 0 ? target.ToString().TrimEnd('\0') : source;
        }
    }
}
