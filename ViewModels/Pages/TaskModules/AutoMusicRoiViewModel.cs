using CommunityToolkit.Mvvm.ComponentModel;
using TrustNTE.Models.TaskConfigs;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    /// <summary>
    /// 负责音游 ROI 区域和阈值配置的子 ViewModel。
    /// </summary>
    public partial class AutoMusicRoiViewModel : ObservableObject
    {
        public AutoMusicConfig Config { get; }

        public AutoMusicRoiViewModel(AutoMusicConfig config)
        {
            Config = config;
        }
    }
}