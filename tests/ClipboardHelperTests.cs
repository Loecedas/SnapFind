using System.Diagnostics;
using Xunit;

namespace PixOcrSearch.Tests
{
    public class ClipboardHelperTests
    {
        [Fact]
        public void SetText_FastAndAccurate()
        {
            string testContent = "SnapFind_Test_" + System.Guid.NewGuid().ToString("N");

            var sw = Stopwatch.StartNew();
            bool result = ClipboardHelper.SetText(testContent);
            sw.Stop();

            Assert.True(result, "ClipboardHelper.SetText 应写入成功");
            Assert.True(sw.ElapsedMilliseconds < 500, $"写入耗时应小于 500ms（实际耗时: {sw.ElapsedMilliseconds}ms）");

            string? current = ClipboardHelper.GetTextNative();
            Assert.Equal(testContent, current);
        }

        [Fact]
        public void Clear_EmptiesClipboard()
        {
            ClipboardHelper.SetText("TempTextToClear");
            System.Threading.Thread.Sleep(50);
            bool cleared = ClipboardHelper.Clear();

            Assert.True(cleared);
            string? current = ClipboardHelper.GetTextNative();
            Assert.True(string.IsNullOrEmpty(current), $"Expected empty clipboard, but got: '{current}'");
        }

        [Fact]
        public void SetText_NullOrEmpty_ClearsClipboard()
        {
            ClipboardHelper.SetText("TempText");
            bool emptyResult = ClipboardHelper.SetText("");

            Assert.True(emptyResult);
            string? current = ClipboardHelper.GetTextNative();
            Assert.True(string.IsNullOrEmpty(current), $"Expected empty clipboard, but got: '{current}'");
        }
    }
}
