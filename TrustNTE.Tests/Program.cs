using System;
using System.Text;
using OpenCvSharp;
using TrustNTE.Tests.DebugTools;

namespace TrustNTE.Tests
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            //        核心   ：强制控制台使用 UTF-8 编码，解决 Emoji 和状态字符乱码
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("========================================");
            Console.WriteLine("  Q 能量 绝对亮度检测 v5 (最终版)");
            Console.WriteLine("========================================");
            Console.WriteLine(" [1] 实时检测 (读取本地已保存的 ROI)");
            Console.WriteLine(" [2] 重新标定 ROI (全屏拖框)");
            Console.WriteLine("========================================");
            Console.Write("请输入编号: ");
            
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) input = "1";

            using var drone = new TestCaptureDrone();
            if (!drone.BindAndStart(50))
            {
                Console.WriteLine("❌ 启动失败，请确保异环游戏已运行。");
                return;
            }

            QEnergyRoiCalibrator.GrabNormalizedFrame = () => drone.GetLatestFrame();

            if (input == "2")
            {
                QEnergyRoiCalibrator.Run();
                return;
            }

            //        既然文件可能被旧值覆盖，实时检测时直接硬编码正确的坐标和阈值，防污染
            var config = new QEnergyConfig
            {
                RoiX = 1767,
                RoiY = 915,
                RoiWidth = 111,
                RoiHeight = 111,
                BrightReadyRatio = 0.15 // 门槛定死 15%
            };

            using var detector = new QEnergyDetector(config);

            Console.WriteLine("按 ESC 键退出预览。");

            while (true)
            {
                using var frame = drone.GetLatestFrame();
                if (frame != null)
                {
                    var result = detector.Analyze(frame);
                    var roi = detector.LastRoi;
                    
                    Cv2.Rectangle(frame, roi, Scalar.Red, 2);
                    
                    using var roiMat = new Mat(frame, roi);
                    using var maskMat = detector.SnapshotLastMask();

                    using var roiBig = new Mat();
                    using var maskBig = new Mat();
                    Cv2.Resize(roiMat, roiBig, new Size(roiMat.Width * 3, roiMat.Height * 3));
                    if (maskMat != null) Cv2.Resize(maskMat, maskBig, new Size(maskMat.Width * 3, maskMat.Height * 3));

                    string state = result.Confirmed ? "✔ 满能量" : "✘ 未满";
                    Console.Write($"\r亮度占比: {result.BrightRatio,6:P1} | 状态: {state}          ");

                    using var preview = new Mat();
                    Cv2.Resize(frame, preview, new Size(1280, 720));
                    Cv2.ImShow("Game Preview", preview);
                    Cv2.ImShow("Q ROI (放大3倍)", roiBig);
                    if (maskMat != null) Cv2.ImShow("Q Bright Mask", maskBig);
                }

                if (Cv2.WaitKey(10) == 27) break;
            }

            Cv2.DestroyAllWindows();
        }
    }
}