using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace TrustNTE.Models.TaskConfigs
{
    public partial class AutoApartmentConfig : ObservableObject
    {
        [ObservableProperty] private bool _isEnabled = false;
        [ObservableProperty] private string _teleportTarget = "维纳公寓";
        [ObservableProperty] private string _staminaCost = "60";

        [ObservableProperty] private string _stage1 = "胡迪尼的魔术舞台";
        [ObservableProperty] private string _challenge1 = "合订本";

        [ObservableProperty] private string _stage2 = "胡迪尼的诡计舞台";
        [ObservableProperty] private string _challenge2 = "小心鸽子";

        [ObservableProperty] private string _stage3 = "泡影罐头工厂";
        [ObservableProperty] private string _challenge3 = "泡影罐头·苹果核";

        [ObservableProperty] private string _stage4 = "兔子洞";
        [ObservableProperty] private string _challenge4 = "钟表把戏";

        partial void OnStaminaCostChanged(string value)
        {
            if (!int.TryParse(value, out int val) || val == 0 || val % 60 != 0 || val > 360)
            {
                // 强制重置
                StaminaCost = "60";
                // 至于弹窗，交给 ViewModel 去监听 Config 的 PropertyChanged 触发
            }
        }
    }
}