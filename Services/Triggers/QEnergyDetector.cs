using System;
using OpenCvSharp;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    /// <summary>
    /// Q 能量（终结技）就绪检测器：
    /// - 每帧分析底部技能栏 Q 图标区域的亮像素占比，判断能量环是否充满
    /// - IsReady：亮占比 ≥ ReadyThreshold 即认为可施放
    /// - NotifyCast：施放后进入短冷却，防止能量读数回落前重复触发
    /// - ROI 与阈值用战斗调试窗口校准（DrawDebug 会画出检测区）
    /// </summary>
    public sealed class QEnergyDetector : IDisposable
    {
        private readonly ILogService _logService;

        // ===== 校准区（1080P 逻辑坐标）=====
        // TODO 战斗调试窗口校准：让框正好罩住 Q 技能图标（含能量环）
        private static readonly Rect QSlotRoi = new(870, 930, 100, 120);

        // ===== 判定阈值 =====
        private const double ReadyThreshold = 0.55;   // 亮像素占比 ≥ 此值 = 充能完毕
        private const double BrightPixelVMin = 170;   // HSV V 通道高于此算"亮"像素

        // ===== 施放冷却 =====
        private const double CastCooldownSeconds = 1.5;

        private DateTime _castCooldownUntil = DateTime.MinValue;
        private double _lastBright;   // 0~1

        public QEnergyDetector(ILogService logService)
        {
            _logService = logService;
        }

        /// <summary>最近一次读数（0~1，亮像素占比）。</summary>
        public double LastBright => _lastBright;

        /// <summary>Q 是否已充满可施放。</summary>
        public bool IsReady => _lastBright >= ReadyThreshold;

        /// <summary>施放后的短冷却中（防重复触发）。</summary>
        public bool InCastCooldown => DateTime.Now < _castCooldownUntil;

        /// <summary>施放后调用，开启冷却窗口。</summary>
        public void NotifyCast() => _castCooldownUntil = DateTime.Now.AddSeconds(CastCooldownSeconds);

        public void Analyze(Mat frame)
        {
            try
            {
                // ROI 越界保护
                int x = Math.Max(0, QSlotRoi.X), y = Math.Max(0, QSlotRoi.Y);
                int w = Math.Min(frame.Width - x, QSlotRoi.Width);
                int h = Math.Min(frame.Height - y, QSlotRoi.Height);
                if (w < 8 || h < 8) { _lastBright = 0; return; }

                using var roi = new Mat(frame, new Rect(x, y, w, h));
                using var hsv = new Mat();
                Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);

                // 取 V 通道算亮像素占比（红线3：全部 using，无成员级 Mat）
                using var v = new Mat();
                Cv2.ExtractChannel(hsv, v, 2);
                using var bright = new Mat();
                Cv2.InRange(v, new Scalar(BrightPixelVMin), new Scalar(255), bright);
                _lastBright = Cv2.CountNonZero(bright) / (double)(w * h);
            }
            catch
            {
                // 单帧异常不影响主循环
            }
        }

        public void DrawDebug(Mat frame)
        {
            bool ready = IsReady;
            var color = ready ? Scalar.LimeGreen : Scalar.Gray;
            Cv2.Rectangle(frame, QSlotRoi, color, 2);
            Cv2.PutText(frame, $"Q {LastBright:P0}{(ready ? " READY" : "")}",
                new Point(QSlotRoi.X, Math.Max(14, QSlotRoi.Y - 6)),
                HersheyFonts.HersheySimplex, 0.55, color, 1);
        }

        public void Dispose()
        {
            // 所有 Mat 均为 using 局部变量，无成员级非托管资源
        }
    }
}
