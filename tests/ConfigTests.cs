using System.Text.Json;
using Xunit;

namespace PixOcrSearch.Tests
{
    public class ConfigTests
    {
        [Fact]
        public void DefaultConfig_HasExpectedDefaults()
        {
            var cfg = new AppConfig();

            Assert.Equal("zh-CN", cfg.Language);
            Assert.Equal("Control,Alt", cfg.HotkeyModifiers);
            Assert.Equal("S", cfg.HotkeyKey);
            Assert.Equal("C", cfg.ControlPanelHotkeyKey);
            Assert.False(cfg.StartWithWindows);
            Assert.False(cfg.AutoCopyToClipboard);
            Assert.Equal("PP-OCRv6_tiny", cfg.OcrModel);
        }

        [Fact]
        public void Config_RoundTrip_PreservesValues()
        {
            var cfg = new AppConfig
            {
                Language = "en-US",
                AutoCopyToClipboard = true,
                DoNotOpenEditWindow = true,
                OcrModel = "PP-OCRv6_small"
            };

            string json = JsonSerializer.Serialize(cfg);
            var back = JsonSerializer.Deserialize<AppConfig>(json);

            Assert.NotNull(back);
            Assert.Equal("en-US", back!.Language);
            Assert.True(back.AutoCopyToClipboard);
            Assert.True(back.DoNotOpenEditWindow);
            Assert.Equal("PP-OCRv6_small", back.OcrModel);
        }

        [Fact]
        public void Config_DeserializeMissingFields_UsesDefaults()
        {
            // 老版本配置可能不含新增字段，反序列化时应回退到默认值
            string legacyJson = "{ \"Language\": \"en-US\" }";

            var cfg = JsonSerializer.Deserialize<AppConfig>(legacyJson);

            Assert.NotNull(cfg);
            Assert.Equal("en-US", cfg!.Language);
            Assert.Equal("Control,Alt", cfg.HotkeyModifiers); // 默认值保留
            Assert.False(cfg.StartWithWindows);                 // 默认值保留
        }
    }
}
