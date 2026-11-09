using OpenCvSharp;
using RapidOCRSharpOnnx;
using RapidOCRSharpOnnx.Configurations;
using RapidOCRSharpOnnx.Providers;
using RapidOCRSharpOnnx.Utils;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class RapidOcrService : IOcrService
    {
        private readonly ILogService _logService;
        private RapidOCRSharp? _engine;
        private volatile bool _isEngineReady = false;
        private readonly object _lock = new object();

        public RapidOcrService(ILogService logService)
        {
            _logService = logService;
        }

        private void EnsureEngineLoaded()
        {
            if (_isEngineReady) return;

            lock (_lock)
            {
                if (_isEngineReady) return;

                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "NTE-Ocr_Models");
                string detPath = Path.Combine(baseDir, "det.onnx");
                string recPath = Path.Combine(baseDir, "rec.onnx");
                string dictPath = Path.Combine(baseDir, "ppocrv6_dict.txt");

                try
                {
                    _engine?.Dispose();
                    _engine = null;

                    if (!File.Exists(detPath) || !File.Exists(recPath))
                    {
                        _logService.LogError("[OCR初始化] 失败: det.onnx 或 rec.onnx 不存在！");
                        return;
                    }

                    string targetDictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ppocr_keys_v1.txt");
                    if (File.Exists(dictPath))
                    {
                        File.Copy(dictPath, targetDictPath, true);
                    }

                    _logService.LogInfo("[OCR初始化] 正在启动 DirectML (GPU 加速) 引擎...");

                    var ocrConfig = new OcrConfig(
                        detPath,
                        recPath,
                        LangRec.CH,
                        OCRVersion.PPOCRV6,
                        null! // 不需要方向分类模型，省时间
                    );

                    // ★ 核心 1：显式传入 0，调用第一块显卡，消除二义性
                    var dmlProvider = new ExecutionProviderDirectML(ocrConfig, 0);
                    _engine = new RapidOCRSharp(dmlProvider);
                    _isEngineReady = true;

                    _logService.LogInfo("[OCR初始化] ✅ GPU 引擎加载成功！");
                }
                catch (Exception ex)
                {
                    _isEngineReady = false;
                    _logService.LogError($"[OCR初始化] 引擎加载崩溃: {ex.Message}");
                }
            }
        }

        public async Task<string> RecognizeTextAsync(Mat image, CancellationToken token = default)
        {
            EnsureEngineLoaded();

            if (image == null || image.Empty() || _engine == null || !_isEngineReady)
                return string.Empty;

            token.ThrowIfCancellationRequested();

            // 通道统一转 BGR
            using var bgrMat = new Mat();
            if (image.Channels() == 4)
                Cv2.CvtColor(image, bgrMat, ColorConversionCodes.BGRA2BGR);
            else if (image.Channels() == 1)
                Cv2.CvtColor(image, bgrMat, ColorConversionCodes.GRAY2BGR);
            else
                image.CopyTo(bgrMat);

            // ★ 核心 2：解决你的 UID 图片识别不出的问题
            // 原图高度只有 27，太小了。我们放大 2.5 倍（不要加黑边，让引擎自己处理）
            using var resizedMat = new Mat();
            Cv2.Resize(bgrMat, resizedMat, new OpenCvSharp.Size(bgrMat.Width * 2.5, bgrMat.Height * 2.5), 0, 0, InterpolationFlags.Cubic);

            return await Task.Run(() =>
            {
                try
                {
                    // ★ 核心 3：直接传 Mat 进内存，绝对不写硬盘！
                    var ocrResult = _engine.RecognizeText(resizedMat);
                    string text = ocrResult?.TextBlocks ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(text)) return string.Empty;

                    // 清理多余字符，只留字母数字中文和冒号
                    string cleanText = Regex.Replace(text, @"[^\w:\u4e00-\u9fa5]", "");

                    if (string.IsNullOrWhiteSpace(cleanText)) return string.Empty;

                    return cleanText;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"[OCR] 识别发生异常: {ex.Message}");
                    return string.Empty;
                }
            }, token).ConfigureAwait(false);
        }
    }
}