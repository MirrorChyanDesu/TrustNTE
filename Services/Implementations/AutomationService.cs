using TrustNTE.Models;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using TrustNTE.Helpers;
using TrustNTE.Services.Interfaces;
using TrustNTE.Services.Triggers;
using TrustNTE.Views.Windows;
using TrustNTE.Services.Implementations;

namespace TrustNTE.Services
{
    public class AutomationService : IAutomationContext
    {
        private readonly IGameService _gameService;
        private readonly LauncherService _launcherService;
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly IInputService _inputService;
        private readonly INteInteractionService _nteInteractionService;
        private readonly IInputRouter _router;
        private readonly ICoordinateMapper _coordMapper;
        private readonly NteRenderContextManager _nteRenderCtx;
        private readonly GameSupervisorService _supervisor;

        private readonly SemaphoreSlim _workflowLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _dispatchCts;
        private Task? _workflowTask;

        private readonly SemaphoreSlim _captureGate = new(1, 1);
        private readonly CancellationTokenSource _idleGuardCts = new();

        private volatile bool _captureEverStarted;
        private int _idleGuardStarted;
        // 点击模式变更播报：记住上次播报的模式，变了才喊
        private string? _lastLoggedClickMode;

        //  图龄报警节流字段
        private DateTime _lastStaleWarnTime = DateTime.MinValue;
        //会话统计：本次运行起点 + 已处理帧数
        private DateTime _engineStartUtc = DateTime.MinValue;
        private long _frameCount;
        // 心跳播报：每 60 秒报一次区间 FPS
        private DateTime _lastHeartbeatUtc = DateTime.MinValue;
        private long _heartbeatBaseFrames;

        //         守护重启门槛：只有真正绑定过游戏窗口后，游戏消失才视为"崩溃"
        private volatile bool _everBoundGame;
        //         用户最近一次手动 Start/Stop 时间（UTC），供守护重启避让
        private DateTime _lastUserEngineActionUtc = DateTime.MinValue;

        public event Action<string>? OnLogReceived;
        public event Action<bool>? OnRunningStateChanged;
        public event Action<BitmapSource>? OnFrameCaptured;
        public event Action<BitmapSource>? OnCombatDebugFrameCaptured;
        public event Action? OnWindowBound;

        public IntPtr CurrentHwnd { get; private set; } = IntPtr.Zero;
        public bool IsPreviewWindowOpen { get; set; } = false;

        public GameSupervisorService Supervisor => _supervisor;
        public NteGameStateTemplateService GameStateTemplates => _gameStateTemplates;

        public TriggerManager Triggers => _triggerManager;

        public bool IsEngineRunning => _cts is { IsCancellationRequested: false };

        //         锁被占 = 正在启动或收尾中（供热键判断方向用）
        public bool IsEngineBusy => _workflowLock.CurrentCount == 0;

        private readonly NteGameStateTemplateService _gameStateTemplates;
        private readonly TriggerManager _triggerManager = new();
        private readonly YoloModelProvider _yoloProvider;
        private Mat? _latestFrame;
        private readonly object _frameLock = new();


        // ================================================================
        // 实时追踪游戏窗口尺寸和DPI缩放的变化
        // ================================================================

        // 记录上一帧的窗口客户区宽度和高度。用于和当前帧对比，判断窗口是否被拉伸或改变。
        private int _lastTrackedW, _lastTrackedH;

        // 记录上一帧的窗口 DPI 缩放比例（1.0 = 100%）。用于跨不同缩放率的显示器拖拽时，判断 DPI 是否改变。
        private double _lastTrackedDpi = 1.0;

        // 记录上一次【打印窗口变化日志】的时间。
        // 作用：日志防抖节流。如果用户按住窗口边框疯狂拖拽，尺寸会一直变，
        // 限制至少间隔 1000 毫秒才允许打印一次日志，防止日志瞬间刷屏卡死 UI。
        private DateTime _lastResizeLogAt = DateTime.MinValue;
        private int _isBizRunning = 0;
        private int _isUiRunning = 0;
        private int _isCombatDebugRunning = 0;

        private readonly AutoStoryTemplateService _storyTemplateService;

