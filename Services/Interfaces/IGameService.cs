using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace TrustNTE.Services.Interfaces
{
    public interface IGameService
    {
        bool IsBackgroundMode { get; set; }
        Task<bool> AutoLaunchGameAsync(string? configuredLauncherPath, CancellationToken token);
        IntPtr FindWindowForProcess(int pid, string? className = null);
        Task<IntPtr> WaitForGameWindowAsync(CancellationToken token);

        //        强制传参（去掉默认值 = 50），防止调用方漏传导致用错频率
        void StartCapture(IntPtr hWnd, string? captureMode, int minIntervalMs);

        void StopCapture();
        Mat? CaptureWindow();
        void Click(IntPtr hWnd, int x, int y);
        string? GetRunningGamePath();

        void ForceResizeTo1080p(IntPtr hwnd);
        Mat? CaptureWindowOf(IntPtr hWnd);

        (Mat? Frame, DateTime ShotAt) GetLatestFrameWithTimestamp();

        // =========        修改为新的“接管/归还”名称 =========

        /// <summary>
        /// 主循环每帧同步全局配置
        /// </summary>
        void UpdateCaptureInterval(int minIntervalMs);

        /// <summary>
        /// 获取当前帧的年龄（用于超时兜底）
        /// </summary>
        long GetCaptureFrameAgeMs();

        /// <summary>
        /// 强制单发截图（兜底机制）
        /// </summary>
        void ForceSingleShot();
    }
}