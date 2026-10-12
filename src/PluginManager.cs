using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using SnapFind.Plugin.Translation;

namespace PixOcrSearch
{
    public class PluginInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "1.0.0";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "SnapFind";
        public bool IsActive { get; set; } = true;
        public string TypeName { get; set; } = "Built-in Extension";
        public string Location { get; set; } = "";
        public bool HasHotkey { get; set; } = false;
        public string HotkeyModifiers { get; set; } = "";
        public string HotkeyKey { get; set; } = "";
        public List<string> Features { get; set; } = new List<string>();
    }

    /// <summary>
    /// 全局插件管理器：负责插件生命周期管理、服务发现与跨模块功能分发
    /// </summary>
    public static class PluginManager
    {
        public static bool IsTranslationPluginAvailable
        {
            get
            {
                if (!ConfigManager.Current.EnableTranslation) return false;
                string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
                if (!Directory.Exists(pluginsDir)) return false;

                string pluginDir = Path.Combine(pluginsDir, "SnapFind.Plugin.Translation");
                if (!Directory.Exists(pluginDir)) return false;

                return File.Exists(Path.Combine(pluginDir, "plugin.json")) ||
                       Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories).Length > 0;
            }
        }

        /// <summary>
        /// 唤起翻译窗口
        /// </summary>
        public static void ShowTranslationWindow(string text = "", Window? owner = null)
        {
            if (!IsTranslationPluginAvailable) return;
            try
            {
                TranslationPlugin.ShowWindow(text, owner);
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动翻译插件失败:\n" + ex.Message, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 异步文本翻译
        /// </summary>
        public static async System.Threading.Tasks.Task<string> TranslateAsync(string text, string from = "auto", string to = "zh-CN")
        {
            if (!IsTranslationPluginAvailable) return text;
            return await TranslationPlugin.Translate(text, from, to);
        }

        /// <summary>
        /// 简繁转换 - 转为繁体
        /// </summary>
        public static string ConvertToTraditional(string text)
        {
            if (!IsTranslationPluginAvailable) return text;
            return TranslationPlugin.ConvertToTraditional(text);
        }

        /// <summary>
        /// 简繁转换 - 转为简体
        /// </summary>
        public static string ConvertToSimplified(string text)
        {
            if (!IsTranslationPluginAvailable) return text;
            return TranslationPlugin.ConvertToSimplified(text);
        }

        /// <summary>
        /// 检查指定插件当前是否处于启用激活状态
        /// </summary>
        public static bool IsPluginActive(string id)
        {
            if (string.Equals(id, "SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigManager.Current.EnableTranslation;
            }

            var disabledList = ConfigManager.Current.DisabledPlugins ?? new List<string>();
            return !disabledList.Contains(id);
        }

        /// <summary>
        /// 开启或关闭指定插件
        /// </summary>
        public static void SetPluginActive(string id, bool active)
        {
            if (string.Equals(id, "SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase))
            {
                ConfigManager.Current.EnableTranslation = active;
            }

            if (ConfigManager.Current.DisabledPlugins == null)
            {
                ConfigManager.Current.DisabledPlugins = new List<string>();
            }

            if (active)
            {
                ConfigManager.Current.DisabledPlugins.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                if (!ConfigManager.Current.DisabledPlugins.Exists(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                {
                    ConfigManager.Current.DisabledPlugins.Add(id);
                }
            }

            ConfigManager.Save();

            // 若变更了翻译插件，即时同步热键与托盘菜单
            if (string.Equals(id, "SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase))
            {
                if (System.Windows.Application.Current is App app)
                {
                    app.ReRegisterHotkey();
                    app.UpdateTrayMenuLanguage();
                }
            }
        }

        /// <summary>
        /// 彻底删除指定插件（完全从磁盘物理删除，无任何内置特权）
        /// </summary>
        public static (bool Success, string Message) DeletePlugin(string id)
        {
            var plugins = GetInstalledPlugins();
            var target = plugins.Find(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                return (false, "找不到指定的插件文件或目录。");
            }

            try
            {
                if (Directory.Exists(target.Location))
                {
                    Directory.Delete(target.Location, true);
                }
                else if (File.Exists(target.Location))
                {
                    File.Delete(target.Location);
                }

                if (string.Equals(id, "SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase))
                {
                    ConfigManager.Current.EnableTranslation = false;
                }

                if (ConfigManager.Current.DisabledPlugins != null)
                {
                    ConfigManager.Current.DisabledPlugins.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
                }
                ConfigManager.Save();

                if (System.Windows.Application.Current is App app)
                {
                    app.ReRegisterHotkey();
                    app.UpdateTrayMenuLanguage();
                }

                return (true, Localization.MsgPluginDeleted);
            }
            catch (Exception ex)
            {
                return (false, "删除插件失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 获取所有已安装与加载的插件列表（全量外部扫描，无任何写死内置项）
        /// </summary>
        public static List<PluginInfo> GetInstalledPlugins()
        {
            var list = new List<PluginInfo>();
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginsDir))
            {
                return list;
            }

            try
            {
                foreach (var dir in Directory.GetDirectories(pluginsDir))
                {
                    string dirName = Path.GetFileName(dir);

                    // 严密过滤非插件文件夹（如包、源码、临时缓存等）
                    if (dirName.Equals("packages", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("src", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("cache", StringComparison.OrdinalIgnoreCase) ||
                        dirName.StartsWith("."))
                    {
                        continue;
                    }

                    string pluginJsonPath = Path.Combine(dir, "plugin.json");
                    var dllFiles = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);

                    // 必须包含 plugin.json 或至少一个 dll，才是合法外部扩展插件
                    if (!File.Exists(pluginJsonPath) && dllFiles.Length == 0)
                    {
                        continue;
                    }

                    string id = dirName;
                    string name = dirName;
                    string ver = "1.0.0";
                    string desc = Localization.IsEnglish ? "External extension plugin" : "外部导入扩展插件";
                    string author = "Third Party";
                    var features = new List<string>();
                    bool hasHotkey = false;

                    if (File.Exists(pluginJsonPath))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(pluginJsonPath));
                            var root = doc.RootElement;
                            if (root.TryGetProperty("id", out var ip)) id = ip.GetString() ?? id;
                            
                            string nameKey = Localization.IsEnglish ? "name_en" : "name";
                            if (root.TryGetProperty(nameKey, out var np) && !string.IsNullOrWhiteSpace(np.GetString()))
                            {
                                name = np.GetString()!;
                            }
                            else if (root.TryGetProperty("name", out var npFallback) && !string.IsNullOrWhiteSpace(npFallback.GetString()))
                            {
                                name = npFallback.GetString()!;
                            }

                            if (root.TryGetProperty("version", out var vp)) ver = vp.GetString() ?? ver;

                            string descKey = Localization.IsEnglish ? "description_en" : "description";
                            if (root.TryGetProperty(descKey, out var dp) && !string.IsNullOrWhiteSpace(dp.GetString()))
                            {
                                desc = dp.GetString()!;
                            }
                            else if (root.TryGetProperty("description", out var dpFallback) && !string.IsNullOrWhiteSpace(dpFallback.GetString()))
                            {
                                desc = dpFallback.GetString()!;
                            }

                            if (root.TryGetProperty("author", out var ap)) author = ap.GetString() ?? author;
                            if (root.TryGetProperty("hasHotkey", out var hp)) hasHotkey = hp.GetBoolean();
                            if (root.TryGetProperty("features", out var fp) && fp.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var fItem in fp.EnumerateArray())
                                {
                                    string? fStr = fItem.GetString();
                                    if (!string.IsNullOrEmpty(fStr)) features.Add(fStr);
                                }
                            }
                        }
                        catch { }
                    }

                    // 适配 Translation 插件专有配置
                    if (id.Equals("SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase))
                    {
                        id = "SnapFind.Plugin.Translation";
                        hasHotkey = true;
                        if (string.IsNullOrEmpty(name) || name == dirName)
                        {
                            name = Localization.IsEnglish ? "Translation & Script Conversion" : "文本翻译与繁简转换插件";
                        }
                        if (string.IsNullOrEmpty(desc) || desc == "外部导入扩展插件")
                        {
                            desc = Localization.IsEnglish
                                ? "Integrates Microsoft Edge translation, Win32 native traditional/simplified conversion, and OCR linking."
                                : "集成微软 Edge 在线翻译通道、Win32 原生离线繁简转换、独立翻译窗口与 OCR 结果一键联动。";
                        }
                        if (features.Count == 0)
                        {
                            features.Add(Localization.IsEnglish ? "🌐 Standalone Translation Window" : "🌐 独立翻译工具窗口");
                            features.Add(Localization.IsEnglish ? "⚡ OCR Realtime Translation" : "⚡ OCR 框选结果自动同步翻译");
                            features.Add(Localization.IsEnglish ? "🔄 Win32 Native Offline Script Conversion" : "🔄 Win32 原生离线繁简转换");
                        }
                    }

                    list.Add(new PluginInfo
                    {
                        Id = id,
                        Name = name,
                        Version = ver,
                        Description = desc,
                        Author = author,
                        IsActive = IsPluginActive(id),
                        TypeName = Localization.IsEnglish ? "External Plugin" : "扩展插件",
                        Location = dir,
                        HasHotkey = hasHotkey,
                        HotkeyModifiers = id == "SnapFind.Plugin.Translation" ? ConfigManager.Current.TranslateHotkeyModifiers : "",
                        HotkeyKey = id == "SnapFind.Plugin.Translation" ? ConfigManager.Current.TranslateHotkeyKey : "",
                        Features = features
                    });
                }

                foreach (var dll in Directory.GetFiles(pluginsDir, "*.dll"))
                {
                    string fileName = Path.GetFileName(dll);
                    if (fileName.StartsWith("SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase)) continue;

                    list.Add(new PluginInfo
                    {
                        Id = fileName,
                        Name = fileName,
                        Version = "1.0.0",
                        Description = Localization.IsEnglish ? "External assembly binary extension" : "外部导入扩展插件",
                        Author = "Third Party",
                        IsActive = IsPluginActive(fileName),
                        TypeName = Localization.IsEnglish ? "External Plugin" : "扩展插件",
                        Location = dll
                    });
                }
            }
            catch { }

            return list;
        }
    }
}
