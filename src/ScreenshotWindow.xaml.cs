using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Rectangle = System.Drawing.Rectangle;

namespace PixOcrSearch
{
    public class SelectedRegionItem
    {
        public Bitmap Bitmap { get; set; } = null!;
        public Rect AbsoluteRect { get; set; }
        public Rect LocalRect { get; set; }
    }

    public partial class ScreenshotWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, uint dpiType, out uint dpiX, out uint dpiY);

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

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

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        private readonly System.Windows.Forms.Screen _screen;
        private Bitmap? _screenSnapshot;
        private Point _startPoint;
        private bool _isDragging;
        private double _scaleX = 1.0;
        private double _scaleY = 1.0;

        private readonly List<SelectedRegionItem> _selectedRegions = new List<SelectedRegionItem>();
        private readonly int _initialRegionOffset = 0;
        private bool _isCompleted = false;

        // Event triggered when single selection is confirmed
        public event Action<Bitmap, Rect>? OnScreenshotCompleted;

        // Event triggered when multi-region selection is confirmed
        public event Action<List<SelectedRegionItem>>? OnMultiScreenshotCompleted;

        // Event triggered when user requests switching to another window/screen
        public event Action<List<SelectedRegionItem>>? OnSwitchWindowRequested;

        public ScreenshotWindow(System.Windows.Forms.Screen screen, int initialRegionOffset = 0)
        {
            InitializeComponent();
            _screen = screen;
            _initialRegionOffset = initialRegionOffset;

            // Get DPI for this screen
            GetScreenDpi(_screen, out _scaleX, out _scaleY);

            // Pre-position window (fallback virtual coordinates)
            Left = _screen.Bounds.X / _scaleX;
            Top = _screen.Bounds.Y / _scaleY;
            Width = _screen.Bounds.Width / _scaleX;
            Height = _screen.Bounds.Height / _scaleY;

            ScreenGeometry.Rect = new Rect(0, 0, Width, Height);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            // Force WS_POPUP to ensure window has no borders or title bar
            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION & ~WS_THICKFRAME);

            // Force window to occupy the exact physical boundaries of the target monitor
            SetWindowPos(hwnd, IntPtr.Zero, 
                         _screen.Bounds.X, _screen.Bounds.Y, 
                         _screen.Bounds.Width, _screen.Bounds.Height, 
                         SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private void GetScreenDpi(System.Windows.Forms.Screen screen, out double scaleX, out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            try
            {
                POINT pt = new POINT(screen.Bounds.X + 1, screen.Bounds.Y + 1);
                IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    uint dpiX, dpiY;
                    if (GetDpiForMonitor(hMonitor, 0, out dpiX, out dpiY) == 0)
                    {
                        scaleX = dpiX / 96.0;
                        scaleY = dpiY / 96.0;
                        return;
                    }
                }
            }
            catch
            {
                // Fallback to default
            }

            // Fallback to presentation source
            var presentationSource = PresentationSource.FromVisual(this);
            if (presentationSource?.CompositionTarget != null)
            {
                scaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                scaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Capture this specific screen's bounds
            CaptureScreen();
        }

        private void CaptureScreen()
        {
            int physicalWidth = _screen.Bounds.Width;
            int physicalHeight = _screen.Bounds.Height;

            // Take GDI snapshot of this screen
            _screenSnapshot = new Bitmap(physicalWidth, physicalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(_screenSnapshot))
            {
                g.CopyFromScreen(_screen.Bounds.X, _screen.Bounds.Y, 0, 0, new System.Drawing.Size(physicalWidth, physicalHeight), CopyPixelOperation.SourceCopy);
            }

            // Bind to BackgroundImg
            BackgroundImg.Source = BitmapToImageSource(_screenSnapshot);
            BackgroundImg.Width = Width;
            BackgroundImg.Height = Height;
        }

        private ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            CrosshairH.Visibility = Visibility.Visible;
            CrosshairV.Visibility = Visibility.Visible;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                CrosshairH.Visibility = Visibility.Collapsed;
                CrosshairV.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _startPoint = e.GetPosition(this);
                CaptureMouse();

                SelectionBorder.Visibility = Visibility.Visible;
                SizeLabelBorder.Visibility = Visibility.Visible;
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                // Right click cancels screenshot
                Close();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            Point currentPoint = e.GetPosition(this);

            // Update Crosshair Guides (Deep Blue)
            CrosshairH.X1 = 0;
            CrosshairH.X2 = Width;
            CrosshairH.Y1 = currentPoint.Y;
            CrosshairH.Y2 = currentPoint.Y;

            CrosshairV.X1 = currentPoint.X;
            CrosshairV.X2 = currentPoint.X;
            CrosshairV.Y1 = 0;
            CrosshairV.Y2 = Height;

            CrosshairH.Visibility = Visibility.Visible;
            CrosshairV.Visibility = Visibility.Visible;

            if (!_isDragging) return;

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(_startPoint.X - currentPoint.X);
            double h = Math.Abs(_startPoint.Y - currentPoint.Y);

            Rect selectionRect = new Rect(x, y, w, h);
            SelectionGeometry.Rect = selectionRect;

            // Position and resize the border
            Canvas.SetLeft(SelectionBorder, x);
            Canvas.SetTop(SelectionBorder, y);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;

            // Position size label (PixPin-like)
            SizeTextBlock.Text = $"{(int)w} x {(int)h}";
            double labelLeft = x;
            double labelTop = y - 22;
            if (labelTop < 0) labelTop = y + h + 5; // Put label below if too close to top

            Canvas.SetLeft(SizeLabelBorder, labelLeft);
            Canvas.SetTop(SizeLabelBorder, labelTop);
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();

                Rect finalRect = SelectionGeometry.Rect;
                if (finalRect.Width > 5 && finalRect.Height > 5 && _screenSnapshot != null)
                {
                    // Crop the bitmap in physical pixels
                    int cropLeft = (int)Math.Max(0, Math.Round(finalRect.Left * _scaleX));
                    int cropTop = (int)Math.Max(0, Math.Round(finalRect.Top * _scaleY));
                    int cropWidth = (int)Math.Min(_screenSnapshot.Width - cropLeft, Math.Round(finalRect.Width * _scaleX));
                    int cropHeight = (int)Math.Min(_screenSnapshot.Height - cropTop, Math.Round(finalRect.Height * _scaleY));

                    if (cropWidth > 0 && cropHeight > 0)
                    {
                        var croppedBmp = new Bitmap(cropWidth, cropHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(croppedBmp))
                        {
                            g.DrawImage(_screenSnapshot, new Rectangle(0, 0, cropWidth, cropHeight),
                                new Rectangle(cropLeft, cropTop, cropWidth, cropHeight), GraphicsUnit.Pixel);
                        }

                        // Translate selection rect to screen coordinates (absolute virtual pixels)
                        Rect absoluteRect = new Rect(
                            finalRect.Left + Left,
                            finalRect.Top + Top,
                            finalRect.Width,
                            finalRect.Height
                        );

                        if (ConfigManager.Current.MultiRegionSelection)
                        {
                            // Multi-region continuous mode
                            var item = new SelectedRegionItem
                            {
                                Bitmap = croppedBmp,
                                AbsoluteRect = absoluteRect,
                                LocalRect = finalRect
                            };
                            _selectedRegions.Add(item);

                            // Keep this hollow-out bright on screen
                            HollowGroup.Children.Add(new RectangleGeometry(finalRect));

                            // Add static visual border
                            var committedBorder = new Border
                            {
                                Width = finalRect.Width,
                                Height = finalRect.Height,
                                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 120, 215)),
                                BorderThickness = new Thickness(1.5),
                                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 0, 120, 215)),
                                IsHitTestVisible = false
                            };
                            Canvas.SetLeft(committedBorder, finalRect.Left);
                            Canvas.SetTop(committedBorder, finalRect.Top);
                            CommittedRegionsCanvas.Children.Add(committedBorder);

                            // Numbered Badge [1], [2], [3]...
                            int badgeIndex = _initialRegionOffset + _selectedRegions.Count;
                            var badgeBorder = new Border
                            {
                                Width = 20,
                                Height = 20,
                                CornerRadius = new CornerRadius(10),
                                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 120, 215)),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                                IsHitTestVisible = false
                            };
                            var badgeText = new TextBlock
                            {
                                Text = badgeIndex.ToString(),
                                Foreground = System.Windows.Media.Brushes.White,
                                FontSize = 10,
                                FontWeight = FontWeights.Bold,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                                TextAlignment = TextAlignment.Center
                            };
                            badgeBorder.Child = badgeText;
                            Canvas.SetLeft(badgeBorder, Math.Max(0, finalRect.Left - 5));
                            Canvas.SetTop(badgeBorder, Math.Max(0, finalRect.Top - 5));
                            CommittedRegionsCanvas.Children.Add(badgeBorder);

                            // Reset current dragging rect and hide borders
                            SelectionGeometry.Rect = new Rect(0, 0, 0, 0);
                            SelectionBorder.Visibility = Visibility.Collapsed;
                            SizeLabelBorder.Visibility = Visibility.Collapsed;

                            // Update and position the floating action bar
                            UpdateFloatingActionBar(finalRect);
                            return;
                        }
                        else
                        {
                            // Single selection mode
                            _isCompleted = true;
                            OnScreenshotCompleted?.Invoke(croppedBmp, absoluteRect);
                            Close();
                            return;
                        }
                    }
                }

                // Ineffective drag
                SelectionGeometry.Rect = new Rect(0, 0, 0, 0);
                SelectionBorder.Visibility = Visibility.Collapsed;
                SizeLabelBorder.Visibility = Visibility.Collapsed;

                if (!ConfigManager.Current.MultiRegionSelection || (_initialRegionOffset == 0 && _selectedRegions.Count == 0))
                {
                    Close();
                }
            }
        }

        private void UpdateFloatingActionBar(Rect latestRect)
        {
            CountBadgeTextBlock.Text = Localization.MultiRegionCountBadge(_initialRegionOffset + _selectedRegions.Count);
            DoneButton.Content = Localization.BtnFinishOcr;
            SwitchWindowButton.Content = Localization.BtnSwitchWindow;
            CancelOcrButton.Content = Localization.BtnCancelOcr;

            FloatingActionBar.Visibility = Visibility.Visible;

            // Position near the latest region's bottom-right
            double barWidth = 280;
            double barHeight = 40;

            double targetLeft = latestRect.Right - barWidth;
            if (targetLeft < 10) targetLeft = latestRect.Left;
            if (targetLeft + barWidth > Width - 10) targetLeft = Width - barWidth - 10;
            if (targetLeft < 10) targetLeft = 10;

            double targetTop = latestRect.Bottom + 8;
            if (targetTop + barHeight > Height - 10) targetTop = latestRect.Top - barHeight - 8;
            if (targetTop < 10) targetTop = 10;

            Canvas.SetLeft(FloatingActionBar, targetLeft);
            Canvas.SetTop(FloatingActionBar, targetTop);
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            FinishMultiSelection();
        }

        private void SwitchWindowButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchWindow();
        }

        private void SwitchWindow()
        {
            if (_selectedRegions.Count > 0 || _initialRegionOffset > 0)
            {
                _isCompleted = true;
                OnSwitchWindowRequested?.Invoke(new List<SelectedRegionItem>(_selectedRegions));
                Close();
            }
        }

        private void CancelOcrButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FinishMultiSelection()
        {
            if (_selectedRegions.Count > 0 || _initialRegionOffset > 0)
            {
                _isCompleted = true;
                OnMultiScreenshotCompleted?.Invoke(new List<SelectedRegionItem>(_selectedRegions));
                Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                if (ConfigManager.Current.MultiRegionSelection && (_selectedRegions.Count > 0 || _initialRegionOffset > 0))
                {
                    FinishMultiSelection();
                }
            }
            else if (e.Key == Key.Tab)
            {
                if (ConfigManager.Current.MultiRegionSelection && (_selectedRegions.Count > 0 || _initialRegionOffset > 0))
                {
                    SwitchWindow();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _screenSnapshot?.Dispose();
            _screenSnapshot = null;

            // Clean up uncommitted bitmaps if window closed without completing
            if (!_isCompleted)
            {
                foreach (var item in _selectedRegions)
                {
                    item.Bitmap?.Dispose();
                }
            }
            _selectedRegions.Clear();
        }
    }
}

