using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;          //     必须加：解决 AppDomain 和 StringComparison
using System.IO;       //     必须加：解决 Path 和 File
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Services;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel
    {
        private readonly LocalApiService _localApiService;

        [ObservableProperty] private bool _isApiEnabled;

        [ObservableProperty] private string _copyDocsText = "一键复制";
        [ObservableProperty] private string _copyStartText = "一键复制";
        [ObservableProperty] private string _copyStopText = "一键复制";
        [ObservableProperty] private string _copyLogText = "一键复制";
        [ObservableProperty] private string _copyScreenText = "一键复制";
        [ObservableProperty] private string _copyBase64Text = "一键复制";
        [ObservableProperty] private string _copyViewerText = "一键复制";

        private CancellationTokenSource? _copyDocsCts;
        private CancellationTokenSource? _copyStartCts;
        private CancellationTokenSource? _copyStopCts;
        private CancellationTokenSource? _copyLogCts;
        private CancellationTokenSource? _copyScreenCts;
        private CancellationTokenSource? _copyBase64Cts;
        private CancellationTokenSource? _copyViewerCts;

        private void InitializeApi()
        {
            IsApiEnabled = _config.EnableLocalApi;
        }

        partial void OnIsApiEnabledChanged(bool value)
        {
            // 启动时恢复开关，或者用户手动拨动，都要开关 API
            if (value) _localApiService.Start();
            else _localApiService.Stop();

            // 只有用户操作时才触发存盘
            if (!_isInitializing) SaveCurrentConfig();
        }

        [RelayCommand]
        private async Task CopyApiDocs()
        {
            // 从文件读取
            string docPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Web", "api_docs.md");
            string docs = "";

            if (File.Exists(docPath))
            {
                docs = await File.ReadAllTextAsync(docPath);
            }
            else
            {
                docs = "未找到 API 文档文件，请检查 Assets/Web/api_docs.md 是否存在。";
            }

            if (ClipboardHelper.SetText(docs))
            {
                _logService.Log("✅ API 文档已复制到剪贴板，可直接粘贴给 AI。");
                await ShowCopyFeedbackAsync(() => CopyDocsText, val => CopyDocsText = val, () => _copyDocsCts, val => _copyDocsCts = val, "一键复制");
            }
        }

        [RelayCommand]
        private async Task CopyApi(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            bool success = ClipboardHelper.SetText(url);
            if (!success) return;

            _logService.Log($"✅ API 地址已复制到剪贴板: {url}");

            string targetUrl = url.ToLower();

            if (targetUrl.Contains("/start"))
                await ShowCopyFeedbackAsync(() => CopyStartText, val => CopyStartText = val, () => _copyStartCts, val => _copyStartCts = val, "一键复制");
            else if (targetUrl.Contains("/stop"))
                await ShowCopyFeedbackAsync(() => CopyStopText, val => CopyStopText = val, () => _copyStopCts, val => _copyStopCts = val, "一键复制");
            else if (targetUrl.Contains("/logs"))
                await ShowCopyFeedbackAsync(() => CopyLogText, val => CopyLogText = val, () => _copyLogCts, val => _copyLogCts = val, "一键复制");
            else if (targetUrl.Contains("/screenshot_base64"))
                await ShowCopyFeedbackAsync(() => CopyBase64Text, val => CopyBase64Text = val, () => _copyBase64Cts, val => _copyBase64Cts = val, "一键复制");
            else if (targetUrl.Contains("/screenshot"))
                await ShowCopyFeedbackAsync(() => CopyScreenText, val => CopyScreenText = val, () => _copyScreenCts, val => _copyScreenCts = val, "一键复制");
            else if (targetUrl.Contains("/viewer"))
                await ShowCopyFeedbackAsync(() => CopyViewerText, val => CopyViewerText = val, () => _copyViewerCts, val => _copyViewerCts = val, "一键复制");
        }

        private async Task ShowCopyFeedbackAsync(
            System.Func<string> getText,
            System.Action<string> setText,
            System.Func<CancellationTokenSource?> getCts,
            System.Action<CancellationTokenSource?> setCts,
            string defaultText)
        {
            var oldCts = getCts();
            oldCts?.Cancel();
            oldCts?.Dispose();

            var newCts = new CancellationTokenSource();
            setCts(newCts);
            var token = newCts.Token;

            setText("✔️ 成功复制"); 

            try
            {
                await Task.Delay(2000, token);
                if (!token.IsCancellationRequested)
                {
                    setText(defaultText);
                }
            }
            catch (TaskCanceledException) { }
        }
    }
}