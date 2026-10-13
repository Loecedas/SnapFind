using System.Windows;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace SnapFind.Plugin.Abstractions;

public interface IUiContribution
{
    FrameworkElement? CreateOcrPanel(WpfTextBox resultTextBox);
    FrameworkElement? CreateSettingsSection();
}
