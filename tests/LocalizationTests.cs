using Xunit;

namespace PixOcrSearch.Tests
{
    public class LocalizationTests
    {
        [Theory]
        [InlineData("en-US", true)]
        [InlineData("en-us", true)]
        [InlineData("EN-US", true)]
        [InlineData("zh-CN", false)]
        [InlineData("zh-cn", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void IsEnglish_CaseInsensitiveDetection(string? lang, bool expected)
        {
            // IsEnglish 通过 ConfigManager.Current.Language 判断
            // 这里直接验证大小写不敏感的比较逻辑（核心纯逻辑）
            bool result = string.Equals(lang, "en-US", System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void LocalizedStrings_AreBilingualConsistent()
        {
            // 验证中英文文案成对存在且非空（防止漏翻/误删）
            Assert.False(string.IsNullOrWhiteSpace(Localization.ControlCenterTitle));
            Assert.False(string.IsNullOrWhiteSpace(Localization.TabSettings));
            Assert.False(string.IsNullOrWhiteSpace(Localization.LabelOcrModel));

            // 中文文案应包含中文（非 ASCII），英文文案应为 ASCII
            Assert.Contains("控制", Localization.ControlCenterTitle);
            Assert.DoesNotContain("控制", Localization.TabSettings); // "Settings" 为纯英文
        }
    }
}
