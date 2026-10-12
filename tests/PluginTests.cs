using System;
using System.IO;
using System.Linq;
using Xunit;
using SnapFind.Plugin.Translation;

namespace PixOcrSearch.Tests
{
    public class PluginTests
    {
        [Fact]
        public void TranslationPlugin_NativeConversion_TraditionalAndSimplified()
        {
            string simplified = "简体中文文本转换测试";
            string traditional = TranslationPlugin.ConvertToTraditional(simplified);
            Assert.False(string.IsNullOrEmpty(traditional));
            Assert.NotEqual(simplified, traditional);

            string backToSimplified = TranslationPlugin.ConvertToSimplified(traditional);
            Assert.Equal(simplified, backToSimplified);
        }

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
            bool initial = PluginManager.IsPluginActive("SnapFind.Plugin.Translation");
            
            PluginManager.SetPluginActive("SnapFind.Plugin.Translation", false);
            Assert.False(PluginManager.IsPluginActive("SnapFind.Plugin.Translation"));

            PluginManager.SetPluginActive("SnapFind.Plugin.Translation", true);
            Assert.True(PluginManager.IsPluginActive("SnapFind.Plugin.Translation"));
        }

        [Fact]
        public void PluginManager_ExternalPlugin_ScanAndPhysicalDelete()
        {
            // 构造测试插件环境
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

                // 验证扫描到该外部插件
                var plugins = PluginManager.GetInstalledPlugins();
                var found = plugins.FirstOrDefault(p => p.Id == "Test.External.Plugin");
                Assert.NotNull(found);
                Assert.Equal("测试扩展插件", found!.Name);
                Assert.Equal("2.0.0", found.Version);
                Assert.Equal("扩展插件", found.TypeName);

                // 验证物理彻底删除
                var (ok, msg) = PluginManager.DeletePlugin("Test.External.Plugin");
                Assert.True(ok);
                Assert.False(Directory.Exists(testPluginDir));

                // 删除后再次扫描为空
                plugins = PluginManager.GetInstalledPlugins();
                Assert.Null(plugins.FirstOrDefault(p => p.Id == "Test.External.Plugin"));
            }
            finally
            {
                if (Directory.Exists(testPluginDir)) Directory.Delete(testPluginDir, true);
            }
        }

        [Fact]
        public void PluginManager_DeleteNonExistentPlugin_ReturnsFalse()
        {
            var result = PluginManager.DeletePlugin("NonExistent_Dummy_Plugin_Id");
            Assert.False(result.Success);
            Assert.Contains("找不到", result.Message);
        }
    }
}
