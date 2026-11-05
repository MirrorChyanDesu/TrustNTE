using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace TrustNTE.Helpers
{
    public static class WriteableBitmapHelper
    {
        /// <summary>
        /// 以无状态方式更新 WriteableBitmap，避免多窗口共享静态实例导致的死锁。
        /// </summary>
        /// <param name="bitmap">调用方自己持有的位图实例引用。</param>
        /// <param name="mat">OpenCV 图像。</param>
        /// <returns>更新后的位图对象。</returns>
        public static WriteableBitmap? Update(ref WriteableBitmap? bitmap, Mat mat)
        {
            if (mat == null || mat.Empty()) return null;

            Mat bgra = mat;
            using var temp = new Mat();

            //  使用 switch 统一处理通道转换，并补全单通道灰度图防护
            switch (mat.Channels())
            {
                case 3: // BGR
                    Cv2.CvtColor(mat, temp, ColorConversionCodes.BGR2BGRA);
                    bgra = temp;
                    break;
                case 1: // 灰度图
                    Cv2.CvtColor(mat, temp, ColorConversionCodes.GRAY2BGRA);
                    bgra = temp;
                    break;
                default: // 4 通道 BGRA
                    if (!mat.IsContinuous())
                    {
                        mat.CopyTo(temp);   // ROI 场景保证 stride == width*4
                        bgra = temp;
                    }
                    break;
            }

            // 只在尺寸变化时重建 WriteableBitmap
            if (bitmap == null || bitmap.PixelWidth != bgra.Width || bitmap.PixelHeight != bgra.Height)
            {
                bitmap = new WriteableBitmap(bgra.Width, bgra.Height, 96, 96, PixelFormats.Bgra32, null);
            }

            int stride = (int)bgra.Step();
            
            // 一步写入，无需 Lock，极其高效
            bitmap.WritePixels(
                new Int32Rect(0, 0, bgra.Width, bgra.Height),
                bgra.Data,
                stride * bgra.Rows,
                stride);

            return bitmap;
        }
    }
}