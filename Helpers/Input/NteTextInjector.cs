using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TrustNTE.Helpers
{
    /// <summary>
    /// 向游戏窗口逐字符直注文本（WM_CHAR）。
    /// [背景] 实测按键级注入不改变系统键盘状态表，Ctrl 组合键两次加长时序仍退化为裸键
    /// （"avav" / "V" 事故）。WM_CHAR 直接投递字符本体，绕过   饰键与剪贴板，
    /// 与已验证有效的单键注入同通道（窗口消息），中文/大小写/数字通吃。
    /// </summary>
    public static class NteTextInjector
    {
        private const uint WM_CHAR = 0x0102;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 逐字符注入文本。delayMs = 单字符间隔（默认 15ms，可经 TextInjectDelayMs 配置）。
        /// 字符本体走 wParam，大写 'N' 到达即 'N'，中文 BMP 字符同样直达。
        /// </summary>
        public static async Task InjectTextAsync(IntPtr hwnd, string text, int delayMs, CancellationToken ct)
        {
            if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(text)) return;
            foreach (var ch in text)
            {
                ct.ThrowIfCancellationRequested();
                // lParam 传 0 即可：字符内容全在 wParam，引擎文本输入只读字符码
                PostMessage(hwnd, WM_CHAR, (IntPtr)ch, IntPtr.Zero);
                await Task.Delay(delayMs, ct);
            }
        }
    }
}
