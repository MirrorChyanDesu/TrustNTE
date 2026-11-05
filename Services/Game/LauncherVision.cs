using OpenCvSharp;
using System;

namespace TrustNTE.Services.Game
{
    public class LauncherVision
    {
        public string CheckStateByTemplate(Mat cropMat, Mat template)
        {
            if (template.Empty()) return "Unknown";

            using var result = new Mat();
            Cv2.MatchTemplate(cropMat, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            if (maxVal >= 0.8)
            {
                Rect iconRect = new Rect(maxLoc.X + 14, maxLoc.Y + 15, 18, 27);
                if (iconRect.Right <= cropMat.Width && iconRect.Bottom <= cropMat.Height)
                {
                    using var iconMat = new Mat(cropMat, iconRect);
                    return CheckButtonStateByColor(iconMat);
                }
            }
            return "Unknown";
        }

        public string CheckButtonStateByColor(Mat iconMat)
        {
            if (iconMat.Empty()) return "Unknown";

            using var hsvMat = new Mat();
            Cv2.CvtColor(iconMat, hsvMat, ColorConversionCodes.BGR2HSV);

            using var yellowMask = new Mat();
            Cv2.InRange(hsvMat, new Scalar(20, 100, 150), new Scalar(35, 255, 255), yellowMask);
            int yellowPixels = Cv2.CountNonZero(yellowMask);

            using var orangeMask1 = new Mat();
            Cv2.InRange(hsvMat, new Scalar(0, 120, 150), new Scalar(18, 255, 255), orangeMask1);
            using var orangeMask2 = new Mat();
            Cv2.InRange(hsvMat, new Scalar(160, 120, 150), new Scalar(180, 255, 255), orangeMask2);
            using var orangeMask = new Mat();
            Cv2.BitwiseOr(orangeMask1, orangeMask2, orangeMask);
            int orangePixels = Cv2.CountNonZero(orangeMask);

            using var startMask1 = new Mat();
            Cv2.InRange(hsvMat, new Scalar(80, 100, 150), new Scalar(95, 255, 255), startMask1);
            using var startMask2 = new Mat();
            Cv2.InRange(hsvMat, new Scalar(0, 0, 180), new Scalar(180, 40, 255), startMask2);
            using var startMask = new Mat();
            Cv2.BitwiseOr(startMask1, startMask2, startMask);
            int startPixels = Cv2.CountNonZero(startMask);

            if (startPixels > 30) return "Start";
            if (orangePixels > 30) return "Pause";
            if (yellowPixels > 30) return "Resume";

            return "Unknown";
        }
    }
}