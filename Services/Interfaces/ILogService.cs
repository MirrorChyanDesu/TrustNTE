using System;

namespace TrustNTE.Services.Interfaces
{
    public interface ILogService
    {
        event Action<string>? OnLogReceived;
        bool IsEnabled { get; set; }

        void Log(string message);

        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }
}