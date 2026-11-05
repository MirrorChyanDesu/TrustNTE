using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoVolleyballHeartViewModel : TaskModuleViewModelBase
    {
        public AutoVolleyballHeartConfig Config { get; }

        public AutoVolleyballHeartViewModel(IConfigService configService, ILogService logService)
            : base(logService, configService, "排球之心")
        {
            Config = BindConfig(c => c.VolleyballHeart, (main, cfg) => main.VolleyballHeart = cfg);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            await Task.Delay(1000, token);
        }
    }
}
