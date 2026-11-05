using System;
using OpenCvSharp;
using Size = OpenCvSharp.Size;
namespace TrustNTE.Services.Triggers
{
    /// <summary>HSV 区间（OpenCV 约定 H:0-179, S/V:0-255）。对齐 ok-nte image_utils.HSVRange</summary>
    public readonly record struct HsvRange(int HMin, int SMin, int VMin, int HMax, int SMax, int VMax)
    {
        public Scalar Lower => new(HMin, SMin, VMin);
        public Scalar Upper => new(HMax, SMax, VMax);
    }

    /// <summary>
    /// ok-nte 战斗索敌视觉工具（game_filters.py + image_utils.py 逐函数对齐）。
    /// 模板构建与运行时必须共用同一预处理函数（保证特征一致）。
    /// </summary>
    public static class NteVisionUtils
    {
        // ---- game_filters.py 三个 HSV 区间（逐字对应）----
        public static readonly HsvRange LvGrayHsv = new(0, 0, 100, 0, 0, 170);      // 暗灰描边：夜图/阴影救星
        public static readonly HsvRange LvWhiteHsv = new(0, 0, 180, 160, 20, 255);   // 亮白：S≤20 排除彩色UI
        public static readonly HsvRange LvRedHsv = new(0, 235, 180, 0, 255, 255);  // 纯红：传说怪红字 Lv

        /// <summary>
        /// image_utils.restore_world_brightness：99 分位亮度拉伸。
        /// 抗 N 卡滤镜/夜图把白字压暗。返回新 Mat（调用方负责 Dispose）。
        /// </summary>
        public static Mat RestoreWorldBrightness(Mat bgr, double percentile = 0.99)
        {
            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

            using var histMat = new Mat();
            using var emptyMask = new Mat();   // mask 传 null 在部分 OpenCvSharp 4.x 早期版本会 NRE
            Cv2.CalcHist(new[] { gray }, new[] { 0 }, emptyMask, histMat, 1,
                         new[] { 256 }, new[] { new Rangef(0f, 256f) });

            using var rowHist = histMat.Reshape(1, 1);   // 统一 1×256，用 At 逐元素读，绕开 GetArray 版本差异

            long total = (long)gray.Width * gray.Height;
            long target = (long)(total * (1.0 - percentile));
            long acc = 0; int robustMax = 255;
            for (int i = 255; i >= 1; i--)
            {
                acc += (long)rowHist.At<float>(0, i);
                if (acc >= target) { robustMax = i; break; }
            }

            if (robustMax < 254 && robustMax > 100)
            {
                var scaled = new Mat();
                Cv2.ConvertScaleAbs(bgr, scaled, 255.0 / robustMax, 0);
                return scaled;
            }
            return bgr.Clone();
        }

        /// <summary>
        /// game_filters.isolate_lv_to_white：亮度   复 → HSV 三路(白|灰|红)合并 → 3×3 膨胀一次。
        /// 返回单通道二值掩码。返回新 Mat（调用方负责 Dispose）。
        /// </summary>
        public static Mat IsolateLvToWhite(Mat bgr)
        {
            using var restored = RestoreWorldBrightness(bgr);
            using var hsv = new Mat();
            Cv2.CvtColor(restored, hsv, ColorConversionCodes.BGR2HSV);

            using var mW = new Mat();
            using var mG = new Mat();
            using var mR = new Mat();
            var mask = new Mat();
            Cv2.InRange(hsv, LvWhiteHsv.Lower, LvWhiteHsv.Upper, mW);
            Cv2.InRange(hsv, LvGrayHsv.Lower, LvGrayHsv.Upper, mG);
            Cv2.InRange(hsv, LvRedHsv.Lower, LvRedHsv.Upper, mR);
            Cv2.BitwiseOr(mW, mG, mask);
            Cv2.BitwiseOr(mask, mR, mask);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.Dilate(mask, mask, kernel);   // morphology_mask 默认：膨胀（非闭运算），与源码一致
            return mask;
        }

        /// <summary>
        /// image_utils.binarize_bgr_by_adaptive_brightness：均值+offset 自适应亮度二值化。
        /// 用于白菱形【运行时】几何验证（对齐 ok-nte find_target 运行时管线；
        /// 245 硬阈值是模板预处理专用，运行时用它会在 HDR/滤镜/夜图下菱形全灭）。
        /// 返回单通道二值 Mat（调用方负责 Dispose）。
        /// </summary>
        public static Mat BinarizeAdaptiveBrightness(Mat bgr, double ratioThreshold = 0.05,
                                                     double offset = 20, int minThreshold = 100)
        {
            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

            double avg = Cv2.Mean(gray).Val0;
            double candidate = Math.Clamp(avg + offset, minThreshold, 255);

            using var cmp = new Mat();
            Cv2.Threshold(gray, cmp, candidate, 255, ThresholdTypes.Binary);
            double ratio = Cv2.CountNonZero(cmp) / (double)((long)gray.Width * gray.Height);

            double finalThreshold = ratio >= ratioThreshold ? candidate : 255;  // 亮场景=无高亮目标 → 输出全黑
            var bin = new Mat();
            Cv2.Threshold(gray, bin, finalThreshold, 255, ThresholdTypes.Binary);
            return bin;
        }
    }
}
