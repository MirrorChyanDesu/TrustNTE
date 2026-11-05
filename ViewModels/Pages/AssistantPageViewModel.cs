using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TrustNTE.Models;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages
{
    public partial class AssistantPageViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;

        public ObservableCollection<AssistantFeatureItem> Features { get; } = new();

        public AssistantPageViewModel(IConfigService configService, IDialogService dialogService)
        {
            _configService = configService;
            _dialogService = dialogService;

            var config = _configService.Load();

            // 1. 初始化真实功能卡片
            var autoDriftItem = new AssistantFeatureItem
            {
                Key = "auto_drift",
                Title = "自动飘逸",
                Description = "自动进行闪避、位移等飘逸操作",
                IsPlaceholder = false,
                IsEnabled = config.AutoDriftEnabled
            };
            autoDriftItem.StateChanged += OnFeatureStateChanged;
            Features.Add(autoDriftItem);

            // 2. 初始化占位功能卡片
            for (int i = 2; i <= 5; i++)
            {
                Features.Add(new AssistantFeatureItem
                {
                    Key = $"placeholder_{i}",
                    Title = $"待开发功能 {i}",
                    Description = "异环目前没有需要辅助启动的角色或者事件",
                    IsPlaceholder = true
                });
            }
        }

        private void OnFeatureStateChanged(object? sender, EventArgs e)
        {
            if (sender is AssistantFeatureItem item && item.Key == "auto_drift")
            {
                _configService.Update(c => c.AutoDriftEnabled = item.IsEnabled);
            }
        }

        [RelayCommand]
        private async Task BindHotkeyAsync(AssistantFeatureItem item)
        {
            if (item.IsPlaceholder)
            {
                await _dialogService.ShowInfoAsync("暂未开放", "异环目前没有需要辅助启动的角色或者事件，该功能暂处于占位状态。");
                return;
            }

            // 真实功能的快捷键绑定逻辑后续接入
            await _dialogService.ShowInfoAsync("快捷键", $"即将为【{item.Title}】配置快捷键，功能开发中。");
        }
    }
}