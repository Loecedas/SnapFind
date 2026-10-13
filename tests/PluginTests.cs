using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PixOcrSearch.Tests
{
    public class PluginTests
    {
        [Fact]
        public void Config_TranslationDefaults_AreValid()
        {
            var cfg = new AppConfig();
            Assert.False(cfg.EnableTranslation);
            Assert.False(cfg.AutoTranslateOcrResult);
            Assert.Equal("zh-CN", cfg.DefaultTargetLanguage);
            Assert.Equal("Control,Alt", cfg.TranslateHotkeyModifiers);
            Assert.Equal("T", cfg.TranslateHotkeyKey);
            Assert.NotNull(cfg.DisabledPlugins);
        }

        [Fact]
        public void PluginManager_TogglePluginActive_Works()
        {
            PluginManager.SetPluginActive("SnapFind.Plugin.Translation", false);
            Assert.False(PluginManager.IsPluginActive("SnapFind.Plugin.Translation"));

            PluginManager.SetPluginActive("SnapFind.Plugin.Translation", true);
            Assert.True(PluginManager.IsPluginActive("SnapFind.Plugin.Translation"));
        }

        [Fact]
        public void PluginManager_DeleteNonExistentPlugin_ReturnsFalse()
        {
            var (ok, msg) = PluginManager.DeletePlugin("NonExistent_Dummy_Plugin_Id");
            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(msg));
        }

        [Fact]
        public void PluginManager_ExternalPlugin_ScanAndPhysicalDelete()
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            string testPluginDir = Path.Combine(pluginsDir, "Test.External.Plugin");
            
            try
            {
                if (Directory.Exists(testPluginDir)) Directory.Delete(testPluginDir, true);
                Directory.CreateDirectory(testPluginDir);

                string jsonContent = @"{
                    ""id"": ""Test.External.Plugin"",
                    ""name"": ""测试扩展插件"",
                    ""version"": ""2.0.0"",
                    ""description"": ""用于测试外部导入与物理删除""
                }";
                File.WriteAllText(Path.Combine(testPluginDir, "plugin.json"), jsonContent);

                // 物理彻底删除
                var (ok, msg) = PluginManager.DeletePlugin("Test.External.Plugin");
                Assert.True(ok);
                Assert.False(Directory.Exists(testPluginDir));
            }
            finally
            {
                if (Directory.Exists(testPluginDir)) Directory.Delete(testPluginDir, true);
            }
        }
    }
}
