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

        // 按键按下事件
        event Action<HotKey>? OnKeyPressed;

        //按键松开事件（用于处理自动飘逸这种长按功能）
        event Action<HotKey>? OnKeyReleased;
    }
}