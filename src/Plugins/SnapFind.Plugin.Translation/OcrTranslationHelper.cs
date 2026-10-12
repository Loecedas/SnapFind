using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SnapFind.Plugin.Translation
{
    /// <summary>
    /// OCR 框选联动翻译辅助工具，可直接为现有的文本框和下拉框绑定自动翻译逻辑
    /// </summary>
    public static class OcrTranslationHelper
    {
        /// <summary>
        /// 为现有的语言下拉框填充受支持的语言列表
        /// </summary>
        public static void PopulateLanguageComboBox(ComboBox comboBox, string defaultLanguageCode = "zh-CN")
        {
            if (comboBox == null) return;

            comboBox.Items.Clear();
            int defaultIndex = 0;
            int idx = 0;

            foreach (var lang in TranslationEngine.SupportedLanguages)
            {
                comboBox.Items.Add(new ComboBoxItem
                {
                    Content = lang.DisplayName,
                    Tag = lang.Code
                });

                if (lang.Code == defaultLanguageCode)
                {
                    defaultIndex = idx;
                }
                idx++;
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = defaultIndex;
            }
        }

        /// <summary>
        /// 一键绑定 OCR 源文本框、译文文本框及目标语言下拉框，自动管理 600ms 防抖与异步翻译
        /// </summary>
        public static DispatcherTimer BindAutoTranslation(
            TextBox sourceBox, 
            TextBox translateResultBox, 
            ComboBox? targetLangComboBox = null, 
            string defaultTargetLanguage = "zh-CN",
            int debounceMs = 600)
        {
            if (sourceBox == null || translateResultBox == null)
            {
                throw new ArgumentNullException("sourceBox and translateResultBox cannot be null");
            }

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(debounceMs)
            };

            string currentTargetLang = defaultTargetLanguage;

            if (targetLangComboBox != null)
            {
                if (targetLangComboBox.Items.Count == 0)
                {
                    PopulateLanguageComboBox(targetLangComboBox, defaultTargetLanguage);
                }

                targetLangComboBox.SelectionChanged += (s, e) =>
                {
                    if (targetLangComboBox.SelectedItem is ComboBoxItem item)
                    {
                        currentTargetLang = item.Tag?.ToString() ?? "zh-CN";
                        TriggerTranslation();
                    }
                };
            }

            string lastExecutedText = "";

            void TriggerTranslation()
            {
                timer.Stop();
                string sourceText = sourceBox.Text;
                lastExecutedText = sourceText;

                if (string.IsNullOrWhiteSpace(sourceText))
                {
                    translateResultBox.Text = string.Empty;
                    return;
                }

                translateResultBox.Text = "正在翻译中...";
                string target = currentTargetLang;

                Task.Run(async () =>
                {
                    string res = await TranslationEngine.TranslateAsync(sourceText, "Microsoft", "auto", target);
                    sourceBox.Dispatcher.Invoke(() =>
                    {
                        if (lastExecutedText == sourceText)
                        {
                            translateResultBox.Text = res;
                        }
                    });
                });
            }

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                TriggerTranslation();
            };

            sourceBox.TextChanged += (s, e) =>
            {
                timer.Stop();
                if (string.IsNullOrWhiteSpace(sourceBox.Text))
                {
                    translateResultBox.Text = string.Empty;
                }
                else
                {
                    timer.Start();
                }
            };

            // 如果源文本框已有初始文本，立即执行一次翻译
            if (!string.IsNullOrWhiteSpace(sourceBox.Text))
            {
                TriggerTranslation();
            }

            return timer;
        }
    }
}
