using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace PixOcrSearch
{
    public class HotkeyHelper : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_SCREENSHOT = 9000;
        private const int HOTKEY_ID_CONTROLPANEL = 9001;

        private IntPtr _hWnd;
        private HwndSource? _hwndSource;
        private Action? _onScreenshotPressed;
        private Action? _onControlPanelPressed;

        public bool Register(Window window, 
                            string modifiersStr, string keyStr, Action onScreenshotPressed,
                            string cpModifiersStr, string cpKeyStr, Action onControlPanelPressed)
        {
            _onScreenshotPressed = onScreenshotPressed;
            _onControlPanelPressed = onControlPanelPressed;

            if (_hWnd == IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(window);
                _hWnd = helper.EnsureHandle();

                _hwndSource = HwndSource.FromHwnd(_hWnd);
                _hwndSource?.AddHook(WndProc);
            }
            else
            {
                // Just unregister the old hotkeys, keeping window and hook alive
                UnregisterHotKey(_hWnd, HOTKEY_ID_SCREENSHOT);
                UnregisterHotKey(_hWnd, HOTKEY_ID_CONTROLPANEL);
            }

            uint modifiers = ParseModifiers(modifiersStr);
            uint vk = ParseKey(keyStr);
            bool ok1 = RegisterHotKey(_hWnd, HOTKEY_ID_SCREENSHOT, modifiers, vk);

            uint cpModifiers = ParseModifiers(cpModifiersStr);
            uint cpVk = ParseKey(cpKeyStr);
            bool ok2 = RegisterHotKey(_hWnd, HOTKEY_ID_CONTROLPANEL, cpModifiers, cpVk);

            return ok1 && ok2;
        }

        public void Unregister()
        {
            if (_hWnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hWnd, HOTKEY_ID_SCREENSHOT);
                UnregisterHotKey(_hWnd, HOTKEY_ID_CONTROLPANEL);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_SCREENSHOT)
                {
                    _onScreenshotPressed?.Invoke();
                    handled = true;
                }
                else if (id == HOTKEY_ID_CONTROLPANEL)
                {
                    _onControlPanelPressed?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private uint ParseModifiers(string modifiersStr)
        {
            uint modifiers = 0;
            var parts = modifiersStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim().ToLowerInvariant();
                if (trimmed == "alt") modifiers |= 0x0001;
                else if (trimmed == "control" || trimmed == "ctrl") modifiers |= 0x0002;
                else if (trimmed == "shift") modifiers |= 0x0004;
                else if (trimmed == "win" || trimmed == "windows") modifiers |= 0x0008;
            }
            return modifiers;
        }

        private uint ParseKey(string keyStr)
        {
            if (Enum.TryParse<Key>(keyStr, true, out var key))
            {
                return (uint)KeyInterop.VirtualKeyFromKey(key);
            }
            return 0x53; // Default 'S' key
        }

        public void Dispose()
        {
            Unregister();
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
            _hWnd = IntPtr.Zero;
        }
    }
}
