using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.AutoRedeemCode;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    /// <summary>
    /// 自动兑换码任务页 ViewModel。
    /// 核心流程：任务页启动 → RedemptionService 单飞执行。
    /// 剪贴板监听、兑换码窗口"一键兑换"按钮为第二阶段。
    /// </summary>
    public partial class AutoRedeemViewModel : TaskModuleViewModelBase
    {
        private readonly IRedemptionService _redemption;

        /// <summary>任务配置（指向 AppConfig 内的兑换码配置实例，绑定与持久化共用同一对象）。</summary>
        public AutoRedeemConfig Config { get; }

        public AutoRedeemViewModel(ILogService logService, IRedemptionService redemption, IConfigService configService)
            : base(logService, configService, "自动兑换码")
        {
            _redemption = redemption;
            Config = BindConfig(c => c.AutoRedeem, (main, cfg) => main.AutoRedeem = cfg);
        }

        /// <summary>兑换码文本 → 有效码列表（顺序保留，去空行/去重）</summary>
        private List<string> ParseCodes(string raw)
        {
            return (raw ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            var codes = ParseCodes(Config.RedeemCodes);
            if (codes.Count == 0)
            {
                _logService.Log("[一键兑换]未配置任何兑换码，请在卡片中填写");
                return;
            }

            _logService.Log($"[一键兑换]开始执行，共解析出 {codes.Count} 个兑换码");

            var progress = new Progress<string>(msg => _logService.Log(msg));
            var result = await _redemption.RunAsync(codes, progress, token);

            if (result.Aborted)
            {
                _logService.Log("[一键兑换]任务被跳过（引擎未就绪或已有兑换任务在执行）");
                return;
            }

            if (result.FailedCodes.Count > 0)
            {
                _logService.Log($"[一键兑换]失败兑换码：{string.Join(", ", result.FailedCodes)}");
            }
        }
    }
}
