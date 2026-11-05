using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace TrustNTE.Services.Interfaces
{
    /// <summary>
    /// OCR 服务接口，负责将图像识别为文本。
    /// </summary>
    public interface IOcrService
    {
        /// <summary>
        /// 识别图像中的文字。
        /// </summary>
        /// <param name="image">待识别的图像（BGR 或 BGRA 格式）。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>识别出的文本；识别失败或未检测到文字时返回 <see cref="string.Empty"/>。</returns>
        //  接口方法签名
        // [   改原因] 1) 原 SoftwareBitmap 依赖 Windows.Graphics.Imaging，与项目 P-2.a（OpenCvSharp4）选型不符，
        //               且 RapidOCRSharpOnnx 只接受 Mat；
        //            2) 增加 CancellationToken，符合规范第 20 条"异步方法必须传递 CancellationToken"；
        //            3) 全项目仅 StartPageViewModel 注入 IOcrService，无其它现存调用点，改动不破坏既有行为。
        Task<string> RecognizeTextAsync(Mat image, CancellationToken token = default);
    }
}