using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms; // 需要 UseWindowsForms=true
using TrustNTE.Helpers;
using TrustNTE.Models.Macro;

namespace TrustNTE.Services.Macro
{
    public interface IMacroSession : IDisposable
    {
        long CurrentDuration { get; }
        int EventCount { get; }
        MacroFile GetMergedMacro(MacroInfo info);
    }

    public class MacroSession : IMacroSession
    {
        private readonly IKeyboardMouseEvents _hook;
        private readonly Stopwatch _stopwatch = new();
        private readonly List<MacroEvent> _rawEvents = new();
        private readonly IntPtr _targetHwnd;
        private readonly HashSet<int> _pressedKeys = new();
        private readonly HashSet<string> _pressedMouseButtons = new();
        private readonly int _targetProcessId = Environment.ProcessId;

        public long CurrentDuration => _stopwatch.ElapsedMilliseconds;
        public int EventCount => _rawEvents.Count;

        public MacroSession(IntPtr targetHwnd)
        {
            _targetHwnd = targetHwnd;
            _hook = Hook.GlobalEvents();
            _hook.KeyDown += OnKeyDown;
            _hook.KeyUp += OnKeyUp;
            _hook.MouseDown += OnMouseDown;
            _hook.MouseUp += OnMouseUp;
            _hook.MouseMove += OnMouseMove;
            _hook.MouseWheel += OnMouseWheel;
            _stopwatch.Restart();
        }

        private bool IsSelfForeground() 
        {
            IntPtr fore = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(fore, out uint pid);
            return pid == _targetProcessId;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (IsSelfForeground() || _pressedKeys.Contains(e.KeyValue)) return;
            _pressedKeys.Add(e.KeyValue);
            Record(MacroEventType.KeyDown, keyCode: e.KeyValue);
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (IsSelfForeground()) return;
            _pressedKeys.Remove(e.KeyValue);
            Record(MacroEventType.KeyUp, keyCode: e.KeyValue);
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (IsSelfForeground()) return;
            string btn = e.Button.ToString();
            if (_pressedMouseButtons.Contains(btn)) return;
            _pressedMouseButtons.Add(btn);
            
            var pt = new Win32Input.POINT { X = Cursor.Position.X, Y = Cursor.Position.Y };
            Win32Input.ScreenToClient(_targetHwnd, ref pt);
            Record(MacroEventType.MouseDown, mx: pt.X, my: pt.Y, button: btn);
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (IsSelfForeground()) return;
            string btn = e.Button.ToString();
            _pressedMouseButtons.Remove(btn);
            
            var pt = new Win32Input.POINT { X = Cursor.Position.X, Y = Cursor.Position.Y };
            Win32Input.ScreenToClient(_targetHwnd, ref pt);
            Record(MacroEventType.MouseUp, mx: pt.X, my: pt.Y, button: btn);
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (IsSelfForeground()) return;
            var pt = new Win32Input.POINT { X = e.X, Y = e.Y };
            Win32Input.ScreenToClient(_targetHwnd, ref pt);
            Record(MacroEventType.MouseMoveTo, mx: pt.X, my: pt.Y);
        }

        private void OnMouseWheel(object? sender, MouseEventArgs e)
        {
            if (IsSelfForeground()) return;
            Record(MacroEventType.MouseWheel, my: e.Delta);
        }

        private void Record(MacroEventType type, int? keyCode = null, int? mx = null, int? my = null, string? button = null)
        {
            _rawEvents.Add(new MacroEvent
            {
                Type = type,
                Time = _stopwatch.ElapsedMilliseconds,
                KeyCode = keyCode,
                MouseX = mx,
                MouseY = my,
                MouseButton = button
            });
        }

        public MacroFile GetMergedMacro(MacroInfo info)
        {
            var result = new MacroFile { Info = info, SchemaVersion = 1 };
            result.MacroEvents = _rawEvents; 
            return result;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _hook.KeyDown -= OnKeyDown;
            _hook.KeyUp -= OnKeyUp;
            _hook.MouseDown -= OnMouseDown;
            _hook.MouseUp -= OnMouseUp;
            _hook.MouseMove -= OnMouseMove;
            _hook.MouseWheel -= OnMouseWheel;
            _hook.Dispose();
        }
    }
}