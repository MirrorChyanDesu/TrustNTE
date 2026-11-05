using OpenCvSharp;
using System;

namespace TrustNTE.Services
{
    /// <summary>
    /// 钓鱼控条颜色分析器（只在传入的 ROI 切片内查找）。
    /// </summary>
    public class FishingRoiAnalyzer
    {
        // 区域 A（绿条）：RGB #2fd5b4 -> BGR (180, 213, 47)
        private static readonly Scalar ColorABgr = new Scalar(180, 213, 47);
        // 区域 B（黄指针）：RGB #fef495 -> BGR (149, 244, 254)
        private static readonly Scalar ColorBBgr = new Scalar(149, 244, 254);

        // 容差值 (参考 Python 里的 30)
        private const int ColorTolerance = 30;
        private const int MinPixels = 5;

        public class ColorRectResult
        {
            public int PixelCount { get; set; }
            public Rect? Rect { get; set; } // 全图绝对坐标
            public int CenterX => Rect.HasValue ? Rect.Value.X + Rect.Value.Width / 2 : -1;
        }

        /// <summary>
        /// 分析传入的 ROI 切片，返回其在全图里的绝对坐标。
        /// </summary>
        public (ColorRectResult a, ColorRectResult b) Analyze(Mat roiMat, int offsetX, int offsetY)
        {
            var resultA = new ColorRectResult();
            var resultB = new ColorRectResult();

            if (roiMat == null || roiMat.Empty()) return (resultA, resultB);

            // 1. 找区域 A
            resultA = DetectColor(roiMat, ColorABgr, offsetX, offsetY);

            // 2. 找区域 B
            resultB = DetectColor(roiMat, ColorBBgr, offsetX, offsetY);

            return (resultA, resultB);
        }

        private ColorRectResult DetectColor(Mat roiMat, Scalar targetBgr, int offsetX, int offsetY)
        {
            var result = new ColorRectResult();

            // [   复] Scalar 不能被 using，直接 new 即可
            var lower = new Scalar(
                (byte)Math.Max(0, targetBgr.Val0 - ColorTolerance),
                (byte)Math.Max(0, targetBgr.Val1 - ColorTolerance),
                (byte)Math.Max(0, targetBgr.Val2 - ColorTolerance)
            );

            var upper = new Scalar(
                (byte)Math.Min(255, targetBgr.Val0 + ColorTolerance),
                (byte)Math.Min(255, targetBgr.Val1 + ColorTolerance),
                (byte)Math.Min(255, targetBgr.Val2 + ColorTolerance)
            );

            using var mask = new Mat();
            Cv2.InRange(roiMat, lower, upper, mask);

            result.PixelCount = Cv2.CountNonZero(mask);
            if (result.PixelCount < MinPixels) return result;

            // [   复] 用 FindContours 替代 FindNonZero，兼容性更好
            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours != null && contours.Length > 0)
            {
                // 因为绿条或黄指针可能会被水波纹打断分成两截，所以我们要取所有轮廓的最小包围盒
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = 0, maxY = 0;

                foreach (var contour in contours)
                {
                    var rect = Cv2.BoundingRect(contour);
                    if (rect.X < minX) minX = rect.X;
                    if (rect.Y < minY) minY = rect.Y;
                    if (rect.Right > maxX) maxX = rect.Right;
                    if (rect.Bottom > maxY) maxY = rect.Bottom;
                }

                // 将小图相对坐标转为全图绝对坐标
                result.Rect = new Rect(
                    minX + offsetX,
                    minY + offsetY,
                    maxX - minX,
                    maxY - minY);
            }

            return result;
        }
    }
}