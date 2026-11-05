using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoPilgrimageConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private string _challengeMode = "联机模式";
        [ObservableProperty] private string _strategy = "挑战1个目标";
        [ObservableProperty] private string _targetsInput = "";
    }
}