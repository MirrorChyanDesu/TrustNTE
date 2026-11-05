using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoFountainConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private string _signMode = "虔诚许愿";
        [ObservableProperty] private bool _autoTeleport = true;
    }
}