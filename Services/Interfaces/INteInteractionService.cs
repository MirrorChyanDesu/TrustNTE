using System;

namespace TrustNTE.Services.Interfaces
{
    /// <summary>
    /// 高级欺骗交互服务，实现类似 NTEInteraction 的物理悬浮点击和防打断机制。
    /// </summary>
    public interface INteInteractionService
    {
        /// <summary>
        /// 执行物理悬浮点击（记录并还原鼠标位置）。
        /// </summary>
        void NteClick(IntPtr hwnd, int clientX, int clientY, bool isLeft = true, bool restoreCursor = true);

        /// <summary>
        /// 物理悬浮中键单击（战斗锁定目标，记录并还原鼠标位置）。
        /// </summary>
        void NteMiddleClick(IntPtr hwnd, int clientX, int clientY);

        /// <summary>
        /// 发送带 ScanCode 的后台按键（按下、保持、抬起，原子动作，内部阻塞 holdMs）。
        /// </summary>
        void NteKeyPress(IntPtr hwnd, int virtualKey, int holdMs = 50);

        /// <summary>
        /// 发送按键按下消息（非阻塞）。与 <see cref="NteKeyUp"/> 配对使用，供需要持续按住的场景调用。
        /// </summary>
        void NteKeyDown(IntPtr hwnd, int virtualKey);

        /// <summary>
        /// 发送按键抬起消息（非阻塞）。与 <see cref="NteKeyDown"/> 配对使用。
        /// </summary>
        void NteKeyUp(IntPtr hwnd, int virtualKey);

        /// <summary>
        /// 在防打断的上下文（BlockInput）中执行一组操作。
        /// </summary>
        void NteOperate(Action action);
    }
}
