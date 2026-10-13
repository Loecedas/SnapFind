#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using PixOcrSearch;
using Xunit;

namespace SnapFind.Tests
{
    public class LastReleaseCacheTests
    {
        [Fact]
        public async Task SaveAndLoad_RoundTrip_WorksSuccessfully()
        {
            string cachePath = LastReleaseCache.GetCacheFilePath();
            string? backup = null;
            if (File.Exists(cachePath))
            {
                backup = File.ReadAllText(cachePath);
            }

            try
            {
                string testTag = "v99.9.9";
                string testBody = "### Test Changelog\n- Feature X";

                await LastReleaseCache.SaveAsync(testTag, testBody);
                var entry = LastReleaseCache.Load();

                Assert.NotNull(entry);
                Assert.Equal(testTag, entry!.TagName);
                Assert.Equal(testBody, entry.Body);
            }
            finally
            {
                if (backup != null)
                {
                    File.WriteAllText(cachePath, backup);
                }
                else if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }
            }
        }

        [Fact]
        public void Load_CorruptedFile_ReturnsNullWithoutThrowing()
        {
            string cachePath = LastReleaseCache.GetCacheFilePath();
            string? backup = null;
            if (File.Exists(cachePath))
            {
                backup = File.ReadAllText(cachePath);
            }

            try
            {
                string? dir = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(cachePath, "{ invalid json content !!!");
                var entry = LastReleaseCache.Load();
                Assert.Null(entry);
            }
            finally
            {
                if (backup != null)
                {
                    File.WriteAllText(cachePath, backup);
                }
                else if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }
            }
        }
    }
}
