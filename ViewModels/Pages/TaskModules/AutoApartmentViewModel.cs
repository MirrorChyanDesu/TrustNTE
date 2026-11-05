using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoApartmentViewModel : TaskModuleViewModelBase
    {
        private const double StaminaWarningCooldownSeconds = 1.5;

        private readonly IDispatcherService _dispatcherService;
        private readonly IDialogService _dialogService;
        private DateTime _lastStaminaWarningTime = DateTime.MinValue;

        public AutoApartmentConfig Config { get; }

        public string[] TeleportTargets { get; } = { "维纳公寓", "伊登公寓", "天景空馆", "金都云邸" };
        public string[] Stages { get; } = { "胡迪尼的魔术舞台", "胡迪尼的诡计舞台", "泡影罐头工厂", "兔子洞" };

        public ObservableCollection<string> Challenges1 { get; } = new() { "合订本", "万花筒", "硬币记" };
        public ObservableCollection<string> Challenges2 { get; } = new() { "小心鸽子", "扑克茶会", "惊喜派对", "心电感应", "越狱艺术" };
        public ObservableCollection<string> Challenges3 { get; } = new() { "泡影罐头·苹果核", "泡影罐头螺旋乐", "泡影罐头·液态梦", "泡影罐头·冷甜点", "泡影罐头·戏剧芯" };
        public ObservableCollection<string> Challenges4 { get; } = new() { "钟表把戏", "雕塑屣馆", "纬线织机", "守卫萝卜", "精神图谱", "轨道之夜" };

        public AutoApartmentViewModel(
            IConfigService configService,
            ILogService logService,
            IDispatcherService dispatcherService,
            IDialogService dialogService)
            : base(logService, configService, "自动公寓")
        {
            _dispatcherService = dispatcherService;
            _dialogService = dialogService;
            Config = BindConfig(c => c.Apartment, (main, cfg) => main.Apartment = cfg);
        }

        // [Phase 1] 持久化由基类接管，这里只保留联动刷新与校验
        protected override void OnConfigChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Config.StaminaCost))
            {
                if (!int.TryParse(Config.StaminaCost, out int val) || val == 0 || val % 60 != 0 || val > 360)
                {
                    _dispatcherService.BeginInvoke(() => _ = ShowStaminaWarningAsync());
                }
            }

            if (e.PropertyName == nameof(Config.Stage1)) UpdateChallenges(1, Config.Stage1);
            if (e.PropertyName == nameof(Config.Stage2)) UpdateChallenges(2, Config.Stage2);
            if (e.PropertyName == nameof(Config.Stage3)) UpdateChallenges(3, Config.Stage3);
            if (e.PropertyName == nameof(Config.Stage4)) UpdateChallenges(4, Config.Stage4);
        }

        // [模块G] 1.5 秒冷却防抖：用户快速输入非法值时避免连续弹窗
        private async Task ShowStaminaWarningAsync()
        {
            if ((DateTime.Now - _lastStaminaWarningTime).TotalSeconds < StaminaWarningCooldownSeconds)
                return;
            _lastStaminaWarningTime = DateTime.Now;

            try
            {
                await _dialogService.ShowInfoAsync(
                    "输入错误",
                    "本性像素（体力）不能为0，必须是60的整数倍，且不能超过360！\n已为您重置为 60。");
            }
            catch
            {
                // 弹窗失败不影响主流程，静默忽略
            }
        }

        private void UpdateChallenges(int index, string stageName)
        {
            ObservableCollection<string> targetList = index switch
            {
                1 => Challenges1,
                2 => Challenges2,
                3 => Challenges3,
                _ => Challenges4
            };

            targetList.Clear();

            if (stageName == "胡迪尼的魔术舞台")
            {
                targetList.Add("合订本"); targetList.Add("万花筒"); targetList.Add("硬币记");
            }
            else if (stageName == "胡迪尼的诡计舞台")
            {
                targetList.Add("小心鸽子"); targetList.Add("扑克茶会"); targetList.Add("惊喜派对");
                targetList.Add("心电感应"); targetList.Add("越狱艺术");
            }
            else if (stageName == "泡影罐头工厂")
            {
                targetList.Add("泡影罐头·苹果核"); targetList.Add("泡影罐头螺旋乐"); targetList.Add("泡影罐头·液态梦");
                targetList.Add("泡影罐头·冷甜点"); targetList.Add("泡影罐头·戏剧芯");
            }
            else if (stageName == "兔子洞")
            {
                targetList.Add("钟表把戏"); targetList.Add("雕塑屣馆"); targetList.Add("纬线织机");
                targetList.Add("守卫萝卜"); targetList.Add("精神图谱"); targetList.Add("轨道之夜");
            }

            if (index == 1) Config.Challenge1 = targetList[0];
            if (index == 2) Config.Challenge2 = targetList[0];
            if (index == 3) Config.Challenge3 = targetList[0];
            if (index == 4) Config.Challenge4 = targetList[0];
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
        }
    }
}
