using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    /// <summary>
    /// 自动战斗配置。
    /// </summary>
    public partial class AutoCombatConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        
        /// <summary>
        /// 目标检测置信度阈值。
        /// </summary>
        [ObservableProperty] private double _confidenceThreshold = 0.65;

        /// <summary>
        /// 是否自动使用终结技 (Q)。
        /// </summary>
        [ObservableProperty] private bool _useUltimate = true;
    }
}