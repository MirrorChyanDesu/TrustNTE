using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;
using Size = OpenCvSharp.Size;
using Rect = OpenCvSharp.Rect;

namespace TrustNTE.Services.Triggers
{
    /// <summary>
    /// 粉爪银行拾取触发器（纯颜色模式）。
    /// 识别拾取提示的粉色药丸横条（HSV 阈值 + 高度/宽高比过滤）。
    /// </summary>
    public class PinkPawPickupTrigger : ITaskTrigger, IDisposable
    {
        // ============ 扫描节奏 ============
        private const int ScanIntervalMs = 350;
        private const int MissStreakBackoff = 8;
        private const int BackoffScanIntervalMs = 700;
        private const int SameSlotCooldownMs = 8000;
        private const int SameSlotRadiusPx = 32;
        private const int AfterPressCooldownMs = 800;

        // ============ 颜色检测参数 ============
        // 粉色 HSV 区间（OpenCV H:0-180）。漏检 → 放宽 S/V 下限；误检 → 收紧 H 区间
        private static readonly Scalar PinkHsvLo = new(140, 70, 60);
        private static readonly Scalar PinkHsvHi = new(178, 255, 255);
        // 药丸形尺寸（1080P 基准）：以"高度"为尺寸基准（横条的高 ≈ 30-45px）
        private const float PillHeightMinPx = 18f;
        private const float PillHeightMaxPx = 90f;
        // 宽高比范围：药丸 3~4 倍；放宽到 1.2~8.0 兼顾短提示与长物品名
        private const double PillAspectMin = 1.2;
        private const double PillAspectMax = 8.0;
        // 最小粉色面积（px²）：杀噪点；药丸粉底面积远超此值
        private const double MinBlobAreaPx = 600;
        public string Name => "粉爪拾取";
        public bool IsEnabled { get; set; } = true;
        public Func<int, int, (int, int)>? CoordMapper { get; set; }

        private readonly IConfigService _configService;
        private readonly IInputService _inputService;
        private readonly INteInteractionService _nteInteraction;
        private readonly ILogService _logService;
        private readonly IntPtr _hwnd;

        private DateTime _lastScanTime = DateTime.MinValue;
        private int _missStreak = 0;
        private int _pressingBusy = 0;
        private Point _lastPressCenter;
        private DateTime _lastPressTime = DateTime.MinValue;
        private Rect? _lastHitRect;
        private string _lastHitInfo = string.Empty;
        private DateTime _lastHitTime = DateTime.MinValue;

        public PinkPawPickupTrigger(
            IConfigService configService, IInputService inputService,
            INteInteractionService nteInteraction, ILogService logService, IntPtr hwnd)
        {
            _configService = configService;
            _inputService = inputService;
            _nteInteraction = nteInteraction;
            _logService = logService;
            _hwnd = hwnd;
        }

