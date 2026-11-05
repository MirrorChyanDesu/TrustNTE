//ROI 坐标解析	兼容 x1,y1,x2,y2 格式，自动纠正错填并转为 OpenCV 的 Rect。
using System;
using OpenCvSharp;

namespace TrustNTE.Helpers
{
    public readonly struct RoiBox
    {
        public int X1 { get; }
        public int Y1 { get; }
        public int X2 { get; }
        public int Y2 { get; }

        public int Width => Math.Abs(X2 - X1);
        public int Height => Math.Abs(Y2 - Y1);
        public int Left => Math.Min(X1, X2);
        public int Top => Math.Min(Y1, Y2);

        public RoiBox(int x1, int y1, int x2, int y2)
        {
            X1 = Math.Min(x1, x2);
            Y1 = Math.Min(y1, y2);
            X2 = Math.Max(x1, x2);
            Y2 = Math.Max(y1, y2);
        }

        public static RoiBox Parse(string roiStr, string fallbackStr = "605,61,1322,88")
        {
            try
            {
                var parts = roiStr.Split(',');
                if (parts.Length == 4)
                {
                    return new RoiBox(
                        int.Parse(parts[0].Trim()),
                        int.Parse(parts[1].Trim()),
                        int.Parse(parts[2].Trim()),
                        int.Parse(parts[3].Trim())
                    );
                }
            }
            catch { }

            var fParts = fallbackStr.Split(',');
            return new RoiBox(
                int.Parse(fParts[0].Trim()),
                int.Parse(fParts[1].Trim()),
                int.Parse(fParts[2].Trim()),
                int.Parse(fParts[3].Trim())
            );
        }

        public Rect ToCvRect()
        {
            return new Rect(Left, Top, Width, Height);
        }
    }
}