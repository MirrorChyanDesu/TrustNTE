using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models;
using TrustNTE.Services.Game;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    public enum SupervisorState { Idle, Launching, Updating, Running, Reconnecting, Restarting, Failed }

    public class GameSupervisorService
    {
        private const float LauncherDesignWidth = 1280f;
        private static readonly Rect LauncherBtnRoi = new(987, 614, 44, 42);
        private static readonly Rect LauncherTextRoi = new(1081, 611, 109, 51);
        private const float BtnOffsetX = 100f;
        private const int MaxRestartAttempts = 3;
        private const int RestartDelaySec = 15;

        private static readonly Rect RetryRoi = new(920, 686, 81, 45);
        private static readonly Rect EnterGameRoi = new(898, 912, 124, 41);
        private static readonly Rect UidRoi = new(1, 1060, 34, 20);

        private const string LauncherClass = "Qt51517QWindowOwnDC";
        private const string LauncherTitle = "NTEGame";

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd); // [A7] 句柄有效性检查

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private readonly ILogService _log;
        private readonly IInputRouter _router;
        private readonly IGameService _gameService;
        private readonly NteGameStateTemplateService _tpl;
        private readonly ScreenCapturer _screen = new();
        private readonly NteRenderContextManager _renderCtx;

        public SupervisorState State { get; private set; } = SupervisorState.Idle;
        public event Action<SupervisorState>? OnStateChanged;

        private bool _inGameConfirmed;
        private int _restartCount;
        private DateTime _lastRetryClick = DateTime.MinValue;
        private DateTime _lastEnterClick = DateTime.MinValue;
        private int _inspecting;

        // 监管器记住的最新游戏句柄。崩溃重启后调用方手里的句柄是旧的，
        // 巡检时用它保鲜，防止 ClientToScreen 对死句柄静默失败导致乱点（点击落在显示器坐标）。
        private volatile IntPtr _latestGameHwnd;

        private TaskRuntimeRegistry.ITaskRuntime[]? _pendingResume;

        public Action? OnLanesStop { get; set; }

        //守护重启避让检查（由 AutomationService 装配注入）
        public Func<bool>? ShouldAbortRestart { get; set; }

        public GameSupervisorService(ILogService log, IInputRouter router, IGameService gameService,
            NteGameStateTemplateService tpl, NteRenderContextManager renderCtx)
        {
            _renderCtx = renderCtx;
            _log = log; _router = router; _gameService = gameService; _tpl = tpl;
        }

        private void SetState(SupervisorState s) { if (State != s) { State = s; OnStateChanged?.Invoke(s); } }

        private static bool GameRunning() =>
            Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).Any();

        // ================================================================
        //      句柄保鲜：巡检入口统一走这里
        //      优先用监管器记录的最新句柄（重启后它才是新的）；
        //      记录的失效则退回传入值，两者都失效返回 Zero（调用方放弃本次巡检）。
        // ================================================================
        private IntPtr ResolveLiveHwnd(IntPtr incoming)
        {
            var known = _latestGameHwnd;
            if (known != IntPtr.Zero && IsWindow(known)) return known;

            if (incoming != IntPtr.Zero && IsWindow(incoming))
            {
                _latestGameHwnd = incoming; // 记录的死了，传入的活着 → 采纳并更新
                return incoming;
            }
            return IntPtr.Zero; // 都死了：等 RestartAfterCrashAsync 流程处理
        }

        // ================================================================
        //                 启动全流程   
        // ================================================================
        public async Task<IntPtr> RunStartupFlowAsync(AppConfig config, CancellationToken token)
        {
            SetState(SupervisorState.Launching);
            _inGameConfirmed = false;

            if (GameRunning())
            {
                _log.Log("游戏已在运行，跳过启动器流程。");
                var hwnd0 = await _gameService.WaitForGameWindowAsync(token);
                // 拿到窗口立刻记账，后续巡检全部以它为准
                if (hwnd0 != IntPtr.Zero) { _latestGameHwnd = hwnd0; SetState(SupervisorState.Running); }
                return hwnd0;
            }

            // 优先使用 MainWindowHandle，防止类名改变导致找不到窗口
            var launcherProc = Process.GetProcessesByName("NTEGame").FirstOrDefault();
            IntPtr lh = launcherProc != null ? launcherProc.MainWindowHandle : IntPtr.Zero;

            if (lh == IntPtr.Zero && launcherProc != null)
            {
                lh = NativeMethods.FindWindow(LauncherClass, LauncherTitle);
            }

            if (launcherProc != null && lh != IntPtr.Zero)
            {
                _log.Log("检测到启动器在后台运行，唤醒…");
                NativeMethods.ShowWindow(lh, NativeMethods.SwRestore);
                NativeMethods.SetForegroundWindow(lh);
                await Task.Delay(500, token);
            }
            else
            {
                string? path = config.GamePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    path = RegistryHelper.FindLauncherPath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    _log.LogError("❌ 未找到有效启动器路径（请到启动页设置）。");
                    SetState(SupervisorState.Failed);
                    return IntPtr.Zero;
                }
                _log.Log($"正在启动启动器: {path}");
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }

            if (lh == IntPtr.Zero)
            {
                for (int i = 0; i < 120; i++)
                {
                    if (token.IsCancellationRequested) return IntPtr.Zero;
                    await Task.Delay(500, token);

                    launcherProc = Process.GetProcessesByName("NTEGame").FirstOrDefault();
                    if (launcherProc != null && launcherProc.MainWindowHandle != IntPtr.Zero)
                    {
                        lh = launcherProc.MainWindowHandle;
                        break;
                    }
                    lh = NativeMethods.FindWindow(LauncherClass, LauncherTitle);
                    if (lh != IntPtr.Zero) break;
                }
            }

            if (lh == IntPtr.Zero)
            {
                _log.LogError("未能找到启动器窗口！");
                SetState(SupervisorState.Failed);
                return IntPtr.Zero;
            }

            _log.Log($"成功绑定启动器窗口: 0x{lh.ToInt64():X}");

            long startTs = DateTimeOffset.Now.ToUnixTimeSeconds();
            int cooldown = 0;
            bool sizeLogged = false;

            while (!token.IsCancellationRequested)
            {
                if (DateTimeOffset.Now.ToUnixTimeSeconds() - startTs > 600)
                {
                    _log.LogError("启动器流程超时（10 分钟）。");
                    SetState(SupervisorState.Failed);
                    return IntPtr.Zero;
                }

                if (GameRunning())
                {
                    _log.Log("检测到游戏客户端已启动，等待游戏窗口…");
                    var gh = await _gameService.WaitForGameWindowAsync(token);
                    //拿到新窗口立刻记账——重启场景的关键：新句柄从此处进入监管器
                    if (gh != IntPtr.Zero) { _latestGameHwnd = gh; SetState(SupervisorState.Running); _log.LogInfo("[监管器] 游戏窗口已就绪"); }
                    return gh;
                }

                if (NativeMethods.IsIconic(lh) || !NativeMethods.IsWindowVisible(lh))
                {
                    NativeMethods.ShowWindow(lh, NativeMethods.SwRestore);
                    NativeMethods.SetForegroundWindow(lh);
                    await Task.Delay(500, token);
                }

                using Mat? frame = _screen.Capture(lh, "PrintWindow");
                if (frame == null || frame.Empty() || frame.Width < 800) { await Task.Delay(500, token); continue; }

                // 每 5 秒保存一次截图，debug排查时启用即可
                //if (DateTime.Now.Second % 5 == 0)
                //{
                //  Cv2.ImWrite("debug_launcher.png", frame);
                // }

                double s = frame.Width / LauncherDesignWidth;
                if (!sizeLogged)
                {
                    sizeLogged = true;
                    _log.LogInfo($"[监管器] 启动器客户区 {frame.Width}x{frame.Height}，缩放系数 {s:F2}");
                }

                // 计算图标 ROI
                var roi = new Rect((int)(LauncherBtnRoi.X * s), (int)(LauncherBtnRoi.Y * s),
                                   (int)(LauncherBtnRoi.Width * s), (int)(LauncherBtnRoi.Height * s));

                // 计算文字 ROI
                var textRoi = new Rect((int)(LauncherTextRoi.X * s), (int)(LauncherTextRoi.Y * s),
                                       (int)(LauncherTextRoi.Width * s), (int)(LauncherTextRoi.Height * s));

                if (roi.Right > frame.Width || roi.Bottom > frame.Height || textRoi.Right > frame.Width || textRoi.Bottom > frame.Height)
                {
                    await Task.Delay(500, token);
                    continue;
                }

                if (cooldown <= 0)
                {
                    var r = _tpl.MatchAny(frame, roi, "Nte_ContinueDownload");
                    if (r != null)
                    {
                        ClickLauncher(lh, r.Value, s);
                        _log.Log($"[异环启动器][{_router.CurrentDrive.ToConfigString()}]继续下载游戏");
                        cooldown = 5;
                    }
                    else if ((r = _tpl.MatchAny(frame, roi, "Nte_StartUpdate")) != null)
                    {
                        if (!config.AutoUpdateGame) { _log.LogWarning("[异环启动器] 检测到更新，自动更新未开启"); await Task.Delay(3000, token); }
                        else
                        {
                            ClickLauncher(lh, r.Value, s);
                            _log.Log($"[异环启动器][{_router.CurrentDrive.ToConfigString()}]点击更新游戏");
                            SetState(SupervisorState.Updating);
                            await Task.Delay(3000, token);
                            cooldown = 5;
                        }
                    }
                    else if ((r = _tpl.MatchAny(frame, roi, "Nte_StartGame")) != null)
                    {
                        ClickLauncher(lh, r.Value, s);
                        _log.Log($"[异环启动器][{_router.CurrentDrive.ToConfigString()}]点击开始游戏");
                        await Task.Delay(3000, token);
                        cooldown = 5;
                    }
                    else if (_tpl.MatchAny(frame, roi, "Nte_PauseDownload") != null)
                    {
                        _log.Log($"[异环启动器][{_router.CurrentDrive.ToConfigString()}]启动器游戏更新中·····");
                        SetState(SupervisorState.Updating);
                        await Task.Delay(3000, token);
                    }
                    // 文字区域匹配“开始游戏”
                    else if ((r = _tpl.MatchAny(frame, textRoi, "Nte_TextStartGame")) != null)
                    {
                        int cx = r.Value.X + r.Value.Width / 2;
                        int cy = r.Value.Y + r.Value.Height / 2;
                        NativeMethods.SetForegroundWindow(lh);
                        _router.Click(lh, cx, cy, true);
                        _log.Log($"【日志输出】[启动器][{_router.CurrentDrive.ToConfigString()}]点击开始游戏");
                        await Task.Delay(3000, token);
                        cooldown = 5;
                    }
                    // 文字区域匹配“开始更新”
                    else if ((r = _tpl.MatchAny(frame, textRoi, "Nte_TextStartUpdate")) != null)
                    {
                        if (!config.AutoUpdateGame)
                        {
                            _log.LogWarning("[异环启动器] 检测到文字更新，自动更新未开启");
                            await Task.Delay(3000, token);
                        }
                        else
                        {
                            int cx = r.Value.X + r.Value.Width / 2;
                            int cy = r.Value.Y + r.Value.Height / 2;
                            NativeMethods.SetForegroundWindow(lh);
                            _router.Click(lh, cx, cy, true);
                            _log.Log($"【日志输出】[启动器][{_router.CurrentDrive.ToConfigString()}]点击更新游戏");
                            SetState(SupervisorState.Updating);
                            await Task.Delay(3000, token);
                        }
                    }
                }
                if (cooldown > 0) cooldown--;
                await Task.Delay(500, token);
            }
            return IntPtr.Zero;
        }

        private void ClickLauncher(IntPtr lh, Rect matchRectFull, double s)
        {
            int x = matchRectFull.X + matchRectFull.Width / 2 + (int)Math.Round(BtnOffsetX * s);
            int y = matchRectFull.Y + matchRectFull.Height / 2;
            NativeMethods.SetForegroundWindow(lh);
            _router.Click(lh, x, y, true);
        }

        // ================================================================
        //                  引擎循环巡检
        // ================================================================
        public async Task InspectGameFrameAsync(Mat frame, IntPtr gameHwnd, AppConfig config, CancellationToken token)
        {
            if (frame.Empty()) return;
            // 调用方可能传的是重启前的死句柄，换成监管器记录的最新活句柄
            gameHwnd = ResolveLiveHwnd(gameHwnd);
            if (gameHwnd == IntPtr.Zero) return;
            if (Interlocked.Exchange(ref _inspecting, 1) != 0) return;
            try
            {
                if (!_inGameConfirmed && _tpl.MatchAny(frame, UidRoi, "uid", 0.75) != null)
                {
                    _inGameConfirmed = true; _restartCount = 0;
                    SetState(SupervisorState.Running);
                    _log.Log($"[异环UE5][{_router.CurrentDrive.ToConfigString()}]识别到玩家uid成功进入游戏");
                    await ResumePendingTasksAsync(config);
                }

                if (!config.AutoReconnectEnabled) return;

                if ((DateTime.Now - _lastRetryClick).TotalSeconds > 3 &&
                    _tpl.MatchAny(frame, RetryRoi, "Nte_DisconnectReconnect_Retry") is Rect retry)
                {
                    _lastRetryClick = DateTime.Now;
                    _log.Log($"[异环UE5][物理点击]掉线点击重试");
                    SetState(SupervisorState.Reconnecting);

                    SetPendingResume();

                    try { OnLanesStop?.Invoke(); } catch { }

                    await TaskRuntimeRegistry.StopAllAsync(TimeSpan.FromSeconds(5));

                    ForcePhysicalClick(gameHwnd, retry.X + retry.Width / 2, retry.Y + retry.Height / 2);
                    _inGameConfirmed = false;
                }

                if (!_inGameConfirmed && (DateTime.Now - _lastEnterClick).TotalSeconds > 3 &&
                    _tpl.MatchAny(frame, EnterGameRoi, "Nte_EnterGame") is Rect enter)
                {
                    _lastEnterClick = DateTime.Now;
                    _log.Log($"[异环UE5][物理点击]点击进入游戏");
                    ForcePhysicalClick(gameHwnd, enter.X + enter.Width / 2, enter.Y + enter.Height / 2);
                    await Task.Delay(2500, token);
                }
            }
            finally { Interlocked.Exchange(ref _inspecting, 0); }
        }

        // ================================================================
        //              空闲心跳巡检（引擎停机后接管）
        // ================================================================
        public async Task InspectIdleFrameAsync(Mat frame, IntPtr hwnd, AppConfig config, CancellationToken token)
        {
            if (frame.Empty()) return;
            //传入最新句柄放弃已过时句柄
            hwnd = ResolveLiveHwnd(hwnd);
            if (hwnd == IntPtr.Zero) return;

            if (!_inGameConfirmed && _tpl.MatchAny(frame, UidRoi, "uid", 0.75) != null)
            {
                _inGameConfirmed = true; _restartCount = 0;
                SetState(SupervisorState.Running);
                _log.Log($"[守护][异环UE5][{_router.CurrentDrive.ToConfigString()}]识别到玩家uid成功进入游戏");
                await ResumePendingTasksAsync(config);
                return;
            }

            if (!config.AutoReconnectEnabled) return;

            if ((DateTime.Now - _lastRetryClick).TotalSeconds > 3 &&
                _tpl.MatchAny(frame, RetryRoi, "Nte_DisconnectReconnect_Retry") is Rect retry)
            {
                _lastRetryClick = DateTime.Now;
                _log.Log($"[守护][异环UE5][物理点击]掉线点击重试");
                SetState(SupervisorState.Reconnecting);
                ForcePhysicalClick(hwnd, retry.X + retry.Width / 2, retry.Y + retry.Height / 2);
                _inGameConfirmed = false;
                return;
            }

            if (!_inGameConfirmed && (DateTime.Now - _lastEnterClick).TotalSeconds > 3 &&
                _tpl.MatchAny(frame, EnterGameRoi, "Nte_EnterGame") is Rect enter)
            {
                _lastEnterClick = DateTime.Now;
                _log.Log($"[守护][异环UE5][物理点击]点击进入游戏");
                ForcePhysicalClick(hwnd, enter.X + enter.Width / 2, enter.Y + enter.Height / 2);
                await Task.Delay(2500, token);
            }
        }

        private void SetPendingResume()
        {
            var snap = TaskRuntimeRegistry.Snapshot();

            if (snap.Length == 0)
            {
                var kept = Volatile.Read(ref _pendingResume);
                if (kept != null && kept.Length > 0)
                    _log.LogInfo($"[守护] 后续检测无新任务，保留原账本（{kept.Length} 个待恢复）。");
                return;
            }

            var old = Interlocked.Exchange(ref _pendingResume, null)
                      ?? Array.Empty<TaskRuntimeRegistry.ITaskRuntime>();
            var merged = old.Union(snap).ToArray();

            Interlocked.Exchange(ref _pendingResume, merged);
            _log.LogWarning($"[守护] 已记账 {snap.Length} 个中断任务：{string.Join("、", snap.Select(t => t.TaskName))}（账本共 {merged.Length} 个）");
        }

        private TaskRuntimeRegistry.ITaskRuntime[] TakePendingResume() => Interlocked.Exchange(ref _pendingResume, null) ?? Array.Empty<TaskRuntimeRegistry.ITaskRuntime>();

        private async Task ResumePendingTasksAsync(AppConfig config)
        {
            var toResume = TakePendingResume();
            if (toResume.Length == 0) return;
            if (!config.AutoResumeTask)
            {
                _log.LogWarning($"[守护] 已中断 {toResume.Length} 个任务（自动恢复未开启）：{Names(toResume)}");
                return;
            }
            await Task.Delay(5000);
            _log.LogWarning($"[守护] 已回到游戏，自动恢复中断的任务：{Names(toResume)}");
            foreach (var t in toResume)
            {
                try
                {
                    if (t.TryStart()) _log.Log($"[守护] ✅ [{t.TaskName}] 已恢复运行（从任务开头干净重跑）。");
                    else _log.LogInfo($"[守护] [{t.TaskName}] 已在运行或启动失败，跳过。");
                }
                catch (Exception ex) { _log.LogError($"[守护] [{t.TaskName}] 恢复失败: {ex.Message}"); }
            }
        }

        private static string Names(TaskRuntimeRegistry.ITaskRuntime[] ts) => string.Join("、", ts.Select(t => t.TaskName));

        private void ForcePhysicalClick(IntPtr hwnd, int clientX, int clientY)
        {
            if (hwnd == IntPtr.Zero) return;

            // 传入的是 1920×1080 归一化帧上的匹配坐标，先换算成当前客户区物理坐标
            (clientX, clientY) = _renderCtx.TranslateNtePointer(clientX, clientY);

            NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
            Win32Input.KeyDown(KeyCode.Alt);
            NativeMethods.SetForegroundWindow(hwnd);
            Win32Input.KeyUp(KeyCode.Alt);
            Win32Input.FakeForeground(hwnd);
            Thread.Sleep(150);

            var pt = new POINT { X = clientX, Y = clientY };
            //  防盲点保险：坐标换算失败 = 句柄已死。
            // ClientToScreen 失败时 POINT 保持原值，客户端坐标会被当屏幕坐标用 → 乱点。
            // 宁可跳过本次点击（外层有 3s 重试机制会补上），绝不盲打。
            if (!ClientToScreen(hwnd, ref pt))
            {
                _log.LogWarning($"[监管器][物理点击] ClientToScreen 失败（句柄 0x{hwnd.ToInt64():X} 已失效），本次点击跳过");
                return;
            }

            GetCursorPos(out var oldPt);
            try
            {
                SetCursorPos(pt.X, pt.Y);
                Thread.Sleep(50);
                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(100);
            }
            finally
            {
                SetCursorPos(oldPt.X, oldPt.Y);
            }
        }

        public async Task<IntPtr?> RestartAfterCrashAsync(AppConfig config, CancellationToken token, bool fromIdleGuard = false)
        {
            if (++_restartCount > MaxRestartAttempts)
            {
                _log.LogError($"[监管器] 连续 {MaxRestartAttempts} 次重启失败，停止自动重启，请人工处理。");
                SetState(SupervisorState.Failed);
                return null;
            }

            SetState(SupervisorState.Restarting);
            _log.LogWarning($"[监管器] 检测到游戏进程退出，{RestartDelaySec}s 后执行第 {_restartCount}/{MaxRestartAttempts} 次自动重启…");

            SetPendingResume();
            try { OnLanesStop?.Invoke(); } catch { }
            await TaskRuntimeRegistry.StopAllAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(RestartDelaySec * 1000, token);

            // 只有"守护线程"触发的重启才需要
            // 工作流内部触发的重启（游戏运行中崩溃）时引擎本来就在跑，
            // 不能被 IsEngineRunning 条件误杀。
            if (fromIdleGuard && ShouldAbortRestart?.Invoke() == true)
            {
                _log.LogInfo("[监管器] 引擎已由用户接管，取消本次自动重启。");
                return null;
            }

            return await RunStartupFlowAsync(config, token);
        }
    }
}