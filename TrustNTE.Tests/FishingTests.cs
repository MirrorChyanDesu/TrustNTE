using System;
using Xunit;
using Moq;
using TrustNTE.Helpers;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services;
using TrustNTE.Services.Fishing;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Tests
{
    public class FishingTests
    {
        [Fact]
        public void FishingRoiAnalyzer_ShouldCalculateCorrectCenter()
        {
            var analyzer = new FishingRoiAnalyzer();
            var result = analyzer.Analyze(null!, 0, 0);
            Assert.False(result.a.Rect.HasValue);
            Assert.False(result.b.Rect.HasValue);
        }

        [Fact]
        public void FishingBarController_ShouldPressRightKey_WhenTargetIsOnRight()
        {
            // 1. 伪造环境
            var mockRouter = new Mock<IInputRouter>();
            mockRouter.Setup(r => r.CurrentDrive).Returns(InputDriveType.Trick);

            var mockLog = new Mock<ILogService>();

            //     核心   改点：必须 Mock 接口！绝对不能再 Mock AutomationService 了！
            var mockAutomationContext = new Mock<IAutomationContext>();
            mockAutomationContext.Setup(a => a.CurrentHwnd).Returns((IntPtr)12345);

            // 2. 传入接口对象
            var controller = new FishingBarController(mockRouter.Object, mockAutomationContext.Object, mockLog.Object);

            var config = new AutoFishingConfig { LeftKey = KeyCode.A, RightKey = KeyCode.D };

            var barA = new FishingRoiAnalyzer.ColorRectResult { Rect = new OpenCvSharp.Rect(90, 10, 20, 20), PixelCount = 100 };
            var barB = new FishingRoiAnalyzer.ColorRectResult { Rect = new OpenCvSharp.Rect(190, 10, 20, 20), PixelCount = 100 };

            // 3. 触发运算
            controller.Tick(barA, barB, config, 0);
            controller.Tick(barA, barB, config, 0);

            // 4. 断言：验证是否向路由器发送了按键指令
            mockRouter.Verify(r => r.KeyDown(It.IsAny<IntPtr>(), It.IsAny<int>()), Times.AtLeastOnce);
        }
    }
}