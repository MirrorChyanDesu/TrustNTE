using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoMarketViewModel : TaskModuleViewModelBase
    {
        public AutoMarketConfig Config { get; }

        public AutoMarketViewModel(IConfigService configService, ILogService logService)
            : base(logService, configService, "福荫集市")
        {
            Config = BindConfig(c => c.Market, (main, cfg) => main.Market = cfg);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
        }
    }
}
