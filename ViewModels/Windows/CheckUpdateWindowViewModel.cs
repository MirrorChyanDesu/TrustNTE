using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TrustNTE.Models.Update;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels
{
    public partial class CheckUpdateWindowViewModel : ObservableObject
    {
        private readonly UpdateService _updateService;
        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;

        private readonly string _remoteVersion;
        private readonly string? _downloadUrl;
        private readonly string? _sha256;

        [ObservableProperty]
        private string _windowTitle = string.Empty;

        [ObservableProperty]
        private string _releaseNote = string.Empty;

        public bool? DialogResult { get; private set; }

        public event EventHandler? CloseRequested;
        public event EventHandler? CloseApplicationRequested;

        public CheckUpdateWindowViewModel(
            UpdateCheckResult result,
            UpdateService updateService,
            IConfigService configService,
            IDialogService dialogService)
        {
            _remoteVersion = result.RemoteVersion;
            _downloadUrl = result.DownloadUrl;
            _sha256 = result.Sha256;
            _updateService = updateService;
            _configService = configService;
            _dialogService = dialogService;

            WindowTitle = $"TrustNTE ! 发现新版本 {result.RemoteVersion}";

            string finalReleaseNote = result.ReleaseNote;
            string localTxtPath = Path.Combine(AppContext.BaseDirectory, "Assets", "TrustNTE.update.txt");

            if (File.Exists(localTxtPath))
            {
                try
                {
                    finalReleaseNote = File.ReadAllText(localTxtPath);
                }
                catch { }
            }

            ReleaseNote = string.IsNullOrWhiteSpace(finalReleaseNote)
                ? "更新内容：\n暂无详细说明。"
                : $"更新内容：\n{finalReleaseNote}";
        }

        [RelayCommand]
        private async Task UpdateFromGithubAsync()
        {
            bool confirmed = await _dialogService.ShowConfirmAsync("警告", "国外服务器下载可能较慢，确定继续吗？");
            if (!confirmed) return;

            if (string.IsNullOrWhiteSpace(_downloadUrl))
            {
                await _dialogService.ShowInfoAsync("提示", "未从服务器获取到下载地址，请稍后重试。");
                return;
            }

            string args = BuildUpdaterArgs("github");
            if (_updateService.TryLaunchUpdater(args, out string? error))
            {
                CloseApplicationRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await _dialogService.ShowInfoAsync("错误", error ?? "启动更新程序失败！");
            }
        }

        [RelayCommand]
        private void OpenLanZou() => Process.Start(new ProcessStartInfo("https://www.ilanzou.com/s/VtD6Tsip") { UseShellExecute = true });

        [RelayCommand]
        private void OpenQuark() => Process.Start(new ProcessStartInfo("https://pan.quark.cn/s/efefad6d7475") { UseShellExecute = true });

        [RelayCommand]
        private void OpenBaidu() => Process.Start(new ProcessStartInfo("https://pan.baidu.com/s/1qGPoVe8ODdm3_Chkh7Iasw?pwd=ibrz") { UseShellExecute = true });

        [RelayCommand]
        private void JoinQQGroup()
        {
            try
            {
                string htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Docs", "Web", "Nte_QQGroup.html");

                if (System.IO.File.Exists(htmlPath))
                {
                    Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
                }
                else
                {
                    Process.Start(new ProcessStartInfo("https://qm.qq.com/q/ZiqhFvJio2") { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _ = _dialogService.ShowInfoAsync("提示", $"无法打开群组页面: {ex.Message}");
            }
        }

        [RelayCommand]
        private void SkipVersion()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void IgnoreVersion()
        {
            _configService.Update(c => c.NotShowNewVersionNoticeEndVersion = _remoteVersion);
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private string BuildUpdaterArgs(string source)
        {
            string args = $"-I --source {source} --version {_remoteVersion}";
            if (!string.IsNullOrWhiteSpace(_downloadUrl)) args += $" --url \"{_downloadUrl}\"";
            if (!string.IsNullOrWhiteSpace(_sha256)) args += $" --sha256 {_sha256}";
            return args;
        }
    }
}