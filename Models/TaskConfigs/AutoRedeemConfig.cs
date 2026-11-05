using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.Models.TaskConfigs
{
    /// <summary>
    /// 自动兑换码任务配置。
    /// 所有坐标/ROI 均以 1920×1080 为唯一基准模版（帧源已由 NteRenderContextManager 归一化），
    /// 禁止在业务层做任何按窗口实际分辨率的二次缩放。
    /// </summary>
    public sealed partial class AutoRedeemConfig : ObservableObject
    {
        //兑换码文本来源，每行一个码
        [ObservableProperty]
        private string _redeemCodes = string.Empty;

        // ============ 一键兑换流程参数（本次新增） ============
        // 坐标/ROI/超时全部配置化，实测校准无需改代码；
        //分辨率归一化基于 1080P 帧源，坐标为绝对像素不参与缩放

        /// <summary>ESC 菜单中"设置"入口的客户端坐标（绝对禁止写死在代码里）</summary>
        [ObservableProperty] private int _clickSettingsX = 1794;
        [ObservableProperty] private int _clickSettingsY = 183;
        [ObservableProperty] private bool _enableDebugSnapshots = true;

        /// <summary>兑换码弹窗页锚点 ROI（x1,y1,x2,y2，1080P 基准模版）</summary>
        [ObservableProperty] private string _roiRedeemCodePage = "882,293,1044,386";
        /// <summary>设置页状态锚点（猎人等级/鉴别等级组）ROI</summary>
        [ObservableProperty] private string _roiGoToRedemptionCode = "1562,230,1776,380";
        /// <summary>设置页"兑换码"跳转按钮 ROI</summary>
        [ObservableProperty] private string _roiRedemptionCodeRedirect = "1548,247,1665,308";
        /// <summary>输入框占位文字锚点 ROI</summary>
        [ObservableProperty] private string _roiPleaseEnterRedemptionCode = "778,533,1074,639";
        /// <summary>确认按钮锚点 ROI</summary>
        [ObservableProperty] private string _roiRedemptionCodeConfirmation = "1151,716,1297,814";

        /// <summary>兑换码输入框点击坐标</summary>
        [ObservableProperty] private int _clickInputX = 955;
        [ObservableProperty] private int _clickInputY = 556;

        /// <summary>ESC 重试上限（找不到设置页锚点时最多再按几次 ESC）</summary>
        [ObservableProperty] private int _escRetryMax = 2;
        /// <summary>单次锚点检测窗口（毫秒）</summary>
        [ObservableProperty] private int _anchorTimeoutMs = 2000;
        /// <summary>导航类锚点超时（跳转/页面/输入框/确认，毫秒）</summary>
        [ObservableProperty] private int _navTimeoutMs = 5000;
        /// <summary>按下 ESC 后的画面稳定等待（毫秒）</summary>
        [ObservableProperty] private int _escDelayMs = 1000;
        /// <summary>点击设置入口后的等待（毫秒）</summary>
        [ObservableProperty] private int _clickSettingsDelayMs = 2000;
        /// <summary>粘贴后、点确认前的稳定等待（毫秒）</summary>
        [ObservableProperty] private int _pasteDelayMs = 300;
        /// <summary>点击确认后的结算等待（毫秒，对齐 BGI 5100ms 等动画播完）</summary>
        [ObservableProperty] private int _afterConfirmDelayMs = 5100;

        /// <summary>兑换码合法格式（空行/非法行直接跳过不进游戏流程）</summary>
        [ObservableProperty] private string _codeRegex = "^[A-Za-z0-9]{6,20}$";

        /// <summary>调试日志开关（输出命中的模板变体名/重试次数）</summary>
        [ObservableProperty] private bool _enableDebugLog = true;

        /// <summary>兑换成功弹窗 ROI（x1,y1,x2,y2，1080P 基准模版）；命中 → 判定成功 → ESC 关闭</summary>
        [ObservableProperty] private string _roiRedeemSuccess = "506,475,1335,933";

        /// <summary>"兑奖码已被使用"提示 ROI（x1,y1,x2,y2，1080P 基准模版）；命中 → 判失败 → 等 2s 直达下一码输入步</summary>
        [ObservableProperty] private string _roiRedeemCodeUsed = "808,486,1112,602";

        /// <summary>"兑奖码无效/已失效"提示 ROI（x1,y1,x2,y2，1080P 基准模版）；命中 → 判失败 → 等 2s 直达下一码输入步</summary>
        [ObservableProperty] private string _roiInvalidRedemptionCode = "868,518,1047,564";

        /// <summary>已使用/无效提示消散等待（毫秒）——识别到后延迟 2s 再输入下一码</summary>
        [ObservableProperty] private int _outcomeDismissDelayMs = 2000;
        [ObservableProperty] private int _placeholderTimeoutMs = 2000;


        /// <summary>WM_CHAR 逐字符注入的单字符间隔（毫秒）</summary>
        [ObservableProperty] private int _textInjectDelayMs = 15;


    }
}
