using CommunityToolkit.Mvvm.ComponentModel;
using TrustNTE.Models.TaskConfigs;
//using TrustNTE.ViewModels.Pages.TaskModules;
using System.Collections.Generic;

namespace TrustNTE.Models
{
    public partial class AppConfig : ObservableObject
    {
        // ==========================================
        // 独立任务模块
        // ==========================================
        public AutoFishingConfig Fishing { get; set; } = new();
        public AutoMusicConfig Music { get; set; } = new();
        public AutoPawConfig Paw { get; set; } = new();
        public AutoApartmentConfig Apartment { get; set; } = new();
        public AutoFountainConfig Fountain { get; set; } = new();
        public AutoMarketConfig Market { get; set; } = new();
        public AutoGiftConfig Gift { get; set; } = new();
        public AutoVolleyballHeartConfig VolleyballHeart { get; set; } = new();
        public AutoHuntConfig Hunt { get; set; } = new();
        public AutoPilgrimageConfig Pilgrimage { get; set; } = new();
        public AutoCombatConfig Combat { get; set; } = new();
        public AutoRedeemConfig AutoRedeem { get; set; } = new();

        // ==========================================
        // 启动页配置
        // ==========================================
        [ObservableProperty] private string _gamePath = string.Empty;
        [ObservableProperty] private string _captureMode = "① PrintWindow";
        [ObservableProperty] private string _triggerInterval = "50";
        [ObservableProperty] private bool _startGameTogether = true;
        [ObservableProperty] private bool _isBackgroundMode = false;
        [ObservableProperty] private double _maskOpacity = 0.1;
        [ObservableProperty] private double _maskTextOpacity = 1.0;
        [ObservableProperty] private bool _autoClaimRewards = false;
        [ObservableProperty] private bool _enableLocalApi = true;

        // ===== 粉爪银行拾取 =====
        [ObservableProperty] private bool _pinkPawPickupEnabled = false;
        [ObservableProperty] private int _pinkPawFPressCount = 3;
        [ObservableProperty] private int _pinkPawFPressIntervalMs = 500;
        [ObservableProperty] private string _pinkPawScanRoi = "1122,535,1511,681";
        [ObservableProperty] private string _pinkPawDetectMode = "颜色";
        [ObservableProperty] private bool _pinkPawIncludeDialogue = false;
        [ObservableProperty] private string _mouseDriveType = "通用点击";

        // ==========================================
        // 快捷键配置
        // ==========================================
        public HotKeyConfig HotKeyConfig { get; set; } = new();

        // ==========================================
        // 设置页配置
        // ==========================================
        [ObservableProperty] private bool _isAutoStartEnabled = false;      // 原版总开关
        [ObservableProperty] private bool _autoStartEngineOnLogin = false;  //     自启后是否自动挂机
        [ObservableProperty] private string _appTheme = "深色";
        [ObservableProperty] private string _uiLanguage = "简体中文";
        [ObservableProperty] private string _gameLanguage = "简体中文";

        // ==========================================
        // 自动更新模块配置
        // ==========================================
        [ObservableProperty] private string _notShowNewVersionNoticeEndVersion = string.Empty;

        // ==========================================
        // 兑换码模块
        // ==========================================
        public string RedeemCodeVersion { get; set; } = string.Empty;
        public bool RedeemCodeNotificationEnabled { get; set; } = true;

        // ==========================================
        // 辅助操控模块
        // ==========================================
        [ObservableProperty] private bool _autoDriftEnabled = false;

        // ==========================================
        // 快捷键配置
        // ==========================================
        public Dictionary<string, HotKeyConfigItem> HotKeySettings { get; set; } = new();

        // ==========================================
        // 实时触发模块配置
        // ==========================================
        [ObservableProperty] private bool _autoLootEnabled = true;
        [ObservableProperty] private string _autoLootEngine = "Paddle";
        [ObservableProperty] private string _autoLootKey = "F";
        [ObservableProperty] private string _autoLootListMode = "白名单模式";
        [ObservableProperty] private string _autoLootWhitelistText = "";
        [ObservableProperty] private string _autoLootBlacklistText = "";
        [ObservableProperty] private bool _autoDialogueEnabled = true;
		[ObservableProperty] private bool _autoTeleportEnabled = true;//自动传送开关
		[ObservableProperty] private bool _autoDialogueAutoOption = true;
        [ObservableProperty] private string _autoDialogueOptionStrategy = "第一个";
        [ObservableProperty] private string _autoStoryLogMode = "双线输出";
        [ObservableProperty] private bool _dailyRewardEnabled = true;
        [ObservableProperty] private bool _yikacheEnabled = true;
        [ObservableProperty] private bool _autoRestoreFocus = true;
        [ObservableProperty] private bool _autoCombatEnabled = false;
        [ObservableProperty] private bool _autoCombatUseUltimate = true;

        [ObservableProperty] private bool _autoCombatTargeting = true;

        [ObservableProperty] private double _autoCombatConfidence = 0.25;
        [ObservableProperty] private bool _isCombatDebugWindowOpen = false;

        // ===== 月卡/环石自动领取 =====
        [ObservableProperty] private bool _monthlyCardEnabled = false; // 总开关
        [ObservableProperty] private bool _monthlyCardAllDay = true; // true=全天, false=指定时间
        [ObservableProperty] private string _monthlyCardStartTime = "03:50";
        [ObservableProperty] private string _monthlyCardEndTime = "04:10";
        [ObservableProperty] private bool _monthlyCardUseLocalTime = true; // true=本地时间, false=北京时间
        public string LastMonthlyCardClaimDate { get; set; } = string.Empty; // 记录上次领取的日期，防重

        // ===== 游戏守护（Supervisor）=====
        [ObservableProperty] private bool _autoUpdateGame = true;
        [ObservableProperty] private bool _autoReconnectEnabled = true;
        [ObservableProperty] private bool _autoRestartOnCrash = true;
        [ObservableProperty] private bool _autoResumeTask = true;
        [ObservableProperty] private bool _idleGuardEnabled = true;

        // 遮罩位置 (1=左下 2=左上 3=右下 4=右上)
        [ObservableProperty] private int _maskPosition = 0;

        // 日志自定义颜色 (Hex格式)
        [ObservableProperty] private string _logColorDefault = "#00FFFF";
        [ObservableProperty] private string _logColorError = "#FF5252";
        [ObservableProperty] private string _logColorGuard = "#FFA940";
        [ObservableProperty] private string _logColorHighFreq = "#7A828E";
        [ObservableProperty] private string _logColorMouse = "#FF00FF";
        [ObservableProperty] private string _logColorKeyboard = "#00FF00";
    }
}