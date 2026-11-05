using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoFountainViewModel : TaskModuleViewModelBase
    {
        public AutoFountainConfig Config { get; }

        public AutoFountainViewModel(IConfigService configService, ILogService logService)
            : base(logService, configService, "喷泉签到")
        {
            Config = BindConfig(c => c.Fountain, (main, cfg) => main.Fountain = cfg);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
        }
    }
}
