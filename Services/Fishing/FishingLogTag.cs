using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Fishing
{
    public static class FishingLogTag
    {
        public static string Of(IInputRouter router) => $"[自动钓鱼][{router.CurrentDrive.ToConfigString()}]";
    }
}