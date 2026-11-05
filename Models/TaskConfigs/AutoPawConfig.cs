using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoPawConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
    }
}