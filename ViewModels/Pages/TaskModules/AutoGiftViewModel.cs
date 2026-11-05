using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    /// <summary>羁绊礼物任务模块的 ViewModel。</summary>
    public partial class AutoGiftViewModel : TaskModuleViewModelBase
    {
        public AutoGiftConfig Config { get; }

        public ObservableCollection<string> AvailableCharacters { get; } = new()
        {
            "无", "九原", "伊洛伊", "卡厄斯", "哈索尔", "塔吉多", "娜娜莉", "安魂曲", "小吱", "早雾",
            "明音凛", "残虹", "法帝娅", "浔", "灵可", "白藏", "真红", "达芙蒂尔", "零", "黑羽"
        };

        public int[] AvailableGifts { get; } = { 1, 2, 3, 4, 5, 6 };
        public int[] AvailableGiftCounts { get; } = { 1, 2, 3 };

        public AutoGiftViewModel(IConfigService configService, ILogService logService)
            : base(logService, configService, "羁绊礼物")
        {
            Config = BindConfig(c => c.Gift, (main, cfg) => main.Gift = cfg);
        }

        // [模块H] 按中文字符拼音升序排序
        [RelayCommand]
        private void SortCharactersAscending()
        {
            var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true);
            ApplySortOrder(AvailableCharacters.OrderBy(x => x, comparer).ToList());
        }

        // [模块H] 按中文字符拼音降序排序
        [RelayCommand]
        private void SortCharactersDescending()
        {
            var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true);
            ApplySortOrder(AvailableCharacters.OrderByDescending(x => x, comparer).ToList());
        }

        private void ApplySortOrder(System.Collections.Generic.List<string> sorted)
        {
            AvailableCharacters.Clear();
            foreach (var item in sorted) AvailableCharacters.Add(item);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
        }
    }
}
