using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;

using System.Runtime.InteropServices;

namespace PixOcrSearch
{
    public partial class EditWindow : Window
    {
        private readonly string _initialText;
        private readonly Rect _selectionRect;

        public EditWindow(string text, Rect selectionRect)
        {
            InitializeComponent();
            _initialText = text;
            _selectionRect = selectionRect;

            OcrTextBox.Text = _initialText;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Dynamically refresh system theme resources before rendering
            App.ApplyTheme();

            // Position the window near the selection
            PositionWindow();

            // Focus text box and select all/place caret at the end
            OcrTextBox.Focus();
            OcrTextBox.SelectAll();
        }

        private void PositionWindow()
        {
            double w = Width;
            double h = ActualHeight > 0 ? ActualHeight : 220;

            // Find the screen containing the center of the selection
            double centerX = _selectionRect.Left + _selectionRect.Width / 2;
            double centerY = _selectionRect.Top + _selectionRect.Height / 2;

            // Retrieve screen metrics for the current monitor using Windows Forms Screen class
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)centerX, (int)centerY));

            // Get DPI of the monitor containing the selection
            double scaleX = 1.0;
            double scaleY = 1.0;
            try
            {
                POINT pt = new POINT((int)centerX, (int)centerY);
                IntPtr hMonitor = MonitorFromPoint(pt, 2); // MONITOR_DEFAULTTONEAREST
                if (hMonitor != IntPtr.Zero)
                {
                    uint dpiX, dpiY;
                    if (GetDpiForMonitor(hMonitor, 0, out dpiX, out dpiY) == 0) // MDT_EFFECTIVE_DPI = 0
                    {
                        scaleX = dpiX / 96.0;
                        scaleY = dpiY / 96.0;
                    }
                }
            }
            catch { }

            // Screen bounds in WPF pixels
            double screenLeft = screen.Bounds.X / scaleX;
            double screenTop = screen.Bounds.Y / scaleY;
            double screenWidth = screen.Bounds.Width / scaleX;
            double screenHeight = screen.Bounds.Height / scaleY;

            // Position at the exact center of this monitor
            Left = screenLeft + (screenWidth - w) / 2;
            Top = screenTop + (screenHeight - h) / 2;
        }

        private void OcrTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Enter key searches; Shift+Enter inserts newline
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    e.Handled = true;
                    PerformSearch();
                }
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                HandleCopyAndClose();
            }
        }

        private void HandleCopyAndClose()
        {
            try
            {
                string textToCopy = OcrTextBox.SelectionLength > 0 ? OcrTextBox.SelectedText : OcrTextBox.Text;
                if (!string.IsNullOrEmpty(textToCopy))
                {
                    Clipboard.SetText(textToCopy);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("复制到剪贴板失败:\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Close();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            string query = OcrTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                try
                {
                    // Replace newlines with spaces for a clean single-line search query
                    string cleanQuery = query.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                    string escapedQuery = Uri.EscapeDataString(cleanQuery);
                    string url = ConfigManager.Current.SearchEngineUrl + escapedQuery;

                    // Open in default browser
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法启动默认浏览器进行搜索:\n" + ex.Message, "搜索失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            Close();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = OcrTextBox.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("复制到剪贴板失败:\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the window from anywhere outside the input area
            if (e.OriginalSource != OcrTextBox)
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
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                HandleCopyAndClose();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            OcrHelper.OptimizeMemory();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, uint dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
