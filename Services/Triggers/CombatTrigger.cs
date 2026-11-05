using OpenCvSharp;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    public class CombatTrigger : ITaskTrigger
    {
        public string Name => "基础战斗";
        public bool IsEnabled { get; set; } = false;
        private readonly ILogService _logService;

        public CombatTrigger(ILogService logService)
        {
            _logService = logService;
        }

        public void DrawDebug(Mat drawMat) { } // 留空

        public void OnCapture(Mat resizedMat, double scale)
        {
            if (!IsEnabled) return;
            // TODO: 基础监控
        }
    }
}