using CommunityToolkit.Mvvm.ComponentModel;
using TrustNTE.Helpers;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoFishingConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private bool _autoTeleport = true;

        [ObservableProperty] private int _sCount = 0;
        [ObservableProperty] private int _aCount = 0;
        [ObservableProperty] private int _bCount = 0;

        [ObservableProperty] private int _failCount = 0;

        // 循环次数和完成后关机
        [ObservableProperty] private int _loopCount = 0;
        [ObservableProperty] private bool _shutdownAfterFinish = false;

        // 动作延迟（毫秒），默认 500
        [ObservableProperty] private int _actionDelayMs = 500;

        [ObservableProperty] private bool _autoBuyBait = true;
        [ObservableProperty] private bool _clearStatsOnStart = false; // 默认开启不清空
        [ObservableProperty] private string _controlRoiStr = "605,61,1322,88";

        // ================= 按键映射 =================
        /// <summary> 交互/抛竿 (默认 F) </summary>
        [ObservableProperty] private int _castKey = KeyCode.F;

        /// <summary> 控条左移 (默认 A) </summary>
        [ObservableProperty] private int _leftKey = KeyCode.A;

        /// <summary> 控条右移 (默认 D) </summary>
        [ObservableProperty] private int _rightKey = KeyCode.D;

        /// <summary> 卖鱼 (默认 Q) </summary>
        [ObservableProperty] private int _sellFishKey = KeyCode.Q;

        /// <summary> 更换鱼饵 (默认 E) </summary>
        [ObservableProperty] private int _changeBaitKey = KeyCode.E;

        /// <summary> 商店 (默认 R) </summary>
        [ObservableProperty] private int _shopKey = KeyCode.R;
    }
}