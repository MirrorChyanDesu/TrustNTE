using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrustNTE.Helpers;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel
    {
        [ObservableProperty] private string _fpsText = "0 FPS";
        private int _frameCount = 0;
        private DispatcherTimer? _fpsTimer;

        public System.Collections.ObjectModel.ObservableCollection<string> CaptureModes { get; } = new() { "① PrintWindow", "② WGC", "③ DXGI", "④ BitBlt", "⑤ DWM" };

        [ObservableProperty] private string _selectedCaptureMode = "① PrintWindow";

        [ObservableProperty] private bool _showPreviewWindow;
        [ObservableProperty] private BitmapSource? _screenSource;
        [ObservableProperty] private string _previewButtonText = TranslationService.Instance["打开预览"];

        public Visibility PreviewPlaceholderVisibility => (!IsRunning || ScreenSource == null) ? Visibility.Visible : Visibility.Collapsed;

        private bool _isMaskShowing = false;
        private DispatcherTimer? _maskTimer;
        [ObservableProperty] private string _maskBtnText = TranslationService.Instance["打开日志遮罩"];

        private bool _isLogWindowOpen = false;

        [ObservableProperty] private bool _isZeroDelayMode = true;
        private int _isRendering = 0;

        private void InitializeCapture()
        {

            // 从配置中恢复上次选择的截图引擎
            if (!string.IsNullOrWhiteSpace(_config.CaptureMode))
            {
                SelectedCaptureMode = _config.CaptureMode;
            }
            // 旧配置名兼容迁移（极其重要，防止老用户配置静默变空）
            if (_config.MouseDriveType == "通用点击")
                _config.MouseDriveType = "通用点击";
        }

        partial void OnSelectedCaptureModeChanged(string value)
        {
            _config.CaptureMode = value;
            SaveCurrentConfig();
        }

        private void InitializeFpsTimer()
        {
            _fpsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _fpsTimer.Tick += (s, e) =>
            {
                if (IsRunning) { FpsText = $"{_frameCount} FPS"; _frameCount = 0; }
                else { FpsText = "0 FPS"; }
            };
            _fpsTimer.Start();
        }

        private void DisposeCapture()
        {
            _fpsTimer?.Stop();
            _maskTimer?.Stop();
            _windowService.CloseLogWindow();
            _windowService.ClosePreviewWindow();
            _windowService.HideOverlayWindow();
        }

        private void UpdateScreenSource(BitmapSource bmp)
        {
            _frameCount++;
            if (!ShowPreviewWindow) return;
            if (System.Windows.Application.Current == null || System.Windows.Application.Current.Dispatcher.HasShutdownStarted) return;

            if (IsZeroDelayMode)
            {
                if (Interlocked.Exchange(ref _isRendering, 1) == 1) return;
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    try
                    {
                        if (IsRunning && ShowPreviewWindow)
                        {
                            ScreenSource = bmp;
                            StatusText = $"抓图中: {bmp.PixelWidth}x{bmp.PixelHeight}";
                            OnPropertyChanged(nameof(PreviewPlaceholderVisibility));
                        }
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception) { }
                    finally { Interlocked.Exchange(ref _isRendering, 0); }
                }));
            }
            else
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (IsRunning && ShowPreviewWindow)
                        {
                            ScreenSource = bmp;
                            StatusText = $"抓图中: {bmp.PixelWidth}x{bmp.PixelHeight}";
                            OnPropertyChanged(nameof(PreviewPlaceholderVisibility));
                        }
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception) { }
                }));
            }
        }

        [RelayCommand]
        private void ToggleCapture()
        {
            if (IsRunning) _automationService.Stop();
            else { SaveCurrentConfig(); _automationService.Start(); }
        }

        [RelayCommand]
        private void OpenLogWindow()
        {
            if (!_isLogWindowOpen)
            {
                _windowService.ShowLogWindow(Logs, () =>
                {
                    _isLogWindowOpen = false;
                    LogWindowBtnText = TranslationService.Instance["打开日志"];
                });
                _isLogWindowOpen = true;
                LogWindowBtnText = TranslationService.Instance["关闭日志"];
            }
            else
            {
                _windowService.CloseLogWindow();
                _isLogWindowOpen = false;
                LogWindowBtnText = TranslationService.Instance["打开日志"];
            }
        }

        partial void OnShowPreviewWindowChanged(bool value)
        {
            _automationService.IsPreviewWindowOpen = value;
            if (value)
            {
                PreviewButtonText = TranslationService.Instance["关闭预览"];
                _windowService.ShowPreviewWindow(this, () => ShowPreviewWindow = false);
            }
            else
            {
                PreviewButtonText = TranslationService.Instance["打开预览"];
                _windowService.ClosePreviewWindow();
                ScreenSource = null;
                OnPropertyChanged(nameof(PreviewPlaceholderVisibility));
            }
        }

        [RelayCommand] private void TogglePreviewWindow() => ShowPreviewWindow = !ShowPreviewWindow;

        private void CloseAllWindows()
        {
            if (_isMaskShowing) ToggleMask();
            if (ShowPreviewWindow) ShowPreviewWindow = false;
        }

        [RelayCommand]
        private void ToggleMask()
        {
            if (!_isMaskShowing)
            {
                if (_automationService.CurrentHwnd == IntPtr.Zero) { _logService.LogWarning("游戏窗口还未绑定，请稍后手动打开遮罩。"); return; }
                _isMaskShowing = true;
                MaskBtnText = TranslationService.Instance["关闭日志遮罩"];
                _windowService.ShowOverlayWindow(this);
                _windowService.SetOverlayBackgroundOpacity(MaskOpacity);
                _windowService.SetOverlayTextOpacity(MaskTextOpacity);
                StartMaskTimer();
                _logService.Log("已开启游戏内日志遮罩");
            }
            else
            {
                _isMaskShowing = false;
                MaskBtnText = TranslationService.Instance["打开日志遮罩"];
                _maskTimer?.Stop();
                _maskTimer = null;
                _windowService.HideOverlayWindow();
                _logService.Log("已关闭游戏内日志遮罩。");
            }
        }

        private void StartMaskTimer()
        {
            _maskTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _maskTimer.Tick += (s, e) =>
            {
                if (!_isMaskShowing) return;
                IntPtr hwnd = _automationService.CurrentHwnd;
                if (hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out NativeMethods.Rect rect))
                {
                    double maskWidth = 450; double maskHeight = 200; double margin = 10;
                    double x = 0, y = 0;
                    switch (_config.MaskPosition)
                    {
                        case 0: x = rect.Left + margin; y = rect.Bottom - maskHeight - 30; break;
                        case 1: x = rect.Right - maskWidth - margin; y = rect.Bottom - maskHeight - 30; break;
                        default: x = rect.Left + margin; y = rect.Bottom - maskHeight - 30; break;
                    }
                    _windowService.UpdateOverlayPosition((int)x, (int)y, (int)maskWidth, (int)maskHeight);
                    _windowService.SetOverlayOpacity((NativeMethods.GetForegroundWindow() == hwnd) ? 1 : 0);
                }
                else { _windowService.HideOverlayWindow(); }
            };
            _maskTimer.Start();
        }

        // ================= 文档帮助 =================

        [RelayCommand]
        private async Task ShowCaptureHelpAsync()
        {
            await ShowDocAsync("各截图引擎的“差异对比”", "capture-engine.md",
                "【① PrintWindow（推荐首选）】...");
        }

        [RelayCommand]
        private async Task ShowTriggerIntervalHelpAsync()
        {
            await ShowDocAsync("抓帧间隔说明", "capture-interval.md",
                "【关于引擎刷新间隔】...");
        }

        [RelayCommand]
        private async Task ShowMouseDriveHelpAsync()
        {
            await ShowDocAsync("鼠标驱动方式说明", "mouse-drive.md",
                "文档：不同点击方式的区别...");
        }

        //        核心修改点：调用高颜值阅读器窗口
        private async Task ShowDocAsync(string title, string fileName, string fallbackText)
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Docs", fileName);
                string content = fallbackText;

                if (System.IO.File.Exists(path))
                {
                    content = await System.IO.File.ReadAllTextAsync(path);
                }

                // 切换到 UI 线程弹出独立的高颜值窗口
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var win = new TrustNTE.Views.Windows.DocViewerWindow(title, content);
                    if (System.Windows.Application.Current?.MainWindow is { IsLoaded: true } owner)
                    {
                        win.Owner = owner;
                    }
                    win.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowInfoAsync("加载失败", $"文档加载失败：{ex.Message}\n请确认 Assets\\Docs\\{fileName} 存在。");
            }
        }
    }
}