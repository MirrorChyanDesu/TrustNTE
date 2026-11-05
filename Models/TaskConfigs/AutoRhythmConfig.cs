using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoRhythmConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private int _loopCount = 0; // 0代表无限
        [ObservableProperty] private bool _autoClickStart = true;
    }
}