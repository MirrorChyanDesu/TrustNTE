using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using TrustNTE.Models;
using TrustNTE.Services.Interfaces;
using Gma.System.MouseKeyHook;

namespace TrustNTE.Services.Implementations
{
    public class HotKeyService : IHotKeyService, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private HwndSource? _hwndSource;

        private readonly Dictionary<string, int> _hotkeyIdMap = new(); // string id -> int hotkey id
        private readonly Dictionary<int, Action> _idActionMap = new(); // int hotkey id -> Action
        private int _currentId = 0;

        private IKeyboardMouseEvents? _globalHook;
        public event Action<HotKey>? OnKeyPressed;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotKeyService()
        {
            var parameters = new HwndSourceParameters("HotKeyServiceHiddenWindow")
            {
                WindowStyle = 0,
                ExtendedWindowStyle = 0
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
        }

        public void RegisterHotKey(string id, HotKey hotkey, Action action)
        {
            if (hotkey.IsEmpty) return;

            // 如果这个业务 ID 已经注册过，先注销旧的
            UnregisterHotKey(id);

            _currentId++;
            int currentId = _currentId;

            uint modifiers = (uint)hotkey.Modifiers;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key); // 原代码是对的

            if (RegisterHotKey(_hwndSource!.Handle, currentId, modifiers, vk))
            {
                _hotkeyIdMap[id] = currentId;
                _idActionMap[currentId] = action;
            }
        }

        public void UnregisterHotKey(string id)
        {
            // [   复] 只注销指定的 id，而不是把所有人杀了
            if (_hotkeyIdMap.TryGetValue(id, out int currentId))
            {
                UnregisterHotKey(_hwndSource!.Handle, currentId);
                _idActionMap.Remove(currentId);
                _hotkeyIdMap.Remove(id);
            }
        }

        public void StartListening()
        {
            if (_globalHook != null) return;
            _globalHook = Hook.GlobalEvents();
            _globalHook.KeyDown += (s, e) =>
            {
                var hk = new HotKey { Key = KeyInterop.KeyFromVirtualKey(e.KeyValue), Modifiers = Keyboard.Modifiers };
                if (!hk.IsEmpty) OnKeyPressed?.Invoke(hk);
            };
        }

        public void StopListening()
        {
            _globalHook?.Dispose();
            _globalHook = null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_idActionMap.TryGetValue(id, out var action))
                {
                    action?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            StopListening();
            _hwndSource?.Dispose();
            _hwndSource = null;
        }
    }
}