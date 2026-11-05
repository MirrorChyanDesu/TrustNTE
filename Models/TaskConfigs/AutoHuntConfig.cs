using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoHuntConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private int _staminaCost = 60;
        [ObservableProperty] private string _replenishMode = "不领取";
        [ObservableProperty] private string _targetBoss = "音霸魔王";
    }
}