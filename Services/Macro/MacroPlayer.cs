using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models.Macro; // 解决找不到 MacroEventType

namespace TrustNTE.Services.Macro
{
    public class MacroPlayer
    {
        private CancellationTokenSource? _cts;

        public bool IsPlaying { get; private set; }

        public async Task PlayAsync(MacroFile macro, IntPtr targetHwnd, int gameWidth, int gameHeight)
        {
            if (IsPlaying) return;
            IsPlaying = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            double scaleX = (double)gameWidth / macro.Info.Width;
            double scaleY = (double)gameHeight / macro.Info.Height;

            await Task.Run(async () =>
            {
                Win32Input.timeBeginPeriod(1);
                var sw = Stopwatch.StartNew();
                try
                {
                    foreach (var e in macro.MacroEvents)
                    {
                        if (token.IsCancellationRequested) break;
                        long targetTime = e.Time;
                        
                        while (sw.ElapsedMilliseconds < targetTime)
                        {
                            long remain = targetTime - sw.ElapsedMilliseconds;
                            if (remain > 15) await Task.Delay(1, token);
                            else if (remain > 1) Thread.Sleep(1);
                            else Thread.SpinWait(50); 
                        }

                        if (token.IsCancellationRequested) break;
                        ExecuteEvent(e, scaleX, scaleY, targetHwnd);
                    }
                }
                catch (OperationCanceledException) { }
                finally
                {
                    Win32Input.timeEndPeriod(1);
                    IsPlaying = false;
                    ReleaseAllKeys(macro);
                }
            }, token);
        }

        private void ExecuteEvent(MacroEvent e, double scaleX, double scaleY, IntPtr hwnd)
        {
            switch (e.Type)
            {
                case MacroEventType.KeyDown:
                    if (e.KeyCode.HasValue) Win32Input.KeyDown(e.KeyCode.Value);
                    break;
                case MacroEventType.KeyUp:
                    if (e.KeyCode.HasValue) Win32Input.KeyUp(e.KeyCode.Value);
                    break;
                case MacroEventType.MouseDown:
                    if (e.MouseButton != null) Win32Input.MouseClick(e.MouseButton, true);
                    break;
                case MacroEventType.MouseUp:
                    if (e.MouseButton != null) Win32Input.MouseClick(e.MouseButton, false);
                    break;
                case MacroEventType.MouseWheel:
                    if (e.MouseY.HasValue) Win32Input.MouseWheel(e.MouseY.Value);
                    break;
                case MacroEventType.MouseMoveTo:
                    if (e.MouseX.HasValue && e.MouseY.HasValue)
                    {
                        int targetX = (int)(e.MouseX.Value * scaleX);
                        int targetY = (int)(e.MouseY.Value * scaleY);
                        var pt = new Win32Input.POINT { X = targetX, Y = targetY };
                        Win32Input.ClientToScreen(hwnd, ref pt);
                        Win32Input.MouseMoveAbsolute(pt.X, pt.Y);
                    }
                    break;
                case MacroEventType.MouseMoveBy:
                    if (e.MouseX.HasValue && e.MouseY.HasValue)
                        Win32Input.MouseMoveRelative(e.MouseX.Value, e.MouseY.Value);
                    break;
            }
        }

        public void Stop() => _cts?.Cancel();

        private void ReleaseAllKeys(MacroFile macro)
        {
            var keys = macro.MacroEvents.Where(x => x.Type == MacroEventType.KeyDown).Select(x => x.KeyCode).Distinct();
            var btns = macro.MacroEvents.Where(x => x.Type == MacroEventType.MouseDown).Select(x => x.MouseButton).Distinct();
            foreach (var k in keys) if (k.HasValue) Win32Input.KeyUp(k.Value);
            foreach (var b in btns) if (b != null) Win32Input.MouseClick(b, false);
        }
    }
}