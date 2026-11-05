namespace TrustNTE.Services.Interfaces
{
    public interface IStartupService
    {
        bool IsEnabled();
        bool Enable();
        bool Disable();
    }
}