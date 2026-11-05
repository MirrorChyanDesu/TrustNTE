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

        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private string _startButtonText = TranslationService.Instance["启动"];
        [ObservableProperty] private string _statusText = TranslationService.Instance["准备就绪，点击启动即可"];
        [ObservableProperty] private bool _startGameTogether = true;
        [ObservableProperty] private bool _isBackgroundMode = false;
        [ObservableProperty] private bool _autoClaimRewards;
        [ObservableProperty] private string _gamePath = string.Empty;
        [ObservableProperty] private string _triggerInterval = "50";

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
                    _logService.Log($"已自动从注册表获取启动器路径: {path}");
                }
            }

            _logService.Log("TrustNTE 核心初始化完成。");

            InitializeCarousel();
            InitializeFpsTimer();
            InitializeApi();
            InitializeOcr();
        }

        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppConfig.GamePath))
            {
                _dispatcherService.Invoke(() =>
                {
                    if (GamePath != _config.GamePath) GamePath = _config.GamePath;
                });
            }
        }

        private readonly List<string> _pendingLogs = new();
        private readonly object _pendingLock = new();
        private DateTime _lastLogFlush = DateTime.MinValue;

        private void AppendLog(string message)
        {
            lock (_pendingLock) _pendingLogs.Add(message);

            // UI 每 200ms 合并刷新一次 如果一条条刷新会导致UI卡顿影响极大
            if ((DateTime.UtcNow - _lastLogFlush).TotalMilliseconds < 200) return;
            _lastLogFlush = DateTime.UtcNow;

            _dispatcherService.BeginInvoke(() =>
            {
                string[] batch;
                lock (_pendingLock)
                {
                    batch = _pendingLogs.ToArray();
                    _pendingLogs.Clear();
                }

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
            _dispatcherService.Invoke(() =>
            {
                IsRunning = isRunning;
                StartButtonText = isRunning ? TranslationService.Instance["停止"] : TranslationService.Instance["启动"];
                StatusText = isRunning ? TranslationService.Instance["自动化运行中..."] : TranslationService.Instance["已停止"];
                OnPropertyChanged(nameof(PreviewPlaceholderVisibility));
            });

            if (!isRunning)
            {
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

                _dispatcherService.Invoke(() =>
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
            _dispatcherService.Invoke(() =>
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

            _configService.Save(config);
        }

        partial void OnGamePathChanged(string value) => SaveCurrentConfig();
        partial void OnTriggerIntervalChanged(string value) => SaveCurrentConfig();
        partial void OnStartGameTogetherChanged(bool value) => SaveCurrentConfig();
        partial void OnIsBackgroundModeChanged(bool value) => SaveCurrentConfig();
        partial void OnAutoClaimRewardsChanged(bool value) => SaveCurrentConfig();
        partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(PreviewPlaceholderVisibility));

        public void Dispose()
        {
            _automationService.OnLogReceived -= AppendLog;
            _automationService.OnRunningStateChanged -= UpdateRunningState;
            _automationService.OnFrameCaptured -= UpdateScreenSource;
            _automationService.OnWindowBound -= HandleWindowBound;
            _config.PropertyChanged -= OnConfigPropertyChanged;
            DisposeCapture();
            DisposeCarousel();
        }
    }
}