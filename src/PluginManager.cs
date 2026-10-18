using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using SnapFind.Plugin.Abstractions;

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

    public static class PluginManager
    {
        private static readonly Dictionary<string, LoadedPluginInstance> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyDictionary<string, LoadedPluginInstance> LoadedPlugins => _loadedPlugins;

        public static bool IsTranslationPluginAvailable
        {
            get
            {
                if (!ConfigManager.Current.EnableTranslation) return false;
                return _loadedPlugins.ContainsKey("SnapFind.Plugin.Translation");
            }
        }

        public static LoadedPluginInstance? LoadAndRegisterPlugin(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                return null;
            }

            try
            {
                var instance = PluginLoader.LoadPlugin(dir);
                if (instance == null)
                {
                    return null;
                }

                string id = instance.Manifest.Id;
                UnloadPluginByIdOrDirectory(id);
                _loadedPlugins[id] = instance;

                var host = new HostServices(dir);
                instance.Plugin.InitializeAsync(host).GetAwaiter().GetResult();

                if (IsPluginActive(id))
                {
                    instance.Plugin.ActivateAsync().GetAwaiter().GetResult();
                }

                return instance;
            }
            catch
            {
                return null;
            }
        }

        public static void InitializeAll()
        {
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (!Directory.Exists(pluginsDir)) return;

            foreach (var dir in Directory.GetDirectories(pluginsDir))
            {
                LoadAndRegisterPlugin(dir);
            }
        }

        public static FrameworkElement? CreateOcrPanel(string pluginId, System.Windows.Controls.TextBox resultTextBox)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var instance) && IsPluginActive(pluginId))
            {
                if (instance.Plugin is IUiContribution ui)
                {
                    return ui.CreateOcrPanel(resultTextBox);
                }
            }
            return null;
        }

        public static void UpdateTranslationOptions()
        {
            if (!_loadedPlugins.TryGetValue("SnapFind.Plugin.Translation", out var instance)) return;
            try
            {
                var plugin = instance.Plugin;
                var pluginType = plugin.GetType();
                var configureMethod = pluginType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance);
                if (configureMethod != null)
                {
                    var parameters = configureMethod.GetParameters();
                    if (parameters.Length > 0)
                    {
                        var optionsType = parameters[0].ParameterType;
                        var optionsObj = Activator.CreateInstance(optionsType);
                        var targetProp = optionsType.GetProperty("DefaultTargetLanguage");
                        targetProp?.SetValue(optionsObj, ConfigManager.Current.DefaultTargetLanguage);
                        configureMethod.Invoke(plugin, new object?[] { optionsObj });
                    }
                }
            }
            catch
            {
            }
        }

        public static void ShowTranslationWindow(string text = "", Window? owner = null)
        {
            if (!_loadedPlugins.TryGetValue("SnapFind.Plugin.Translation", out var instance)) return;
            try
            {
                UpdateTranslationOptions();
                var plugin = instance.Plugin;
                var method = plugin.GetType().GetMethod("OpenTranslateWindow", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(plugin, new object?[] { text, owner });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动翻译插件失败:\n" + ex.Message, Localization.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static async System.Threading.Tasks.Task<string> TranslateAsync(string text, string from = "auto", string to = "zh-CN")
        {
            if (!_loadedPlugins.TryGetValue("SnapFind.Plugin.Translation", out var instance)) return text;
            try
            {
                var plugin = instance.Plugin;
                var method = plugin.GetType().GetMethod("TranslateAsync", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    var task = method.Invoke(plugin, new object[] { text, "Microsoft", from, to }) as System.Threading.Tasks.Task<string>;
                    if (task != null)
                    {
                        return await task;
                    }
                }
            }
            catch { }
            return text;
        }

        public static string ConvertToTraditional(string text)
        {
            if (!_loadedPlugins.TryGetValue("SnapFind.Plugin.Translation", out var instance)) return text;
            try
            {
                var plugin = instance.Plugin;
                var method = plugin.GetType().GetMethod("ToTraditional", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    return method.Invoke(plugin, new object[] { text }) as string ?? text;
                }
            }
            catch { }
            return text;
        }

        public static string ConvertToSimplified(string text)
        {
            if (!_loadedPlugins.TryGetValue("SnapFind.Plugin.Translation", out var instance)) return text;
            try
            {
                var plugin = instance.Plugin;
                var method = plugin.GetType().GetMethod("ToSimplified", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    return method.Invoke(plugin, new object[] { text }) as string ?? text;
                }
            }
            catch { }
            return text;
        }

        public static bool IsPluginActive(string id)
        {
            if (string.Equals(id, "SnapFind.Plugin.Translation", StringComparison.OrdinalIgnoreCase))
            {
                return ConfigManager.Current.EnableTranslation;
            }

            var disabledList = ConfigManager.Current.DisabledPlugins ?? new List<string>();
            return !disabledList.Contains(id);
        }

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
                if (_loadedPlugins.TryGetValue(id, out var inst))
                {
                    inst.Plugin.ActivateAsync().GetAwaiter().GetResult();
                }
            }
            else
            {
                if (!ConfigManager.Current.DisabledPlugins.Exists(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                {
                    ConfigManager.Current.DisabledPlugins.Add(id);
                }
                if (_loadedPlugins.TryGetValue(id, out var inst))
                {
                    inst.Plugin.DeactivateAsync().GetAwaiter().GetResult();
                }
            }

            ConfigManager.Save();
        }

        private static void SafeUnloadPluginInstance(LoadedPluginInstance instance)
        {
            try
            {
                instance.Dispose();
            }
            catch { }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private static void UnloadPluginByIdOrDirectory(string idOrDir)
        {
            string? foundKey = null;
            LoadedPluginInstance? foundInstance = null;

            foreach (var pair in _loadedPlugins)
            {
                if (string.Equals(pair.Key, idOrDir, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Value.PluginDirectory, idOrDir, StringComparison.OrdinalIgnoreCase))
                {
                    foundKey = pair.Key;
                    foundInstance = pair.Value;
                    break;
                }
            }

            if (foundKey != null && foundInstance != null)
            {
                _loadedPlugins.Remove(foundKey);
                SafeUnloadPluginInstance(foundInstance);
            }
        }

        private static void MoveOrCopyDirectory(string source, string target)
        {
            if (string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(target)), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.Move(source, target);
                    return;
                }
                catch { }
            }

            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(source, file);
                string destFile = Path.Combine(target, rel);
                string? destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, destFile, true);
            }
            SafeDeleteDirectory(source);
        }

        private static bool SafeDeleteDirectory(string path, int maxRetries = 3, int delayMs = 50)
        {
            if (!Directory.Exists(path)) return true;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    Directory.Delete(path, true);
                    return true;
                }
                catch
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            try
            {
                string tempDeleted = Path.Combine(Path.GetTempPath(), $"SnapFind_{Path.GetFileName(path)}_{Guid.NewGuid():N}.deleted");
                Directory.Move(path, tempDeleted);

                System.Threading.Tasks.Task.Run(() =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        System.Threading.Thread.Sleep(200);
                        try
                        {
                            if (!Directory.Exists(tempDeleted)) break;
                            Directory.Delete(tempDeleted, true);
                            break;
                        }
                        catch { }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"无法删除或移动目录 '{path}': {ex.Message}", ex);
            }
        }

        private static bool SafeDeleteFile(string path, int maxRetries = 3, int delayMs = 50)
        {
            if (!File.Exists(path)) return true;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(path);
                    return true;
                }
                catch
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            try
            {
                string tempDeleted = Path.Combine(Path.GetTempPath(), $"SnapFind_{Path.GetFileName(path)}_{Guid.NewGuid():N}.deleted");
                File.Move(path, tempDeleted);

                System.Threading.Tasks.Task.Run(() =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        System.Threading.Thread.Sleep(200);
                        try
                        {
                            if (!File.Exists(tempDeleted)) break;
                            File.Delete(tempDeleted);
                            break;
                        }
                        catch { }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                throw new IOException($"无法删除或移动文件 '{path}': {ex.Message}", ex);
            }
        }

        public static bool ImportPluginPackage(string packagePath, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
                if (!Directory.Exists(pluginsDir)) Directory.CreateDirectory(pluginsDir);

                string ext = Path.GetExtension(packagePath).ToLowerInvariant();
                string targetDir = "";

                if (ext == ".zip")
                {
                    string tempExtract = Path.Combine(Path.GetTempPath(), "SnapFind_Plugin_" + Guid.NewGuid().ToString("N"));
                    System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, tempExtract, true);

                    string manifestFile = Path.Combine(tempExtract, "plugin.json");
                    string pluginId = "";

                    if (File.Exists(manifestFile))
                    {
                        try
                        {
                            var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestFile));
                            if (doc.RootElement.TryGetProperty("id", out var idProp))
                            {
                                pluginId = idProp.GetString() ?? "";
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(pluginId))
                    {
                        var subDirs = Directory.GetDirectories(tempExtract);
                        if (subDirs.Length == 1 && File.Exists(Path.Combine(subDirs[0], "plugin.json")))
                        {
                            try
                            {
                                var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(subDirs[0], "plugin.json")));
                                if (doc.RootElement.TryGetProperty("id", out var idProp))
                                {
                                    pluginId = idProp.GetString() ?? "";
                                }
                            }
                            catch { }
                        }
                    }

                    if (string.IsNullOrEmpty(pluginId))
                    {
                        pluginId = Path.GetFileNameWithoutExtension(packagePath);
                    }

                    targetDir = Path.Combine(pluginsDir, pluginId);

                    UnloadPluginByIdOrDirectory(pluginId);
                    UnloadPluginByIdOrDirectory(targetDir);

                    if (Directory.Exists(targetDir))
                    {
                        SafeDeleteDirectory(targetDir);
                    }

                    if (File.Exists(Path.Combine(tempExtract, "plugin.json")))
                    {
                        MoveOrCopyDirectory(tempExtract, targetDir);
                    }
                    else
                    {
                        var subDirs = Directory.GetDirectories(tempExtract);
                        if (subDirs.Length == 1)
                        {
                            MoveOrCopyDirectory(subDirs[0], targetDir);
                            SafeDeleteDirectory(tempExtract);
                        }
                        else
                        {
                            MoveOrCopyDirectory(tempExtract, targetDir);
                        }
                    }
                }
                else if (ext == ".dll")
                {
                    string fileName = Path.GetFileName(packagePath);
                    string dest = Path.Combine(pluginsDir, fileName);

                    UnloadPluginByIdOrDirectory(dest);
                    if (File.Exists(dest))
                    {
                        SafeDeleteFile(dest);
                    }

                    File.Copy(packagePath, dest, true);
                    return true;
                }
                else
                {
                    errorMessage = "不支持的插件文件格式，仅支持 .zip 插件压缩包或 .dll 程序集。";
                    return false;
                }

                if (!string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir))
                {
                    LoadAndRegisterPlugin(targetDir);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static (bool ok, string errorMessage) DeletePlugin(string idOrLocation)
        {
            try
            {
                string targetPath = idOrLocation;
                string foundKey = "";
                LoadedPluginInstance? targetInstance = null;

                foreach (var pair in _loadedPlugins)
                {
                    if (string.Equals(pair.Key, idOrLocation, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(pair.Value.PluginDirectory, idOrLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        foundKey = pair.Key;
                        targetPath = pair.Value.PluginDirectory;
                        targetInstance = pair.Value;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(foundKey) && targetInstance != null)
                {
                    _loadedPlugins.Remove(foundKey);
                    SafeUnloadPluginInstance(targetInstance);
                }

                if (Directory.Exists(targetPath))
                {
                    SafeDeleteDirectory(targetPath);
                    return (true, "");
                }
                if (File.Exists(targetPath))
                {
                    SafeDeleteFile(targetPath);
                    return (true, "");
                }

                string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", idOrLocation);
                if (Directory.Exists(pluginsDir))
                {
                    SafeDeleteDirectory(pluginsDir);
                    return (true, "");
                }

                return (false, "目标插件文件或目录不存在。");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static void OpenPluginFolder(string location)
        {
            try
            {
                string target = Directory.Exists(location) ? location : Path.GetDirectoryName(location) ?? location;
                if (Directory.Exists(target))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{target}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        public static List<PluginInfo> GetInstalledPlugins()
        {
            var list = new List<PluginInfo>();
            string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

            if (!Directory.Exists(pluginsDir))
            {
                try { Directory.CreateDirectory(pluginsDir); } catch { return list; }
            }

            foreach (var pair in _loadedPlugins)
            {
                var manifest = pair.Value.Manifest;
                list.Add(new PluginInfo
                {
                    Id = manifest.Id,
                    Name = Localization.IsEnglish && !string.IsNullOrEmpty(manifest.NameEn) ? manifest.NameEn : manifest.Name,
                    Version = manifest.Version,
                    Description = (Localization.IsEnglish && !string.IsNullOrEmpty(manifest.DescriptionEn) ? manifest.DescriptionEn : manifest.Description) ?? "",
                    Author = manifest.Author,
                    IsActive = IsPluginActive(manifest.Id),
                    TypeName = Localization.IsEnglish ? "External Plugin" : "扩展插件",
                    Location = pair.Value.PluginDirectory,
                    HasHotkey = manifest.HasHotkey,
                    HotkeyModifiers = manifest.Id == "SnapFind.Plugin.Translation" ? ConfigManager.Current.TranslateHotkeyModifiers : "",
                    HotkeyKey = manifest.Id == "SnapFind.Plugin.Translation" ? ConfigManager.Current.TranslateHotkeyKey : "",
                    Features = Localization.IsEnglish && manifest.FeaturesEn != null && manifest.FeaturesEn.Count > 0 ? manifest.FeaturesEn : (manifest.Features ?? new List<string>())
                });
            }

            return list;
        }
    }
}


