using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using TrustNTE.Helpers;
using TrustNTE.Services.Game;

namespace TrustNTE.Tests.DebugTools
{
    public sealed class TestCaptureDrone : IDisposable
    {
        private readonly ScreenCapturer _capturer = new();
        private readonly object _frameLock = new();

        private CancellationTokenSource? _cts;
        private Mat? _latestFrame;
        private IntPtr _hwnd = IntPtr.Zero;

        public bool IsRunning { get; private set; }

        public bool BindAndStart(int intervalMs = 50)
        {
            Stop();
            var process = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();
            if (process == null) return false;

            _hwnd = NativeMethods.FindWindow(GameConstants.GameWindowClass, null);
            if (_hwnd == IntPtr.Zero) return false;

            Console.WriteLine($"✅ 成功绑定窗口 0x{_hwnd:X}，拉取频率 {intervalMs}ms");
            _cts = new CancellationTokenSource();
            IsRunning = true;

            Task.Run(() => CaptureLoop(intervalMs, _cts.Token));
            return true;
        }

        private void CaptureLoop(int intervalMs, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var raw = _capturer.Capture(_hwnd, "PrintWindow");
                    if (raw != null && !raw.Empty())
                    {
                        Mat? synced = raw;
                        if (raw.Width != 1920 || raw.Height != 1080)
                        {
                            synced = new Mat();
                            Cv2.Resize(raw, synced, new Size(1920, 1080));
                        }

                        lock (_frameLock)
                        {
                            _latestFrame?.Dispose();
                            _latestFrame = synced.Clone();
                        }
                        if (synced != raw) synced.Dispose();
                    }
                }
                catch { }

                int delay = intervalMs - (int)sw.ElapsedMilliseconds;
                if (delay > 0) Thread.Sleep(delay);
            }
        }

        public Mat? GetLatestFrame()
        {
            lock (_frameLock) return _latestFrame?.Clone();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
            lock (_frameLock) { _latestFrame?.Dispose(); _latestFrame = null; }
        }

        public void Dispose() => Stop();
    }
}