using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TrustNTE.Models.Update;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels
{
    /// <summary>
    /// 更新提示窗口的 ViewModel，承载更新提示展示与用户操作响应。
    /// </summary>
    public partial class CheckUpdateWindowViewModel : ObservableObject
    {
        private readonly UpdateService _updateService;
        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;

        private readonly string _remoteVersion;
        private readonly string? _downloadUrl;
        private readonly string? _sha256;

        /// <summary>
        /// 窗口标题文本。
        /// </summary>
        [ObservableProperty]
        private string _windowTitle = string.Empty;

        /// <summary>
        /// 更新说明文本。
        /// </summary>
        [ObservableProperty]
        private string _releaseNote = string.Empty;

        /// <summary>
        /// 对话框结果。true 表示更新已启动或用户确认，false 表示取消或关闭。
        /// </summary>
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// 当 ViewModel 请求关闭窗口时触发，由 View 订阅并设置 DialogResult。
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// 当 ViewModel 请求退出整个应用时触发，由 View 订阅并调用 Application.Current.Shutdown。
        /// </summary>
        public event EventHandler? CloseApplicationRequested;

        /// <summary>
        /// 初始化 <see cref="CheckUpdateWindowViewModel"/> 实例。
        /// </summary>
        /// <param name="result">检查更新的结果。</param>
        /// <param name="updateService">更新服务，用于启动更新器。</param>
        /// <param name="configService">配置服务，用于记录"不再提示"版本号。</param>
        /// <param name="dialogService">对话框服务。</param>
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
            ReleaseNote = string.IsNullOrWhiteSpace(result.ReleaseNote)
                ? "更新内容：\n暂无详细说明。"
                : $"更新内容：\n{result.ReleaseNote}";
        }

        /// <summary>
        /// 从 Github 渠道更新。
        /// </summary>
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

        /// <summary>
        /// 打开百度网盘下载提示。
        /// </summary>
        [RelayCommand]
        private async Task OpenBaiduPanAsync()
        {
            await _dialogService.ShowInfoAsync("提示", "正在打开百度网盘下载页面，提取码：xxxx");
        }

        /// <summary>
        /// 打开 QQ 群加入链接。
        /// </summary>
        [RelayCommand]
        private void JoinQQGroup()
        {
            Process.Start(new ProcessStartInfo("https://qm.qq.com/q/796636370") { UseShellExecute = true });
        }

        /// <summary>
        /// 跳过当前版本，不记录忽略。
        /// </summary>
        [RelayCommand]
        private void SkipVersion()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 不再提示该版本，写入配置后关闭。
        /// </summary>
        [RelayCommand]
        private void IgnoreVersion()
        {
            _configService.Update(c => c.NotShowNewVersionNoticeEndVersion = _remoteVersion);
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 取消关闭窗口。
        /// </summary>
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