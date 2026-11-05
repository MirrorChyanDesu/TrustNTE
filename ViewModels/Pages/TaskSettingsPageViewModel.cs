using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using TrustNTE.ViewModels.Pages.TaskModules;

namespace TrustNTE.ViewModels.Pages
{
    public partial class TaskSettingsPageViewModel : ObservableObject, IDisposable
    {
        public ObservableCollection<object> Modules { get; } = new();

        public TaskSettingsPageViewModel(
            AutoFishingViewModel fishingVM,
            AutoMusicViewModel musicVM,
            AutoPawViewModel pawVM,
            AutoApartmentViewModel apartmentVM,
            AutoFountainViewModel fountainVM,
            AutoMarketViewModel marketVM,
            AutoGiftViewModel giftVM,
            AutoVolleyballHeartViewModel volleyballHeartVM,
            AutoRedeemViewModel redeemVM,
            AutoHuntViewModel huntVM,
            AutoPilgrimageViewModel pilgrimageVM)
        {
            Modules.Add(fishingVM);
            Modules.Add(musicVM);
            Modules.Add(pawVM);
            Modules.Add(apartmentVM);
            Modules.Add(fountainVM);
            Modules.Add(marketVM);
            Modules.Add(giftVM);
            Modules.Add(volleyballHeartVM);
            Modules.Add(redeemVM);
            Modules.Add(huntVM);
            Modules.Add(pilgrimageVM);
        }

        public void Dispose()
        {
        }
    }
}