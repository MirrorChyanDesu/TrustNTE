using System;
using System.Collections.ObjectModel;
using System.Windows;
using TrustNTE.ViewModels;
using TrustNTE.Views;
using TrustNTE.Views.Windows;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class WindowService : IWindowService
    {
        private readonly IClipboardService _clipboardService;
        private Window? _logWindow;
        private PreviewWindow? _previewWindow;
        private OverlayWindow? _overlayWindow;
        private AutoMusicDebugWindow? _autoMusicDebugWindow;
        private CombatDebugWindow? _combatDebugWindow;
        private MapMatchDebugWindow? _mapMatchDebugWindow;
        private AutoFishingPreviewWindow? _autoFishingPreviewWindow;

        public WindowService(IClipboardService clipboardService)
        {
            _clipboardService = clipboardService;
        }

        /// <summary>
        /// 辅助方法：把主界面强行拉到前台
        /// </summary>
        private void RestoreMainWindowFocus()
        {
            if (Application.Current?.MainWindow != null)
            {
                var mainWin = Application.Current.MainWindow;

                if (mainWin.WindowState == WindowState.Minimized)
                    mainWin.WindowState = WindowState.Normal;

                mainWin.Show();

                // 专治全屏游戏抢焦点
                bool wasTopmost = mainWin.Topmost;
                mainWin.Topmost = true;
                mainWin.Topmost = wasTopmost;

                mainWin.Activate();
                mainWin.Focus();
            }
        }

        public void SendLogToWindows(string message)
        {
            if (_logWindow is LogWindow lw) lw.ReceiveLog(message);
            _overlayWindow?.EnqueueLine(message);
        }

        public void ShowLogWindow(ObservableCollection<string> logs, Action onClosed)
        {
            if (_logWindow == null)
            {
                var viewModel = new LogWindowViewModel(_clipboardService, logs);
                _logWindow = new LogWindow(viewModel) { Owner = Application.Current.MainWindow };
                _logWindow.Closed += (s, e) => { _logWindow = null; onClosed?.Invoke(); };
                _logWindow.Show();
            }
        }

        public void CloseLogWindow()
        {
            _logWindow?.Close();
            _logWindow = null;
            RestoreMainWindowFocus(); 
        }

        public bool? ShowPromptDialog(PromptDialogViewModel viewModel)
        {
            var dialog = new PromptDialog(viewModel) { Owner = Application.Current.MainWindow };
            return dialog.ShowDialog();
        }

        public bool? ShowListConfigDialog(ListConfigDialogViewModel viewModel)
        {
            var dialog = new ListConfigDialog(viewModel) { Owner = Application.Current.MainWindow };
            return dialog.ShowDialog();
        }

        public bool? ShowUpdateWindow(CheckUpdateWindowViewModel viewModel)
        {
            var dialog = new CheckUpdateWindow(viewModel);
            if (Application.Current?.MainWindow is { IsVisible: true } owner)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.ShowInTaskbar = true;
            }
            return dialog.ShowDialog();
        }

        public void ShowPreviewWindow(object dataContext, Action onClosed)
        {
            if (_previewWindow == null)
            {
                _previewWindow = new PreviewWindow { DataContext = dataContext, Owner = Application.Current.MainWindow };
                _previewWindow.Closed += (s, e) => { _previewWindow = null; onClosed?.Invoke(); };
                _previewWindow.Show();
            }
        }

        public void ClosePreviewWindow()
        {
            _previewWindow?.Close();
            _previewWindow = null;
        }

        public void ShowOverlayWindow(object dataContext)
        {
            if (_overlayWindow == null)
            {
                _overlayWindow = new OverlayWindow { DataContext = dataContext };
                _overlayWindow.Show();
            }
            else
            {
                _overlayWindow.Show();
            }
        }

        public void HideOverlayWindow()
        {
            _overlayWindow?.HideMask();
            RestoreMainWindowFocus(); //    压底
        }

        public void UpdateOverlayPosition(int x, int y, int width, int height)
        {
            _overlayWindow?.ShowMask(x, y, width, height);
        }

        public void SetOverlayOpacity(double opacity)
        {
            if (_overlayWindow != null) _overlayWindow.Opacity = opacity;
        }

        public void SetOverlayBackgroundOpacity(double opacity)
        {
            _overlayWindow?.SetBackgroundOpacity(opacity);
        }

        public void SetOverlayTextOpacity(double opacity)
        {
            _overlayWindow?.SetTextOpacity(opacity);
        }

        public void ShowAutoMusicDebugWindow(object dataContext)
        {
            if (_autoMusicDebugWindow == null || !_autoMusicDebugWindow.IsVisible)
            {
                _autoMusicDebugWindow = new AutoMusicDebugWindow
                {
                    DataContext = dataContext,
                    Owner = Application.Current.MainWindow
                };
                // 用户点右上角X关闭时，也恢复主界面
                _autoMusicDebugWindow.Closed += (s, e) => { _autoMusicDebugWindow = null; RestoreMainWindowFocus(); };
                _autoMusicDebugWindow.Show();
            }
            else
            {
                _autoMusicDebugWindow.Activate();
            }
        }

        public void ShowStartErrorWindow()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var win = new StartErrorWindow { Owner = Application.Current.MainWindow };
                win.ShowDialog();
            });
        }

        public void ShowCombatDebugWindow(CombatDebugWindowViewModel viewModel)
        {
            if (_combatDebugWindow == null || !_combatDebugWindow.IsVisible)
            {
                _combatDebugWindow = new CombatDebugWindow { DataContext = viewModel };
                // 用户点右上角X关闭时，也恢复主界面
                _combatDebugWindow.Closed += (s, e) => { _combatDebugWindow = null; RestoreMainWindowFocus(); };
                _combatDebugWindow.Show();
            }
            else
            {
                _combatDebugWindow.Activate();
            }
        }

        public void CloseCombatDebugWindow()
        {
            _combatDebugWindow?.Close();
            _combatDebugWindow = null;
            RestoreMainWindowFocus(); //    压底
        }

        public void ShowMapMatchDebugWindow(MapMatchDebugWindowViewModel viewModel)
        {
            if (_mapMatchDebugWindow == null || !_mapMatchDebugWindow.IsVisible)
            {
                _mapMatchDebugWindow = new MapMatchDebugWindow
                {
                    DataContext = viewModel,
                    Owner = Application.Current.MainWindow
                };
                // 用户点右上角X关闭时，也恢复主界面
                _mapMatchDebugWindow.Closed += (s, e) => { _mapMatchDebugWindow = null; RestoreMainWindowFocus(); };
                _mapMatchDebugWindow.Show();
            }
            else
            {
                _mapMatchDebugWindow.Activate();
            }
        }

        public void CloseMapMatchDebugWindow()
        {
            _mapMatchDebugWindow?.Close();
            _mapMatchDebugWindow = null;
            RestoreMainWindowFocus(); //    压底
        }

        public Window? ShowAutoFishingPreviewWindow(AutoFishingPreviewViewModel viewModel)
        {
            if (_autoFishingPreviewWindow == null || !_autoFishingPreviewWindow.IsVisible)
            {
                _autoFishingPreviewWindow = new AutoFishingPreviewWindow
                {
                    DataContext = viewModel,
                    Owner = Application.Current.MainWindow
                };
                // 用户点右上角X关闭时，也恢复主界面
                _autoFishingPreviewWindow.Closed += (s, e) => { _autoFishingPreviewWindow = null; RestoreMainWindowFocus(); };
                _autoFishingPreviewWindow.Show();
                return _autoFishingPreviewWindow;
            }
            else
            {
                _autoFishingPreviewWindow.Activate();
                return _autoFishingPreviewWindow;
            }
        }

        public void CloseAutoFishingPreviewWindow()
        {
            _autoFishingPreviewWindow?.Close();
            _autoFishingPreviewWindow = null;
            RestoreMainWindowFocus(); 
        }
    }
}