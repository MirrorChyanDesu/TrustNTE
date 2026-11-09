using OpenCvSharp;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TrustNTE.Services
{
    public sealed class NteRenderContextManager
    {
        public const int NteBaseWidth = 1920;
        public const int NteBaseHeight = 1080;

        public int NteCurrentWidth { get; private set; } = NteBaseWidth;
        public int NteCurrentHeight { get; private set; } = NteBaseHeight;

        public bool IsNteStandard => NteCurrentWidth == NteBaseWidth && NteCurrentHeight == NteBaseHeight;
        public bool IsNteAbnormalRatio => Math.Abs((double)NteCurrentWidth / NteCurrentHeight - 16.0 / 9.0) > 0.01;

        public void UpdateNteContext(int w, int h)
        {
            NteCurrentWidth = w;
            NteCurrentHeight = h;
        }

        public Mat SyncNteRenderFrame(Mat frame)
        {
            if (frame.Width == NteBaseWidth && frame.Height == NteBaseHeight)
                return frame.Clone();

            try
            {
                var syncedFrame = new Mat();

                // 🚨 核心   ：使用 Cubic（三次插值），极大程度保留放大后的边缘锐度！
                Cv2.Resize(frame, syncedFrame, new OpenCvSharp.Size(NteBaseWidth, NteBaseHeight), 0, 0, InterpolationFlags.Cubic);

                return syncedFrame;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[归一化严重异常] {ex.Message}");
                return frame.Clone();
            }
        }

        public (int Px, int Py) TranslateNtePointer(int x, int y)
        {
            int px = (int)Math.Round(x * (double)NteCurrentWidth / NteBaseWidth);
            int py = (int)Math.Round(y * (double)NteCurrentHeight / NteBaseHeight);
            return (Math.Clamp(px, 0, NteCurrentWidth - 1), Math.Clamp(py, 0, NteCurrentHeight - 1));
        }
    }
}