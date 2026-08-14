using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
    public partial class MultiSessionBarWindow : Window
    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public event Action? OnContinueRequested;
        public event Action? OnCompleteRequested;
        public event Action? OnCancelRequested;
        public event Action<int>? OnDeleteRegionRequested;
        public event Action<int, int>? OnReorderRequested;
        public event Action? OnUndoRequested;

        private readonly string _hotkeyDisplay;
        private List<SelectedRegionItem> _stagedRegions = new List<SelectedRegionItem>();
        private bool _hasUndoHistory = false;

        public MultiSessionBarWindow(List<SelectedRegionItem> stagedRegions, string hotkeyDisplay, bool hasUndoHistory = false, bool openDrawer = false)
        {
            InitializeComponent();
            _hotkeyDisplay = hotkeyDisplay;
            _stagedRegions = stagedRegions;
            _hasUndoHistory = hasUndoHistory;

            if (openDrawer)
            {
                ManageToggleButton.IsChecked = true;
                ManagementPanel.Visibility = Visibility.Visible;
            }

            App.ApplyTheme();
            RefreshStagedList(_stagedRegions, _hasUndoHistory);
        }

        private ImageSource? BitmapToImageSource(Bitmap? bitmap)
        {
            if (bitmap == null) return null;
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

        private WpfBrush GetBrush(string resourceKey, Color fallback)
        {
            if (WpfApplication.Current.Resources.Contains(resourceKey) && WpfApplication.Current.Resources[resourceKey] is WpfBrush b)
            {
                return b;
            }
            return new SolidColorBrush(fallback);
        }

        public void RefreshStagedList(List<SelectedRegionItem> stagedRegions, bool hasUndoHistory)
        {
            _stagedRegions = stagedRegions;
            _hasUndoHistory = hasUndoHistory;

            BadgeTextBlock.Text = Localization.SwitchSessionBadge(_stagedRegions.Count);
            TipTextBlock.Text = Localization.SwitchSessionTip(_hotkeyDisplay);

            UndoButton.IsEnabled = _hasUndoHistory;

            StagedItemsContainer.Children.Clear();

            WpfBrush windowBg = GetBrush("ThemeWindowBg", Color.FromRgb(30, 30, 34));
            WpfBrush windowBorder = GetBrush("ThemeWindowBorder", Color.FromRgb(63, 63, 70));
            WpfBrush inputBg = GetBrush("ThemeInputBg", Color.FromRgb(18, 18, 21));
            WpfBrush inputBorder = GetBrush("ThemeInputBorder", Color.FromRgb(45, 45, 50));
            WpfBrush textBrush = GetBrush("ThemeText", Color.FromRgb(255, 255, 255));
            WpfBrush btnBg = GetBrush("ThemeBtnBg", Color.FromRgb(45, 45, 48));
            WpfBrush btnBorder = GetBrush("ThemeBtnBorder", Color.FromRgb(63, 63, 70));

            var modernBtnStyle = FindResource("ModernButton") as Style;
            var dangerBtnStyle = FindResource("DangerButton") as Style;

            for (int i = 0; i < _stagedRegions.Count; i++)
            {
                int index = i;
                int displayNum = i + 1;
                var region = _stagedRegions[i];

                var rowBorder = new Border
                {
                    Background = windowBg,
                    BorderBrush = windowBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(0, 2, 0, 2),
                    SnapsToDevicePixels = true
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badge
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Thumbnail
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

                // 1. Badge Number (Light gray theme matching control panel)
                var badgeBorder = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(4),
                    Background = btnBg,
                    BorderBrush = btnBorder,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var badgeText = new TextBlock
                {
                    Text = displayNum.ToString(),
                    Foreground = textBrush,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new WpfFontFamily("Microsoft YaHei UI, Segoe UI"),
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                badgeBorder.Child = badgeText;
                Grid.SetColumn(badgeBorder, 0);
                rowGrid.Children.Add(badgeBorder);

                // 2. Real Image Thumbnail Preview
                var thumbnailBorder = new Border
                {
                    Background = inputBg,
                    BorderBrush = inputBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(2),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    SnapsToDevicePixels = true
                };
                var thumbImg = new System.Windows.Controls.Image
                {
                    Source = BitmapToImageSource(region.Bitmap),
                    MaxHeight = 32,
                    MaxWidth = 140,
                    Stretch = Stretch.Uniform,
                    SnapsToDevicePixels = true
                };
                thumbnailBorder.Child = thumbImg;
                Grid.SetColumn(thumbnailBorder, 1);
                rowGrid.Children.Add(thumbnailBorder);

                // 3. Info text
                var infoText = new TextBlock
                {
                    Text = $"选区 #{displayNum}  ({(int)region.LocalRect.Width} × {(int)region.LocalRect.Height} px)",
                    Foreground = textBrush,
                    FontSize = 12,
                    FontFamily = new WpfFontFamily("Microsoft YaHei UI, Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(infoText, 2);
                rowGrid.Children.Add(infoText);

                // 4. Action buttons (Move Up, Move Down, Delete)
                var actionsPanel = new StackPanel
                {
                    Orientation = WpfOrientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Move Up Button (Always visible, disabled when first item)
                bool canMoveUp = i > 0;
                var upBtn = new WpfButton
                {
                    Content = Localization.BtnMoveUp,
                    ToolTip = canMoveUp ? $"将 [{displayNum}] 号前移/插入到上一项之前" : "已是第一项",
                    Style = modernBtnStyle,
                    Padding = new Thickness(8, 3, 8, 3),
                    IsEnabled = canMoveUp,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                if (canMoveUp)
                {
                    upBtn.Click += (s, e) =>
                    {
                        OnReorderRequested?.Invoke(index, index - 1);
                    };
                }
                actionsPanel.Children.Add(upBtn);

                // Move Down Button (Always visible, disabled when last item)
                bool canMoveDown = i < _stagedRegions.Count - 1;
                var downBtn = new WpfButton
                {
                    Content = Localization.BtnMoveDown,
                    ToolTip = canMoveDown ? $"将 [{displayNum}] 号后移/插入到下一项之后" : "已是最后一项",
                    Style = modernBtnStyle,
                    Padding = new Thickness(8, 3, 8, 3),
                    IsEnabled = canMoveDown,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                if (canMoveDown)
                {
                    downBtn.Click += (s, e) =>
                    {
                        OnReorderRequested?.Invoke(index, index + 1);
                    };
                }
                actionsPanel.Children.Add(downBtn);

                // Delete Button
                var deleteBtn = new WpfButton
                {
                    Content = Localization.BtnDelete,
                    ToolTip = $"删除第 [{displayNum}] 处选区",
                    Style = dangerBtnStyle,
                    Padding = new Thickness(9, 3, 9, 3)
                };
                deleteBtn.Click += (s, e) =>
                {
                    OnDeleteRegionRequested?.Invoke(index);
                };
                actionsPanel.Children.Add(deleteBtn);

                Grid.SetColumn(actionsPanel, 3);
                rowGrid.Children.Add(actionsPanel);

                rowBorder.Child = rowGrid;
                StagedItemsContainer.Children.Add(rowBorder);
            }
        }

        private void ManageToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ManagementPanel.Visibility = ManageToggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            OnUndoRequested?.Invoke();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = Math.Round((screenWidth - ActualWidth) / 2);
            Top = 16;
            Activate();
            Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                OnUndoRequested?.Invoke();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                OnCompleteRequested?.Invoke();
            }
            else if (e.Key == Key.Escape)
            {
                OnCancelRequested?.Invoke();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            OnContinueRequested?.Invoke();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            OnCompleteRequested?.Invoke();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            OnCancelRequested?.Invoke();
        }
    }
}
