using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels
{
    /// <summary>
    /// 运行日志窗口的 ViewModel，承载日志复制与清空命令。
    /// </summary>
    public partial class LogWindowViewModel : ObservableObject, IDisposable
    {
        private const string DefaultCopyText = "一键复制日志";
        private const string EmptyLogText = "没有日志可复制";
        private const string CopySuccessText = "复制成功!";
        private const string CopyFailedText = "复制失败(被占用)";
        private const int ResetDelayMilliseconds = 1500;

        private readonly IClipboardService _clipboardService;
        private CancellationTokenSource? _resetCts;
        private bool _isDisposed;

        /// <summary>
        /// 日志集合。与 StartPageViewModel 共享同一实例，用于实时接收日志。
        /// </summary>
        public ObservableCollection<string> Logs { get; }

        /// <summary>
        /// 复制按钮当前显示的文字。
        /// </summary>
        [ObservableProperty]
        private string _copyButtonText = DefaultCopyText;

        /// <summary>
        /// 初始化 <see cref="LogWindowViewModel"/> 实例。
        /// </summary>
        /// <param name="clipboardService">剪贴板服务。</param>
        /// <param name="logs">与 StartPageViewModel 共享的日志集合。</param>
        public LogWindowViewModel(IClipboardService clipboardService, ObservableCollection<string> logs)
        {
            _clipboardService = clipboardService;
            Logs = logs;
        }

        /// <summary>
        /// 复制全部日志到剪贴板。
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task CopyAllAsync()
        {
            if (_isDisposed) return;

            // 取消上一次的文案恢复任务，保证连续点击时"最近一次点击的文案"胜出
            _resetCts?.Cancel();
            _resetCts?.Dispose();
            _resetCts = new CancellationTokenSource();
            CancellationToken token = _resetCts.Token;

            if (Logs.Count == 0)
            {
                CopyButtonText = EmptyLogText;
            }
            else
            {
                string allLogs = string.Join(Environment.NewLine, Logs);
                // SetText 内部为 P/Invoke，必须在 UI 线程执行，此处 await 之前运行于 UI 线程
                bool success = _clipboardService.SetText(allLogs);
                CopyButtonText = success ? CopySuccessText : CopyFailedText;
            }

            try
            {
                await Task.Delay(ResetDelayMilliseconds, token);
                CopyButtonText = DefaultCopyText;
            }
            catch (OperationCanceledException)
            {
                // 被新的点击取消，文案由新的调度负责恢复
            }
        }

        /// <summary>
        /// 清空日志集合。
        /// </summary>
        [RelayCommand]
        private void Clear()
        {
            if (_isDisposed) return;
            Logs.Clear();
        }

        /// <summary>
        /// 释放资源并取消所有待恢复的文案任务。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _resetCts?.Cancel();
            _resetCts?.Dispose();
            _resetCts = null;
        }
    }
}