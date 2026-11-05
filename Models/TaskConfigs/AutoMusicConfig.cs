using CommunityToolkit.Mvvm.ComponentModel;
using TrustNTE.Helpers;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class RoiPointConfig : ObservableObject
    {
        [ObservableProperty] private double _x;
        [ObservableProperty] private double _y;
    }

    public partial class AutoMusicConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;

        // 流程控制
        [ObservableProperty] private int _loopCount = 0; // 0 = 无限循环
        [ObservableProperty] private bool _autoClickStart = true;

        // 键位（默认 DFJK）
        [ObservableProperty] private string _keyD = "D";
        [ObservableProperty] private string _keyF = "F";
        [ObservableProperty] private string _keyJ = "J";
        [ObservableProperty] private string _keyK = "K";

        // ==================== 判定参数 ====================

        /// <summary>【弃用】保留防报错</summary>
        [ObservableProperty] private double _darkTriggerRatio = 0.08;
        /// <summary>【弃用】保留防报错</summary>
        [ObservableProperty] private double _darkResetRatio = 0.50;

        /// <summary>亮度上限 V≤ (0~255)。调大防漏按，调小防误触。</summary>
        [ObservableProperty] private int _darkVHi = 30;

        /// <summary>最小按键间隔（毫秒）。</summary>
        [ObservableProperty] private int _minFireGapMs = 50;

        /// <summary>按住时长（毫秒）。</summary>
        [ObservableProperty] private int _keyDownTimeMs = 5;

        /// <summary>侦测半径（像素）。</summary>
        [ObservableProperty] private int _observeRadius = 5;

        /// <summary>
        /// 触发像素个数。判定圈内出现多少个特征像素就触发按键。
        /// 【调大】更迟钝防误触；【调小】更灵敏。
        /// </summary>
        [ObservableProperty] private int _hitPixelCount = 15;

        /// <summary>
        /// 复位像素个数。按键后，特征像素必须降到这个值以下，才允许下一次判定。
        /// </summary>
        [ObservableProperty] private int _resetPixelCount = 2;

        /// <summary>
        /// 强制复位时间（毫秒）。超时强行解锁判定，防止特效太长卡死漏键。
        /// </summary>
        [ObservableProperty] private int _forceResetMs = 100;

        // 调试与输入
        [ObservableProperty] private double _debugFontScale = 0.45;
        [ObservableProperty] private bool _enableDebugLog = false;
        [ObservableProperty] private InputDriveType _inputMode = InputDriveType.Trick;

        // ROI 坐标配置 (百分比)
        public RoiPointConfig RoiD { get; set; } = new() { X = 0.2301, Y = 0.7715 };
        public RoiPointConfig RoiF { get; set; } = new() { X = 0.4055, Y = 0.7715 };
        public RoiPointConfig RoiJ { get; set; } = new() { X = 0.5941, Y = 0.7715 };
        public RoiPointConfig RoiK { get; set; } = new() { X = 0.7699, Y = 0.7715 };
    }
}