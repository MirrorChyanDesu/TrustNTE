using Xunit;
using TrustNTE.Services.Game;

namespace TrustNTE.Tests
{
    public class CaptureTests
    {
        [Fact]
        public void ScreenCapturer_NormalizeMode_ShouldParseCorrectly()
        {
            Assert.Equal("PrintWindow", ScreenCapturer.NormalizeMode("① PrintWindow"));
            Assert.Equal("BitBlt", ScreenCapturer.NormalizeMode("④ BitBlt"));
            Assert.Equal("Screen", ScreenCapturer.NormalizeMode("DXGI"));
            Assert.Equal("WGC", ScreenCapturer.NormalizeMode("WGC"));

            // 传空值应该默认给 PrintWindow
            Assert.Equal("PrintWindow", ScreenCapturer.NormalizeMode(null));
        }
    }
}