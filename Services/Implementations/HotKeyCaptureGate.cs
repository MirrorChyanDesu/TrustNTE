using System;

namespace TrustNTE.Services
{
    /// <summary>
    /// 快捷键录入闸门：录入框聚焦期间挂起全部全局热键注册。
    /// 原因：RegisterHotKey 的键会被系统截走（WM_HOTKEY），焦点窗口收不到，
    /// 导致“已被全局占用的键永远无法录入”。同时静音钩子路由，防止录入动作误触发功能。
    /// </summary>
    public static class HotKeyCaptureGate
    {
        public static bool IsCapturing { get; private set; }
        public static event Action? Started;
        public static event Action? Ended;

        public static void Begin()
        {
            if (IsCapturing) return;
            IsCapturing = true;
            Started?.Invoke();
        }

        public static void End()
        {
            if (!IsCapturing) return;
            IsCapturing = false;
            Ended?.Invoke();
        }
    }
}
