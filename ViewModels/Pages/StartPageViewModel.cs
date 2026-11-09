using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using TrustNTE.Helpers;
using TrustNTE.Models;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel : ObservableObject, ILogCollectionProvider, IDisposable
    {
        // 所有 partial 文件共享的依赖项
        private readonly AutomationService _automationService;
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly IInputService _inputService;
        private readonly INteInteractionService _nteInteractionService;
        private readonly IDialogService _dialogService;
        private readonly IWindowService _windowService;
        private readonly IDispatcherService _dispatcherService;
        private readonly IOcrService _ocrService;
        private readonly IClipboardService _clipboardService;
        private readonly IModelDownloadService _modelDownloadService;

        private readonly IShellService _shellService;

        private AppConfig _config;
        private bool _isInitializing = true;

        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private string _startButtonText = TranslationService.Instance["启动"];
        [ObservableProperty] private string _statusText = TranslationService.Instance["准备就绪，点击启动即可"];
        [ObservableProperty] private bool _startGameTogether = true;
        [ObservableProperty] private bool _isBackgroundMode = false;
        [ObservableProperty] private bool _autoClaimRewards;
        [ObservableProperty] private string _gamePath = string.Empty;
        [ObservableProperty] private string _triggerInterval = "50";

        // 定时刷日志的 Timer
        private readonly System.Threading.Timer _logFlushTimer;

        public StartPageViewModel(
            AutomationService automationService,
            IConfigService configService,
            ILogService logService,
            IInputService inputService,
            LocalApiService localApiService,
            IDialogService dialogService,
            IWindowService windowService,
            IDispatcherService dispatcherService,
            INteInteractionService nteInteractionService,
            IOcrService ocrService,
            IClipboardService clipboardService,
            IModelDownloadService modelDownloadService,
            IShellService shellService)
        {
            _automationService = automationService;
            _configService = configService;
            _logService = logService;
            _inputService = inputService;
            _dialogService = dialogService;
            _windowService = windowService;
            _dispatcherService = dispatcherService;
            _nteInteractionService = nteInteractionService;
            _ocrService = ocrService;
            _clipboardService = clipboardService;
            _modelDownloadService = modelDownloadService;
            _shellService = shellService;

            _localApiService = localApiService;

            _automationService.OnLogReceived += AppendLog;
            _automationService.OnRunningStateChanged += UpdateRunningState;
            _automationService.OnFrameCaptured += UpdateScreenSource;
            _automationService.OnWindowBound += HandleWindowBound;

            _config = _configService.Load();
            _config.PropertyChanged += OnConfigPropertyChanged;

            GamePath = _config.GamePath;
            TriggerInterval = _config.TriggerInterval;
            StartGameTogether = _config.StartGameTogether;
            IsBackgroundMode = _config.IsBackgroundMode;
            AutoClaimRewards = _config.AutoClaimRewards;
            MaskOpacity = _config.MaskOpacity;
            MaskTextOpacity = _config.MaskTextOpacity;

            // 恢复日志开关状态
            IsLogEnabled = _config.IsLogEnabled;

            if (string.IsNullOrWhiteSpace(_config.MouseDriveType) || Array.IndexOf(MouseDriveTypes, _config.MouseDriveType) < 0)
            {
                MouseDriveType = "物理点击";
                _config.MouseDriveType = "物理点击";
                SaveCurrentConfig();
            }
            else
            {
                MouseDriveType = _config.MouseDriveType;
            }

            if (string.IsNullOrWhiteSpace(GamePath))
            {
                var path = RegistryHelper.FindLauncherPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    GamePath = path;
                    _config.GamePath = path; // 同步 config 对象
                    _logService.Log($"已自动从注册表获取启动器路径: {path}");
                    SaveCurrentConfig();     // 显式保存一次，防止被防抖锁拦截丢失
                }
            }

            _logService.Log("TrustNTE 核心初始化完成。");

            InitializeCarousel();
            InitializeFpsTimer();
            InitializeApi();
            InitializeOcr();
            // 调用截图引擎的初始化
            InitializeCapture();

            // 启动后台定时器，每 200 毫秒强制检查并刷新一次日志
            _logFlushTimer = new System.Threading.Timer(_ => FlushLogs(), null, 200, 200);
            // 初始化完毕，关掉防抖锁
            _isInitializing = false;

        }

        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 统一异步派发，消除"UI 阻塞等退出时回调死锁"整类隐患
            _dispatcherService.BeginInvoke(() =>
            {
                if (GamePath != _config.GamePath) GamePath = _config.GamePath;
            });
        }

        private readonly List<string> _pendingLogs = new();
        private readonly object _pendingLock = new();

        private void AppendLog(string message)
        {
            // 只管接收，立刻返回。不占用后台线程时间，也不会卡队列
            lock (_pendingLock) _pendingLogs.Add(message);
        }

        // 由定时器驱动，主动去拉取积压的日志并推送到 UI
        private void FlushLogs()
        {
            string[] batch;
            lock (_pendingLock)
            {
                if (_pendingLogs.Count == 0) return;
                batch = _pendingLogs.ToArray();
                _pendingLogs.Clear();
            }

            _dispatcherService.BeginInvoke(() =>
            {
                foreach (var line in batch)
                {
                    Logs.Add(line);
                    if (Logs.Count > 200) Logs.RemoveAt(0);
                    _windowService.SendLogToWindows(line);
                }
            });
        }

        private void UpdateRunningState(bool isRunning)
        {
            // [改·退出生死线] Invoke → BeginInvoke：
            // 程序退出时 UI 线程阻塞在 autoService.WaitForExit() 上，
            // 引擎 finally 会触发本回调；若是同步 Invoke，回调在 UI 队列里
            // 等 UI 空闲、UI 在等 WaitForExit —— 互相等，白卡到 3 秒超时。
            // BeginInvoke 排队即返回，不阻塞引擎收尾线程。
            _dispatcherService.BeginInvoke(() =>
            {
                IsRunning = isRunning;
                StartButtonText = isRunning ? TranslationService.Instance["停止"] : TranslationService.Instance["启动"];
                StatusText = isRunning ? TranslationService.Instance["自动化运行中..."] : TranslationService.Instance["已停止"];
                OnPropertyChanged(nameof(PreviewPlaceholderVisibility));
            });

            if (!isRunning)
            {
                // 停止时强制把队列里最后残留的日志全部推送到 UI
                _dispatcherService.BeginInvoke(() =>
                {
                    lock (_pendingLock)
                    {
                        if (_pendingLogs.Count > 0)
                        {
                            foreach (var line in _pendingLogs)
                            {
                                Logs.Add(line);
                                if (Logs.Count > 200) Logs.RemoveAt(0);
                                _windowService.SendLogToWindows(line);
                            }
                            _pendingLogs.Clear();
                        }
                    }
                });

                // BeginInvoke：CloseAllWindows 也在退出路径上，同样不能同步等 UI
                _dispatcherService.BeginInvoke(() =>
                {
                    ScreenSource = null;
                    _frameCount = 0;
                    FpsText = "0 FPS";
                    CloseAllWindows();
                });
            }
        }

        private void HandleWindowBound()
        {
            // BeginInvoke：与全局统一异步派发
            _dispatcherService.BeginInvoke(() =>
            {
                if (IsRunning && !_isMaskShowing)
                {
                    ToggleMask();
                }
            });
        }

        private void SaveCurrentConfig()
        {
            var config = _configService.Load();
            config.GamePath = this.GamePath;
            config.TriggerInterval = this.TriggerInterval;
            config.StartGameTogether = this.StartGameTogether;
            config.IsBackgroundMode = this.IsBackgroundMode;
            config.AutoClaimRewards = this.AutoClaimRewards;
            config.MaskOpacity = this.MaskOpacity;
            config.MaskTextOpacity = this.MaskTextOpacity;
            config.EnableLocalApi = this.IsApiEnabled;
            config.MouseDriveType = this.MouseDriveType;
            config.CaptureMode = this.SelectedCaptureMode;
                        // 把日志开关保存到配置文件
            config.IsLogEnabled = this.IsLogEnabled;

            _configService.Save(config);
        }

        // if (!_isInitializing) 守卫
        partial void OnGamePathChanged(string value) { if (!_isInitializing) SaveCurrentConfig(); }
        partial void OnTriggerIntervalChanged(string value) { if (!_isInitializing) SaveCurrentConfig(); }
        partial void OnStartGameTogetherChanged(bool value) { if (!_isInitializing) SaveCurrentConfig(); }
        partial void OnIsBackgroundModeChanged(bool value) { if (!_isInitializing) SaveCurrentConfig(); }
        partial void OnAutoClaimRewardsChanged(bool value) { if (!_isInitializing) SaveCurrentConfig(); }
        partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(PreviewPlaceholderVisibility));

        public void Dispose()
        {
            _automationService.OnLogReceived -= AppendLog;
            _automationService.OnRunningStateChanged -= UpdateRunningState;
            _automationService.OnFrameCaptured -= UpdateScreenSource;
            _automationService.OnWindowBound -= HandleWindowBound;
            _config.PropertyChanged -= OnConfigPropertyChanged;

            // 销毁定时器
            _logFlushTimer?.Dispose();

            DisposeCapture();
            DisposeCarousel();
        }
    }
}