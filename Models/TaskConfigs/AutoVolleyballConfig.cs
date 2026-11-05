using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoVolleyballConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private int _teleportTarget = 1; // 1不传送 2碧波 3理想馆
        [ObservableProperty] private string _executionMode = "刷经验模式";
    }
}