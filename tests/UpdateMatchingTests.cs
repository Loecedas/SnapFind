using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PixOcrSearch.Tests
{
    public class UpdateMatchingTests
    {
        private static string? SelectAsset(
            IEnumerable<string> assets,
            string currentEdition,
            bool isInstalled)
        {
            string targetPattern = isInstalled ? $"SnapFindSetup_{currentEdition}_" : $"SnapFindPortable_{currentEdition}_";
            string targetExtension = isInstalled ? ".exe" : ".zip";
            string oppositeEdition = currentEdition == "Rapid" ? "Paddle" : "Rapid";

            foreach (var name in assets)
            {
                if (name.StartsWith(targetPattern, StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(targetExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            string legacyPattern = isInstalled ? "SnapFindSetup_" : "SnapFindPortable_";
            foreach (var name in assets)
            {
                if (name.StartsWith(legacyPattern, StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(targetExtension, StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains(oppositeEdition, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            return null;
        }

        [Fact]
        public void RapidInstalled_MatchesRapidSetup_NotPaddle()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.zip",
                "SnapFindPortable_Rapid_v2.4.8.zip",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = SelectAsset(assets, "Rapid", isInstalled: true);
            Assert.Equal("SnapFindSetup_Rapid_v2.4.8.exe", matched);
        }

        [Fact]
        public void RapidPortable_MatchesRapidPortable_NotPaddle()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.zip",
                "SnapFindPortable_Rapid_v2.4.8.zip",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = SelectAsset(assets, "Rapid", isInstalled: false);
            Assert.Equal("SnapFindPortable_Rapid_v2.4.8.zip", matched);
        }

        [Fact]
        public void PaddleInstalled_MatchesPaddleSetup()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.zip",
                "SnapFindPortable_Rapid_v2.4.8.zip",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = SelectAsset(assets, "Paddle", isInstalled: true);
            Assert.Equal("SnapFindSetup_Paddle_v2.4.8.exe", matched);
        }

        [Fact]
        public void LegacyRelease_MatchesWhenNoEditionPrefix()
        {
            var assets = new[]
            {
                "SnapFindPortable_v2.4.5.zip",
                "SnapFindSetup_v2.4.5.exe"
            };

            var matched = SelectAsset(assets, "Rapid", isInstalled: true);
            Assert.Equal("SnapFindSetup_v2.4.5.exe", matched);
        }

        [Fact]
        public void Rapid_DoesNotFallbackToPaddle_WhenRapidMissing()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.zip",
                "SnapFindSetup_Paddle_v2.4.8.exe"
            };

            var matched = SelectAsset(assets, "Rapid", isInstalled: true);
            Assert.Null(matched);
        }
    }
}
