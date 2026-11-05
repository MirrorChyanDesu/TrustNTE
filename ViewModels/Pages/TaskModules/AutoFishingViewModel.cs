using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services;
using TrustNTE.Services.Fishing;
using TrustNTE.Services.Interfaces;
using TrustNTE.Helpers;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoFishingViewModel : TaskModuleViewModelBase
    {
        private readonly AutomationService _automationService;
        private readonly IWindowService _windowService;
        private readonly IDialogService _dialogService;
        private readonly FishingBarController _barController;
        private readonly AutoFishingPreviewRenderer _previewRenderer;
        private readonly FishingTaskEngine _engine;
        private AutoFishingPreviewViewModel? _previewVm;

        public AutoFishingConfig Config { get; }

        public int TotalFishCount => Config.SCount + Config.ACount + Config.BCount;

        public AutoFishingViewModel(
            IConfigService configService,
            ILogService logService,
            AutomationService automationService,
            IInputRouter router,
            FishingTemplateService templateService,
            IWindowService windowService,
            IDialogService dialogService,
            FishingBarController barController,
            AutoFishingPreviewRenderer previewRenderer)
            : base(logService, configService, "自动钓鱼")
        {
            _automationService = automationService;
            _windowService = windowService;
            _dialogService = dialogService;
            _barController = barController;
            _previewRenderer = previewRenderer;

            // [红线] 引擎与 UI 绑定共用同一 Config 实例（鱼获统计的持久化依赖此链路）
            Config = BindConfig(c => c.Fishing, (main, cfg) => main.Fishing = cfg);

            var recognizer = new FishingSceneRecognizer(templateService);
            var stateMachine = new FishingStateMachine(
                Config, router, logService, templateService, barController, automationService, recognizer);
            var roiAnalyzer = new FishingRoiAnalyzer();
            _engine = new FishingTaskEngine(automationService, recognizer, stateMachine, barController,
                logService, Config, roiAnalyzer, previewRenderer);
        }

        // [Phase 1] 持久化由基类接管，这里只保留鱼数联动刷新
        protected override void OnConfigChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AutoFishingConfig.SCount)
                || e.PropertyName == nameof(AutoFishingConfig.ACount)
                || e.PropertyName == nameof(AutoFishingConfig.BCount))
            {
                OnPropertyChanged(nameof(TotalFishCount));
            }
        }

        [RelayCommand]
        private void ClearStats()
        {
            Config.SCount = 0;
            Config.ACount = 0;
            Config.BCount = 0;
            OnPropertyChanged(nameof(TotalFishCount));
            _logService.LogInfo("[自动钓鱼] 已清除统计数据");
        }

        [RelayCommand]
        private async Task CancelShutdownAsync()
        {
            try
            {
                var psi = new ProcessStartInfo("shutdown", "/a")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    if (proc.ExitCode == 0)
                    {
                        _logService.LogInfo("[自动钓鱼] 已成功取消关机任务");
                        await _dialogService.ShowInfoAsync("取消关机", "已成功取消即将进行的关机任务！");
                    }
                    else
                    {
                        _logService.LogInfo("[自动钓鱼] 未发现正在进行的关机任务");
                        await _dialogService.ShowInfoAsync("提示", "当前没有关机任务！");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"[自动钓鱼] 取消关机失败: {ex.Message}");
                await _dialogService.ShowInfoAsync("错误", $"取消失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private void ResetRoi()
        {
            Config.ControlRoiStr = "605,61,1322,88";
            _logService.LogInfo("[自动钓鱼] ROI 区域已重置为默认值 (605,61,1322,88)");
        }

        [RelayCommand]
        private void OpenPreviewWindow()
        {
            if (_previewVm == null)
            {
                _previewVm = new AutoFishingPreviewViewModel((roiStr) =>
                {
                    Config.ControlRoiStr = roiStr;
                    _logService.LogInfo($"[自动钓鱼] 玩家手动框选，ROI 已更新为: {roiStr}");
                });
            }
            _windowService.ShowAutoFishingPreviewWindow(_previewVm);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            if (_automationService.CurrentHwnd == IntPtr.Zero)
            {
                _logService.LogError("无法启动自动钓鱼：截图引擎未启动或未获取到游戏窗口。");
                _windowService.ShowStartErrorWindow();
                return;
            }

            await _engine.RunAsync(
                token,
                () => _previewVm,
                onFishCountChanged: (_) => OnPropertyChanged(nameof(TotalFishCount)),
                onExit: () =>
                {
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                        () => _windowService.CloseAutoFishingPreviewWindow());
                });
        }

        public override void Dispose()
        {
            // [Phase 1] 配置解绑已由 base.Dispose() 统一处理
            _engine.RequestStop();
            _barController.Release();
            _previewRenderer.Reset();
            if (_previewVm != null)
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { _previewVm.PreviewImage = null; } catch { }
                }));
            }
            base.Dispose();
        }
    }
}
