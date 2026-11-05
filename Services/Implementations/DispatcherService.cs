using System;
using System.Windows;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    /// <summary>
    /// 调度器服务实现。
    /// </summary>
    public class DispatcherService : IDispatcherService
    {
        private static System.Windows.Threading.Dispatcher? Dispatcher => Application.Current?.Dispatcher;

        public void Invoke(Action action)
        {
            var d = Dispatcher;
            // 如果应用程序正在关闭，或者 Dispatcher 已经关闭，直接返回，防止抛异常
            if (d == null || d.HasShutdownStarted || d.HasShutdownFinished) return;

            if (d.CheckAccess())
            {
                action();
            }
            else
            {
                try
                {
                    d.Invoke(action);
                }
                catch (TaskCanceledException)
                {
                    // 忽略：通常发生在程序关闭时，UI线程已停止响应同步调用
                }
                catch (OperationCanceledException)
                {
                    // 忽略：同上
                }
                catch (Exception)
                {
                    // 忽略其他在关闭阶段可能出现的异常
                }
            }
        }

        public void BeginInvoke(Action action)
        {
            var d = Dispatcher;
            if (d == null || d.HasShutdownStarted || d.HasShutdownFinished) return;

            if (d.CheckAccess())
            {
                action();
            }
            else
            {
                try
                {
                    d.BeginInvoke(action);
                }
                catch (TaskCanceledException) { }
                catch (Exception) { }
            }
        }
    }
}