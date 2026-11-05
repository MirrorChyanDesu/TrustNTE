using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TrustNTE.Helpers
{
    // 注意：这里是 public static class，代表纯静态工具类
    public static class ClipboardHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        /// <summary>
        /// 尝试将文本复制到系统剪贴板，包含底层重试机制，无视流氓输入法占用。
        /// </summary>
        public static bool SetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            IntPtr hGlobal = IntPtr.Zero;
            try
            {
                int bytes = (text.Length + 1) * 2;
                hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                if (hGlobal == IntPtr.Zero) return false;

                IntPtr target = GlobalLock(hGlobal);
                if (target == IntPtr.Zero) return false;

                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0);

                GlobalUnlock(hGlobal);

                bool opened = false;
                for (int i = 0; i < 5; i++)
                {
                    if (OpenClipboard(IntPtr.Zero))
                    {
                        opened = true;
                        break;
                    }
                    Thread.Sleep(10);
                }

                if (!opened) return false;

                try
                {
                    EmptyClipboard();
                    if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                        return false;

                    hGlobal = IntPtr.Zero;
                    return true;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hGlobal != IntPtr.Zero)
                {
                    GlobalFree(hGlobal);
                }
            }
        }
    }
}