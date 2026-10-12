using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// 独立的高可用剪贴板安全写入助手，具有重试机制和 Native API 保底
    /// </summary>
    public static class PluginClipboard
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        /// <summary>
        /// 安全将文本写入剪贴板
        /// </summary>
        public static bool SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            // 1. 优先尝试标准 WPF Clipboard（多轮快速退避）
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (COMException)
                {
                    Thread.Sleep(30 * (i + 1));
                }
                catch
                {
                    break;
                }
            }

            // 2. 原生 Win32 API 兜底
            return SetTextWin32(text);
        }

        private static bool SetTextWin32(string text)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();

                        int bytesCount = (text.Length + 1) * 2;
                        IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytesCount);
                        if (hGlobal == IntPtr.Zero)
                        {
                            return false;
                        }

                        IntPtr pTarget = GlobalLock(hGlobal);
                        if (pTarget == IntPtr.Zero)
                        {
                            GlobalFree(hGlobal);
                            return false;
                        }

                        Marshal.Copy(text.ToCharArray(), 0, pTarget, text.Length);
                        Marshal.WriteInt16(pTarget + (text.Length * 2), 0); // null-terminator
                        GlobalUnlock(hGlobal);

                        if (SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero)
                        {
                            return true;
                        }
                        else
                        {
                            GlobalFree(hGlobal);
                        }
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(40 * (attempt + 1));
            }
            return false;
        }
    }
}
