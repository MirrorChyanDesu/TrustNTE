using System;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;

namespace TrustNTE.Services.Interfaces
{
    public interface IInputRouter
    {
        /// <summary>
        /// 当前配置生效的驱动类型（用于日志展示）
        /// </summary>
        InputDriveType CurrentDrive { get; }

        // ===== 鼠标 =====
        /// <summary>
        /// 按配置驱动点击（1080P 逻辑坐标，内部归一化到当前视口）。
        /// 返回实际物理客户区坐标，供调用方打日志。
        /// </summary>
        (int Px, int Py) ClickLogical(IntPtr hwnd, int logicalX, int logicalY, bool isLeft = true);

        /// <summary>
        /// 按配置驱动点击（物理客户区坐标，不做归一化）。
        /// </summary>
        void Click(IntPtr hwnd, int clientX, int clientY, bool isLeft = true);

        /// <summary>
        /// 中键单击（战斗锁定目标）。1080P 逻辑坐标，按配置驱动路由。
        /// 返回实际物理客户区坐标，供调用方打日志。
        /// </summary>
        (int Px, int Py) MiddleClickLogical(IntPtr hwnd, int logicalX, int logicalY);

        /// <summary>
        /// 中键单击（物理客户区坐标，不做归一化）。
        /// </summary>
        void MiddleClick(IntPtr hwnd, int clientX, int clientY);

        // ===== 键盘 =====
        /// <summary>按配置驱动按键（按下+保持+抬起，阻塞调用线程）。</summary>
        void KeyPress(IntPtr hwnd, int virtualKey, int holdMs = 50);

        /// <summary>按配置驱动按下（不阻塞），与 KeyUp 配对（钓鱼等持续按住场景）。</summary>
        void KeyDown(IntPtr hwnd, int virtualKey);

        /// <summary>抬起。[卡键防护] 实际使用 KeyDown 时刻记录的驱动。</summary>
        void KeyUp(IntPtr hwnd, int virtualKey);

        // ===== 强制指定驱动（测试页/特殊场景，无视配置）=====
        void ClickWith(InputDriveType drive, IntPtr hwnd, int clientX, int clientY, bool isLeft = true);
        void KeyPressWith(InputDriveType drive, IntPtr hwnd, int virtualKey, int holdMs = 50);

        // ===== 文本注入（WM_CHAR 直注，与驱动无关）=====
        Task InjectTextAsync(IntPtr hwnd, string text, int delayMs, CancellationToken ct);

        // ===== 停止/退出时的卡键释放 =====
        /// <summary>
        /// 一次性抬起当前所有按住的键。[卡键防护] 引擎停止、程序退出前必须调用。
        /// 物理驱动键不依赖 hwnd，即使句柄已失效也能抬起。
        /// </summary>
        void ReleaseAllHeldKeys(IntPtr hwnd);
    }
}
