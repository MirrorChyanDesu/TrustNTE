using System;

namespace TrustNTE.Services.Interfaces
{
    /// <summary>
    /// 输入服务接口，负责发送键盘和鼠标事件。
    /// </summary>
    public interface IInputService
    {
        /// <summary>
        /// 发送键盘按下或抬起。
        /// </summary>
        void SendKey(IntPtr hWnd, int virtualKey, bool isDown);

        /// <summary>
        /// 发送键盘点击（按下并松开）。
        /// </summary>
        void SendKeyPress(IntPtr hWnd, int virtualKey, int holdMs = 50);

        /// <summary>
        /// 发送带有欺骗焦点机制的后台鼠标点击（左键或右键）。
        /// </summary>
        void SendBackgroundMouseClick(IntPtr hWnd, int x, int y, bool isLeft = true);

        /// <summary>
        /// 发送后台鼠标中键点击（用于战斗锁定目标）。
        /// </summary>
        void SendMiddleClick(IntPtr hWnd, int x, int y);

        /// <summary>
        /// 瞬移物理中键点击（战斗锁定目标的物理驱动通道）：拉前台、真实中键、还原鼠标。
        /// </summary>
        void SendPhysicalMiddleClickWithRestore(IntPtr hWnd, int clientX, int clientY);

        /// <summary>
        /// 发送后台鼠标左键按下（用于长按重击）。
        /// </summary>
        void SendLeftMouseDown(IntPtr hWnd, int x, int y);

        /// <summary>
        /// 发送后台鼠标左键抬起（用于结束重击）。
        /// </summary>
        void SendLeftMouseUp(IntPtr hWnd, int x, int y);

        /// <summary>
        /// 瞬移物理点击：强行拉到前台，记录并移动鼠标点击，然后瞬间还原鼠标。
        /// 专门用于破解 UE 引擎（如音游按钮）。
        /// [新增] isLeft：支持右键物理点击（战斗锁定目标场景），默认左键，存量调用零改动。
        /// </summary>
        void SendPhysicalClickWithRestore(IntPtr hWnd, int clientX, int clientY, bool isLeft = true);
    }
}
