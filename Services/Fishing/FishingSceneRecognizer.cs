using OpenCvSharp;
using System;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Fishing
{
    public readonly record struct FishingSceneSnapshot(
        FishingRoiAnalyzer.ColorRectResult BarA,
        FishingRoiAnalyzer.ColorRectResult BarB,
        bool BarVisible,
        bool HitFish,
        bool HitCastRod,
        bool HitStart,
        bool HitInteract,
        bool OverflowToast,
        bool BaitEmptyToast
    );

    public class FishingSceneRecognizer
    {
        private readonly FishingTemplateService _templateService;

        private DateTime _lastResultTime = DateTime.MinValue;
        private DateTime _lastInteractPressTime = DateTime.MinValue;
        private DateTime _lastCastPressTime = DateTime.MinValue;

        // 粉色气泡的 HSV 范围
        private static readonly Scalar InteractHsvLow = new Scalar(140, 100, 180);
        private static readonly Scalar InteractHsvHigh = new Scalar(175, 255, 255);

        public FishingSceneRecognizer(FishingTemplateService templateService)
        {
            _templateService = templateService;
        }

        public void Reset()
        {
            _lastResultTime = DateTime.MinValue;
            _lastInteractPressTime = DateTime.MinValue;
            _lastCastPressTime = DateTime.MinValue;
        }

        public void MarkInteractPressed() => _lastInteractPressTime = DateTime.Now;
        public void MarkCastPressed() => _lastCastPressTime = DateTime.Now;

        public (FishingRoiAnalyzer.ColorRectResult a, FishingRoiAnalyzer.ColorRectResult b) AnalyzeBars(Mat frame, Rect mainRoi, FishingRoiAnalyzer roiAnalyzer)
        {
            FishingRoiAnalyzer.ColorRectResult barA = new();
            FishingRoiAnalyzer.ColorRectResult barB = new();
            var actualRoi = mainRoi.Intersect(new Rect(0, 0, frame.Width, frame.Height));

            if (actualRoi.Width > 0 && actualRoi.Height > 0)
            {
                using var roiMat = new Mat(frame, actualRoi);
                (barA, barB) = roiAnalyzer.Analyze(roiMat, actualRoi.X, actualRoi.Y);
            }
            return (barA, barB);
        }

        // ★ 新增：粉色颜色检测方法
        public bool CheckPinkBubble(Mat frame, Rect searchRect)
        {
            var actualRect = searchRect.Intersect(new Rect(0, 0, frame.Width, frame.Height));
            if (actualRect.Width <= 0 || actualRect.Height <= 0) return false;

            using var roiMat = new Mat(frame, actualRect);
            using var hsv = new Mat();
            Cv2.CvtColor(roiMat, hsv, ColorConversionCodes.BGR2HSV);

            using var mask = new Mat();
            Cv2.InRange(hsv, InteractHsvLow, InteractHsvHigh, mask);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(mask, mask, kernel);

            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach (var contour in contours)
            {
                var rect = Cv2.BoundingRect(contour);
                if (rect.Width > 50 && rect.Height > 15 && rect.Width > rect.Height * 2)
                {
                    return true;
                }
            }
            return false;
        }

        public FishingSceneSnapshot AnalyzeScene(Mat frame, FishingRoiAnalyzer.ColorRectResult barA, FishingRoiAnalyzer.ColorRectResult barB)
        {
            bool isBarVisible = barA.Rect.HasValue && barB.Rect.HasValue;
            if (isBarVisible)
                return new FishingSceneSnapshot(barA, barB, true, false, false, false, false, false, false);

            bool overflow = _templateService.MatchAny(frame, FishingSceneMap.OverflowRect, FishingSceneMap.TplOverflow) != null;
            bool baitEmpty = _templateService.MatchAny(frame, FishingSceneMap.BaitEmptyRect, FishingSceneMap.TplBaitEmpty) != null;

            if (baitEmpty)
                return new FishingSceneSnapshot(barA, barB, false, false, false, false, false, overflow, true);

            bool hitFish = _templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishS) != null ||
                           _templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishA) != null ||
                           _templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishB) != null;

            if (hitFish)
            {
                if ((DateTime.Now - _lastResultTime).TotalSeconds < 1.5) hitFish = false;
                else _lastResultTime = DateTime.Now;
            }

            bool hitCastRod = false, hitStart = false, hitInteract = false;

            bool castCooldown = (DateTime.Now - _lastCastPressTime).TotalMilliseconds < FishingSceneMap.CastCooldownMs;
            if (!hitFish && !castCooldown)
            {
                hitCastRod = _templateService.MatchAny(frame, FishingSceneMap.CastRodRect1, FishingSceneMap.TplCastRod) != null ||
                             _templateService.MatchAny(frame, FishingSceneMap.CastRodRect2, FishingSceneMap.TplCastRod) != null;
            }

            if (!hitFish && !hitCastRod && !castCooldown)
                hitStart = _templateService.MatchAny(frame, FishingSceneMap.StartRect, FishingSceneMap.TplStartFishing) != null;

            bool interactCooldown = (DateTime.Now - _lastInteractPressTime).TotalMilliseconds < FishingSceneMap.InteractCooldownMs;
            if (!hitFish && !hitCastRod && !hitStart && !interactCooldown)
            {
                // ★ 双管齐下：多尺度灰度匹配 + 颜色检测兜底
                hitInteract = _templateService.MatchAnyMultiScale(frame, FishingSceneMap.InteractRect, FishingSceneMap.TplFishingInteract) != null
                              || CheckPinkBubble(frame, FishingSceneMap.InteractRect);
            }

            return new FishingSceneSnapshot(barA, barB, false, hitFish, hitCastRod, hitStart, hitInteract, overflow, false);
        }
    }
}