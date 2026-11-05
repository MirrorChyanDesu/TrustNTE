using OpenCvSharp;
using System;

namespace TrustNTE.Tests.DebugTools
{
    public sealed class QEnergyDetector : IDisposable
    {
        private readonly QEnergyConfig _cfg;
        private Mat? _hsv;
        private Mat? _brightMask;
        private Mat? _ringBand;
        private int _hitStreak;

        public QEnergyDetector(QEnergyConfig cfg) => _cfg = cfg;
        public Rect LastRoi { get; private set; }

        public sealed record QEnergyResult(double BrightRatio, bool IsReady, bool Confirmed)
        {
            public static readonly QEnergyResult Empty = new(0, false, false);
        }

        public QEnergyResult Analyze(Mat normalizedFrame)
        {
            int cw = normalizedFrame.Width, ch = normalizedFrame.Height;
            if (cw < _cfg.RoiWidth || ch < _cfg.RoiHeight) return QEnergyResult.Empty;

            int x = _cfg.AnchorRight ? _cfg.RoiX + (cw - 1920) : _cfg.RoiX;
            x = Math.Clamp(x, 0, cw - _cfg.RoiWidth);
            int y = Math.Clamp(_cfg.RoiY, 0, ch - _cfg.RoiHeight);
            LastRoi = new Rect(x, y, _cfg.RoiWidth, _cfg.RoiHeight);

            using var roi = new Mat(normalizedFrame, LastRoi);
            _hsv ??= new Mat(_cfg.RoiHeight, _cfg.RoiWidth, MatType.CV_8UC3);
            Cv2.CvtColor(roi, _hsv, ColorConversionCodes.BGR2HSV);

            _brightMask ??= new Mat();

            // 1. 提取高亮像素
            Cv2.InRange(_hsv, new Scalar(0, 0, _cfg.BrightMinV), new Scalar(180, 255, 255), _brightMask);

            // 2. 环形遮罩
            _ringBand ??= new Mat(_cfg.RoiHeight, _cfg.RoiWidth, MatType.CV_8UC1);
            _ringBand.SetTo(0);
            int cx = _cfg.RoiWidth / 2, cy = _cfg.RoiHeight / 2;
            int r = Math.Min(_cfg.RoiWidth, _cfg.RoiHeight) / 2;
            int rIn = (int)(r * 0.70);
            int rOut = (int)(r * 0.98);

            Cv2.Circle(_ringBand, cx, cy, rOut, Scalar.All(255), -1);
            Cv2.Circle(_ringBand, cx, cy, rIn, Scalar.All(0), -1);

            if (_cfg.ExcludeBottomPx > 0)
            {
                Cv2.Rectangle(_ringBand, new Rect(0, _cfg.RoiHeight - _cfg.ExcludeBottomPx, _cfg.RoiWidth, _cfg.ExcludeBottomPx), Scalar.All(0), -1);
            }

            // 3. 计算占比
            Cv2.BitwiseAnd(_brightMask, _ringBand, _brightMask);
            int ringArea = Cv2.CountNonZero(_ringBand);
            double bright = ringArea > 0 ? Cv2.CountNonZero(_brightMask) / (double)ringArea : 0;

            // 4. 判定
            bool raw = bright >= _cfg.BrightReadyRatio;
            _hitStreak = raw ? _hitStreak + 1 : 0;

            return new QEnergyResult(bright, raw, raw && _hitStreak >= _cfg.ConfirmFrames);
        }

        public Mat? SnapshotLastMask()
        {
            if (_brightMask is not { IsDisposed: false }) return null;
            var bgr = new Mat();
            Cv2.CvtColor(_brightMask, bgr, ColorConversionCodes.GRAY2BGR);
            return bgr;
        }

        public void Dispose()
        {
            _hsv?.Dispose(); _brightMask?.Dispose(); _ringBand?.Dispose();
        }
    }
}