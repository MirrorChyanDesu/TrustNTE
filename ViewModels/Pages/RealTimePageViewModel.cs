using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TrustNTE.Models;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;
using TrustNTE.ViewModels;

namespace TrustNTE.ViewModels.Pages
{
    public partial class RealTimePageViewModel : ObservableObject, IDisposable
    {
        // ====== 依赖注入字段 ======
        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;
        private readonly IWindowService _windowService;
        private readonly AutomationService _automationService;
        private readonly IMapMatchService _mapMatchService;

        public AppConfig Config { get; }
        public string[] ListModes { get; } = { "白名单模式", "黑名单模式" };
        public string[] OptionStrategies { get; } = { "第一个", "第二个" };
        public string[] LogOutputModes { get; } = { "单项输出", "双线输出" };

        private CombatDebugWindowViewModel? _combatDebugVm;

        // ========= 地图匹配模块属性 =========
        [ObservableProperty] private bool _isMapMatchingEnabled;
        [ObservableProperty] private string _mapMatchStatus = "等待启动";
        [ObservableProperty] private string _mapMatchCoordinates = "X: -, Y: -";

        private CancellationTokenSource? _mapMatchCts;
        private int _isMatching = 0;
        private MapMatchDebugWindowViewModel? _mapMatchDebugVm;

        // =========        月卡 UI 辅助属性 =========
        public bool IsMonthlyCardCustomTime
        {
            get => !Config.MonthlyCardAllDay;
            set
            {
                Config.MonthlyCardAllDay = !value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTimeSettingVisible));
            }
        }

        public Visibility IsTimeSettingVisible => Config.MonthlyCardAllDay ? Visibility.Collapsed : Visibility.Visible;

        //        下拉框绑定索引（0=本地，1=北京时间）
        public int MonthlyCardTimeZoneIndex
        {
            get => Config.MonthlyCardUseLocalTime ? 0 : 1;
            set
            {
                Config.MonthlyCardUseLocalTime = (value == 0);
                OnPropertyChanged();
            }
        }

        public RealTimePageViewModel(
            IConfigService configService,
            IDialogService dialogService,
            IWindowService windowService,
            AutomationService automationService,
            IMapMatchService mapMatchService)
        {
            _configService = configService;
            _dialogService = dialogService;
            _windowService = windowService;
            _automationService = automationService;
            _mapMatchService = mapMatchService;

            Config = _configService.Load();
            Config.PropertyChanged += OnConfigChanged;

            _automationService.OnRunningStateChanged += OnAutomationRunningStateChanged;
        }

        private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            _configService.Save(Config);

            // 监听月卡模式切换，刷新 UI 显隐
            if (e.PropertyName == nameof(AppConfig.MonthlyCardAllDay))
            {
                OnPropertyChanged(nameof(IsMonthlyCardCustomTime));
                OnPropertyChanged(nameof(IsTimeSettingVisible));
            }

            // 原有：战斗调试窗口控制
            if (e.PropertyName == nameof(AppConfig.IsCombatDebugWindowOpen))
            {
                if (Config.IsCombatDebugWindowOpen)
                {
                    if (_automationService.CurrentHwnd == IntPtr.Zero)
                    {
                        Config.IsCombatDebugWindowOpen = false;
                        _windowService.ShowStartErrorWindow();
                        return;
                    }

                    if (_combatDebugVm == null)
                    {
                        _combatDebugVm = new CombatDebugWindowViewModel();
                        _combatDebugVm.WindowClosed += () =>
                        {
                            Config.IsCombatDebugWindowOpen = false;
                            _combatDebugVm = null;
                        };
                    }
                    _windowService.ShowCombatDebugWindow(_combatDebugVm);
                }
                else
                {
                    _windowService.CloseCombatDebugWindow();
                    _combatDebugVm = null;
                }
            }
        }

        private void OnAutomationRunningStateChanged(bool isRunning)
        {
            if (!isRunning && Config.IsCombatDebugWindowOpen)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    Config.IsCombatDebugWindowOpen = false;
                });
            }

            if (!isRunning && IsMapMatchingEnabled)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsMapMatchingEnabled = false;
                });
            }
        }

        // ========= 地图匹配开关逻辑 =========
        partial void OnIsMapMatchingEnabledChanged(bool value)
        {
            if (value)
            {
                if (_automationService.CurrentHwnd == IntPtr.Zero)
                {
                    MapMatchStatus = "失败: 请先开启截图器";
                    IsMapMatchingEnabled = false;
                    return;
                }

                MapMatchStatus = "正在匹配...";
                _mapMatchCts = new CancellationTokenSource();
                _ = MapMatchLoopAsync(_mapMatchCts.Token);
            }
            else
            {
                _mapMatchCts?.Cancel();
                _mapMatchCts?.Dispose();
                _mapMatchCts = null;
                MapMatchStatus = "已停止";
                MapMatchCoordinates = "X: -, Y: -";
                _mapMatchDebugVm?.ClearMarker();
            }
        }

        [RelayCommand]
        private void OpenMapMatchDebug()
        {
            if (_mapMatchDebugVm == null)
            {
                _mapMatchDebugVm = new MapMatchDebugWindowViewModel();
            }
            _windowService.ShowMapMatchDebugWindow(_mapMatchDebugVm);
        }

        private async Task MapMatchLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Interlocked.Exchange(ref _isMatching, 1) == 0)
                {
                    try
                    {
                        using var frame = _automationService.GetLatestFrame();
                        if (frame != null && !frame.Empty())
                        {
                            var result = _mapMatchService.MatchMiniMapToBigMap(frame, 157, 136, 90);

                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                if (result.HasValue)
                                {
                                    MapMatchStatus = "匹配成功";
                                    MapMatchCoordinates = $"X: {result.Value.X}, Y: {result.Value.Y}";
                                    _mapMatchDebugVm?.UpdateMarker(result.Value.X, result.Value.Y);
                                }
                                else
                                {
                                    MapMatchStatus = "特征不足/匹配失败";
                                    _mapMatchDebugVm?.ClearMarker();
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"地图匹配异常: {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isMatching, 0);
                    }
                }

                try
                {
                    await Task.Delay(300, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        [RelayCommand]
        private void OpenListConfig()
        {
            var vm = new ListConfigDialogViewModel(
                Config.AutoLootListMode,
                Config.AutoLootWhitelistText,
                Config.AutoLootBlacklistText,
                _dialogService);

            var result = _windowService.ShowListConfigDialog(vm);
            if (result == true)
            {
                Config.AutoLootWhitelistText = vm.FinalWhiteText;
                Config.AutoLootBlacklistText = vm.FinalBlackText;
                _configService.Save(Config);
            }
        }

        [RelayCommand]
        private void ShowMonthlyCardDocAsync()
        {
            TrustNTE.Views.Windows.DocViewerWindow.ShowDoc("月卡功能说明", "monthly_card.md");
        }

        public void Dispose()
        {
            Config.PropertyChanged -= OnConfigChanged;
            _automationService.OnRunningStateChanged -= OnAutomationRunningStateChanged;

            _mapMatchCts?.Cancel();
            _mapMatchCts?.Dispose();

            _windowService.CloseMapMatchDebugWindow();

            if (Config.IsCombatDebugWindowOpen)
            {
                Config.IsCombatDebugWindowOpen = false;
            }
        }
    }
}