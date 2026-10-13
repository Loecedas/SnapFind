using System;
using System.IO;
using SnapFind.Plugin.Abstractions;

namespace PixOcrSearch
{
    public class HostServices : IHostServices
    {
        public string AppVersion => "2.5.0";
        public string PluginDirectory { get; }
        public string PluginsRootDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        public string ConfigDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

        public HostServices(string pluginDirectory)
        {
            PluginDirectory = pluginDirectory;
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
        }

        public bool RegisterHotkey(string key, Action callback)
        {
            return true;
        }

        public void UnregisterHotkey(string key)
        {
        }
    }
}
