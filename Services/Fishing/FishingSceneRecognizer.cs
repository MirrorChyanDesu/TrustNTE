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

        //       去掉了 checkHud 参数，门控由状态机决定
        public FishingSceneSnapshot AnalyzeScene(Mat frame, FishingRoiAnalyzer.ColorRectResult barA, FishingRoiAnalyzer.ColorRectResult barB)
        {
            bool isBarVisible = barA.Rect.HasValue && barB.Rect.HasValue;

            if (isBarVisible)
                return new FishingSceneSnapshot(barA, barB, true, false, false, false, false, false, false);

            bool overflow = _templateService.MatchAny(frame, FishingSceneMap.OverflowRect, FishingSceneMap.TplOverflow) != null;
            bool baitEmpty = _templateService.MatchAny(frame, FishingSceneMap.BaitEmptyRect, FishingSceneMap.TplBaitEmpty) != null;

            if (baitEmpty)
                return new FishingSceneSnapshot(barA, barB, false, false, false, false, false, overflow, true);

            //       修复了之前的拼写错误：FishResultMap -> FishResultRect
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
                hitInteract = _templateService.MatchAny(frame, FishingSceneMap.InteractRect, FishingSceneMap.TplFishingInteract) != null;

            return new FishingSceneSnapshot(barA, barB, false, hitFish, hitCastRod, hitStart, hitInteract, overflow, false);
        }
    }
}