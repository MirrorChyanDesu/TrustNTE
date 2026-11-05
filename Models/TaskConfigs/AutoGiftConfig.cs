using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace TrustNTE.Models.TaskConfigs
{
    /// <summary>
    /// 单个赠礼槽位的配置。
    /// </summary>
    public partial class GiftSlotConfig : ObservableObject
    {
        /// <summary>
        /// 赠礼角色名称。
        /// </summary>
        [ObservableProperty] private string _characterName = "无";

        /// <summary>
        /// 赠送次数。
        /// </summary>
        [ObservableProperty] private int _giftCount = 1;

        /// <summary>
        /// 礼物选择类型。
        /// </summary>
        [ObservableProperty] private int _giftType = 1;
    }

    /// <summary>
    /// 羁绊礼物模块的配置。
    /// </summary>
    public partial class AutoGiftConfig : ObservableObject
    {
        /// <summary>
        /// 模块是否启用。
        /// </summary>
        [ObservableProperty] private bool _isEnabled = false;

        /// <summary>
        /// 7个赠礼槽位。
        /// </summary>
        public ObservableCollection<GiftSlotConfig> Slots { get; } = new()
        {
            new GiftSlotConfig(), new GiftSlotConfig(), new GiftSlotConfig(),
            new GiftSlotConfig(), new GiftSlotConfig(), new GiftSlotConfig(),
            new GiftSlotConfig()
        };
    }
}