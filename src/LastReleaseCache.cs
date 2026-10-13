using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PixOcrSearch
{
    public sealed class ReleaseCacheEntry
    {
        public string TagName { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public static class LastReleaseCache
    {
        private static readonly object _ioLock = new object();

        public static string GetCacheFilePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "cache", "last_release.json");
        }

        public static ReleaseCacheEntry? Load()
        {
            lock (_ioLock)
            {
                try
                {
                    string path = GetCacheFilePath();
                    if (!File.Exists(path))
                    {
                        return null;
                    }

                    string json = File.ReadAllText(path);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return null;
                    }

                    return JsonSerializer.Deserialize<ReleaseCacheEntry>(json);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static async Task SaveAsync(string tagName, string body)
        {
            if (string.IsNullOrWhiteSpace(tagName) && string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            await Task.Run(() =>
            {
                lock (_ioLock)
                {
                    try
                    {
                        string path = GetCacheFilePath();
                        string? dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        var entry = new ReleaseCacheEntry
                        {
                            TagName = tagName ?? "",
                            Body = body ?? "",
                            CachedAtUtc = DateTime.UtcNow
                        };

                        string json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
                        string tmpPath = path + ".tmp";
                        File.WriteAllText(tmpPath, json);

                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                        File.Move(tmpPath, path);
                    }
                    catch
                    {
                        // Best effort cache; do not fail caller
                    }
                }
            });
        }
    }
}