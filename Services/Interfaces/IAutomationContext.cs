using System;

namespace TrustNTE.Services.Interfaces
{
    public interface IAutomationContext
    {
        IntPtr CurrentHwnd { get; }
    }
}