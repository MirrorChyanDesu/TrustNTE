using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;

namespace TrustNTE.Services.Game
{
    public class WindowMessenger : IDisposable
    {
        private CancellationTokenSource? _keepAliveCts;

        public void StartKeepAlive(IntPtr hWnd)
        {
            StopKeepAlive();
            _keepAliveCts = new CancellationTokenSource();
            var token = _keepAliveCts.Token;

            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (NativeMethods.IsIconic(hWnd))
                            NativeMethods.ShowWindow(hWnd, 9);
                    }
                    catch { }
                    Thread.Sleep(1000);
                }
            }, token);
        }

        public void StopKeepAlive()
        {
            _keepAliveCts?.Cancel();
            _keepAliveCts?.Dispose();
            _keepAliveCts = null;
        }

        public void SendActivateMessages(IntPtr hWnd)
        {
            NativeMethods.PostMessage(hWnd, 0x001C, (IntPtr)1, IntPtr.Zero);
            NativeMethods.PostMessage(hWnd, 0x0006, (IntPtr)1, IntPtr.Zero);
            NativeMethods.PostMessage(hWnd, 0x0007, IntPtr.Zero, IntPtr.Zero);
        }

        public void Dispose() => StopKeepAlive();
    }
}