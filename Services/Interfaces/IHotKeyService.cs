using System;
using TrustNTE.Models;

namespace TrustNTE.Services.Interfaces
{
    public interface IHotKeyService
    {
        void RegisterHotKey(string id, HotKey hotkey, Action action);
        void UnregisterHotKey(string id);
        void StartListening();
        void StopListening();
        event Action<HotKey>? OnKeyPressed;
    }
}