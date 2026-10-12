using System.Collections.Generic;
using Xunit;

namespace PixOcrSearch.Tests
{
    public class UpdateMatchingTests
    {
        [Fact]
        public void RapidInstalled_MatchesRapidSetup_Not7zExe_NotPaddle()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.7z.exe",
                "SnapFindPortable_Rapid_v2.4.8.7z.exe",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: true);
            Assert.Equal("SnapFindSetup_Rapid_v2.4.8.exe", matched);
        }

        [Fact]
        public void RapidPortable_MatchesRapid7zExe_NotPaddle_NotSetup()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.7z.exe",
                "SnapFindPortable_Rapid_v2.4.8.7z.exe",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: false);
            Assert.Equal("SnapFindPortable_Rapid_v2.4.8.7z.exe", matched);
        }

        [Fact]
        public void RapidPortable_Prefers7zExe_OverZip()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.7z.exe",
                "SnapFindPortable_Rapid_v2.4.8.zip",
                "SnapFindPortable_Rapid_v2.4.8.7z.exe",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: false);
            Assert.Equal("SnapFindPortable_Rapid_v2.4.8.7z.exe", matched);
        }

        [Fact]
        public void RapidPortable_FallsBackToZip_WhenNo7zExe()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.zip",
                "SnapFindPortable_Rapid_v2.4.8.zip",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: false);
            Assert.Equal("SnapFindPortable_Rapid_v2.4.8.zip", matched);
        }

        [Fact]
        public void PaddlePortable_MatchesPaddle7zExe_NotRapid_NotSetup()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.7z.exe",
                "SnapFindPortable_Rapid_v2.4.8.7z.exe",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = UpdateHelper.SelectAsset(assets, "Paddle", isInstalled: false);
            Assert.Equal("SnapFindPortable_Paddle_v2.4.8.7z.exe", matched);
        }

        [Fact]
        public void PaddleInstalled_MatchesPaddleSetup_NotRapid_NotPortable()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.7z.exe",
                "SnapFindPortable_Rapid_v2.4.8.7z.exe",
                "SnapFindSetup_Paddle_v2.4.8.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matched = UpdateHelper.SelectAsset(assets, "Paddle", isInstalled: true);
            Assert.Equal("SnapFindSetup_Paddle_v2.4.8.exe", matched);
        }

        [Fact]
        public void LegacyRelease_MatchesWhenNoEditionPrefix()
        {
            var assets = new[]
            {
                "SnapFindPortable_v2.4.5.7z.exe",
                "SnapFindSetup_v2.4.5.exe"
            };

            var matchedInstaller = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: true);
            Assert.Equal("SnapFindSetup_v2.4.5.exe", matchedInstaller);

            var matchedPortable = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: false);
            Assert.Equal("SnapFindPortable_v2.4.5.7z.exe", matchedPortable);
        }

        [Fact]
        public void Rapid_DoesNotFallbackToPaddle_WhenRapidMissing()
        {
            var assets = new[]
            {
                "SnapFindPortable_Paddle_v2.4.8.7z.exe",
                "SnapFindSetup_Paddle_v2.4.8.exe"
            };

            var matchedInstaller = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: true);
            Assert.Null(matchedInstaller);

            var matchedPortable = UpdateHelper.SelectAsset(assets, "Rapid", isInstalled: false);
            Assert.Null(matchedPortable);
        }

        [Fact]
        public void Paddle_DoesNotFallbackToRapid_WhenPaddleMissing()
        {
            var assets = new[]
            {
                "SnapFindPortable_Rapid_v2.4.8.7z.exe",
                "SnapFindSetup_Rapid_v2.4.8.exe"
            };

            var matchedInstaller = UpdateHelper.SelectAsset(assets, "Paddle", isInstalled: true);
            Assert.Null(matchedInstaller);

            var matchedPortable = UpdateHelper.SelectAsset(assets, "Paddle", isInstalled: false);
            Assert.Null(matchedPortable);
        }
    }
}
