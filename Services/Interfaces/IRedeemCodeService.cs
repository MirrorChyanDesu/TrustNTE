using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models;

namespace TrustNTE.Services.Interfaces
{
    public interface IRedeemCodeService
    {
        Task<RedeemCodeFeed?> GetLatestFeedAsync(CancellationToken token = default);
    }
}