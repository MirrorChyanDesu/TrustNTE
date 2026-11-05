using OpenCvSharp;
using System;
using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;
using TrustNTE.Services.Triggers;

namespace TrustNTE.Services.Triggers
{
    /// <summary>
    /// 月卡/环石奖励自动领取触发器（独立闹钟机制版）。
    /// </summary>
    public class MonthlyCardTrigger : ITaskTrigger
    {
        /// <summary>点击后冷却：等领取动画/界面切换，防连点</summary>
        private const int ActionCooldownMs = 3000;

        /// <summary>判断区域（1080P 基准）</summary>
        private static readonly Rect SearchRoi = new(547, 664, 856, 364);

        public string Name => "月卡领取";
        public bool IsEnabled { get; set; } = true;
        public Func<int, int, (int, int)>? CoordMapper { get; set; }

        private readonly IConfigService _configService;
        private readonly IInputService _inputService;
        private readonly INteInteractionService _nteInteraction;
        private readonly ILogService _logService;
        private readonly IntPtr _hwnd;
        private readonly MonthlyCardTemplateService _templates;

        private DateTime _lastActionTime = DateTime.MinValue;
        private Rect? _lastHitRect;
        private string _lastHitKey = string.Empty;
        private DateTime _lastHitTime = DateTime.MinValue;

        public MonthlyCardTrigger(
            IConfigService configService, IInputService inputService,
            INteInteractionService nteInteraction, ILogService logService, IntPtr hwnd)
        {
            _configService = configService;
            _inputService = inputService;
            _nteInteraction = nteInteraction;
            _logService = logService;
            _hwnd = hwnd;
            _templates = new MonthlyCardTemplateService(logService);
        }

        public void DrawDebug(Mat drawMat)
        {
            var config = _configService.Load();
            if (!IsEnabled || !config.MonthlyCardEnabled) return;
            Cv2.Rectangle(drawMat, SearchRoi, new Scalar(0, 255, 255), 1);
            if (_lastHitRect.HasValue && (DateTime.Now - _lastHitTime).TotalSeconds <= 1)
            {
                Cv2.Rectangle(drawMat, _lastHitRect.Value, new Scalar(0, 255, 0), 2);
                Cv2.PutText(drawMat, _lastHitKey,
                    new Point(_lastHitRect.Value.X, Math.Max(12, _lastHitRect.Value.Y - 5)),
                    HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
            }
        }

        public void OnCapture(Mat resizedMat, double scale)
        {
            var config = _configService.Load();
            if (!config.MonthlyCardEnabled) return;

            // 1. 获取用于判断的时间基准
            DateTime checkTime = config.MonthlyCardUseLocalTime
                ? DateTime.Now
                : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "China Standard Time");

            // 2. 跨天防重：今天是否已经领过了？
            string todayStr = checkTime.ToString("yyyy-MM-dd");
            if (config.LastMonthlyCardClaimDate == todayStr) return;

            // 3. 闹钟时间窗口判定
            if (!config.MonthlyCardAllDay)
            {
                if (TimeSpan.TryParse(config.MonthlyCardStartTime, out var startTime) &&
                    TimeSpan.TryParse(config.MonthlyCardEndTime, out var endTime))
                {
                    var nowTime = checkTime.TimeOfDay;
                    // 判断是否在时间窗口内（兼容跨天的情况，如 23:50 ~ 00:10）
                    bool inTimeWindow = startTime <= endTime
                        ? (nowTime >= startTime && nowTime <= endTime)
                        : (nowTime >= startTime || nowTime <= endTime);

                    // 没到时间或时间已过，立刻退出，不消耗任何性能
                    if (!inTimeWindow) return;
                }
            }

            // 4. 动作冷却（防止画面闪烁导致连续点击）
            if ((DateTime.Now - _lastActionTime).TotalMilliseconds <= ActionCooldownMs) return;

            // 5. 图像识别
            var hit = _templates.MatchAnyAcrossAllGroups(resizedMat, SearchRoi, out var groupKey);
            if (hit == null) return;

            // 6. 执行点击
            int cx = hit.Value.X + hit.Value.Width / 2;
            int cy = hit.Value.Y + hit.Value.Height / 2;

            string driveType = config.MouseDriveType ?? "物理点击";
            var (px, py) = CoordMapper?.Invoke(cx, cy) ?? (cx, cy);
            switch (driveType)
            {
                case "后台点击": _inputService.SendBackgroundMouseClick(_hwnd, px, py, true); break;
                case "通用点击": _nteInteraction.NteClick(_hwnd, px, py); break;
                case "物理点击":
                default: _inputService.SendPhysicalClickWithRestore(_hwnd, px, py); break;
            }

            _logService.LogInfo($"[月卡奖励] 成功识别并点击领取！今日已标记。");

            // 7. 保存状态，标记今天已领
            _lastActionTime = DateTime.Now;
            _lastHitRect = hit.Value;
            _lastHitKey = groupKey;
            _lastHitTime = DateTime.Now;

            config.LastMonthlyCardClaimDate = todayStr;
            _configService.Save(config);
        }
    }
}