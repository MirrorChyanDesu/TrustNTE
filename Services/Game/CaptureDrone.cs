using OpenCvSharp;
using System;
using System.Numerics;
using System.Threading;

namespace TrustNTE.Services.Game
{
    /// <summary>
    /// 截图无人机：按可变间隔生产最新帧到单槽缓冲（latest-wins）。
    /// 实际频率 = min(UI配置诉求, 高帧率消费者诉求)，由 GameService 合成后下发。
    /// [修复#14] 代际令牌：Stop 超时未死的旧线程醒来即自杀。
    /// [A6加固·句柄自愈] 连续失败达阈值时，通过定位器重寻游戏窗口并无缝切换——
    ///                   防止监管器漏绑时无人机拿着死句柄空转。
    /// </summary>
    public sealed class CaptureDrone : IDisposable
    {
        private readonly ScreenCapturer _capturer = new();
        private readonly Action<string>? _onLog;          // 零依赖日志回调
        private readonly Func<IntPtr>? _windowLocator;    //  窗口定位器（按标题/类名找，非缓存句柄）

        private Thread? _thread;
        private volatile bool _running;
        private volatile IntPtr _hwnd;                     //   volatile：自愈线程写，日志读
        private string _mode = "PrintWindow";
        private volatile int _minIntervalMs;

        // 启动代际：每次 Start 递增；旧线程发现代际不符自动退出
        private int _gen;

        //  自愈参数：连续失败 8 次才触发重寻（全屏切换/合成器瞬时抖动不算病）
        private const int HealThreshold = 8;

        private readonly object _frameLock = new();
        private Mat? _latestFrame;
        private DateTime _lastCaptureTime = DateTime.MinValue;

        // 健康度遥测
        private int _consecutiveFailures;

        public bool IsRunning => _running;

        /// <summary>缓冲帧图龄（毫秒）。截图持续失败时它会持续增长——这是"静默陈旧"的探测器。</summary>
        public long LastFrameAgeMs
        {
            get { lock (_frameLock) return (long)(DateTime.Now - _lastCaptureTime).TotalMilliseconds; }
        }

        /// <summary>连续失败次数（0 = 健康）。</summary>
        public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

        public CaptureDrone(Action<string>? onLog = null, Func<IntPtr>? windowLocator = null)
        {
            _onLog = onLog;
            _windowLocator = windowLocator;
        }

        public void Start(IntPtr hwnd, string mode, int minIntervalMs = 50)
        {
            Stop();
            _hwnd = hwnd;
            _mode = mode;
            _minIntervalMs = Math.Max(1, minIntervalMs);
            _running = true;

            // 领取代际，线程闭包携带自己的代数
            int gen = Interlocked.Increment(ref _gen);
            _thread = new Thread(() => Loop(gen))
            {
                IsBackground = true,
                Name = "CaptureDrone",
                Priority = ThreadPriority.AboveNormal
            };
            _thread.Start();
        }

        /// <summary>运行中调速：下一帧立即按新间隔执行。</summary>
        public void UpdateInterval(int minIntervalMs) => _minIntervalMs = Math.Max(1, minIntervalMs);

        /// <summary>外部单发帧喂入缓冲（应急兜底通道）。所有权移交，调用方不再 Dispose。</summary>
        public void Feed(Mat frame)
        {
            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = frame;
                _lastCaptureTime = DateTime.Now;
            }
        }

        private void Loop(int gen)
        {
            while (_running && gen == Volatile.Read(ref _gen))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Mat? frame = null;
                try { frame = _capturer.Capture(_hwnd, _mode); } catch { }

                // 本线程已是孤儿，自行清理退出
                if (gen != Volatile.Read(ref _gen))
                {
                    frame?.Dispose();
                    return;
                }

                if (frame != null && !frame.Empty())
                {
                    Volatile.Write(ref _consecutiveFailures, 0); // 成功清零
                    lock (_frameLock)
                    {
                        _latestFrame?.Dispose();
                        _latestFrame = frame;
                        _lastCaptureTime = DateTime.Now; // 在源头盖上拍摄时间戳
                    }
                }
                else
                {
                    frame?.Dispose();
                    int fails = Interlocked.Increment(ref _consecutiveFailures);
                    // 节流日志：只在 5 和 2 的幂次（10/20/40/80...）打点，防刷屏
                    if (fails == 5 || BitOperations.IsPow2(fails))
                        _onLog?.Invoke($"[截图无人机] 已连续 {fails} 次截图失败（窗口 0x{_hwnd.ToInt64():X}，模式 {_mode}）");

                    // ══════════════════════════════════════════════
                    // [A6加固·句柄自愈] 疑似死句柄 → 重寻窗口
                    // 条件：失败达阈值 + 配置了定位器。重寻到不同句柄 = 窗口被重建过
                    // ══════════════════════════════════════════════
                    if (fails >= HealThreshold && _windowLocator != null)
                    {
                        try
                        {
                            IntPtr fresh = _windowLocator();
                            if (fresh != IntPtr.Zero && fresh != _hwnd)
                            {
                                IntPtr old = _hwnd;
                                _hwnd = fresh; // volatile 写，Loop 内自写自读无竞争
                                Volatile.Write(ref _consecutiveFailures, 0);
                                _onLog?.Invoke($"[截图无人机] 句柄自愈成功: 0x{old.ToInt64():X} → 0x{fresh.ToInt64():X}");
                            }
                            else if (fresh == IntPtr.Zero)
                            {
                                _onLog?.Invoke("[截图无人机] 句柄自愈失败：定位器未找到游戏窗口（游戏可能未启动）");
                            }
                            // fresh == _hwnd：句柄没变，问题出在别处（如全屏切换），不动
                        }
                        catch { /* 定位器异常不拖垮截图循环 */ }
                    }
                }

                int remain = _minIntervalMs - (int)sw.ElapsedMilliseconds;
                if (remain > 0) Thread.Sleep(remain);
            }
        }

        public Mat? GetLatest()
        {
            lock (_frameLock) return _latestFrame?.Clone();
        }

        // 提供给音游的高精度接口，连图带时间戳一起给
        public (Mat? Frame, DateTime ShotAt) GetLatestWithTimestamp()
        {
            lock (_frameLock)
            {
                if (_latestFrame != null) return (_latestFrame.Clone(), _lastCaptureTime);
                return (null, DateTime.MinValue);
            }
        }

        public void Stop()
        {
            _running = false;
            Interlocked.Increment(ref _gen); // 代际失效：卡死的旧线程醒来即自杀
            try
            {
                if (_thread != null && !_thread.Join(500))
                    _onLog?.Invoke("[截图无人机] 停止超时，旧截图线程将在本次截图返回后自动退出");
            }
            catch { }
            _thread = null;
            Volatile.Write(ref _consecutiveFailures, 0); // 降落清零
            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }
        }

        public void Dispose() => Stop();
    }
}
