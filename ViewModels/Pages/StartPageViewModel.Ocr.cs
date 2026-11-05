using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;
using TrustNTE.Views.Windows; //        引入窗口命名空间

namespace TrustNTE.ViewModels.Pages
{
    /// <summary>
    /// <see cref="StartPageViewModel"/> 的 OCR 测试 partial 文件。
    /// 负责 OCR 模型路径展示、UID 区域 OCR 测试、模型目录打开、OCR 文档说明。
    /// </summary>
    public partial class StartPageViewModel
    {
        /// <summary>
        /// OCR 模型根目录（与 RapidOcrService 保持一致）。
        /// </summary>
        private static readonly string OcrModelDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "NTE-Ocr_Models");

        /// <summary>
        /// UID 识别区域（3, 1051, 193x27）。与 GameLoadingTrigger.UidRoi（2, 1061, 33x18）
        /// 用途不同：这里用于读取完整 UID 数字，故区域更宽。
        /// </summary>
        private static readonly Rect UidOcrRoi = new Rect(3, 1051, 193, 27);

        /// <summary>
        /// det.onnx 完整路径；若不存在则显示占位提示。
        /// </summary>
        [ObservableProperty]
        private string _detModelPath = string.Empty;

        /// <summary>
        /// rec.onnx 完整路径；若不存在则显示占位提示。
        /// </summary>
        [ObservableProperty]
        private string _recModelPath = string.Empty;

        /// <summary>
        /// 初始化 OCR 模块字段。由主构造函数在末尾调用。
        /// </summary>
        private void InitializeOcr()
        {
            string det = Path.Combine(OcrModelDir, "det.onnx");
            string rec = Path.Combine(OcrModelDir, "rec.onnx");
            DetModelPath = File.Exists(det) ? det : string.Empty;
            RecModelPath = File.Exists(rec) ? rec : string.Empty;
        }

        /// <summary>
        /// 执行 UID 区域 OCR 测试。
        /// </summary>
        [RelayCommand]
        private async Task TestUidOcrAsync()
        {
            if (_automationService.CurrentHwnd == IntPtr.Zero)
            {
                await _dialogService.ShowInfoAsync("无法测试", "请先前往【启动】页面开启截图器并绑定游戏窗口！");
                return;
            }

            using var frame = _automationService.GetLatestFrame();
            if (frame == null || frame.Empty())
            {
                await _dialogService.ShowInfoAsync("无法测试", "未能获取到游戏画面，请确认截图器正在运行。");
                return;
            }

            if (UidOcrRoi.Right > frame.Width || UidOcrRoi.Bottom > frame.Height)
            {
                await _dialogService.ShowInfoAsync(
                    "无法测试",
                    $"当前画面尺寸 {frame.Width}x{frame.Height} 小于 UID 区域（3,1051，宽高 193x27），请确认游戏分辨率为 1920x1080。");
                return;
            }

            _logService.LogInfo($"开始 OCR 测试 UID 区域 193x27...");

            string result;
            try
            {
                using var uidMat = new Mat(frame, UidOcrRoi);

                // [新增] 调试功能：保存当前截图，并自动打开文件夹供用户查看截取是否正确
                string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "OcrTest");
                if (!Directory.Exists(testDir)) Directory.CreateDirectory(testDir);
                string imgPath = Path.Combine(testDir, $"uid_test_{DateTime.Now:HHmmss}.png");
                Cv2.ImWrite(imgPath, uidMat);
                _shellService.OpenFolder(testDir);

                result = await _ocrService.RecognizeTextAsync(uidMat);
            }
            catch (Exception ex)
            {
                _logService.LogError($"OCR 识别异常: {ex.Message}");
                await _dialogService.ShowInfoAsync("OCR 识别失败", ex.Message);
                return;
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                _logService.LogWarning("OCR 未识别到文字。");
                await _dialogService.ShowInfoAsync(
                    "OCR 测试结果",
                    "未识别到文字。\n请查看刚打开的 OcrTest 文件夹中的截图，确认是否截到了清晰的 UID。");
            }
            else
            {
                _logService.LogInfo($"UID 识别结果: {result}");
                await _dialogService.ShowInfoAsync("OCR 测试结果", $"识别结果：\n{result}");
            }
        }

        /// <summary>
        /// 打开模型所在目录。
        /// </summary>
        /// <param name="path">当前绑定的模型文件路径；为 null 或不存在时打开模型根目录。</param>
        [RelayCommand]
        private void OpenModelFolder(string? path)
        {
            string targetDir = OcrModelDir;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    targetDir = dir;
                }
            }

            if (!Directory.Exists(targetDir))
            {
                _logService.LogWarning($"OCR 模型目录不存在: {targetDir}");
                return;
            }

            _shellService.OpenFolder(targetDir);
        }

        /// <summary>
        /// 显示 OCR 使用说明对话框（改为高颜值阅读器）。
        /// </summary>
        [RelayCommand]
        private void ShowOcrDocs()
        {

            DocViewerWindow.ShowDoc("OCR 使用说明", "ocr.md");
        }
    }
}