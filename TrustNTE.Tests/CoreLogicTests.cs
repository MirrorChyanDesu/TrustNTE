using System;
using Xunit;
using TrustNTE.Helpers;
using TrustNTE.Models;
using TrustNTE.Models.TaskConfigs; //     用于 AutoMusicConfig
using System.Text.RegularExpressions;

namespace TrustNTE.Tests
{
    public class CoreLogicTests
    {
        // 测试 1：键码映射是否绝对正确
        [Fact]
        public void KeyCode_Mapping_ShouldBeCorrect()
        {
            Assert.Equal(0x46, KeyCode.F);
            Assert.Equal(0x1B, KeyCode.Esc);
        }

        // 测试 2：RoiBox 字符串解析
        [Fact]
        public void RoiBox_Parse_ShouldHandleValidString()
        {
            var box = RoiBox.Parse("100,200,300,400");
            Assert.Equal(100, box.X1);
            Assert.Equal(200, box.Y1);
            Assert.Equal(200, box.Width);
            Assert.Equal(200, box.Height);
        }

        // 测试 3：调色盘的颜色拦截逻辑
        [Fact]
        public void LogPalette_Pick_ShouldReturnCorrectColor()
        {
            var config = new AppConfig();
            LogPalette.UpdateFromConfig(config);

            var redBrush = LogPalette.Pick("[错误] 找不到文件！");
            var defaultBrush = LogPalette.Pick("普通的信息");

            Assert.NotNull(redBrush);
            Assert.NotNull(defaultBrush);
            Assert.NotSame(redBrush, defaultBrush);
        }

        // 测试 4：日志隐私脱敏正则
        [Fact]
        public void LogSanitization_ShouldHideUserNames()
        {
            var regex = new Regex("C:\\\\Users\\\\[^\\\\]+", RegexOptions.IgnoreCase);
            string originalLog = "C:\\Users\\张三\\Desktop\\TrustNTE\\config.json 读取失败";
            string safeLog = regex.Replace(originalLog, "C:\\Users\\***");

            Assert.DoesNotContain("张三", safeLog);
            Assert.Contains("***", safeLog);
        }

        //     测试 5：自动音游配置解析与健壮性
        [Fact]
        public void AutoMusicConfig_ShouldHaveValidDefaultKeys()
        {
            var config = new AutoMusicConfig();

            // 验证音游默认键位是否合法
            Assert.Equal("D", config.KeyD);
            Assert.Equal("F", config.KeyF);
            Assert.Equal("J", config.KeyJ);
            Assert.Equal("K", config.KeyK);

            // 验证默认音符检测阈值是否在合理范围 (0 到 1 之间)
            Assert.InRange(config.DarkTriggerRatio, 0.01, 1.0);

            // 验证默认按住时间不可能是负数
            Assert.True(config.KeyDownTimeMs > 0);
        }
    }
}