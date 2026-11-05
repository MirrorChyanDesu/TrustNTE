using System;
using System.Runtime.InteropServices;

namespace TrustNTE.Helpers
{
    public static class Win32Input
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx; public int dy; public uint mouseData;
            public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk; public ushort wScan; public uint dwFlags;
            public uint time; public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex);

        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint uPeriod);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint uPeriod);

        // [新增] 用于发送焦点欺骗消息
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_ACTIVATE = 0x0006;
        private const uint WM_ACTIVATEAPP = 0x001C;
        private const uint WM_SETFOCUS = 0x0007;
        private const uint WM_MOUSEACTIVATE = 0x0021;

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        public const uint INPUT_MOUSE = 0;
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0008;
        public const uint MAPVK_VK_TO_VSC = 0;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;

        /// <summary>
        /// 强行告诉窗口它获得了焦点，防止底层丢弃输入。
        /// </summary>
        public static void FakeForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_ACTIVATEAPP, (IntPtr)1, IntPtr.Zero);
            PostMessage(hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
            PostMessage(hWnd, WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero);
            PostMessage(hWnd, WM_MOUSEACTIVATE, (IntPtr)1, IntPtr.Zero);
        }

        public static void KeyDown(int vk) => SendKey(vk, false);
        public static void KeyUp(int vk) => SendKey(vk, true);

        private static void SendKey(int vk, bool up)
        {
            ushort scanCode = (ushort)MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = 0;
            inputs[0].U.ki.wScan = scanCode;
            inputs[0].U.ki.dwFlags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0);

            uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (result != 1) { /* 记录日志：输入被 UIPI 阻挡 */ }
        }

        public static void MouseMoveAbsolute(int x, int y)
        {
            int screenW = GetSystemMetrics(78); // SM_CXVIRTUALSCREEN
            int screenH = GetSystemMetrics(79); // SM_CYVIRTUALSCREEN
            int screenX = GetSystemMetrics(76); // SM_XVIRTUALSCREEN
            int screenY = GetSystemMetrics(77); // SM_YVIRTUALSCREEN

            int absX = (int)(((x - screenX) * 65535.0) / screenW);
            int absY = (int)(((y - screenY) * 65535.0) / screenH);

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;
            inputs[0].U.mi.dx = absX;
            inputs[0].U.mi.dy = absY;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void MouseMoveRelative(int dx, int dy)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dwFlags = MOUSEEVENTF_MOVE;
            inputs[0].U.mi.dx = dx;
            inputs[0].U.mi.dy = dy;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void MouseClick(string button, bool down)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dwFlags = button switch
            {
                "Left" => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
                "Right" => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
                "Middle" => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
                _ => 0
            };
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void MouseWheel(int delta)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].U.mi.dwFlags = MOUSEEVENTF_WHEEL;
            inputs[0].U.mi.mouseData = (uint)delta;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}