using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoPawViewModel : TaskModuleViewModelBase
    {
        public AutoPawConfig Config { get; }

        public AutoPawViewModel(IConfigService configService, ILogService logService)
            : base(logService, configService, "自动粉爪")
        {
            Config = BindConfig(c => c.Paw, (main, cfg) => main.Paw = cfg);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
        }
    }
}
