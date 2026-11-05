using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    /// <summary>
    /// 交互提示图集服务（Assets\Nte_Word_F）。
    /// 包含：白手（杂物）、宝箱1、宝箱2。
    /// </summary>
    public sealed class NteWordFTemplateService : SpriteAtlasTemplateServiceBase
    {
        public NteWordFTemplateService(ILogService logService)
            : base("Nte_Word_F", "交互提示", logService)
        {
        }
    }
}