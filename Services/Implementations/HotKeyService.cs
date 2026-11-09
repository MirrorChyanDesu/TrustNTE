using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using TrustNTE.Models;
using TrustNTE.Services.Interfaces;
using Gma.System.MouseKeyHook;
using WF = System.Windows.Forms; // ★ 别名，防止和 WPF 冲突

namespace TrustNTE.Services.Implementations
{
    public class HotKeyService : IHotKeyService, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private HwndSource? _hwndSource;

        private readonly Dictionary<string, int> _hotkeyIdMap = new();
        private readonly Dictionary<int, Action> _idActionMap = new();
        private int _currentId = 0;

        private IKeyboardMouseEvents? _globalHook;

        public event Action<HotKey>? OnKeyPressed;
        public event Action<HotKey>? OnKeyReleased;

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

        // ★ 新增：WinForms Keys → WPF ModifierKeys 映射
        private static ModifierKeys ToWpfModifiers(WF.Keys keys)
        {
            var m = ModifierKeys.None;
            if ((keys & WF.Keys.Control) != 0) m |= ModifierKeys.Control;
            if ((keys & WF.Keys.Shift) != 0) m |= ModifierKeys.Shift;
            if ((keys & WF.Keys.Alt) != 0) m |= ModifierKeys.Alt;
            if ((keys & WF.Keys.LWin) != 0 || (keys & WF.Keys.RWin) != 0) m |= ModifierKeys.Windows;
            return m;
        }

        public void RegisterHotKey(string id, HotKey hotkey, Action action)
        {
            if (hotkey.IsEmpty) return;
            UnregisterHotKey(id);

            _currentId++;
            int currentId = _currentId;

            uint modifiers = (uint)hotkey.Modifiers;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key);

            if (RegisterHotKey(_hwndSource!.Handle, currentId, modifiers, vk))
            {
                _hotkeyIdMap[id] = currentId;
                _idActionMap[currentId] = action;
            }
        }

        public void UnregisterHotKey(string id)
        {
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
                var hk = new HotKey { Key = KeyInterop.KeyFromVirtualKey(e.KeyValue), Modifiers = ToWpfModifiers(e.Modifiers) };
                if (!hk.IsEmpty) OnKeyPressed?.Invoke(hk);
            };

            _globalHook.KeyUp += (s, e) =>
            {
                var hk = new HotKey { Key = KeyInterop.KeyFromVirtualKey(e.KeyValue), Modifiers = ToWpfModifiers(e.Modifiers) };
                if (!hk.IsEmpty) OnKeyReleased?.Invoke(hk);
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