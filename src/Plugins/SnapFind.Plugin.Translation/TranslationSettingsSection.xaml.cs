using System;
using System.Windows;
using System.Windows.Controls;

namespace SnapFind.Plugin.Translation
{
    public partial class TranslationSettingsSection : UserControl
    {
        public event EventHandler<bool>? TranslationEnabledChanged;
        public event EventHandler<string>? TargetLanguageChanged;

        private string _targetLanguage = "zh-CN";

        public bool IsTranslationEnabled
        {
            get => EnableTranslationCheckBox.IsChecked == true;
            set
            {
                EnableTranslationCheckBox.IsChecked = value;
                UpdatePanelState();
            }
        }

        public string SelectedTargetLanguage
        {
            get => _targetLanguage;
            set
            {
                _targetLanguage = value;
                SelectLanguageInComboBox(value);
            }
        }

        public TranslationSettingsSection()
        {
            InitializeComponent();
            Loaded += TranslationSettingsSection_Loaded;
        }

        private void TranslationSettingsSection_Loaded(object sender, RoutedEventArgs e)
        {
            PluginThemeHelper.ApplyTheme(Resources);
            PopulateLanguages();
            UpdatePanelState();
        }

        private void PopulateLanguages()
        {
            if (TargetLanguageComboBox.Items.Count > 0) return;

            int selectedIndex = 0;
            int idx = 0;

            foreach (var lang in TranslationEngine.SupportedLanguages)
            {
                TargetLanguageComboBox.Items.Add(new ComboBoxItem
                {
                    Content = lang.DisplayName,
                    Tag = lang.Code
                });

                if (lang.Code == _targetLanguage)
                {
                    selectedIndex = idx;
                }
                idx++;
            }

            if (TargetLanguageComboBox.Items.Count > 0)
            {
                TargetLanguageComboBox.SelectedIndex = selectedIndex;
            }
        }

        private void SelectLanguageInComboBox(string langCode)
        {
            for (int i = 0; i < TargetLanguageComboBox.Items.Count; i++)
            {
                if (TargetLanguageComboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == langCode)
                {
                    TargetLanguageComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void UpdatePanelState()
        {
            if (TranslationProviderPanel != null)
            {
                TranslationProviderPanel.IsEnabled = IsTranslationEnabled;
                TranslationProviderPanel.Opacity = IsTranslationEnabled ? 1.0 : 0.5;
            }
        }

        private void EnableTranslationCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdatePanelState();
            TranslationEnabledChanged?.Invoke(this, IsTranslationEnabled);
        }

        private void TargetLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                _targetLanguage = item.Tag?.ToString() ?? "zh-CN";
                TargetLanguageChanged?.Invoke(this, _targetLanguage);
            }
        }
    }
}
