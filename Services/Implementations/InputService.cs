using System;
using System.Runtime.InteropServices;
using System.Threading;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Interfaces.Implementations
{
    /// <summary>
    /// 输入服务实现。
    /// </summary>
    public class InputService : IInputService
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        // 键盘消息
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

        // 焦点欺骗消息
        private const uint WM_ACTIVATE = 0x0006;
        private const uint WM_ACTIVATEAPP = 0x001C;
        private const uint WM_SETFOCUS = 0x0007;
        private const uint WM_MOUSEACTIVATE = 0x0021;

        // 鼠标消息
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MBUTTONDOWN = 0x0207;
        private const uint WM_MBUTTONUP = 0x0208;

        // 物理鼠标事件
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        private const int SW_RESTORE = 9;

        // 鼠标按键状态参数
        private static readonly IntPtr MK_LBUTTON = (IntPtr)1;
        private static readonly IntPtr MK_RBUTTON = (IntPtr)2;
        private static readonly IntPtr MK_MBUTTON = (IntPtr)16;

        private readonly ILogService _logService;

        public InputService(ILogService logService)
        {
            _logService = logService;
        }

        /// <summary>
        /// 向后台窗口发送“假焦点”，欺骗游戏引擎认为自己在前台。
        /// </summary>
        private void FakeForeground(IntPtr hWnd)
        {
            PostMessage(hWnd, WM_ACTIVATEAPP, (IntPtr)1, IntPtr.Zero);
            PostMessage(hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
            PostMessage(hWnd, WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero);
            PostMessage(hWnd, WM_MOUSEACTIVATE, (IntPtr)1, IntPtr.Zero);
        }

        // ══════════════════════════════════════════════════════════════
        // [A6·物理输入前置置顶] 所有物理点击/按键的强制前置条件
        //
        // 旧代码裸调 SetForegroundWindow：
        //   ① Windows 前台锁会静默拒绝后台进程的抢焦点请求（返回 false 无人检查）
        //      → 崩溃重启场景（TrustNTE 在后台、启动器抢焦点）置顶必失败
        //      → mouse_event 真实点击落在当前前台窗口上 = 乱点，会误伤桌面软件
        //   ② 无条件 ShowWindow(SW_RESTORE) 会把最大化/无边框全屏打回窗口化
        //
        // 新实现：
        //   快路径：已是前台 → 一次指针比较直接返回，高频场景零开销
        //   慢路径：最小化才还原 → AttachThreadInput 绕前台锁 → 验证结果
        //   失败：返回 false，调用方必须跳过本次动作（宁可漏一次，不可乱点）
        // ══════════════════════════════════════════════════════════════
        public bool ForceForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            // 快路径：目标已是前台，零开销
            if (GetForegroundWindow() == hWnd) return true;

            // 仅最小化时才还原（防把最大化/全屏窗口打回窗口化）
            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);

            // 前台锁绕过：把当前线程挂到前台窗口线程的输入队列，再抢前台
            uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            uint myThread = GetCurrentThreadId();
            bool attached = false;
            bool switched = false;
            try
            {
                if (fgThread != 0 && fgThread != myThread)
                    attached = AttachThreadInput(myThread, fgThread, true);
                switched = SetForegroundWindow(hWnd);
            }
            finally
            {
                if (attached) AttachThreadInput(myThread, fgThread, false);
            }

            bool ok = GetForegroundWindow() == hWnd;
            if (ok)
            {
                if (switched) Thread.Sleep(80); // 真发生了切换，给游戏一点激活处理时间
            }
            else
            {
                _logService.LogWarning("[物理输入] 置顶失败（前台锁/句柄失效），本次物理动作已跳过，防止乱点");
            }
            return ok;
        }

        public void SendKey(IntPtr hWnd, int virtualKey, bool isDown)
        {
            if (hWnd == IntPtr.Zero) return;

            uint scanCode = MapVirtualKey((uint)virtualKey, 0);
            uint lParam = 1u | (scanCode << 16);
            if (!isDown) lParam |= 0xC0000000;

            uint msg = isDown ? WM_KEYDOWN : WM_KEYUP;
            FakeForeground(hWnd);
            PostMessage(hWnd, msg, (IntPtr)virtualKey, (IntPtr)lParam);
        }

        public void SendKeyPress(IntPtr hWnd, int virtualKey, int holdMs = 50)
        {
            SendKey(hWnd, virtualKey, true);
            Thread.Sleep(holdMs);
            SendKey(hWnd, virtualKey, false);
        }

        /// <summary>
        /// 原有的纯后台点击（不抢焦点、不移动物理鼠标）——后台通道，不置顶
        /// </summary>
        public void SendBackgroundMouseClick(IntPtr hWnd, int x, int y, bool isLeft = true)
        {
            if (hWnd == IntPtr.Zero) return;

            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOACTIVATE = 0x0010;
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));

            FakeForeground(hWnd);

            PostMessage(hWnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
            Thread.Sleep(20);

            if (isLeft)
            {
                PostMessage(hWnd, WM_LBUTTONDOWN, MK_LBUTTON, lParam);
                Thread.Sleep(50);
                PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
            }
            else
            {
                PostMessage(hWnd, WM_RBUTTONDOWN, MK_RBUTTON, lParam);
                Thread.Sleep(50);
                PostMessage(hWnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
            }
        }

        /// <summary>
        /// 中键点击（战斗锁定目标，后台通道）
        /// </summary>
        public void SendMiddleClick(IntPtr hWnd, int x, int y)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));

            FakeForeground(hWnd);

            PostMessage(hWnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
            Thread.Sleep(20);

            PostMessage(hWnd, WM_MBUTTONDOWN, MK_MBUTTON, lParam);
            Thread.Sleep(50);
            PostMessage(hWnd, WM_MBUTTONUP, IntPtr.Zero, lParam);
        }

        /// <summary>
        /// 物理中键点击（战斗锁定目标的物理驱动通道）：
        /// 强制置顶（失败即跳过）→ 记录鼠标 → 移到目标点 → 真实中键按下/抬起 → 还原鼠标位置。
        /// </summary>
        public void SendPhysicalMiddleClickWithRestore(IntPtr hWnd, int clientX, int clientY)
        {
            if (hWnd == IntPtr.Zero) return;

            if (!ForceForeground(hWnd)) return; // [A6] 置顶失败 → 跳过，绝不盲点

            POINT pt = new POINT { X = clientX, Y = clientY };
            ClientToScreen(hWnd, ref pt);
            GetCursorPos(out POINT oldPoint);

            try
            {
                SetCursorPos(pt.X, pt.Y);
                Thread.Sleep(50);

                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(20);
                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
            }
            finally
            {
                SetCursorPos(oldPoint.X, oldPoint.Y);
            }
        }

        /// <summary>
        /// 左键按下（用于重击长按）
        /// </summary>
        public void SendLeftMouseDown(IntPtr hWnd, int x, int y)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));

            FakeForeground(hWnd);
            PostMessage(hWnd, WM_LBUTTONDOWN, MK_LBUTTON, lParam);
        }

        /// <summary>
        /// 左键抬起（用于结束重击）
        /// </summary>
        public void SendLeftMouseUp(IntPtr hWnd, int x, int y)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
            PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
        }

        /// <summary>
        /// 瞬移物理点击，专门用来破解音游那种拒绝假点击的强校验 UI。
        /// [改] 入口强制置顶：失败即跳过（防止重启后旧句柄/前台锁导致乱点）。
        /// </summary>
        public void SendPhysicalClickWithRestore(IntPtr hWnd, int clientX, int clientY, bool isLeft = true)
        {
            if (hWnd == IntPtr.Zero) return;

            if (!ForceForeground(hWnd)) return; // [A6] 置顶失败 → 跳过，绝不盲点

            POINT pt = new POINT { X = clientX, Y = clientY };
            ClientToScreen(hWnd, ref pt);
            GetCursorPos(out POINT oldPoint);

            try
            {
                SetCursorPos(pt.X, pt.Y);
                Thread.Sleep(50);

                uint down = isLeft ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN;
                uint up = isLeft ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP;

                mouse_event(down, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(20);
                mouse_event(up, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
            }
            finally
            {
                SetCursorPos(oldPoint.X, oldPoint.Y);
            }
        }
    }
}
