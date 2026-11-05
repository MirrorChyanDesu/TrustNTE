namespace TrustNTE.Helpers
{
    /// <summary>输入驱动类型。与 AppConfig.MouseDriveType 中文配置一一对应。</summary>
    public enum InputDriveType
    {
        /// <summary>物理点击：真实移动鼠标 + SendInput。最高兼容，但抢焦点、动真鼠标、真实键盘会被注入。</summary>
        Physical = 0,
        /// <summary>后台点击：PostMessage 纯后台，不动鼠标不抢焦点，部分引擎拒收。</summary>
        Background = 1,
        /// <summary>通用点击：FakeForeground + ScanCode lParam，专治 UE 引擎丢弃后台消息。</summary>
        Trick = 2,
    }

    public static class InputDriveTypeExtensions
    {
        /// <summary>中文配置 → 枚举。兜底默认"通用点击"（与 AppConfig 初始值一致）。</summary>
        public static InputDriveType Parse(string? raw) => raw?.Trim() switch
        {
            "物理点击" => InputDriveType.Physical,
            "后台点击" => InputDriveType.Background,
            "通用点击" => InputDriveType.Trick,
            _ => InputDriveType.Trick,
        };

        public static string ToConfigString(this InputDriveType d) => d switch
        {
            InputDriveType.Physical => "物理点击",
            InputDriveType.Background => "后台点击",
            _ => "通用点击",
        };
    }
}
