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

                // 指向你刚下载的新字典
                string dictPath = Path.Combine(baseDir, "ppocrv6_dict.txt");

                try
                {
                    _engine?.Dispose();
                    _engine = null;

                    _logService.LogInfo("[OCR初始化] 开始加载模型...");

                    if (!File.Exists(detPath) || !File.Exists(recPath))
                    {
                        _logService.LogError("[OCR初始化] 失败: det.onnx 或 rec.onnx 不存在！");
                        return;
                    }
                    if (!File.Exists(dictPath))
                    {
                        _logService.LogError($"[OCR初始化] 失败: 找不到字典文件 {dictPath}，请确认已经放入该目录！");
                        return;
                    }

                    _logService.LogInfo("[OCR初始化] 正在初始化底层引擎 (PPOCRV6, 加载外部标准字典)...");

                    // RapidOCRSharp 底层会自动去程序根目录查找 ppocr_keys_v1.txt 作为字典
                    // 我们先把新下载的 ppocrv6_dict.txt 复制过去
                    string targetDictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ppocr_keys_v1.txt");
                    try
                    {
                        File.Copy(dictPath, targetDictPath, true);
                        _logService.LogInfo("[OCR初始化] 已将标准字典 ppocrv6_dict.txt 复制到根目录作为 ppocr_keys_v1.txt 使用。");
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"[OCR初始化] 复制字典失败: {ex.Message}");
                        return;
                    }

                    var ocrConfig = new OcrConfig(
                        detPath,
                        recPath,
                        LangRec.CH,
                        OCRVersion.PPOCRV6, // 必须使用 V6 版本
                        null                // clsMobilePath 传 null
                    );

                    _engine = new RapidOCRSharp(new ExecutionProviderCPU(ocrConfig));
                    _isEngineReady = true;

                    _logService.LogInfo("[OCR初始化] 模型加载成功！引擎已就绪。");
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
            {
                _logService.LogWarning("[OCR] 识别失败：图像为空或引擎未就绪。");
                return string.Empty;
            }

            token.ThrowIfCancellationRequested();

            using var clonedMat = image.Clone();
            using var bgrMat = new Mat();

            if (clonedMat.Channels() == 4)
                Cv2.CvtColor(clonedMat, bgrMat, ColorConversionCodes.BGRA2BGR);
            else if (clonedMat.Channels() == 1)
                Cv2.CvtColor(clonedMat, bgrMat, ColorConversionCodes.GRAY2BGR);
            else
                clonedMat.CopyTo(bgrMat);

            // 放大 3 倍 + 加边距
            using var resizedMat = new Mat();
            Cv2.Resize(bgrMat, resizedMat, new OpenCvSharp.Size(bgrMat.Width * 3, bgrMat.Height * 3), 0, 0, InterpolationFlags.Cubic);

            using var paddedMat = new Mat();
            Cv2.CopyMakeBorder(resizedMat, paddedMat, 30, 30, 30, 30, BorderTypes.Constant, new Scalar(0, 0, 0));

            return await Task.Run(() =>
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}.png");
                try
                {
                    Cv2.ImWrite(tempPath, paddedMat);

                    var ocrResult = _engine.RecognizeText(tempPath);
                    string text = ocrResult?.TextBlocks ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _logService.LogInfo("[OCR] 识别完成，但文本内容为空。");
                        return string.Empty;
                    }

                    string cleanText = Regex.Replace(text, @"[^\w:]", "");
                    _logService.LogInfo($"[OCR] 原始结果: '{text}' | 清理后: '{cleanText}'");
                    return cleanText;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"[OCR] 识别过程发生异常: {ex.Message}");
                    return string.Empty;
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
            }, token).ConfigureAwait(false);
        }
    }
}