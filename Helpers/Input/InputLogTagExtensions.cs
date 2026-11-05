using TrustNTE.Services.Interfaces;

namespace TrustNTE.Helpers
{
    /// <summary>
    /// 输入日志标签扩展：调用方生成"→ 动作 (x,y) [驱动名]"片段，拼进自己的日志行。
    /// Router 本身不打日志（钓鱼 30ms/Tick 会刷爆），驱动名统一从这里取，消灭散落的中文字符串。
    /// </summary>
    public static class InputLogTagExtensions
    {
        /// <summary>"→ 左键点击 (960,540) [通用点击]"。verb 可传 左键点击/右键点击/点击 等。</summary>
        public static string ClickTag(this IInputRouter router, int x, int y, string verb = "点击")
            => $"→ {verb} ({x},{y}) [{router.CurrentDrive.ToConfigString()}]";

        /// <summary>"→ 按下 F 键 [通用点击]"。与原 AutoDialogueTrigger 日志格式完全对齐。</summary>
        public static string KeyTag(this IInputRouter router, int virtualKey)
            => $"→ 按下 {KeyCodeName(virtualKey)} 键 [{router.CurrentDrive.ToConfigString()}]";

        /// <summary>虚拟键码 → 显示名。统一出口，替代各 Trigger 里私养的 GetKeyName。</summary>
        public static string KeyCodeName(int vk) => vk switch
        {
            KeyCode.Esc => "ESC",
            KeyCode.Space => "Space",
            KeyCode.Enter => "Enter",
            KeyCode.Tab => "Tab",
            KeyCode.Backspace => "Backspace",
            _ when vk is >= KeyCode.A and <= KeyCode.Z => ((char)vk).ToString(),
            _ when vk is >= KeyCode.D0 and <= KeyCode.D9 => ((char)vk).ToString(),
            _ => $"0x{vk:X2}",
        };
    }
}
