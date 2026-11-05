using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;
using TrustNTE.Services.Game;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TrustNTE.Services.Implementations
{
    public class GameService : IGameService
    {
        public bool IsBackgroundMode { get; set; } = false;
        public string CurrentModeName { get; private set; } = "PrintWindow";
        private IntPtr _currentHwnd = IntPtr.Zero;

        private readonly WindowFinder _windowFinder = new();
        private readonly ScreenCapturer _screenCapturer = new();
        private readonly WindowMessenger _windowMessenger;
        private readonly IInputService _inputService;
        private readonly ILogService _logService;
        private readonly CaptureDrone _drone;

        private int _uiIntervalMs = 50;
        private readonly object _intervalGate = new();

        public GameService(IInputService inputService, ILogService logService)
        {
            _inputService = inputService;
            _logService = logService;
            _windowMessenger = new WindowMessenger();
            _drone = new CaptureDrone(msg => _logService.LogWarning(msg));
        }

        public async Task<bool> AutoLaunchGameAsync(string? configuredLauncherPath, CancellationToken token)
        {
            var gameProc = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();
            if (gameProc != null) return true;

            string? launchPath = configuredLauncherPath;
            if (string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath))
                launchPath = RegistryHelper.FindLauncherPath();

            if (!string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath))
            {
                Process.Start(new ProcessStartInfo { FileName = launchPath, UseShellExecute = true });
            }

            for (int i = 0; i < 600; i++)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token);
                var newGameProc = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();
                if (newGameProc != null) return true;
            }
            return false;
        }

        public IntPtr FindWindowForProcess(int pid, string? className = null)
        {
            return _windowFinder.FindWindowForProcess(pid, className);
        }

        public async Task<IntPtr> WaitForGameWindowAsync(CancellationToken token)
        {
            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 120; i++)
            {
                if (token.IsCancellationRequested) return IntPtr.Zero;

                var gameProc = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();
                if (gameProc != null)
                {
                    hwnd = FindWindowForProcess(gameProc.Id, GameConstants.GameWindowClass);
                    if (hwnd != IntPtr.Zero) return hwnd;
                }
                await Task.Delay(1000, token);
            }
            return IntPtr.Zero;
        }

        public string? GetRunningGamePath()
        {
            try
            {
                var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe));
                if (processes.Length > 0)
                {
                    var mainModule = processes[0].MainModule;
                    if (mainModule != null) return mainModule.FileName;
                }
            }
            catch { }
            return null;
        }

        public void StartCapture(IntPtr hWnd, string? captureMode, int minIntervalMs)
        {
            StopCapture();
            _currentHwnd = hWnd;
            CurrentModeName = ScreenCapturer.NormalizeMode(captureMode);

            _windowMessenger.SendActivateMessages(hWnd);
            _windowMessenger.StartKeepAlive(hWnd);

            lock (_intervalGate)
            {
                _uiIntervalMs = Math.Max(1, minIntervalMs);
                _drone.Start(hWnd, CurrentModeName, _uiIntervalMs);
            }
        }

        /// <summary>
        /// 主循环每帧同步全局配置。
        /// </summary>
        public void UpdateCaptureInterval(int minIntervalMs)
        {
            lock (_intervalGate)
            {
                _uiIntervalMs = Math.Max(1, minIntervalMs);
                if (_drone.IsRunning)
                {
                    _drone.UpdateInterval(_uiIntervalMs);
                }
            }
        }

        public long GetCaptureFrameAgeMs() => _drone.LastFrameAgeMs;

        public void ForceSingleShot()
        {
            var f = SingleShot(_currentHwnd);
            if (f != null && !f.Empty()) _drone.Feed(f);
            else f?.Dispose();
        }

        public (Mat? Frame, DateTime ShotAt) GetLatestFrameWithTimestamp()
        {
            if (_drone.IsRunning)
                return _drone.GetLatestWithTimestamp();

            return (SingleShot(_currentHwnd), DateTime.Now);
        }

        public Mat? CaptureWindow()
            => _drone.IsRunning ? _drone.GetLatest() : SingleShot(_currentHwnd);

        public Mat? CaptureWindowOf(IntPtr hWnd)
            => SingleShot(hWnd);

        private Mat? SingleShot(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || NativeMethods.IsIconic(hWnd)) return null;
            return _screenCapturer.Capture(hWnd, CurrentModeName);
        }

        public void StopCapture()
        {
            _drone.Stop();
            _windowMessenger.StopKeepAlive();
            _currentHwnd = IntPtr.Zero;
        }

        public void Click(IntPtr hWnd, int x, int y)
        {
            _inputService.SendBackgroundMouseClick(hWnd, x, y, true);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        public void ForceResizeTo1080p(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            if (!GetClientRect(hwnd, out RECT clientRect) || !GetWindowRect(hwnd, out RECT windowRect)) return;

            int clientWidth = clientRect.Right - clientRect.Left;
            int clientHeight = clientRect.Bottom - clientRect.Top;
            if (clientWidth == 1920 && clientHeight == 1080) return;

            int extraWidth = (windowRect.Right - windowRect.Left) - clientWidth;
            int extraHeight = (windowRect.Bottom - windowRect.Top) - clientHeight;

            int targetWindowWidth = 1920 + extraWidth;
            int targetWindowHeight = 1080 + extraHeight;

            int currentWidth = windowRect.Right - windowRect.Left;
            int currentHeight = windowRect.Bottom - windowRect.Top;
            int centerX = windowRect.Left + currentWidth / 2;
            int centerY = windowRect.Top + currentHeight / 2;

            int targetX = centerX - targetWindowWidth / 2;
            int targetY = centerY - targetWindowHeight / 2;

            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;

            SetWindowPos(hwnd, IntPtr.Zero, targetX, targetY, targetWindowWidth, targetWindowHeight, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }
}