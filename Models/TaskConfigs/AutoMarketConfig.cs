using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoMarketConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private string _conditionMode = "调整时间";
        [ObservableProperty] private bool _autoTeleport = true;
    }
}