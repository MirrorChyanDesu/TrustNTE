using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models;
using TrustNTE.Services.AutoRedeemCode;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels
{
    public partial class RedeemCodeWindowViewModel : ObservableObject, IDisposable
    {
        private readonly IClipboardService _clipboardService;
        private readonly IConfigService _configService;
        private readonly IRedemptionService _redemption;
        private readonly ILogService _logService;
        private readonly CancellationTokenSource _cts = new();

        private readonly RedeemCodeFeed _allFeed;

        [ObservableProperty] private bool _isCnServerSelected = true;
        [ObservableProperty] private bool _isNotificationEnabled;

        private bool _isRedeemingLocal;

        public bool IsAnyRedeeming => _isRedeemingLocal || _redemption.IsRunning;
        public bool CanStartRedeem => !IsAnyRedeeming;

        public ObservableCollection<RedeemCodeGroupViewModel> FilteredGroups { get; } = new();

        public RedeemCodeWindowViewModel(
            RedeemCodeFeed feed,
            IClipboardService clipboardService,
            IConfigService configService,
            IRedemptionService redemption,
            ILogService logService)
        {
            _allFeed = feed;
            _clipboardService = clipboardService;
            _configService = configService;
            _redemption = redemption;
            _logService = logService;

            var config = _configService.Load();
            IsNotificationEnabled = config.RedeemCodeNotificationEnabled;

            RefreshList();
        }

        partial void OnIsCnServerSelectedChanged(bool value)
        {
            RefreshList();
        }

        partial void OnIsNotificationEnabledChanged(bool value)
        {
            var config = _configService.Load();
            config.RedeemCodeNotificationEnabled = value;
            _configService.Save(config);
        }

        private void RefreshList()
        {
            FilteredGroups.Clear();

            string targetServer = IsCnServerSelected ? "CN" : "Global";

            var items = _allFeed.Items.Where(x => x.Server.Equals(targetServer, StringComparison.OrdinalIgnoreCase)).ToList();

            if (items.Count == 0 && targetServer == "CN")
            {
                items = _allFeed.Items.Where(x => string.IsNullOrEmpty(x.Server)).ToList();
            }

            foreach (var item in items)
            {
                FilteredGroups.Add(new RedeemCodeGroupViewModel(item));
            }
        }

        [RelayCommand]
        private void SelectCnServer() => IsCnServerSelected = true;

        [RelayCommand]
        private void SelectGlobalServer() => IsCnServerSelected = false;

        [RelayCommand]
        private async Task CopyCodesAsync(RedeemCodeGroupViewModel group)
        {
            if (group == null || group.Codes.Count == 0 || group.IsCopying) return;

            group.IsCopying = true;
            try
            {
                string text = string.Join("\n", group.Codes);
                if (_clipboardService.SetText(text))
                {
                    group.IsCopied = true;
                    await Task.Delay(1500, _cts.Token);
                    group.IsCopied = false;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                group.IsCopying = false;
            }
        }

        // 一键兑换命令
        [RelayCommand]
        private async Task AutoRedeemGroupAsync(RedeemCodeGroupViewModel group)
        {
            if (group is null) return;
            if (IsAnyRedeeming)
            {
                _logService.Log("[一键兑换]已有兑换任务在执行，本次请求忽略");
                return;
            }

            var codes = ExtractCodes(group);
            if (codes.Count == 0)
            {
                _logService.Log("[一键兑换]该分组没有可兑换的码");
                return;
            }

            // 同步写回任务页配置（让任务页卡片可见，配置持久化）
            var app = _configService.Load();
            app.AutoRedeem.RedeemCodes = string.Join(Environment.NewLine, codes);
            _configService.Save(app);

            SetRedeeming(true);
            try
            {
                _logService.Log($"[一键兑换]开始执行，共 {codes.Count} 个兑换码");
                var progress = new Progress<string>(msg => _logService.Log(msg));

                // fire-and-forget：窗口可以中途关闭，任务继续在后台跑完
                var result = await _redemption.RunAsync(codes, progress, CancellationToken.None);

                if (result.Aborted)
                    _logService.Log("[一键兑换]任务被跳过（引擎未就绪或已有兑换任务在执行）");
                else if (result.FailedCodes.Count > 0)
                    _logService.LogError($"[一键兑换]失败兑换码：{string.Join(", ", result.FailedCodes)}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[一键兑换]执行异常：{ex.Message}");
            }
            finally
            {
                SetRedeeming(false);
            }
        }

        private void SetRedeeming(bool value)
        {
            _isRedeemingLocal = value;
            OnPropertyChanged(nameof(IsAnyRedeeming));
            OnPropertyChanged(nameof(CanStartRedeem));
        }

        private static List<string> ExtractCodes(RedeemCodeGroupViewModel group)
        {
            return (group.Codes ?? new List<string>())
                .Select(c => (c ?? string.Empty).Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();
        }

        public void Dispose()
        {
            // 只 Cancel，不 Dispose：
            // CopyCodesAsync 里的 Task.Delay(1500, _cts.Token) 可能在窗口关闭后才响应取消，
            // 此时若 token 已释放会抛 ObjectDisposedException（catch 接不住）。CTS 交给 GC 回收。
            _cts.Cancel();
        }
    }
}
