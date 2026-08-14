using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
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
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfCursors = System.Windows.Input.Cursors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfBrush = System.Windows.Media.Brush;
using WpfApplication = System.Windows.Application;

namespace PixOcrSearch
{
    public class SelectedRegionItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Bitmap Bitmap { get; set; } = null!;
        public Rect AbsoluteRect { get; set; }
        public Rect LocalRect { get; set; }
    }

    public enum RegionActionType
    {
        Delete,
        Reorder
    }

    public class RegionActionHistoryItem
    {
        public RegionActionType ActionType { get; set; }
        public SelectedRegionItem? Item { get; set; }
        public int TargetIndex { get; set; }
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }
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
        private readonly Stack<RegionActionHistoryItem> _historyStack = new Stack<RegionActionHistoryItem>();
        private readonly int _initialRegionOffset = 0;
        private bool _isCompleted = false;

        // Floating bar drag state
        private bool _isBarDragging = false;
        private bool _isBarManuallyMoved = false;
        private Point _barDragStartPoint;
        private double _barStartLeft;
        private double _barStartTop;

        // Committed region box drag state
        private bool _isRegionDragging = false;
        private int _draggingRegionIndex = -1;
        private Point _regionDragStartPoint;
        private Rect _regionOriginalRect;

        // Events
        public event Action<Bitmap, Rect>? OnScreenshotCompleted;
        public event Action<List<SelectedRegionItem>>? OnMultiScreenshotCompleted;
        public event Action<List<SelectedRegionItem>, bool>? OnSwitchWindowRequested;

        public ScreenshotWindow(System.Windows.Forms.Screen screen, int initialRegionOffset = 0)
        {
            InitializeComponent();
            _screen = screen;
            _initialRegionOffset = initialRegionOffset;

            GetScreenDpi(_screen, out _scaleX, out _scaleY);

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

            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_CAPTION & ~WS_THICKFRAME);

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
            catch { }

            var presentationSource = PresentationSource.FromVisual(this);
            if (presentationSource?.CompositionTarget != null)
            {
                scaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                scaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CaptureScreen();
        }

        private void CaptureScreen()
        {
            int physicalWidth = _screen.Bounds.Width;
            int physicalHeight = _screen.Bounds.Height;

            _screenSnapshot = new Bitmap(physicalWidth, physicalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(_screenSnapshot))
            {
                g.CopyFromScreen(_screen.Bounds.X, _screen.Bounds.Y, 0, 0, new System.Drawing.Size(physicalWidth, physicalHeight), CopyPixelOperation.SourceCopy);
            }

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
            if (!_isDragging && !_isRegionDragging)
            {
                CrosshairH.Visibility = Visibility.Collapsed;
                CrosshairV.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isBarDragging || _isRegionDragging) return;

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
                Close();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            Point currentPoint = e.GetPosition(this);

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

            if (_isRegionDragging && _draggingRegionIndex >= 0 && _draggingRegionIndex < _selectedRegions.Count)
            {
                double deltaX = currentPoint.X - _regionDragStartPoint.X;
                double deltaY = currentPoint.Y - _regionDragStartPoint.Y;

                double newLeft = _regionOriginalRect.Left + deltaX;
                double newTop = _regionOriginalRect.Top + deltaY;

                if (newLeft < 0) newLeft = 0;
                if (newLeft + _regionOriginalRect.Width > Width) newLeft = Width - _regionOriginalRect.Width;
                if (newTop < 0) newTop = 0;
                if (newTop + _regionOriginalRect.Height > Height) newTop = Height - _regionOriginalRect.Height;

                _selectedRegions[_draggingRegionIndex].LocalRect = new Rect(newLeft, newTop, _regionOriginalRect.Width, _regionOriginalRect.Height);
                RedrawCommittedRegions(isLiveDragging: true);
                return;
            }

            if (!_isDragging) return;

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(_startPoint.X - currentPoint.X);
            double h = Math.Abs(_startPoint.Y - currentPoint.Y);

            Rect selectionRect = new Rect(x, y, w, h);
            SelectionGeometry.Rect = selectionRect;

            Canvas.SetLeft(SelectionBorder, x);
            Canvas.SetTop(SelectionBorder, y);
            SelectionBorder.Width = w;
            SelectionBorder.Height = h;

            SizeTextBlock.Text = $"{(int)w} x {(int)h}";
            double labelLeft = x;
            double labelTop = y - 22;
            if (labelTop < 0) labelTop = y + h + 5;

            Canvas.SetLeft(SizeLabelBorder, labelLeft);
            Canvas.SetTop(SizeLabelBorder, labelTop);
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_isRegionDragging)
                {
                    _isRegionDragging = false;
                    ReleaseMouseCapture();

                    if (_draggingRegionIndex >= 0 && _draggingRegionIndex < _selectedRegions.Count)
                    {
                        var item = _selectedRegions[_draggingRegionIndex];
                        item.AbsoluteRect = new Rect(item.LocalRect.Left + Left, item.LocalRect.Top + Top, item.LocalRect.Width, item.LocalRect.Height);
                    }
                    _draggingRegionIndex = -1;
                    RedrawCommittedRegions();
                    return;
                }

                if (_isDragging)
                {
                    _isDragging = false;
                    ReleaseMouseCapture();

                    Rect finalRect = SelectionGeometry.Rect;
                    if (finalRect.Width > 5 && finalRect.Height > 5 && _screenSnapshot != null)
                    {
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

                            Rect absoluteRect = new Rect(
                                finalRect.Left + Left,
                                finalRect.Top + Top,
                                finalRect.Width,
                                finalRect.Height
                            );

                            if (ConfigManager.Current.MultiRegionSelection)
                            {
                                var item = new SelectedRegionItem
                                {
                                    Bitmap = croppedBmp,
                                    AbsoluteRect = absoluteRect,
                                    LocalRect = finalRect
                                };

                                _selectedRegions.Add(item);

                                // Reset dragging visual indicators
                                SelectionGeometry.Rect = new Rect(0, 0, 0, 0);
                                SelectionBorder.Visibility = Visibility.Collapsed;
                                SizeLabelBorder.Visibility = Visibility.Collapsed;

                                RedrawCommittedRegions();
                                UpdateFloatingActionBar(finalRect);
                                return;
                            }
                            else
                            {
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

                    if (!ConfigManager.Current.MultiRegionSelection || _selectedRegions.Count == 0)
                    {
                        Close();
                    }
                }
            }
        }

        private void RecropRegionBitmap(SelectedRegionItem item)
        {
            if (_screenSnapshot == null) return;

            int cropLeft = (int)Math.Max(0, Math.Round(item.LocalRect.Left * _scaleX));
            int cropTop = (int)Math.Max(0, Math.Round(item.LocalRect.Top * _scaleY));
            int cropWidth = (int)Math.Min(_screenSnapshot.Width - cropLeft, Math.Round(item.LocalRect.Width * _scaleX));
            int cropHeight = (int)Math.Min(_screenSnapshot.Height - cropTop, Math.Round(item.LocalRect.Height * _scaleY));

            if (cropWidth > 0 && cropHeight > 0)
            {
                item.Bitmap?.Dispose();
                var croppedBmp = new Bitmap(cropWidth, cropHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(croppedBmp))
                {
                    g.DrawImage(_screenSnapshot, new Rectangle(0, 0, cropWidth, cropHeight),
                        new Rectangle(cropLeft, cropTop, cropWidth, cropHeight), GraphicsUnit.Pixel);
                }
                item.Bitmap = croppedBmp;
                item.AbsoluteRect = new Rect(item.LocalRect.Left + Left, item.LocalRect.Top + Top, item.LocalRect.Width, item.LocalRect.Height);
            }
        }

        private WpfBrush GetBrush(string resourceKey, Color fallback)
        {
            if (WpfApplication.Current.Resources.Contains(resourceKey) && WpfApplication.Current.Resources[resourceKey] is WpfBrush b)
            {
                return b;
            }
            return new SolidColorBrush(fallback);
        }

        private void RedrawCommittedRegions(bool isLiveDragging = false)
        {
            CommittedRegionsCanvas.Children.Clear();
            HollowGroup.Children.Clear();

            // Always add dragging geometry slot
            HollowGroup.Children.Add(SelectionGeometry);

            WpfBrush windowBg = GetBrush("ThemeWindowBg", Color.FromRgb(30, 30, 34));
            WpfBrush windowBorder = GetBrush("ThemeWindowBorder", Color.FromRgb(63, 63, 70));
            WpfBrush btnBg = GetBrush("ThemeBtnBg", Color.FromRgb(45, 45, 48));
            WpfBrush btnBorder = GetBrush("ThemeBtnBorder", Color.FromRgb(63, 63, 70));
            WpfBrush btnFg = GetBrush("ThemeBtnFg", Color.FromRgb(225, 225, 225));

            for (int i = 0; i < _selectedRegions.Count; i++)
            {
                var regionItem = _selectedRegions[i];
                int regionIndex = i;
                int badgeNumber = _initialRegionOffset + i + 1;

                // 1. Keep hollow-out cutout on screen
                HollowGroup.Children.Add(new RectangleGeometry(regionItem.LocalRect));

                // 2. Draggable Border with live image content
                var committedBorder = new Border
                {
                    Width = regionItem.LocalRect.Width,
                    Height = regionItem.LocalRect.Height,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)),
                    BorderThickness = new Thickness(1.5),
                    Background = windowBg,
                    Cursor = WpfCursors.SizeAll,
                    SnapsToDevicePixels = true,
                    ToolTip = "按住此选区可任意拖动平移位置"
                };
                committedBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    Direction = 270,
                    ShadowDepth = 2,
                    Opacity = 0.4,
                    Color = Colors.Black
                };

                var imgContent = new System.Windows.Controls.Image
                {
                    Source = BitmapToImageSource(regionItem.Bitmap),
                    Stretch = Stretch.Fill,
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false
                };
                committedBorder.Child = imgContent;

                committedBorder.MouseLeftButtonDown += (s, e) =>
                {
                    _isRegionDragging = true;
                    _draggingRegionIndex = regionIndex;
                    _regionDragStartPoint = e.GetPosition(this);
                    _regionOriginalRect = regionItem.LocalRect;
                    CaptureMouse();
                    e.Handled = true;
                };

                Canvas.SetLeft(committedBorder, regionItem.LocalRect.Left);
                Canvas.SetTop(committedBorder, regionItem.LocalRect.Top);
                CommittedRegionsCanvas.Children.Add(committedBorder);

                // 3. Compact Floating Control Pill with Number Badge, Move Up, Move Down, and Delete
                var pillBorder = new Border
                {
                    Background = windowBg,
                    BorderBrush = windowBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 2, 4, 2),
                    SnapsToDevicePixels = true
                };
                pillBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    Direction = 270,
                    ShadowDepth = 2,
                    Opacity = 0.45,
                    Color = Colors.Black
                };

                var pillStack = new StackPanel
                {
                    Orientation = WpfOrientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Number badge (Light gray theme matching control panel)
                var badgeBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(3),
                    Background = btnBg,
                    BorderBrush = btnBorder,
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                var badgeText = new TextBlock
                {
                    Text = badgeNumber.ToString(),
                    Foreground = GetBrush("ThemeText", Color.FromRgb(255, 255, 255)),
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new WpfFontFamily("Microsoft YaHei UI, Segoe UI"),
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                badgeBorder.Child = badgeText;
                pillStack.Children.Add(badgeBorder);

                var miniBtnStyle = FindResource("PillMiniButton") as Style;
                var delBtnStyle = FindResource("PillDeleteButton") as Style;

                // If multiple boxes on the same screen, allow local reordering
                if (_selectedRegions.Count > 1)
                {
                    bool canMoveUp = i > 0;
                    var moveUpBtn = new WpfButton
                    {
                        Content = "▲",
                        ToolTip = canMoveUp ? $"将 [{badgeNumber}] 号前移/插入到上一项之前" : "已是第一项",
                        Style = miniBtnStyle,
                        Padding = new Thickness(4, 1, 4, 1),
                        IsEnabled = canMoveUp,
                        Margin = new Thickness(0, 0, 3, 0)
                    };
                    if (canMoveUp)
                    {
                        moveUpBtn.Click += (s, e) =>
                        {
                            MoveRegion(regionIndex, regionIndex - 1);
                        };
                    }
                    pillStack.Children.Add(moveUpBtn);

                    bool canMoveDown = i < _selectedRegions.Count - 1;
                    var moveDownBtn = new WpfButton
                    {
                        Content = "▼",
                        ToolTip = canMoveDown ? $"将 [{badgeNumber}] 号后移/插入到下一项之后" : "已是最后一项",
                        Style = miniBtnStyle,
                        Padding = new Thickness(4, 1, 4, 1),
                        IsEnabled = canMoveDown,
                        Margin = new Thickness(0, 0, 3, 0)
                    };
                    if (canMoveDown)
                    {
                        moveDownBtn.Click += (s, e) =>
                        {
                            MoveRegion(regionIndex, regionIndex + 1);
                        };
                    }
                    pillStack.Children.Add(moveDownBtn);
                }

                // ✕ Delete Button with rounded CornerRadius="3"
                var deleteBtn = new WpfButton
                {
                    Content = "✕",
                    ToolTip = $"删除此 [{badgeNumber}] 号选区",
                    Style = delBtnStyle,
                    Padding = new Thickness(5, 1, 5, 1)
                };
                deleteBtn.Click += (s, e) =>
                {
                    DeleteRegion(regionIndex);
                };
                pillStack.Children.Add(deleteBtn);

                pillBorder.Child = pillStack;

                double pillLeft = Math.Max(0, regionItem.LocalRect.Left - 4);
                double pillTop = regionItem.LocalRect.Top - 26;
                if (pillTop < 5) pillTop = regionItem.LocalRect.Bottom + 4;

                Canvas.SetLeft(pillBorder, pillLeft);
                Canvas.SetTop(pillBorder, pillTop);
                CommittedRegionsCanvas.Children.Add(pillBorder);
            }

            if (!isLiveDragging)
            {
                int totalCount = _initialRegionOffset + _selectedRegions.Count;
                CountBadgeTextBlock.Text = Localization.MultiRegionCountBadge(totalCount);
                UndoButton.Visibility = _historyStack.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                InsertButton.Visibility = (_initialRegionOffset > 0 && _selectedRegions.Count > 0) ? Visibility.Visible : Visibility.Collapsed;

                if (_selectedRegions.Count == 0 && _initialRegionOffset == 0)
                {
                    FloatingActionBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchWindow(openDrawer: true);
        }

        private void MoveRegion(int fromIndex, int toIndex)
        {
            if (fromIndex >= 0 && fromIndex < _selectedRegions.Count && toIndex >= 0 && toIndex < _selectedRegions.Count && fromIndex != toIndex)
            {
                var item = _selectedRegions[fromIndex];
                _selectedRegions.RemoveAt(fromIndex);
                _selectedRegions.Insert(toIndex, item);

                _historyStack.Push(new RegionActionHistoryItem
                {
                    ActionType = RegionActionType.Reorder,
                    FromIndex = fromIndex,
                    ToIndex = toIndex
                });

                RedrawCommittedRegions();
            }
        }

        private void DeleteRegion(int index)
        {
            if (index >= 0 && index < _selectedRegions.Count)
            {
                var item = _selectedRegions[index];
                _selectedRegions.RemoveAt(index);

                _historyStack.Push(new RegionActionHistoryItem
                {
                    ActionType = RegionActionType.Delete,
                    Item = item,
                    TargetIndex = index
                });

                if (_selectedRegions.Count == 0 && _initialRegionOffset > 0)
                {
                    OnSwitchWindowRequested?.Invoke(_selectedRegions, false);
                    return;
                }

                RedrawCommittedRegions();

                if (_selectedRegions.Count > 0)
                {
                    int lastIdx = Math.Min(index, _selectedRegions.Count - 1);
                    UpdateFloatingActionBar(_selectedRegions[lastIdx].LocalRect);
                }
                else
                {
                    FloatingActionBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void UndoLastAction()
        {
            if (_historyStack.Count > 0)
            {
                var historyItem = _historyStack.Pop();

                if (historyItem.ActionType == RegionActionType.Delete && historyItem.Item != null)
                {
                    int restoreIndex = Math.Min(historyItem.TargetIndex, _selectedRegions.Count);
                    _selectedRegions.Insert(restoreIndex, historyItem.Item);
                }
                else if (historyItem.ActionType == RegionActionType.Reorder)
                {
                    // Reverse the reorder
                    if (historyItem.ToIndex >= 0 && historyItem.ToIndex < _selectedRegions.Count)
                    {
                        var item = _selectedRegions[historyItem.ToIndex];
                        _selectedRegions.RemoveAt(historyItem.ToIndex);
                        _selectedRegions.Insert(historyItem.FromIndex, item);
                    }
                }

                RedrawCommittedRegions();
            }
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            UndoLastAction();
        }

        private void UpdateFloatingActionBar(Rect latestRect)
        {
            FloatingActionBar.Visibility = Visibility.Visible;
            UndoButton.Visibility = _historyStack.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Only auto-position if the user hasn't manually dragged the bar
            if (!_isBarManuallyMoved)
            {
                double barWidth = 340;
                double barHeight = 44;

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
        }

        private void FloatingActionBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Do not initiate drag if user clicked a button inside
            if (e.OriginalSource is DependencyObject dep && FindParent<WpfButton>(dep) != null)
            {
                return;
            }

            _isBarDragging = true;
            _isBarManuallyMoved = true;
            _barDragStartPoint = e.GetPosition(this);

            double left = Canvas.GetLeft(FloatingActionBar);
            double top = Canvas.GetTop(FloatingActionBar);
            _barStartLeft = double.IsNaN(left) ? 0 : left;
            _barStartTop = double.IsNaN(top) ? 0 : top;

            FloatingActionBar.CaptureMouse();
            e.Handled = true;
        }

        private void FloatingActionBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isBarDragging)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _barDragStartPoint.X;
                double deltaY = currentPoint.Y - _barDragStartPoint.Y;

                double newLeft = _barStartLeft + deltaX;
                double newTop = _barStartTop + deltaY;

                double barWidth = FloatingActionBar.ActualWidth > 0 ? FloatingActionBar.ActualWidth : 340;
                double barHeight = FloatingActionBar.ActualHeight > 0 ? FloatingActionBar.ActualHeight : 44;

                if (newLeft < 5) newLeft = 5;
                if (newLeft + barWidth > Width - 5) newLeft = Width - barWidth - 5;
                if (newTop < 5) newTop = 5;
                if (newTop + barHeight > Height - 5) newTop = Height - barHeight - 5;

                Canvas.SetLeft(FloatingActionBar, newLeft);
                Canvas.SetTop(FloatingActionBar, newTop);
                e.Handled = true;
            }
        }

        private void FloatingActionBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isBarDragging)
            {
                _isBarDragging = false;
                FloatingActionBar.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T correctlyTyped) return correctlyTyped;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            FinishMultiSelection();
        }

        private void SwitchWindowButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchWindow(openDrawer: false);
        }

        private void SwitchWindow(bool openDrawer = false)
        {
            if (_selectedRegions.Count > 0 || _initialRegionOffset > 0)
            {
                _isCompleted = true;
                OnSwitchWindowRequested?.Invoke(new List<SelectedRegionItem>(_selectedRegions), openDrawer);
                Close();
            }
        }

        private void CancelOcrButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FinishMultiSelection()
        {
            if (_selectedRegions.Count > 0)
            {
                _isCompleted = true;
                OnMultiScreenshotCompleted?.Invoke(new List<SelectedRegionItem>(_selectedRegions));
                Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                UndoLastAction();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                if (ConfigManager.Current.MultiRegionSelection && _selectedRegions.Count > 0)
                {
                    FinishMultiSelection();
                }
            }
            else if (e.Key == Key.Tab)
            {
                if (ConfigManager.Current.MultiRegionSelection && _selectedRegions.Count > 0)
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

            if (!_isCompleted)
            {
                foreach (var item in _selectedRegions)
                {
                    item.Bitmap?.Dispose();
                }
            }

            foreach (var history in _historyStack)
            {
                if (history.ActionType == RegionActionType.Delete)
                {
                    history.Item?.Bitmap?.Dispose();
                }
            }

            _selectedRegions.Clear();
            _historyStack.Clear();
        }
    }
}
