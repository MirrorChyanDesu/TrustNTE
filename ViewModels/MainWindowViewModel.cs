using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models;
using TrustNTE.Models.Update;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels
{
    /// <summary>
    /// 主窗口的 ViewModel。
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly IRedeemCodeService _redeemService;
        private readonly IConfigService _configService;
        private readonly IRedeemWindowService _redeemWindowService;
        private readonly IDispatcherService _dispatcherService;
        private readonly IDialogService _dialogService;
        private readonly UpdateService _updateService;
        private readonly IWindowService _windowService;

        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// 全局取消令牌，供启动任务和运行期异步操作使用。
        /// </summary>
        public CancellationToken Token => _cts.Token;

        private CancellationTokenSource? _infoBarCts;
        private bool _isDisposed = false;
        private RedeemCodeFeed? _latestFeed;

        /// <summary>
        /// 兑换码 InfoBar 是否打开。
        /// </summary>
        [ObservableProperty]
        private bool _isRedeemInfoBarOpen;

        /// <summary>
        /// 兑换码 InfoBar 的提示文本。
        /// </summary>
        [ObservableProperty]
        private string _redeemInfoBarMessage = "发现最新的游戏兑换码查看！";

        /// <summary>
        /// 初始化 <see cref="MainWindowViewModel"/> 实例。
        /// </summary>
        /// <param name="redeemService">兑换码服务。</param>
        /// <param name="configService">配置服务。</param>
        /// <param name="redeemWindowService">兑换码窗口服务。</param>
        /// <param name="dispatcherService">调度器服务。</param>
        /// <param name="dialogService">对话框服务。</param>
        /// <param name="updateService">更新服务。</param>
        /// <param name="windowService">窗口服务，用于显示更新提示窗口。</param>
        // [模块E]    改原因：构造函数新增 UpdateService / IWindowService 依赖，
        // 将原 MainWindow.xaml.cs 里的服务定位器和更新检查逻辑迁入 ViewModel；
        // IRedeemWindowService 参数名由 windowService 改为 redeemWindowService，避免与新 IWindowService 参数名冲突
        public MainWindowViewModel(
            IRedeemCodeService redeemService,
            IConfigService configService,
            IRedeemWindowService redeemWindowService,
            IDispatcherService dispatcherService,
            IDialogService dialogService,
            UpdateService updateService,
            IWindowService windowService)
        {
            _redeemService = redeemService;
            _configService = configService;
            _redeemWindowService = redeemWindowService;
            _dispatcherService = dispatcherService;
            _dialogService = dialogService;
            _updateService = updateService;
            _windowService = windowService;
        }

        /// <summary>
        /// 启动后延迟执行初始化任务。包含兑换码检查与版本更新检查。
        /// </summary>
        // [模块E]    改原因：将原 MainWindow.xaml.cs 中 MainWindow_Loaded 里的 Task.Delay / InitializeAsync /
        // CheckUpdateAsync 逻辑迁入 ViewModel，使 View 层不再包含业务逻辑
        public async Task RunStartupTasksAsync()
        {
            if (_isDisposed) return;

            try
            {
                await Task.Delay(3000, _cts.Token);
                await InitializeAsync();
                await CheckUpdateOnStartupAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动任务异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化兑换码检查。
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isDisposed) return;

            try
            {
                var feed = await _redeemService.GetLatestFeedAsync(_cts.Token);
                if (feed == null || feed.Items.Count == 0 || string.IsNullOrWhiteSpace(feed.Version)) return;

                var config = _configService.Load();

                if (feed.Version != config.RedeemCodeVersion)
                {
                    _dispatcherService.BeginInvoke(() =>
                    {
                        _latestFeed = feed;
                        IsRedeemInfoBarOpen = true;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查兑换码异常: {ex.Message}");
            }
        }

        private async Task CheckUpdateOnStartupAsync()
        {
            if (_isDisposed) return;

            try
            {
                var result = await _updateService.CheckUpdateAsync(
                    new UpdateOption { Trigger = UpdateTrigger.Auto, Channel = UpdateChannel.Stable });
                if (result == null || !result.NeedPrompt) return;

                var vm = new CheckUpdateWindowViewModel(result, _updateService, _configService, _dialogService);
                _windowService.ShowUpdateWindow(vm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动检查更新异常: {ex.Message}");
            }
        }

        partial void OnIsRedeemInfoBarOpenChanged(bool value)
        {
            if (_isDisposed) return;

            _infoBarCts?.Cancel();
            _infoBarCts?.Dispose();
            _infoBarCts = null;

            if (value)
            {
                _infoBarCts = new CancellationTokenSource();
                var token = _infoBarCts.Token;

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(30000, token);
                        _dispatcherService.BeginInvoke(() => IsRedeemInfoBarOpen = false);
                    }
                    catch (OperationCanceledException) { }
                    catch (ObjectDisposedException) { }
                }, token);
            }
        }

        /// <summary>
        /// 打开兑换码窗口并记录已查看版本。
        /// </summary>
        [RelayCommand]
        private void OpenRedeemWindow()
        {
            if (_latestFeed == null) return;

            var version = _latestFeed.Version;
            _configService.Update(c => c.RedeemCodeVersion = version);

            IsRedeemInfoBarOpen = false;
            _redeemWindowService.ShowRedeemWindow(_latestFeed);

            _latestFeed = null;
        }

        /// <summary>
        /// 手动打开兑换码窗口，主动拉取最新兑换码。
        /// </summary>
        [RelayCommand]
        private async Task ManualOpenRedeemWindowAsync()
        {
            if (_isDisposed) return;

            try
            {
                var feed = await _redeemService.GetLatestFeedAsync(_cts.Token);
                if (feed != null && feed.Items.Count > 0)
                {
                    _redeemWindowService.ShowRedeemWindow(feed);
                }
                else
                {
                    await _dialogService.ShowInfoAsync("未能获取到兑换码", "请检查网络连接。");
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowInfoAsync("获取失败", $"发生异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源并取消未完成的异步任务。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();

                _infoBarCts?.Cancel();
                _infoBarCts?.Dispose();
            }
            catch (ObjectDisposedException) { }
        }
    }
}