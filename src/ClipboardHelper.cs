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
    /// 具备：Win32 原生高性能极速通道（首选）、微秒/毫秒级智能退避重试、原生假失败校验、STA 线程安全调度
    /// 彻底消除 WPF Clipboard.SetDataObject 在多剪贴板监听软件（如 ropy、zsclip）并发抢占下的 3~4 秒阻塞卡顿
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        // 智能毫秒级退避序列，总耗时极短（<220ms），在争用释放瞬间即可精准写入
        private static readonly int[] RetryBackoffs = { 3, 6, 12, 20, 30, 40, 50, 60 };

        /// <summary>
        /// 安全将文本复制到系统剪贴板（首选 Win32 原生极速通道，毫秒级响应）
        /// </summary>
        /// <param name="text">待复制的文本</param>
        /// <returns>是否成功复制</returns>
        public static bool SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Clear();
            }

            // 1. 首选通道：Win32 原生 P/Invoke 写入
            // 直接操作操作系统剪贴板共享内存，绕过繁重的 WPF COM/OLE 层与 OleFlushClipboard，
            // 避免触发 WPF 内部写死的 10*100ms 同步阻塞重试，在高并发剪贴板监听器（如 ropy, zsclip）下通常 0~1ms 完成
            if (SetTextNative(text))
            {
                return true;
            }

            // 2. 次选校验：Win32 原生假失败校验
            // 若在收尾阶段有监听工具抢先读取，但目标文本实际上已写入成功，直接判定为成功
            try
            {
                if (GetTextNative() == text)
                {
                    return true;
                }
            }
            catch
            {
                // 静默忽略
            }

            // 3. 最终兜底：托管环境极简写入（仅在 STA 线程单次尝试，不加耗时循环）
            try
            {
                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() => SetTextManagedFallback(text));
                }
                return SetTextManagedFallback(text);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 使用原生 Win32 API 写入 Unicode 文本（首选核心引擎）
        /// </summary>
        private static bool SetTextNative(string text)
        {
            if (text == null) return false;

            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
            UIntPtr size = new UIntPtr((uint)bytes.Length);

            for (int i = 0; i < RetryBackoffs.Length; i++)
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

                Thread.Sleep(RetryBackoffs[i]);
            }

            return false;
        }

        /// <summary>
        /// 使用原生 Win32 API 读取当前剪贴板 Unicode 文本（零 OLE 开销）
        /// </summary>
        public static string? GetTextNative()
        {
            for (int i = 0; i < 3; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                        if (hData != IntPtr.Zero)
                        {
                            IntPtr pText = GlobalLock(hData);
                            if (pText != IntPtr.Zero)
                            {
                                try
                                {
                                    return Marshal.PtrToStringUni(pText);
                                }
                                finally
                                {
                                    GlobalUnlock(hData);
                                }
                            }
                        }
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(5);
            }
            return null;
        }

        /// <summary>
        /// 安全清空剪贴板（优先原生 Win32，零延迟）
        /// </summary>
        public static bool Clear()
        {
            // 优先原生 Win32 清空
            for (int i = 0; i < RetryBackoffs.Length; i++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        if (EmptyClipboard())
                        {
                            return true;
                        }
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(RetryBackoffs[i]);
            }

            // 托管轻量兜底
            try
            {
                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        Clipboard.Clear();
                        return true;
                    });
                }
                Clipboard.Clear();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 托管兜底单次写入（不使用 copy=true 强制 OleFlushClipboard，避免争夺死锁）
        /// </summary>
        private static bool SetTextManagedFallback(string text)
        {
            try
            {
                // copy=false 仅注册数据对象，不触发耗时的 OleFlushClipboard
                Clipboard.SetDataObject(text, false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
