using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class ClipboardService : IClipboardService
    {
        public bool SetText(string text)
        {
            return ClipboardHelper.SetText(text);
        }
    }
}