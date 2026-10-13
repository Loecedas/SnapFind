using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using SnapFind.Plugin.Abstractions;

namespace PixOcrSearch
{
    public sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginMainPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginMainPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Do not reload Abstractions or host assemblies; share them
            if (assemblyName.Name == "SnapFind.Plugin.Abstractions" ||
                assemblyName.Name == "SnapFind")
            {
                return null;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

    public sealed class LoadedPluginInstance : IDisposable
    {
        public PluginManifest Manifest { get; }
        public IPlugin Plugin { get; }
        public PluginLoadContext Context { get; }
        public string PluginDirectory { get; }

        public LoadedPluginInstance(PluginManifest manifest, IPlugin plugin, PluginLoadContext context, string pluginDirectory)
        {
            Manifest = manifest;
            Plugin = plugin;
            Context = context;
            PluginDirectory = pluginDirectory;
        }

        public void Dispose()
        {
            try
            {
                Plugin.Dispose();
            }
            catch { }

            try
            {
                Context.Unload();
            }
            catch { }
        }
    }

    public static class PluginLoader
    {
        public static LoadedPluginInstance? LoadPlugin(string pluginDirectory)
        {
            try
            {
                string manifestPath = Path.Combine(pluginDirectory, "plugin.json");
                if (!File.Exists(manifestPath))
                {
                    return null;
                }

                string manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Entry))
                {
                    return null;
                }

                if (manifest.ApiVersion > PluginApi.Current)
                {
                    return null;
                }

                string entryDllPath = Path.Combine(pluginDirectory, manifest.Entry);
                if (!File.Exists(entryDllPath))
                {
                    return null;
                }

                var alc = new PluginLoadContext(entryDllPath);
                var assembly = alc.LoadFromAssemblyPath(entryDllPath);

                Type? pluginType = null;
                if (!string.IsNullOrWhiteSpace(manifest.EntryType))
                {
                    pluginType = assembly.GetType(manifest.EntryType);
                }

                if (pluginType == null)
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            pluginType = type;
                            break;
                        }
                    }
                }

                if (pluginType == null)
                {
                    alc.Unload();
                    return null;
                }

                var pluginObj = Activator.CreateInstance(pluginType) as IPlugin;
                if (pluginObj == null)
                {
                    alc.Unload();
                    return null;
                }

                return new LoadedPluginInstance(manifest, pluginObj, alc, pluginDirectory);
            }
            catch
            {
                return null;
            }
        }
    }
}
