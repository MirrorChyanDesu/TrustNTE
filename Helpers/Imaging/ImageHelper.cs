using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace TrustNTE.Helpers
{
    public static class ImageHelper
    {
        public static BitmapSource MatToBitmapSource(Mat mat)
        {
            if (mat == null || mat.Empty()) return null!;

            Mat bgra = mat;
            bool needConvert = mat.Channels() == 3;
            if (needConvert)
            {
                bgra = new Mat();
                Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);
            }

            if (!bgra.IsContinuous())
            {
                bgra = bgra.Clone();
            }

            int width = bgra.Cols, height = bgra.Rows;
            int step = (int)bgra.Step();

            byte[] pixels = new byte[step * height];

            if (bgra.Data != IntPtr.Zero)
            {
                Marshal.Copy(bgra.Data, pixels, 0, pixels.Length);
            }

            if (needConvert) bgra.Dispose();

            var bs = BitmapSource.Create(width, height, 96, 96,
                PixelFormats.Bgra32, null, pixels, step);

            bs.Freeze();
            return bs;
        }

        // 增加 ImreadModes 参数，默认为灰度图
        public static Mat? LoadTemplateImage(string fileName, ImreadModes mode = ImreadModes.Grayscale)
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameTask", "Launcher", "Assets");

            if (!Directory.Exists(baseDir))
            {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                for (int i = 0; i < 5; i++)
                {
                    var parent = Directory.GetParent(currentDir);
                    if (parent == null) break;
                    currentDir = parent.FullName;
                    string testPath = Path.Combine(currentDir, "GameTask", "Launcher", "Assets");
                    if (Directory.Exists(testPath))
                    {
                        baseDir = testPath;
                        break;
                    }
                }
            }

            string filePath = Path.Combine(baseDir, fileName);
            if (File.Exists(filePath))
            {
                return Cv2.ImRead(filePath, mode);
            }

            return null;
        }
    }
}