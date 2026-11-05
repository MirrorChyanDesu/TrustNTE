using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace TrustNTE.Helpers
{
    public static class ImageMatcher
    {
        // 原有的单模板匹配（作为快捷调用）
        public static bool Match(Mat originalMat, Mat template, Rect roi, double scale, double threshold, out Rect matchedRect)
        {
            return MatchMulti(originalMat, new List<Mat> { template }, roi, scale, threshold, out matchedRect);
        }

        // 多模板匹配（只转一次原图灰度，遍历所有模板找最高分）
        public static bool MatchMulti(Mat originalMat, List<Mat> templates, Rect roi, double scale, double threshold, out Rect matchedRect)
        {
            matchedRect = new Rect();
            if (templates == null || templates.Count == 0) return false;

            try
            {
                // 1. 裁剪原图 ROI 并转一次灰度（无论多少模板，原图只转1次）
                Rect actualRoi = roi.Intersect(new Rect(0, 0, originalMat.Width, originalMat.Height));
                if (actualRoi.Width <= 0 || actualRoi.Height <= 0) return false;

                using var roiMat = new Mat(originalMat, actualRoi);

                // 不使用外部扩展方法，直接原生转换，防止编译报错
                using var grayRoi = new Mat();
                if (roiMat.Channels() > 1)
                    Cv2.CvtColor(roiMat, grayRoi, ColorConversionCodes.BGR2GRAY);
                else
                    roiMat.CopyTo(grayRoi);

                double highestScore = -1;
                Rect bestMatchRect = new Rect();
                bool found = false;

                // 2. 遍历所有模板
                foreach (var template in templates)
                {
                    if (template == null || template.Empty()) continue;

                    // 直接使用原模板，不做任何 ToGray() 转换！
                    // （因为你的图集在加载时已经是灰度图，这里直接拿来匹配，0 开销）

                    using var result = new Mat();
                    Cv2.MatchTemplate(grayRoi, template, result, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

                    // 不立刻 return，而是记录最高分！
                    if (maxVal > highestScore)
                    {
                        highestScore = maxVal;
                        bestMatchRect = new Rect(actualRoi.X + maxLoc.X, actualRoi.Y + maxLoc.Y, template.Width, template.Height);
                        found = true;
                    }
                }

                // 3. 遍历结束后，统一判断最高分是否达标
                if (found && highestScore >= threshold)
                {
                    matchedRect = bestMatchRect;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ImageMatcher Error: {ex.Message}");
            }
            return false;
        }
    }
}