using System;

namespace TrustNTE.Services.Interfaces
{
    public interface IDispatcherService
    {
        void Invoke(Action action);
        void BeginInvoke(Action action);
    }
}