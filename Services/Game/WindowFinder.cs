using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;

namespace TrustNTE.Services.Game
{
    public class WindowFinder
    {
        public IntPtr FindWindowForProcess(int pid, string? className = null)
        {
            IntPtr result = IntPtr.Zero;
            int maxArea = 0;

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);
                if (windowPid == pid)
                {
                    if (!string.IsNullOrEmpty(className))
                    {
                        var sb = new StringBuilder(256);
                        NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
                        if (sb.ToString() != className) return true;
                    }
                    if (NativeMethods.IsWindowVisible(hWnd))
                    {
                        NativeMethods.GetWindowRect(hWnd, out var rect);
                        int area = (rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                        if (area > maxArea && (rect.Right - rect.Left) > 200 && (rect.Bottom - rect.Top) > 200)
                        {
                            maxArea = area;
                            result = hWnd;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        public async Task<IntPtr> WaitForGameWindowAsync(CancellationToken token)
        {
            var gameProc = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();
            if (gameProc == null) return IntPtr.Zero;

            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 60; i++)
            {
                if (token.IsCancellationRequested) return IntPtr.Zero;
                hwnd = FindWindowForProcess(gameProc.Id, GameConstants.GameWindowClass);
                if (hwnd != IntPtr.Zero) break;
                await Task.Delay(500, token);
            }
            return hwnd;
        }
    }
}