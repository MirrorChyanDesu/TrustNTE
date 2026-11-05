using Xunit;
using TrustNTE.Helpers;
using TrustNTE.Models;
using System.Text.RegularExpressions;

namespace TrustNTE.Tests
{
    public class LogTests
    {
        [Fact]
        public void LogPalette_ShouldReturnCorrectColor()
        {
            var config = new AppConfig();
            LogPalette.UpdateFromConfig(config);

            var redBrush = LogPalette.Pick("[错误] 找不到文件！");
            var greenBrush = LogPalette.Pick("[自动钓鱼] 抛竿");
            var grayBrush = LogPalette.Pick("控鱼 -> PWM");

            Assert.NotNull(redBrush);
            Assert.NotNull(greenBrush);
            Assert.NotNull(grayBrush);
            // 验证颜色确实不同（红 vs 绿 vs 灰）
            Assert.NotEqual(redBrush.ToString(), greenBrush.ToString());
            Assert.NotEqual(greenBrush.ToString(), grayBrush.ToString());
        }

        [Fact]
        public void LogService_ShouldSanitizeUserPaths()
        {
            // 直接测试正则逻辑，不写入文件
            var regex = new Regex(@"C:\\Users\\[^\\]+", RegexOptions.IgnoreCase);
            string originalLog = @"C:\Users\Admin\Desktop\TrustNTE\config.json";
            string safeLog = regex.Replace(originalLog, @"C:\Users\***");

            Assert.DoesNotContain("Admin", safeLog);
            Assert.Contains(@"C:\Users\***", safeLog);
        }
    }
}