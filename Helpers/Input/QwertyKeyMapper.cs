using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace TrustNTE.Helpers
{
    /// <summary>
    /// QWERTY 物理键位映射器，负责将按键转换为包含 ScanCode 的底层 lParam，防止 UE 引擎不识别。
    /// </summary>
    public static class QwertyKeyMapper
    {
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private const uint MAPVK_VK_TO_VSC = 0;

        /// <summary>
        /// 获取虚拟键码对应的 ScanCode。
        /// </summary>
        /// <param name="virtualKey">虚拟键码 (VK)。</param>
        /// <returns>扫描码。</returns>
        public static uint GetScanCode(int virtualKey)
        {
            return MapVirtualKey((uint)virtualKey, MAPVK_VK_TO_VSC);
        }

        /// <summary>
        /// 构造发送 WM_KEYDOWN 或 WM_KEYUP 所需的 lParam。
        /// </summary>
        /// <param name="virtualKey">虚拟键码。</param>
        /// <param name="isKeyUp">是否为抬起事件。</param>
        /// <returns>拼装好的 lParam 指针。</returns>
        public static IntPtr BuildKeyLParam(int virtualKey, bool isKeyUp)
        {
            uint scanCode = GetScanCode(virtualKey);
            uint lParam = 1u | (scanCode << 16);

            if (isKeyUp)
            {
                lParam |= 0xC0000000;
            }

            // 扩展键处理（如方向键、Home、End等）
            if (virtualKey == 0x25 || virtualKey == 0x26 || virtualKey == 0x27 || virtualKey == 0x28 ||
                virtualKey == 0x21 || virtualKey == 0x22 || virtualKey == 0x23 || virtualKey == 0x24)
            {
                lParam |= 0x01000000;
            }

            return (IntPtr)lParam;
        }
    }
}