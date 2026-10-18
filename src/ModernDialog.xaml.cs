using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PixOcrSearch
{
    public partial class ModernDialog : Window
    {
        private readonly MessageBoxButton _buttonMode;
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public ModernDialog(string message, string title, MessageBoxButton button, MessageBoxImage image, Window? owner = null)
        {
            InitializeComponent();
            _buttonMode = button;

            Title = title;
            TitleText.Text = title;
            MessageText.Text = message;

            ConfigureIcon(image);
            ConfigureButtons(button);

            var resolvedOwner = owner ?? ResolveActiveWindow();
            if (resolvedOwner != null && resolvedOwner.IsVisible)
            {
                Owner = resolvedOwner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                Owner = null;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private static Window? ResolveActiveWindow()
        {
            if (Application.Current == null) return null;
            foreach (Window win in Application.Current.Windows)
            {
                if (win.IsActive && win is not ModernDialog && win.IsVisible) return win;
            }
            var main = Application.Current.MainWindow;
            return (main != null && main is not ModernDialog && main.IsVisible) ? main : null;
        }

        private void ConfigureIcon(MessageBoxImage image)
        {
            switch (image)
            {
                case MessageBoxImage.Information:
                    HeaderIconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
                    HeaderIconPath.Fill = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    HeaderIconPath.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Warning:
                    HeaderIconPath.Data = Geometry.Parse("M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z");
                    HeaderIconPath.Fill = new SolidColorBrush(Color.FromRgb(216, 153, 0));
                    HeaderIconPath.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Error:
                    HeaderIconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm5 13.59L15.59 17 12 13.41 8.41 17 7 15.59 10.59 12 7 8.41 8.41 7 12 10.59 15.59 7 17 8.41 13.41 12 17 15.59z");
                    HeaderIconPath.Fill = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                    HeaderIconPath.Visibility = Visibility.Visible;
                    break;
                case MessageBoxImage.Question:
                    HeaderIconPath.Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 16h-2v-2h2v2zm1.07-7.75l-.9.92C12.45 11.9 12 12.5 12 14h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H7c0-2.76 2.24-5 5-5s5 2.24 5 5c0 1.04-.42 1.99-1.07 2.75z");
                    HeaderIconPath.Fill = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    HeaderIconPath.Visibility = Visibility.Visible;
                    break;
                default:
                    HeaderIconPath.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void ConfigureButtons(MessageBoxButton button)
        {
            ButtonsPanel.Children.Clear();

            switch (button)
            {
                case MessageBoxButton.OK:
                    AddButton(Localization.IsEnglish ? "OK" : "确定", MessageBoxResult.OK, isPrimary: true);
                    break;

                case MessageBoxButton.OKCancel:
                    AddButton(Localization.IsEnglish ? "OK" : "确定", MessageBoxResult.OK, isPrimary: true);
                    AddButton(Localization.IsEnglish ? "Cancel" : "取消", MessageBoxResult.Cancel, isPrimary: false, margin: new Thickness(8, 0, 0, 0));
                    break;

                case MessageBoxButton.YesNo:
                    AddButton(Localization.IsEnglish ? "Yes" : "是", MessageBoxResult.Yes, isPrimary: true);
                    AddButton(Localization.IsEnglish ? "No" : "否", MessageBoxResult.No, isPrimary: false, margin: new Thickness(8, 0, 0, 0));
                    break;

                case MessageBoxButton.YesNoCancel:
                    AddButton(Localization.IsEnglish ? "Yes" : "是", MessageBoxResult.Yes, isPrimary: true);
                    AddButton(Localization.IsEnglish ? "No" : "否", MessageBoxResult.No, isPrimary: false, margin: new Thickness(8, 0, 0, 0));
                    AddButton(Localization.IsEnglish ? "Cancel" : "取消", MessageBoxResult.Cancel, isPrimary: false, margin: new Thickness(8, 0, 0, 0));
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool isPrimary, Thickness? margin = null)
        {
            var btn = new Button
            {
                Content = text,
                Style = (Style)FindResource(isPrimary ? "PrimaryButton" : "ModernButton"),
                Margin = margin ?? new Thickness(0)
            };
            btn.Click += (s, e) =>
            {
                Result = result;
                Close();
            };
            ButtonsPanel.Children.Add(btn);
        }

        private MessageBoxResult GetDismissResult()
        {
            return _buttonMode switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                _ => MessageBoxResult.Cancel
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = GetDismissResult();
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Result = GetDismissResult();
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                if (ButtonsPanel.Children.Count > 0 && ButtonsPanel.Children[0] is Button firstBtn)
                {
                    firstBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        public static MessageBoxResult Show(string message, string title = "SnapFind", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None, Window? owner = null)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() => Show(message, title, button, image, owner));
            }

            var dlg = new ModernDialog(message, title, button, image, owner);
            dlg.ShowDialog();
            return dlg.Result;
        }
    }
}
