using System;
using System.Threading.Tasks;

namespace SnapFind.Plugin.Abstractions;

public interface IPlugin : IDisposable
{
    PluginManifest Manifest { get; }
    Task InitializeAsync(IHostServices host);
    Task ActivateAsync();
    Task DeactivateAsync();
}

public interface IHostServices
{
    string AppVersion { get; }
    string PluginDirectory { get; }
    string PluginsRootDirectory { get; }
    string ConfigDirectory { get; }
    void Log(string message, LogLevel level = LogLevel.Info);
    bool RegisterHotkey(string key, Action callback);
    void UnregisterHotkey(string key);
}

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error
}
