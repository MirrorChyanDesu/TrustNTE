using CommunityToolkit.Mvvm.Input;
using System;
using Microsoft.Win32;
using TrustNTE.Helpers;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel
    {
        [RelayCommand]
        private void BrowseGamePath()
        {
            var dialog = new OpenFileDialog { Title = "请选择异环启动器", Filter = "可执行文件 (*.exe)|*.exe" };
            if (dialog.ShowDialog() == true) GamePath = dialog.FileName;
        }

        [RelayCommand]
        private void AutoDetectGamePath()
        {
            var path = RegistryHelper.FindLauncherPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                GamePath = path;
                _logService.Log($"自动获取成功！启动器路径: {path}");
            }
            else
            {
                _logService.LogError("自动获取失败，注册表中未找到异环启动器，请手动浏览。");
            }
        }
    }
}