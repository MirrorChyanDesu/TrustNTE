using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoPilgrimageViewModel : TaskModuleViewModelBase
    {
        public AutoPilgrimageConfig Config { get; }

        public AutoPilgrimageViewModel(IConfigService configService, ILogService logService)
            : base(logService, configService, "异象巡礼")
        {
            Config = BindConfig(c => c.Pilgrimage, (main, cfg) => main.Pilgrimage = cfg);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
        }
    }
}
