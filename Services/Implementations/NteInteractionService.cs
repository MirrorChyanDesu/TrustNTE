using System;
using System.Runtime.InteropServices;
using System.Threading;
using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class NteInteractionService : INteInteractionService
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool BlockInput(bool fBlockIt);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MBUTTONDOWN = 0x0207;   //        中键按下
        private const uint WM_MBUTTONUP = 0x0208;     //        中键抬起
        private const uint WM_MOUSEMOVE = 0x0200;

        private static readonly IntPtr MK_LBUTTON = (IntPtr)1;
        private static readonly IntPtr MK_RBUTTON = (IntPtr)2;
        private static readonly IntPtr MK_MBUTTON = (IntPtr)16;   //        MK_MBUTTON = 0x0010

        private readonly ILogService _logService;
        private readonly object _inputLock = new object();
        private bool _isOperating = false;

        public NteInteractionService(ILogService logService)
        {
            _logService = logService;
        }

        public void NteClick(IntPtr hwnd, int clientX, int clientY, bool isLeft = true, bool restoreCursor = true)
        {
            if (hwnd == IntPtr.Zero) return;
            Win32Input.FakeForeground(hwnd);

            lock (_inputLock)
            {
                POINT oldPoint = default;
                bool shouldRestore = restoreCursor && !_isOperating;
                if (shouldRestore)
                {
                    GetCursorPos(out oldPoint);
                }

                POINT pt = new POINT { X = clientX, Y = clientY };
                ClientToScreen(hwnd, ref pt);
                SetCursorPos(pt.X, pt.Y);
                Thread.Sleep(35);

                IntPtr lParam = (IntPtr)((clientY << 16) | (clientX & 0xFFFF));
                PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);

                if (isLeft)
                {
                    PostMessage(hwnd, WM_LBUTTONDOWN, MK_LBUTTON, lParam);
                    Thread.Sleep(20);
                    PostMessage(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                }
                else
                {
                    PostMessage(hwnd, WM_RBUTTONDOWN, MK_RBUTTON, lParam);
                    Thread.Sleep(20);
                    PostMessage(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
                }

                if (shouldRestore)
                {
                    Thread.Sleep(35);
                    SetCursorPos(oldPoint.X, oldPoint.Y);
                }
            }
        }

        /// <summary>
        ///        物理悬浮中键单击（战斗锁定目标）。结构与 NteClick 一致：假前台 → 移动光标 → 中键按下/抬起 → 还原鼠标。
        /// </summary>
        public void NteMiddleClick(IntPtr hwnd, int clientX, int clientY)
        {
            if (hwnd == IntPtr.Zero) return;
            Win32Input.FakeForeground(hwnd);

            lock (_inputLock)
            {
                POINT oldPoint = default;
                bool shouldRestore = !_isOperating;
                if (shouldRestore)
                {
                    GetCursorPos(out oldPoint);
                }

                POINT pt = new POINT { X = clientX, Y = clientY };
                ClientToScreen(hwnd, ref pt);
                SetCursorPos(pt.X, pt.Y);
                Thread.Sleep(35);

                IntPtr lParam = (IntPtr)((clientY << 16) | (clientX & 0xFFFF));
                PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
                PostMessage(hwnd, WM_MBUTTONDOWN, MK_MBUTTON, lParam);
                Thread.Sleep(20);
                PostMessage(hwnd, WM_MBUTTONUP, IntPtr.Zero, lParam);

                if (shouldRestore)
                {
                    Thread.Sleep(35);
                    SetCursorPos(oldPoint.X, oldPoint.Y);
                }
            }
        }

        // [保留] 原有方法，行为完全不变，其他模块继续使用
        public void NteKeyPress(IntPtr hwnd, int virtualKey, int holdMs = 50)
        {
            if (hwnd == IntPtr.Zero) return;
            Win32Input.FakeForeground(hwnd);

            lock (_inputLock)
            {
                IntPtr lParamDown = QwertyKeyMapper.BuildKeyLParam(virtualKey, false);
                IntPtr lParamUp = QwertyKeyMapper.BuildKeyLParam(virtualKey, true);

                PostMessage(hwnd, WM_KEYDOWN, (IntPtr)virtualKey, lParamDown);
                Thread.Sleep(holdMs);
                PostMessage(hwnd, WM_KEYUP, (IntPtr)virtualKey, lParamUp);
            }
        }

        // [新增] 只发按下消息，不阻塞。供钓鱼控条等需要持续按住的场景使用。
        public void NteKeyDown(IntPtr hwnd, int virtualKey)
        {
            if (hwnd == IntPtr.Zero) return;
            Win32Input.FakeForeground(hwnd);

            lock (_inputLock)
            {
                IntPtr lParamDown = QwertyKeyMapper.BuildKeyLParam(virtualKey, false);
                PostMessage(hwnd, WM_KEYDOWN, (IntPtr)virtualKey, lParamDown);
            }
        }

        // [新增] 只发抬起消息，不阻塞。与 NteKeyDown 配对使用。
        public void NteKeyUp(IntPtr hwnd, int virtualKey)
        {
            if (hwnd == IntPtr.Zero) return;

            lock (_inputLock)
            {
                IntPtr lParamUp = QwertyKeyMapper.BuildKeyLParam(virtualKey, true);
                PostMessage(hwnd, WM_KEYUP, (IntPtr)virtualKey, lParamUp);
            }
        }

        public void NteOperate(Action action)
        {
            lock (_inputLock)
            {
                bool isOuter = false;
                POINT oldPoint = default;

                if (!_isOperating)
                {
                    GetCursorPos(out oldPoint);
                    _isOperating = true;
                    isOuter = true;
                }

                try
                {
                    BlockInput(true);
                    action();
                }
                catch (Exception ex)
                {
                    _logService.LogError($"[NteOperate] 执行异常: {ex.Message}");
                }
                finally
                {
                    if (isOuter)
                    {
                        _isOperating = false;
                        if (oldPoint.X != 0 || oldPoint.Y != 0)
                        {
                            SetCursorPos(oldPoint.X, oldPoint.Y);
                        }
                    }
                    BlockInput(false);
                }
            }
        }
    }
}
