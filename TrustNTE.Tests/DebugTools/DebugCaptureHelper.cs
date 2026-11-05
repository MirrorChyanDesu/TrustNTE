using System;
using System.Diagnostics;
using System.Linq;
using OpenCvSharp;
using TrustNTE.Helpers;
using TrustNTE.Services.Game;

namespace TrustNTE.Tests.DebugTools
{
    public static class DebugCaptureHelper
    {
        private static readonly ScreenCapturer _capturer = new();

        /// <summary>
        /// 获取游戏窗口句柄（利用主项目的 NativeMethods）
        /// </summary>
        public static IntPtr GetGameHwnd()
        {
            var process = Process.GetProcessesByName(
                System.IO.Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();

            if (process == null) return IntPtr.Zero;

            return NativeMethods.FindWindow(GameConstants.GameWindowClass, null);
        }

        /// <summary>
        /// 直接用主项目的 PrintWindow 引擎截一张 1920x1080 的图，不怕窗口被遮挡
        /// </summary>
        public static Mat? Capture()
        {
            IntPtr hwnd = GetGameHwnd();
            if (hwnd == IntPtr.Zero) return null;

            // 用 PrintWindow 模式截取窗口（支持后台截图）
            using var rawMat = _capturer.Capture(hwnd, "PrintWindow");
            if (rawMat == null || rawMat.Empty()) return null;

            // 如果尺寸不是 1920x1080，强行缩放（模拟 NteRenderContextManager 的效果）
            if (rawMat.Width != 1920 || rawMat.Height != 1080)
            {
                var resized = new Mat();
                Cv2.Resize(rawMat, resized, new OpenCvSharp.Size(1920, 1080));
                return resized;
            }

            return rawMat.Clone();
        }
    }
}