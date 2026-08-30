using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace PixOcrSearch
{
    /// <summary>
    /// 高可用工业级剪贴板安全操作辅助类
    /// 具备：STA 线程调度、WPF OLE 智能退避重试、Win32 原生 API 兜底、假失败静默过滤
    /// </summary>
    public static class ClipboardHelper
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
        /// 安全将文本复制到系统剪贴板（全流程四重防护）
        /// </summary>
        /// <param name="text">待复制的文本</param>
        /// <returns>是否成功复制</returns>
        public static bool SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Clear();
            }

            // 1. 确保在 STA / UI 线程中调度
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(() => SetText(text));
                }
                catch
                {
                    // Fallback to direct thread if dispatcher fails
                }
            }

            // 2. 第一重防线：WPF SetDataObject 智能退避重试 (最多重试 10 次)
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return true;
                }
                catch
                {
                    Thread.Sleep(15 + i * 5); // 15ms, 20ms, 25ms, ... 逐步退避让出 CPU
                }
            }

            // 3. 第二重防线：Win32 原生 P/Invoke 兜底（直接写入操作系统共享内存，绕过 COM OLE 层）
            if (SetTextNative(text))
            {
                return true;
            }

            // 4. 第三重防线：假失败校验（检查剪贴板当前内容是否已是目标文本）
            // 若系统监听工具在 WPF 收尾阶段抢占抛错，但实际数据已写入，则静默判定为成功
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == text)
                {
                    return true;
                }
            }
            catch
            {
                // 静默忽略读取异常
            }

            return false;
        }

        /// <summary>
        /// 安全清空剪贴板
        /// </summary>
        public static bool Clear()
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(Clear);
                }
                catch { }
            }

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.Clear();
                    return true;
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }

            // Native Clear fallback
            for (int i = 0; i < 5; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        EmptyClipboard();
                        return true;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(10);
            }

            return false;
        }

        /// <summary>
        /// 使用原生 Win32 API 写入 Unicode 文本
        /// </summary>
        private static bool SetTextNative(string text)
        {
            if (text == null) return false;

            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
            UIntPtr size = new UIntPtr((uint)bytes.Length);

            for (int i = 0; i < 10; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    IntPtr hGlobal = IntPtr.Zero;
                    try
                    {
                        EmptyClipboard();

                        hGlobal = GlobalAlloc(GMEM_MOVEABLE, size);
                        if (hGlobal == IntPtr.Zero)
                        {
                            return false;
                        }

                        IntPtr target = GlobalLock(hGlobal);
                        if (target == IntPtr.Zero)
                        {
                            GlobalFree(hGlobal);
                            return false;
                        }

                        Marshal.Copy(bytes, 0, target, bytes.Length);
                        GlobalUnlock(hGlobal);

                        if (SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero)
                        {
                            // 成功将所有权移交给剪贴板系统，不调用 GlobalFree
                            hGlobal = IntPtr.Zero;
                            return true;
                        }
                    }
                    finally
                    {
                        if (hGlobal != IntPtr.Zero)
                        {
                            GlobalFree(hGlobal);
                        }
                        CloseClipboard();
                    }
                }
                Thread.Sleep(15 + i * 5);
            }

            return false;
        }
    }
}
