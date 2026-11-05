using System;
using OpenCvSharp;

namespace TrustNTE.Tests.DebugTools
{
    public static class QEnergyRoiCalibrator
    {
        public static Func<Mat?> GrabNormalizedFrame = () => null;

        public static void Run()
        {
            Console.WriteLine("[标定] 正在等待后台截图线程就绪...");

            Mat? frame = null;
            int retry = 0;
            while (frame == null && retry < 50)
            {
                frame = GrabNormalizedFrame();
                if (frame == null)
                {
                    System.Threading.Thread.Sleep(100);
                    retry++;
                }
            }

            if (frame == null)
            {
                Console.WriteLine("[标定] 抓帧失败（等待超时），请检查游戏画面是否正常。");
                return;
            }

            string w1 = "Step1: 拖框套住Q槽位, 按回车(Enter)确认";
            var roi = Cv2.SelectROI(w1, frame, showCrosshair: true, fromCenter: false);
            Cv2.DestroyWindow(w1);

            if (roi.Width <= 10 || roi.Height <= 10)
            {
                Console.WriteLine("[标定] 未选择或框太小，退出");
                return;
            }

            Console.WriteLine($"[ROI] X={roi.X} Y={roi.Y} W={roi.Width} H={roi.Height}  ← 已保存到 JSON");

            var cfg = QEnergyConfig.Load();
            cfg.RoiX = roi.X;
            cfg.RoiY = roi.Y;
            cfg.RoiWidth = roi.Width;
            cfg.RoiHeight = roi.Height;
            cfg.Save();

            using var det = new QEnergyDetector(cfg);
            string w2 = "Step2: 验证亮度 (按 ESC 退出)";
            Cv2.NamedWindow(w2, WindowFlags.AutoSize);

            while (true)
            {
                using var f = GrabNormalizedFrame();
                if (f == null) break;

                var r = det.Analyze(f);

                using var view = new Mat(f, det.LastRoi).Clone();

                Cv2.Rectangle(view, new Rect(0, 0, view.Width, view.Height), Scalar.Red, 1);

                Cv2.Resize(view, view, new Size(view.Width * 3, view.Height * 3), 0, 0, InterpolationFlags.Nearest);

                Cv2.ImShow(w2, view);
                Console.Write($"\r亮度占比:{r.BrightRatio,6:P1} | " +
                              (r.Confirmed ? "✅满能量" : "未满") + "        ");

                if (Cv2.WaitKey(50) == 27) break;
            }
            Cv2.DestroyAllWindows();
        }
    }
}