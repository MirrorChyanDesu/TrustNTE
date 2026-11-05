using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoHuntViewModel : TaskModuleViewModelBase
    {
        public AutoHuntConfig Config { get; }

        public AutoHuntViewModel(IConfigService configService, ILogService logService)
            : base(logService, configService, "异象追猎")
        {
            Config = BindConfig(c => c.Hunt, (main, cfg) => main.Hunt = cfg);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            // TODO: 异象追猎业务逻辑
            await Task.Delay(1000, token);
        }
    }
}
