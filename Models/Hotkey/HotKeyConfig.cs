namespace TrustNTE.Models
{
    /// <summary>
    /// 快捷键配置数据模型，用于持久化到 JSON。
    /// </summary>
    public class HotKeyConfig
    {
        /// <summary>
        /// 启动/停止截图的快捷键。
        /// </summary>
        public string StartCaptureHotkey { get; set; } = "F1";

        /// <summary>
        /// 启动/停止截图的快捷键类型。
        /// </summary>
        public string StartCaptureHotkeyType { get; set; } = "GlobalRegister";

        /// <summary>
        /// 停止当前自动化任务的快捷键。
        /// </summary>
        public string CancelTaskHotkey { get; set; } = "";

        /// <summary>
        /// 停止当前自动化任务的快捷键类型。
        /// </summary>
        public string CancelTaskHotkeyType { get; set; } = "KeyboardMonitor";

        /// <summary>
        /// 游戏截图的快捷键。
        /// </summary>
        public string TakeScreenshotHotkey { get; set; } = "";

        /// <summary>
        /// 游戏截图的快捷键类型。
        /// </summary>
        public string TakeScreenshotHotkeyType { get; set; } = "KeyboardMonitor";

        /// <summary>
        /// 打开日志窗口的快捷键。
        /// </summary>
        public string OpenLogHotkey { get; set; } = "";

        /// <summary>
        /// 打开日志窗口的快捷键类型。
        /// </summary>
        public string OpenLogHotkeyType { get; set; } = "KeyboardMonitor";

        /// <summary>
        /// 打开实时预览窗口的快捷键。
        /// </summary>
        public string OpenPreviewHotkey { get; set; } = "";

        /// <summary>
        /// 打开实时预览窗口的快捷键类型。
        /// </summary>
        public string OpenPreviewHotkeyType { get; set; } = "KeyboardMonitor";

        /// <summary>
        /// 自动拾取开关的快捷键。
        /// </summary>
        public string AutoLootHotkey { get; set; } = "";

        /// <summary>
        /// 自动拾取开关的快捷键类型。
        /// </summary>
        public string AutoLootHotkeyType { get; set; } = "KeyboardMonitor";

        /// <summary>
        /// 自动剧情开关的快捷键。
        /// </summary>
        public string AutoDialogueHotkey { get; set; } = "";

        /// <summary>
        /// 自动剧情开关的快捷键类型。
        /// </summary>
        public string AutoDialogueHotkeyType { get; set; } = "KeyboardMonitor";

        /// <summary>
        /// 自动战斗开关的快捷键。
        /// </summary>
        public string AutoCombatHotkey { get; set; } = "";

        /// <summary>
        /// 自动战斗开关的快捷键类型。
        /// </summary>
        public string AutoCombatHotkeyType { get; set; } = "KeyboardMonitor";
    }
}