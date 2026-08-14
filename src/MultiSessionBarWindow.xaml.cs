using System;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PixOcrSearch
{
    public partial class MultiSessionBarWindow : Window
    {
        public event Action? OnContinueRequested;
        public event Action? OnCompleteRequested;
        public event Action? OnCancelRequested;

        private readonly int _capturedCount;
        private readonly string _hotkeyDisplay;

        public MultiSessionBarWindow(int capturedCount, string hotkeyDisplay)
        {
            InitializeComponent();
            _capturedCount = capturedCount;
            _hotkeyDisplay = hotkeyDisplay;

            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            BadgeTextBlock.Text = Localization.SwitchSessionBadge(_capturedCount);
            TipTextBlock.Text = Localization.SwitchSessionTip(_hotkeyDisplay);
            ContinueButton.Content = Localization.BtnSwitchContinue;
            DoneButton.Content = Localization.BtnFinishOcr;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position at top center of main display
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = (screenWidth - ActualWidth) / 2;
            Top = 16;
            Activate();
            Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
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