        public AutomationService(
            IGameService gameService, LauncherService launcherService, IConfigService configService,
            ILogService logService, IInputService inputService, INteInteractionService nteInteractionService,
            AutoStoryTemplateService storyTemplateService, IInputRouter router, ICoordinateMapper coordMapper,
            NteRenderContextManager nteRenderCtx, GameSupervisorService supervisor, NteGameStateTemplateService gameStateTemplates)
        {
            _gameService = gameService;
            _launcherService = launcherService;
            _configService = configService;
            _logService = logService;
            _inputService = inputService;
            _nteInteractionService = nteInteractionService;
            _storyTemplateService = storyTemplateService;
            _router = router;
            _coordMapper = coordMapper;
            _nteRenderCtx = nteRenderCtx;
            _supervisor = supervisor;
            _gameStateTemplates = gameStateTemplates;

            _yoloProvider = new YoloModelProvider(CreateCombatYoloDetector, _logService);
            _logService.OnLogReceived += (msg) => OnLogReceived?.Invoke(msg);

            //         守护重启避让：引擎在跑 / 正在启停 / 用户 30 秒内手动操作过 → 守护不接管
            _supervisor.ShouldAbortRestart = () =>
                IsEngineRunning
                || _workflowLock.CurrentCount == 0
                || (DateTime.UtcNow - _lastUserEngineActionUtc).TotalSeconds < 30;
        }
        private void LogClickModeIfChanged(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode) || mode == _lastLoggedClickMode) return;
            _lastLoggedClickMode = mode;
            _logService.LogInfo($"[输入] 已启用【{mode}】");
        }
        private ICombatYoloDetector CreateCombatYoloDetector() => new OpenVinoYoloDetectorAdapter(GameConstants.CombatYoloModelPath);
        public YoloModelProvider YoloProvider => _yoloProvider;

        private TriggerContext BuildTriggerContext(IntPtr hwnd) => new()
        {
            Automation = this,
            Log = _logService,
            Config = _configService,
            Input = _inputService,
            NteInteraction = _nteInteractionService,
            Router = _router,
            Hwnd = hwnd,
            StoryTemplates = _storyTemplateService,
            YoloProvider = _yoloProvider,
            LegacyCoordMapper = (lx, ly) => MapLogical(lx, ly),
        };

        public (int Px, int Py) MapLogical(int lx, int ly) => _coordMapper.LogicalToClient(CurrentHwnd, lx, ly);
        public void ClickLogical(IntPtr hwnd, int lx, int ly, bool isLeft = true)
        {
            var (px, py) = _coordMapper.LogicalToClient(hwnd, lx, ly);
            _nteInteractionService.NteClick(hwnd, px, py, isLeft);
        }

        public void EnableCombatTriggers()
        {
            try { if (_configService.Load().AutoCombatEnabled) _yoloProvider.EnsureStartLoading(); } catch { }
            _triggerManager.EnableCombatTriggers(_logService);
        }

        public Mat? GetLatestFrame() { lock (_frameLock) return _latestFrame?.Clone(); }

        public bool TryGetClientRegion(out int x, out int y, out int w, out int h, out double dpiScale)
        {
            x = y = w = h = 0; dpiScale = 1.0;
            if (CurrentHwnd == IntPtr.Zero) return false;
            if (!Win32Input.GetClientRect(CurrentHwnd, out var rc)) return false;
            var pt = new Win32Input.POINT { X = 0, Y = 0 };
            if (!Win32Input.ClientToScreen(CurrentHwnd, ref pt)) return false;
            x = pt.X; y = pt.Y; w = rc.Right - rc.Left; h = rc.Bottom - rc.Top;
            dpiScale = Win32Input.GetDpiForWindow(CurrentHwnd) / 96.0;
            return w > 0 && h > 0;
        }

        public void Start()
        {
            //         记录用户操作时间（守护避让依据）
            _lastUserEngineActionUtc = DateTime.UtcNow;

            // 引擎已绑定窗口、正常运行中 → 忽略重复启动
            if (IsEngineRunning && CurrentHwnd != IntPtr.Zero)
            {
                _logService.LogInfo("[引擎] 已在运行，忽略重复启动请求。");
                return;
            }

            // [热键卡死] 锁被占 = 引擎正在启动或收尾中。
            // 绝不在调用线程（UI 线程）上等待：原 _workflowTask.Wait(3s) 会与
            // 工作流 finally 里的 Dispatcher.Invoke 形成短暂死锁，直到 3 秒超时才解开，
            // 表现为按热键后 UI 冻结 3 秒。现在直接忽略，稍后可再按。
            if (!_workflowLock.Wait(0))
            {
                _logService.LogWarning("[引擎] 正在启动或停止中，已忽略本次启动请求（稍后可再试）。");
                return;
            }

            // 锁已持有；由 _workflowTask 的 finally 负责释放（原救援路径已删除——
            // 它只在"锁永久泄漏"的病态场景才有意义，代价却是每次都赌 3 秒 UI 冻结）
            _captureEverStarted = true;
            EnsureIdleGuardStarted();

            var config = _configService.Load();
            _cts = new CancellationTokenSource();
            _dispatchCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            var token = _cts.Token;

            OnRunningStateChanged?.Invoke(true);

            _workflowTask = Task.Run(async () =>
            {
                try
                {
                    await WorkflowLoopAsync(token);
                }
                catch (OperationCanceledException)
                {
                    _logService.Log("自动化任务已手动停止。");
                }
                catch (Exception ex)
                {
                    _logService.LogError($"自动化任务发生错误: {ex.Message}");
                }
                finally
                {
                    if (_engineStartUtc != DateTime.MinValue)
                    {
                        var run = DateTime.UtcNow - _engineStartUtc;
                        string dur = $"{(int)run.TotalHours}:{run.Minutes:D2}:{run.Seconds:D2}";
                        _logService.LogInfo($"[引擎] 本次运行 {dur} | 处理 {Interlocked.Read(ref _frameCount):N0} 帧 | 已安全收摊。");
                        _engineStartUtc = DateTime.MinValue;
                    }

                    _gameService.StopCapture();
                    _dispatchCts?.Cancel();
                    await WaitForBizIdleAsync(TimeSpan.FromSeconds(1.5));
                    try { _router.ReleaseAllHeldKeys(CurrentHwnd); } catch { }
                    _workflowLock.Release();
                    CurrentHwnd = IntPtr.Zero;
                    OnRunningStateChanged?.Invoke(false);
                    _triggerManager.DisposeAll();
                    lock (_frameLock)
                    {
                        _latestFrame?.Dispose();
                        _latestFrame = null;
                    }
                    _dispatchCts?.Dispose();
                    _dispatchCts = null;
                }
            });
        }


        private void EnsureIdleGuardStarted()
        {
            if (Interlocked.Exchange(ref _idleGuardStarted, 1) == 1) return;
            _ = Task.Run(() => IdleGuardLoopAsync(_idleGuardCts.Token));
        }

        public void Stop()
        {
            //         记录用户操作时间（守护避让依据）
            _lastUserEngineActionUtc = DateTime.UtcNow;

            _logService.Log("正在停止自动化引擎...");
            _cts?.Cancel();
            _dispatchCts?.Cancel();
        }

        // 专门用于程序彻底退出时的清理，连幽灵守护线程一起杀
        public void ShutdownForExit()
        {
            try { _idleGuardCts.Cancel(); } catch { }
            try { _cts?.Cancel(); } catch { }
            try { _dispatchCts?.Cancel(); } catch { }
        }
        public void WaitForExit(int timeoutMs)
        {
            var t = _workflowTask;
            if (t == null) return;
            try { t.Wait(timeoutMs); } catch { }
        }

        private async Task WaitForBizIdleAsync(TimeSpan budget)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < budget)
            {
                if (Volatile.Read(ref _isBizRunning) == 0 && Volatile.Read(ref _isUiRunning) == 0 && Volatile.Read(ref _isCombatDebugRunning) == 0) return;
                await Task.Delay(20);
            }
        }

        private async Task IdleGuardLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token);
                    if (!_captureEverStarted) continue;
                    if (!_configService.Load().IdleGuardEnabled) continue;

                    if (IsEngineRunning || TaskRuntimeRegistry.Count > 0) continue;
                    // 30 秒内手动 Start/Stop 过 → 守护静默。
                    // 否则用户刚点停止、引擎收尾完成，IdleGuard 1 秒内接管巡检，
                    // 看到进入游戏/重连按钮就动真鼠标——"我都叫停了它还在点"。
                    // 复用 ShouldAbortRestart 同款 30 秒约定。
                    if ((DateTime.UtcNow - _lastUserEngineActionUtc).TotalSeconds < 30) continue;

                    var cfg = _configService.Load();

                    if (!Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).Any())
                    {
                        //  从未绑定过游戏窗口 → 不存在"崩溃"。
                        //  原逻辑会把"游戏还没启动（如启动器更新中）+ 引擎已停止"误判为游戏崩溃，
                        //  触发不受停止控制的幽灵重启，还会替用户点启动器按钮。
                        if (!_everBoundGame) continue;
                        if (cfg.AutoRestartOnCrash) await _supervisor.RestartAfterCrashAsync(cfg, token, fromIdleGuard: true);
                        continue;
                    }

                    if (_triggerManager.ExtraBusyCheck?.Invoke() ?? false) continue;

                    var hwnd = CurrentHwnd != IntPtr.Zero ? CurrentHwnd : FindGameWindowQuiet();
                    if (hwnd == IntPtr.Zero) continue;

                    Mat? frame;
                    if (!_captureGate.Wait(0)) continue;
                    try { frame = _gameService.CaptureWindowOf(hwnd); }
                    finally { _captureGate.Release(); }

                    if (frame == null || frame.Empty()) continue;
                    // ↓ 新增：与主循环同款归一化，IdleGuard 的 1080P ROI/模板才能工作
                    if (Win32Input.GetClientRect(hwnd, out var crc) && crc.Right > crc.Left)
                        _nteRenderCtx.UpdateNteContext(crc.Right - crc.Left, crc.Bottom - crc.Top);
                    var synced = _nteRenderCtx.SyncNteRenderFrame(frame);
                    frame.Dispose();
                    frame = synced;
                    try { await _supervisor.InspectIdleFrameAsync(frame, hwnd, cfg, token); }
                    finally { frame.Dispose(); }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private IntPtr FindGameWindowQuiet()
        {
            var p = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();
            return p == null ? IntPtr.Zero : _gameService.FindWindowForProcess(p.Id, GameConstants.GameWindowClass);
        }

        private async Task WorkflowLoopAsync(CancellationToken token)
        {
            var config = _configService.Load();
            IntPtr hwnd = config.StartGameTogether ? await _supervisor.RunStartupFlowAsync(config, token) : await _gameService.WaitForGameWindowAsync(token);

            if (token.IsCancellationRequested) return;
            if (hwnd == IntPtr.Zero)
            {
                if (token.IsCancellationRequested) return;
                _logService.LogError("❌ 没有找到异环游戏窗口！");
                if (!App.IsAutoStartCommand)
                {
                    Application.Current?.Dispatcher.Invoke(() => new StartErrorWindow { Owner = Application.Current.MainWindow }.ShowDialog());
                }
                return;
            }

            CurrentHwnd = hwnd;
            _engineStartUtc = DateTime.UtcNow;              //会话计时起点
            Interlocked.Exchange(ref _frameCount, 0);
            _lastHeartbeatUtc = _engineStartUtc;            //  心跳起点
            _heartbeatBaseFrames = 0;
            _everBoundGame = true; // 已真正绑定游戏窗口，此后游戏消失才允许守护判定为崩溃
            _logService.Log("成功绑定异环窗口句柄");
            if (Win32Input.GetClientRect(hwnd, out var initialRc))
            {
                _lastTrackedW = initialRc.Right - initialRc.Left;
                _lastTrackedH = initialRc.Bottom - initialRc.Top;
                _lastTrackedDpi = Win32Input.GetDpiForWindow(hwnd) / 96.0;
                _logService.LogInfo($"[NteCore] 已绑定渲染窗口 0x{hwnd.ToInt64():X} | 视口尺寸: {_lastTrackedW}x{_lastTrackedH} | 缩放DPI: {_lastTrackedDpi:F2}");
            }

            
            LogClickModeIfChanged(config.MouseDriveType);

            Application.Current?.Dispatcher.Invoke(() => OnWindowBound?.Invoke());

            int bootInterval = int.TryParse(config.TriggerInterval, out int bootMs) ? Math.Clamp(bootMs, 10, 2000) : 50;
            _gameService.StartCapture(hwnd, config.CaptureMode, bootInterval);

            _triggerManager.InitializeAll(BuildTriggerContext(hwnd));

            while (!token.IsCancellationRequested)
            {
                var cfgNow = _configService.Load();
                LogClickModeIfChanged(cfgNow.MouseDriveType);

                if (!Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).Any())
                {
                    if (!cfgNow.AutoRestartOnCrash) { _logService.LogWarning("⚠️ 检测到游戏已退出，自动停止截图引擎。"); break; }
                    _gameService.StopCapture();
                    var newHwnd = await _supervisor.RestartAfterCrashAsync(cfgNow, token);
                    if (newHwnd is IntPtr nh)
                    {
                        CurrentHwnd = nh;
                        hwnd = nh;

                        // 崩溃重启后重新绑定并打印参数
                        if (Win32Input.GetClientRect(nh, out var rrc))
                        {
                            _lastTrackedW = rrc.Right - rrc.Left;
                            _lastTrackedH = rrc.Bottom - rrc.Top;
                            _lastTrackedDpi = Win32Input.GetDpiForWindow(nh) / 96.0;
                            _logService.LogInfo($"[NteCore] 崩溃重启，重新绑定 0x{nh.ToInt64():X} | 视口 {_lastTrackedW}x{_lastTrackedH} | DPI {_lastTrackedDpi:F2}");
                        }

                        int restartInterval = int.TryParse(cfgNow.TriggerInterval, out int rMs) ? Math.Clamp(rMs, 10, 2000) : 50;
                        _gameService.StartCapture(nh, cfgNow.CaptureMode, restartInterval);

                        _triggerManager.DisposeAll();
                        _triggerManager.InitializeAll(BuildTriggerContext(nh));
                        continue;
                    }
                    break;
                }

                int currentInterval = int.TryParse(cfgNow.TriggerInterval, out int parsedMs) ? Math.Clamp(parsedMs, 10, 2000) : 50;

                _gameService.UpdateCaptureInterval(currentInterval);
                // 心跳：每 60 秒播报区间 FPS；截图通道死了会喊 0.0，一眼识破
                var utcNow = DateTime.UtcNow;
                if ((utcNow - _lastHeartbeatUtc).TotalSeconds >= 60)
                {
                    long frames = Interlocked.Read(ref _frameCount);
                    double fps = (frames - _heartbeatBaseFrames) / (utcNow - _lastHeartbeatUtc).TotalSeconds;
                    _logService.LogInfo($"[引擎] 心跳 | 区间 {fps:F1} FPS | 累计 {frames:N0} 帧");
                    _lastHeartbeatUtc = utcNow;
                    _heartbeatBaseFrames = frames;
                }


                try
                {
                    using var mat = _gameService.CaptureWindow();
                    if (mat != null && !mat.Empty())
                    {
                        Interlocked.Increment(ref _frameCount);// 帧计数
                        long frameAge = _gameService.GetCaptureFrameAgeMs();
                        if (frameAge > Math.Max(currentInterval * 5, 500))
                        {
                            if ((DateTime.Now - _lastStaleWarnTime).TotalSeconds > 5)
                            {
                                _lastStaleWarnTime = DateTime.Now;
                                _logService.LogWarning($"[截图] 画面已 {frameAge}ms 未更新，截图通道疑似卡死，执行单发兜底");
                            }
                            _gameService.ForceSingleShot();
                        }

                        Mat processMat = mat.Clone();
                        try
                        {
                            // 实时追踪窗口尺寸和 DPI，并同步给归一化上下文
                            if (Win32Input.GetClientRect(CurrentHwnd, out var crc) && crc.Right > crc.Left)
                            {
                                int w = crc.Right - crc.Left;
                                int h = crc.Bottom - crc.Top;
                                double dpi = Win32Input.GetDpiForWindow(CurrentHwnd) / 96.0;

                                bool changed = w != _lastTrackedW || h != _lastTrackedH || Math.Abs(dpi - _lastTrackedDpi) > 0.01;
                                if (changed)
                                {
                                    if (_lastTrackedW > 0 && (DateTime.Now - _lastResizeLogAt).TotalMilliseconds > 1000)
                                    {
                                        _logService.LogInfo($"[NteCore] 检测到窗口变化: 视口 {_lastTrackedW}x{_lastTrackedH} → {w}x{h} | DPI {_lastTrackedDpi:F2} → {dpi:F2} | 归一化系数已实时同步");
                                        _lastResizeLogAt = DateTime.Now;

                                        // 16:9 警告
                                        double aspect = (double)w / h;
                                        if (Math.Abs(aspect - 16.0 / 9.0) > 0.05)
                                        {
                                            _logService.LogWarning($"[NteCore] 当前视口 {w}x{h} 宽高比偏离 16:9，模板匹配可能失准，建议恢复 16:9 分辨率！");
                                        }
                                    }
                                    _lastTrackedW = w;
                                    _lastTrackedH = h;
                                    _lastTrackedDpi = dpi;
                                }

                                // 必须每帧更新，不能放进 changed 里，确保归一化系数绝对实时
                                _nteRenderCtx.UpdateNteContext(w, h);
                            }
                            else
                            {
                                _nteRenderCtx.UpdateNteContext(processMat.Width, processMat.Height);
                            }

                            Mat syncedMat = _nteRenderCtx.SyncNteRenderFrame(processMat);
                            processMat.Dispose();
                            processMat = syncedMat;

                            await _supervisor.InspectGameFrameAsync(processMat, hwnd, cfgNow, token);

                            lock (_frameLock) { _latestFrame?.Dispose(); _latestFrame = processMat.Clone(); }

                            if (IsPreviewWindowOpen && OnFrameCaptured != null && Interlocked.Exchange(ref _isUiRunning, 1) == 0)
                            {
                                var uiMat = processMat.Clone();
                                var dispatchToken = _dispatchCts!.Token;
                                _ = Task.Run(() =>
                                {
                                    try { dispatchToken.ThrowIfCancellationRequested(); _triggerManager.DrawDebug(uiMat); OnFrameCaptured?.Invoke(ImageHelper.MatToBitmapSource(uiMat)); }
                                    catch (OperationCanceledException) { }
                                    finally { uiMat.Dispose(); Interlocked.Exchange(ref _isUiRunning, 0); }
                                });
                            }

                            if (cfgNow.IsCombatDebugWindowOpen && OnCombatDebugFrameCaptured != null && Interlocked.Exchange(ref _isCombatDebugRunning, 1) == 0)
                            {
                                var debugMat = processMat.Clone();
                                var dispatchToken = _dispatchCts!.Token;
                                _ = Task.Run(() =>
                                {
                                    // YOLO 战斗视角只画战斗触发器
                                    try { dispatchToken.ThrowIfCancellationRequested(); _triggerManager.DrawCombatDebug(debugMat); OnCombatDebugFrameCaptured?.Invoke(ImageHelper.MatToBitmapSource(debugMat)); }
                                    catch (OperationCanceledException) { }
                                    finally { debugMat.Dispose(); Interlocked.Exchange(ref _isCombatDebugRunning, 0); }
                                });
                            }

                            if (Interlocked.Exchange(ref _isBizRunning, 1) == 0)
                            {
                                var bizMat = processMat.Clone();
                                var dispatchToken = _dispatchCts!.Token;
                                _ = Task.Run(() =>
                                {
                                    try { dispatchToken.ThrowIfCancellationRequested(); _triggerManager.Dispatch(bizMat, 1.0, dispatchToken); }
                                    catch (OperationCanceledException) { }
                                    catch (Exception ex) { _logService.LogError($"业务识别异常: {ex.Message}"); }
                                    finally { bizMat.Dispose(); Interlocked.Exchange(ref _isBizRunning, 0); }
                                });
                            }
                        }
                        finally { processMat.Dispose(); }
                    }
                }
                catch (OperationCanceledException) { throw; } // 手动停止不算异常，交外层按正常停止收尾
                catch (Exception ex) { _logService.LogError($"主循环异常: {ex.Message}"); }
                await Task.Delay(currentInterval, token);
            }
        }
    }
}