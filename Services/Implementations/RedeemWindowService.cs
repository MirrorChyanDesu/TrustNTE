using System.Windows;
using TrustNTE.Models;
using TrustNTE.Services.AutoRedeemCode;
using TrustNTE.Services.Interfaces;
using TrustNTE.ViewModels;
using TrustNTE.Views;

namespace TrustNTE.Services.Implementations
{
    public class RedeemWindowService : IRedeemWindowService
    {
        private readonly IClipboardService _clipboardService;
        private readonly IConfigService _configService;
        private readonly IRedemptionService _redemptionService;
        private readonly ILogService _logService;

        public RedeemWindowService(
            IClipboardService clipboardService,
            IConfigService configService,
            IRedemptionService redemptionService,
            ILogService logService)
        {
            _clipboardService = clipboardService;
            _configService = configService;
            _redemptionService = redemptionService;
            _logService = logService;
        }

        public void ShowRedeemWindow(RedeemCodeFeed feed)
        {
            var vm = new RedeemCodeWindowViewModel(feed, _clipboardService, _configService, _redemptionService, _logService);
            var window = new RedeemCodeWindow { DataContext = vm };

            if (Application.Current?.MainWindow is { IsLoaded: true } owner)
                window.Owner = owner;

            window.ShowDialog();
        }
    }
}