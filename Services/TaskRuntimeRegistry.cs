using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TrustNTE.Services
{
    public static class TaskRuntimeRegistry
    {
        public interface ITaskRuntime
        {
            string TaskName { get; }
            Task StopAsync(TimeSpan timeout);
            bool TryStart();
        }

        private static readonly List<ITaskRuntime> Running = new();
        private static readonly object Lock = new();

        /// <summary>    账本变化事件：注册/注销时触发（备用钩子，当前互斥为派发时检查）。</summary>
        public static event Action? Changed;

        public static int Count { get { lock (Lock) return Running.Count; } }

        public static void Register(ITaskRuntime t)
        {
            lock (Lock) { if (!Running.Contains(t)) Running.Add(t); }
            Changed?.Invoke();
        }

        public static void Unregister(ITaskRuntime t)
        {
            lock (Lock) Running.Remove(t);
            Changed?.Invoke();
        }

        public static ITaskRuntime[] Snapshot()
        {
            lock (Lock) return Running.ToArray();
        }

        public static async Task StopAllAsync(TimeSpan timeoutPerTask)
        {
            ITaskRuntime[] snapshot;
            lock (Lock) snapshot = Running.ToArray();
            foreach (var t in snapshot)
            {
                try { await t.StopAsync(timeoutPerTask); } catch { }
            }
        }

        /// <summary>    有界确认：所有任务真正退出（账本清空）才返回 true。</summary>
        public static async Task<bool> WaitAllStoppedAsync(TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                lock (Lock) { if (Running.Count == 0) return true; }
                await Task.Delay(100).ConfigureAwait(false);
            }
            lock (Lock) return Running.Count == 0;
        }
    }
}