        public void DrawDebug(Mat drawMat)
        {
            var config = _configService.Load();
            if (!IsEnabled || !config.PinkPawPickupEnabled) return;

            // 画出检测 ROI 的框（黄色）
            var roi = ParseRoi(config.PinkPawScanRoi, drawMat.Width, drawMat.Height);
            if (roi.HasValue) Cv2.Rectangle(drawMat, roi.Value, new Scalar(0, 255, 255), 1);

            if (_lastHitRect.HasValue && (DateTime.Now - _lastHitTime).TotalSeconds <= 1)
            {
                Cv2.Rectangle(drawMat, _lastHitRect.Value, new Scalar(0, 255, 0), 2);
                Cv2.PutText(drawMat, _lastHitInfo,
                    new Point(_lastHitRect.Value.X, Math.Max(12, _lastHitRect.Value.Y - 5)),
                    HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
            }
        }

        public void OnCapture(Mat resizedMat, double scale)
        {
            var config = _configService.Load();
            if (!config.PinkPawPickupEnabled) return;
            if (Interlocked.CompareExchange(ref _pressingBusy, 0, 0) == 1) return;

            int scanInterval = _missStreak >= MissStreakBackoff ? BackoffScanIntervalMs : ScanIntervalMs;
            if ((DateTime.Now - _lastScanTime).TotalMilliseconds < scanInterval) return;
            _lastScanTime = DateTime.Now;

            // IncludeDialogue=false → 配置 ROI（拾取提示区）；true → 全帧（连对话药丸也要）
            var roi = config.PinkPawIncludeDialogue
                ? new Rect(0, 0, resizedMat.Width, resizedMat.Height)
                : ParseRoi(config.PinkPawScanRoi, resizedMat.Width, resizedMat.Height)
                  ?? new Rect(0, 0, resizedMat.Width, resizedMat.Height);

            // 纯颜色检测
            Rect? hit = DetectPinkPill(resizedMat, roi, out var blobInfo);
            string hitLabel = $"药丸({blobInfo})";
            string logAction = $"[粉爪拾取] 命中 {hitLabel}";

            if (hit == null) { _missStreak++; return; }
            _missStreak = 0;

            var center = new Point(hit.Value.X + hit.Value.Width / 2, hit.Value.Y + hit.Value.Height / 2);
            _lastHitRect = hit.Value;
            _lastHitInfo = hitLabel;
            _lastHitTime = DateTime.Now;

            // 同槽冷却：掐断"提示未被消耗 → 无限重按"循环
            if ((DateTime.Now - _lastPressTime).TotalMilliseconds < SameSlotCooldownMs &&
                Math.Abs(center.X - _lastPressCenter.X) <= SameSlotRadiusPx &&
                Math.Abs(center.Y - _lastPressCenter.Y) <= SameSlotRadiusPx)
            {
                return;
            }

            int count = Math.Max(1, config.PinkPawFPressCount);
            int pressIntervalMs = Math.Max(100, config.PinkPawFPressIntervalMs);
            string driveType = config.MouseDriveType ?? "物理点击";

            _logService.LogInfo($"{logAction} @ ({hit.Value.X},{hit.Value.Y}) " +
                                $"宽高({hit.Value.Width},{hit.Value.Height}) → 按下 F ×{count}");

            _lastPressCenter = center;
            _lastPressTime = DateTime.Now;

            if (Interlocked.Exchange(ref _pressingBusy, 1) == 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            ExecuteKeyPress(driveType);
                            await Task.Delay(pressIntervalMs);
                        }
                        await Task.Delay(AfterPressCooldownMs);
                    }
                    catch { }
                    finally { Interlocked.Exchange(ref _pressingBusy, 0); }
                });
            }
        }

        /// <summary>
        /// 粉色药丸检测：ROI 内 HSV 阈值 → 找粉色色块 → 高度/宽高比过滤 → 返回最大命中块。
        /// </summary>
        private static Rect? DetectPinkPill(Mat frame, Rect searchRoi, out string blobInfo)
        {
            blobInfo = string.Empty;
            Rect actual = searchRoi.Intersect(new Rect(0, 0, frame.Width, frame.Height));
            if (actual.Width <= 0 || actual.Height <= 0) return null;

            using var roiMat = new Mat(frame, actual);
            using var hsv = new Mat();
            Cv2.CvtColor(roiMat, hsv, ColorConversionCodes.BGR2HSV);
            using var mask = new Mat();
            Cv2.InRange(hsv, PinkHsvLo, PinkHsvHi, mask);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);   // 去噪点

            Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            Rect? best = null;
            double bestArea = 0;
            foreach (var c in contours)
            {
                double area = Cv2.ContourArea(c);
                if (area < MinBlobAreaPx) continue;

                var br = Cv2.BoundingRect(c);
                float height = br.Height;
                if (height < PillHeightMinPx || height > PillHeightMaxPx) continue;

                double aspect = (double)br.Width / Math.Max(1, br.Height);
                if (aspect < PillAspectMin || aspect > PillAspectMax) continue;

                if (area > bestArea)
                {
                    bestArea = area;
                    best = br;
                    blobInfo = $"{br.Width}x{br.Height}/a{aspect:F1}";
                }
            }

            if (best == null) return null;
            return new Rect(best.Value.X + actual.X, best.Value.Y + actual.Y, best.Value.Width, best.Value.Height);
        }

        /// <summary>按"鼠标驱动"三通道路由执行按键</summary>
        private void ExecuteKeyPress(string driveType)
        {
            const int keyF = KeyCode.F;
            switch (driveType)
            {
                case "后台点击": _inputService.SendKeyPress(_hwnd, keyF, 50); break;
                case "通用点击": _nteInteraction.NteKeyPress(_hwnd, keyF, 50); break;
                case "物理点击":
                default:
                    Win32Input.FakeForeground(_hwnd);
                    Win32Input.KeyDown(keyF); Thread.Sleep(50); Win32Input.KeyUp(keyF);
                    break;
            }
        }

        private static Rect? ParseRoi(string roiStr, int frameW, int frameH)
        {
            if (string.IsNullOrWhiteSpace(roiStr)) return null;
            var p = roiStr.Split(',');
            if (p.Length != 4) return null;
            if (!int.TryParse(p[0].Trim(), out var x1) || !int.TryParse(p[1].Trim(), out var y1)
                || !int.TryParse(p[2].Trim(), out var x2) || !int.TryParse(p[3].Trim(), out var y2))
                return null;
            var rect = new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
            return rect.Intersect(new Rect(0, 0, frameW, frameH));
        }

        public void Dispose()
        {
            // 纯颜色检测
        }
    }
}