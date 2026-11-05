using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoVolleyballHeartConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private int _teleportTarget = 1;
    }
}