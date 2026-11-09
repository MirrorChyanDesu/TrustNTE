using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel
    {
        public string[] MouseDriveTypes { get; } = { "物理点击", "通用点击", "后台点击" };
        
        [ObservableProperty] private string _mouseDriveType;
        [ObservableProperty] private int _testClickX = 666;
        [ObservableProperty] private int _testClickY = 666;

        partial void OnMouseDriveTypeChanged(string value) { if (!_isInitializing) SaveCurrentConfig(); }

        private async Task TestKeyAsync(int vkCode, string keyName)
        {
            IntPtr hwnd = _automationService.CurrentHwnd;
            if (hwnd == IntPtr.Zero)
            {
                _logService.LogError($"测试失败：未获取到游戏窗口句柄！");
                await _dialogService.ShowInfoAsync("未绑定窗口", "请先点击主界面的【启动】按钮！");
                return;
            }
            _inputService.SendKeyPress(hwnd, vkCode);
            _logService.LogInfo($"[测试] 已发送后台按键: {keyName} 键 (句柄:{hwnd})");
        }

        [RelayCommand] private async Task TestSendW() => await TestKeyAsync(0x57, "W");
        [RelayCommand] private async Task TestSendS() => await TestKeyAsync(0x53, "S");
        [RelayCommand] private async Task TestSendA() => await TestKeyAsync(0x41, "A");
        [RelayCommand] private async Task TestSendD() => await TestKeyAsync(0x44, "D");
        [RelayCommand] private async Task TestSendF() => await TestKeyAsync(0x46, "F");
        [RelayCommand] private async Task TestSendSpace() => await TestKeyAsync(0x20, "空格");

        private async Task<bool> CheckWindowAsync()
        {
            IntPtr hwnd = _automationService.CurrentHwnd;
            if (hwnd == IntPtr.Zero)
            {
                _logService.LogError("测试失败：未获取到游戏窗口句柄！");
                await _dialogService.ShowInfoAsync("未绑定窗口", "请先点击主界面的【启动】按钮！");
                return false;
            }
            return true;
        }

        [RelayCommand]
        private async Task TestBackgroundClick()
        {
            if (!await CheckWindowAsync()) return;
            IntPtr hwnd = _automationService.CurrentHwnd;
            _inputService.SendBackgroundMouseClick(hwnd, TestClickX, TestClickY, true);
            _logService.LogInfo($"[测试] 已发送 后台点击: ({TestClickX}, {TestClickY})");
        }

        [RelayCommand]
        private async Task TestPhysicalClick()
        {
            if (!await CheckWindowAsync()) return;
            IntPtr hwnd = _automationService.CurrentHwnd;
            await Task.Run(() => _inputService.SendPhysicalClickWithRestore(hwnd, TestClickX, TestClickY));
            _logService.LogInfo($"[测试] 已发送 物理点击: ({TestClickX}, {TestClickY})");
        }

        [RelayCommand]
        private async Task TestTrickClick()
        {
            if (!await CheckWindowAsync()) return;
            IntPtr hwnd = _automationService.CurrentHwnd;
            await Task.Run(() => _nteInteractionService.NteClick(hwnd, TestClickX, TestClickY));
            _logService.LogInfo($"[测试] 已发送 通用点击: ({TestClickX}, {TestClickY})");
        }
    }
}