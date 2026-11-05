using OpenCvSharp;
using System;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    public class AutoLootTrigger : ITaskTrigger
    {
        public string Name => "自动拾取";
        public bool IsEnabled { get; set; } = false;

        public Func<int, int, (int, int)>? CoordMapper { get; set; }

        private readonly IConfigService _configService;
        private readonly IInputService _inputService;
        private readonly IntPtr _hwnd;
        private DateTime _lastClickTime = DateTime.MinValue;

        public AutoLootTrigger(IConfigService configService, IInputService inputService, IntPtr hwnd)
        {
            _configService = configService;
            _inputService = inputService;
            _hwnd = hwnd;
        }

        public void DrawDebug(Mat drawMat)
        {
            if (!IsEnabled) return;
            // 以后有拾取框了，可以在这里画：Cv2.Rectangle(drawMat, LootRoi, new Scalar(0, 255, 0), 2);
        }

        public void OnCapture(Mat resizedMat, double scale)
        {
            var config = _configService.Load();
            if (!config.AutoLootEnabled) return;
            if ((DateTime.Now - _lastClickTime).TotalMilliseconds <= 1000) return;

            // TODO: 在这里加上找 "F" 键图标 的逻辑
        }
    }
}