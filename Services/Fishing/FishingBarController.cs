using System;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Fishing
{
    public class FishingBarController : IDisposable
    {
        // ==================== 物理参数 ====================
        private const double DefaultAccelPerTick = 2.2;
        private const double DefaultCoastRetention = 0.90;

        //         4：前瞻补一帧（1 -> 2，覆盖截屏+识别+驱动真实延迟，防冲过头）
        private const int InputLatencyTicks = 2;

        private const int TickMs = 30;
        private const bool ImpulseMode = false;

        // ==================== 控制参数 ====================
        private const int DeadZonePx = 10;
        private const double StopSpeedEps = 1.2;
        private const double VelocityAlphaB = 0.40;
        private const double VelocityAlphaA = 0.50;
        private const int GreenSafeHalfWidth = 55;
        private const int FarFullSpeedPx = 90;
        private const double BackoffTolerance = 2.5;
        private const double VTargetTolerance = 0.8;
        private const int SpikeGuardPx = 200;
        private const double BrakeDamping = 1.3;
        private const int MinDetectPixelsB = 8;

        private const int MissTolerance = 5;

        private const int MinPulseMs = 10;
        private const int MaxPulseMs = 55;
        private const int PulseGapMs = 18;
        private const int MinSwitchIntervalMs = 55;

        private const double GainEma = 0.07;
        private const double CoastEma = 0.05;

        // ==================== 补丁 A：A 失明容忍参数 ====================
        private int _aLostStreak;
        private double _virtualAX;
        private const int ALostMemoryFrames = 15;

        // ==================== 补丁 B：无响应看门狗参数 ====================
        private bool _passiveMode;
        private double _passiveAnchorBx;
        private int _passiveHits;
        private int _actuateStreak;
        private double _actuateAnchorBx;
        private const int ActuateProbeFrames = 6;
        private const double NoResponsePx = 2.0;
        private const double LifeSignPx = 5.0;

        // ==================== 遥测与护栏 ====================
        private readonly ILogService _log;
        private string _lastLoggedMode = "-";
        private DateTime _lastTelemetryAt = DateTime.MinValue;
        private const int TelemetryIntervalMs = 1000;

        private DateTime _passiveSince;
        private const int PassiveMaxMs = 2500;
        private const double PassiveEntryMaxErrPx = 120;
        private const double FishLifeSignV = 1.5;

        private string Tag => $"[自动钓鱼][{_router.CurrentDrive.ToConfigString()}]";

        // ==================== 运行态 ====================
        private readonly object _gate = new();
        private double _estVA, _estVB;
        private int _lastAX = int.MinValue, _lastBX = int.MinValue;
        private double _gain = DefaultAccelPerTick;
        private double _r = DefaultCoastRetention;
        private int _activeDir, _activeVk;
        private int _holdStreak;
        private int _coastStreak;
        private int _starvedStreak;
        private DateTime _busyUntil = DateTime.MinValue;
        private DateTime _lastSwitch = DateTime.MinValue;
        private string _lastMode = "-";
        private string _dbg = "";
        private double _prevVcForCoast, _prevVcForHold;

        private int _pressGen;

        private readonly System.Threading.Timer? _watchdog;
        private DateTime _lastTickAtUtc = DateTime.UtcNow;
        private const int HeartbeatTimeoutMs = 400;
        private const int WatchdogPeriodMs = 150;

        private readonly IInputRouter _router;
        private readonly IAutomationContext _automationService;

        public FishingBarController(
            IInputRouter router,
            IAutomationContext automationService,
            ILogService logService)
        {
            _router = router;
            _automationService = automationService;
            _log = logService;


            _watchdog = new System.Threading.Timer(WatchdogTick, null, WatchdogPeriodMs, WatchdogPeriodMs);
        }

        private double CoastFactor => _r / (1 - _r);
        private double VMax => _gain * _r / (1 - _r);

        public void Reset()
        {
            lock (_gate)
            {
                _lastTickAtUtc = DateTime.UtcNow; 
                ReleaseInternal();
                _busyUntil = DateTime.MinValue;

                _aLostStreak = 0;
                _virtualAX = 0;
                _lastAX = _lastBX = int.MinValue;
                _passiveMode = false;
                _passiveHits = 0;
                _actuateStreak = 0;
                _prevVcForCoast = _prevVcForHold = 0;
                _lastMode = "-";
                _dbg = "";
                _lastLoggedMode = "-";

                _starvedStreak = 0;
                _holdStreak = _coastStreak = 0;
                _estVA = _estVB = 0;
            }
        }

        public void Tick(FishingRoiAnalyzer.ColorRectResult barA, FishingRoiAnalyzer.ColorRectResult barB, AutoFishingConfig config, int barMissCount)
        {
            lock (_gate)
            {
                _lastTickAtUtc = DateTime.UtcNow; 
                LogTelemetry();

                bool busy = DateTime.UtcNow < _busyUntil && _lastMode != "HOLD";

                bool aOk = barA.Rect.HasValue;
                bool bOk = barB.Rect.HasValue;

                if (aOk)
                {
                    _aLostStreak = 0;
                    _virtualAX = barA.CenterX;
                }
                else if (++_aLostStreak <= ALostMemoryFrames)
                {
                    _virtualAX += _estVA;
                }
                else
                {
                    Release();
                    if (barMissCount > MissTolerance) ResetCalibration();
                    else _estVA *= 0.5;
                    return;
                }

                if (!bOk)
                {
                    if (barMissCount > MissTolerance) { Release(); ResetCalibration(); }
                    else { _estVB *= 0.5; }
                    return;
                }

                int aX = (int)Math.Round(_virtualAX);
                int bX = barB.CenterX;

                if (_lastBX != int.MinValue && barB.PixelCount >= MinDetectPixelsB)
                {
                    int raw = bX - _lastBX;
                    _estVB = Math.Abs(raw) > SpikeGuardPx ? 0 : VelocityAlphaB * raw + (1 - VelocityAlphaB) * _estVB;
                }
                if (_lastAX != int.MinValue)
                {
                    int raw = aX - _lastAX;
                    _estVA = Math.Abs(raw) > SpikeGuardPx ? 0 : VelocityAlphaA * raw + (1 - VelocityAlphaA) * _estVA;
                }
                _lastAX = aX; _lastBX = bX;

                double vC = _estVB;
                double vT = _estVA;
                int e = bX - aX;

                if (_passiveMode)
                {
                    _lastBX = bX;
                    bool lifeByCursor = Math.Abs(bX - _passiveAnchorBx) > LifeSignPx;
                    bool lifeByFish = Math.Abs(_estVA) > FishLifeSignV;
                    bool lifeByTimeout = (DateTime.UtcNow - _passiveSince).TotalMilliseconds > PassiveMaxMs;

                    if (lifeByCursor || lifeByFish || lifeByTimeout)
                    {
                        _passiveMode = false; _passiveHits = 0; _actuateStreak = 0;
                        _estVB = _estVA = 0;
                        _lastMode = lifeByTimeout && !lifeByCursor && !lifeByFish ? "RESUME(超时兜底)" : "RESUME(咬钩)";
                    }
                    else
                    {
                        Release();
                        _lastMode = "PASSIVE(等咬钩)";
                        return;
                    }
                }

                if (busy) return;

                if (_activeDir != 0)
                {
                    if (_actuateStreak == 0) _actuateAnchorBx = bX;
                    _actuateStreak++;

                    bool noResponse = _actuateStreak >= ActuateProbeFrames
                        && Math.Abs(bX - _actuateAnchorBx) < NoResponsePx
                        && Math.Abs(_estVB) < 0.5
                        && Math.Abs(e) < PassiveEntryMaxErrPx;

                    if (noResponse)
                    {
                        if (++_passiveHits >= 2)
                        {
                            _passiveMode = true;
                            _passiveAnchorBx = bX;
                            _passiveSince = DateTime.UtcNow;
                            Release();
                            ResetCalibration();
                            _lastMode = "PASSIVE(无响应)";
                            return;
                        }
                    }
                    else if (Math.Abs(bX - _actuateAnchorBx) >= NoResponsePx || Math.Abs(e) >= PassiveEntryMaxErrPx)
                    {
                        _passiveHits = 0;
                    }
                }
                else
                {
                    _actuateStreak = 0;
                    _passiveHits = 0;
                }

                UpdateCalibration(vC);

                if (Math.Abs(e) <= DeadZonePx && Math.Abs(vC - vT) <= StopSpeedEps)
                {
                    Release(); _holdStreak = 0; _coastStreak++; _lastMode = "STOP"; return;
                }

                double eFuture = e + (vC - vT) * InputLatencyTicks;
                double targetErr = 0;
                double eStopNow = eFuture + (vC - vT) * CoastFactor;
                if (Math.Abs(eStopNow) > GreenSafeHalfWidth) targetErr = Math.Sign(eStopNow) * GreenSafeHalfWidth;

                double vSet = vT + (targetErr - eFuture) / CoastFactor;
                double limit = VMax;
                bool starving = Math.Abs(vSet) > VMax && Math.Sign(vC - vT) == Math.Sign(e);
                if (starving) _starvedStreak++; else _starvedStreak = 0;
                if (_starvedStreak > 5) limit = VMax * 0.85;
                vSet = Math.Clamp(vSet, -limit, limit);

                double vErr = vSet - vC;
                _dbg = $"e={e} vC={vC:F1} vT={vT:F1} vSet={vSet:F1} g={_gain:F2} r={_r:F2}";

                if (Math.Abs(vErr) <= VTargetTolerance)
                {
                    Release(); _holdStreak = 0; _coastStreak++; _lastMode = "COAST"; return;
                }

                bool decel = vC != 0 && Math.Sign(vErr) != Math.Sign(vC);
                if (decel && Math.Abs(vErr) <= BackoffTolerance)
                {
                    Release(); _holdStreak = 0; _coastStreak++; _lastMode = "BACKOFF"; return;
                }

                int dir = Math.Sign(vErr);
                int vk = dir < 0 ? config.LeftKey : config.RightKey;

                if (!decel && (Math.Abs(eFuture) > FarFullSpeedPx || Math.Abs(vSet) >= 0.85 * VMax))
                {
                    HoldDown(vk, dir, urgent: true);
                    _holdStreak++; _coastStreak = 0;
                    _lastMode = "HOLD"; return;
                }

                double pulseTicks = Math.Abs(vErr) / _gain;
                if (decel) pulseTicks *= BrakeDamping;
                int holdMs = ImpulseMode ? MinPulseMs : (int)Math.Clamp(pulseTicks * TickMs, MinPulseMs, MaxPulseMs);

                SendPulse(vk, dir, holdMs, urgent: decel);
                _holdStreak = 0; _coastStreak = 0;
                _lastMode = decel ? $"BRAKE({holdMs}ms)" : $"PWM({holdMs}ms)";
            }
        }

        private DateTime _lastRealLogAt = DateTime.MinValue;

        private void LogTelemetry()
        {
            if (_lastMode == "-") { _lastLoggedMode = "-"; return; }
            var modeKey = _lastMode.Split('(')[0];
            bool changed = modeKey != _lastLoggedMode;

            if ((DateTime.UtcNow - _lastRealLogAt).TotalMilliseconds < 500) return;
            if (!changed && (DateTime.UtcNow - _lastTelemetryAt).TotalMilliseconds < TelemetryIntervalMs) return;

            _log.LogInfo(changed ? $"{Tag} 控鱼 -> {_lastMode} | {_dbg}" : $"{Tag} 心跳 {_lastMode} | {_dbg}");
            _lastLoggedMode = modeKey;
            _lastTelemetryAt = DateTime.UtcNow;
            _lastRealLogAt = DateTime.UtcNow;
        }

        private void UpdateCalibration(double vC)
        {
            if (_activeDir == 0 && _coastStreak >= 1)
            {
                double vPrev = _prevVcForCoast;
                _prevVcForCoast = vC;
                if (Math.Abs(vPrev) > 2.5 && Math.Abs(vC) > 0.3)
                {
                    double rs = vC / vPrev;
                    if (rs > 0.60 && rs < 0.98) _r = (1 - CoastEma) * _r + CoastEma * rs;
                }
                return;
            }
            _prevVcForCoast = vC;
            if (_activeDir != 0 && _holdStreak >= 2)
            {
                double dv = (vC - _prevVcForHold) * _activeDir;
                _prevVcForHold = vC;
                if (dv > 0.1 && dv < _gain * 3) _gain = (1 - GainEma) * _gain + GainEma * (dv / _r);
            }
            _prevVcForHold = vC;
        }

        private void ResetCalibration()
        {
            _gain = DefaultAccelPerTick;
            _r = DefaultCoastRetention;
            _estVA = _estVB = 0;
            _starvedStreak = 0;
            _holdStreak = _coastStreak = 0;
        }

        public void Release()
        {
            lock (_gate)
            {
                ReleaseInternal();
            }
        }

        public string GetDebugSnapshot() => $"{_lastMode} | {_dbg} | {Tag}";

        private void HoldDown(int vk, int dir, bool urgent)
        {
            if (_activeDir == dir && _activeVk == vk) return;
            if (!urgent && _activeDir != 0 && (DateTime.UtcNow - _lastSwitch).TotalMilliseconds < MinSwitchIntervalMs) return;

            ReleaseInternal();
            _activeDir = dir; _activeVk = vk;
            _lastSwitch = DateTime.UtcNow;
            _busyUntil = DateTime.MinValue;

            _router.KeyDown(_automationService.CurrentHwnd, vk);
        }

        //         2：按键同步下发 + 失败进日志 + 代际拦截
        private void SendPulse(int vk, int dir, int holdMs, bool urgent)
        {
            if (!urgent && _activeDir != 0 && dir != _activeDir && (DateTime.UtcNow - _lastSwitch).TotalMilliseconds < MinSwitchIntervalMs) return;

            ReleaseInternal();
            _activeDir = dir; _activeVk = vk;
            _lastSwitch = DateTime.UtcNow;
            _busyUntil = DateTime.UtcNow.AddMilliseconds(holdMs + PulseGapMs);

            try
            {
                // 当帧立即按下，不再交给线程池
                _router.KeyDown(_automationService.CurrentHwnd, vk);
            }
            catch (Exception ex)
            {
                _log.LogError($"{Tag} KeyDown 失败: {ex.Message}");
                _activeDir = 0; _activeVk = 0;
                return;
            }

            int gen = Volatile.Read(ref _pressGen); //     原子读取
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(holdMs);
                    // 如果代际没变，才允许抬起
                    if (gen == Volatile.Read(ref _pressGen))
                        _router.KeyUp(_automationService.CurrentHwnd, vk);
                }
                catch (Exception ex)
                {
                    _log.LogError($"{Tag} KeyUp 失败: {ex.Message}");
                }
            });
        }

        private void ReleaseInternal()
        {
            Interlocked.Increment(ref _pressGen); 
            if (_activeDir == 0) return;
            int vk = _activeVk;
            _activeDir = 0; _activeVk = 0;

            _router.KeyUp(_automationService.CurrentHwnd, vk);
        }

        private void WatchdogTick(object? state)
        {
            bool kicked = false;
            lock (_gate)
            {
                // 如果当前有键按着，且超过 400ms 没有收到 Tick 喂狗，说明主循环卡死了
                kicked = _activeDir != 0 && (DateTime.UtcNow - _lastTickAtUtc).TotalMilliseconds > HeartbeatTimeoutMs;
                if (kicked)
                {
                    ReleaseInternal();
                    _lastMode = "WATCHDOG";
                }
            }
            if (kicked)
                _log.LogWarning($"{Tag} [看门狗] 主循环心跳丢失>{HeartbeatTimeoutMs}ms，已强制抬键保护");
        }

        public void Dispose()
        {
            _watchdog?.Change(Timeout.Infinite, Timeout.Infinite);
            _watchdog?.Dispose();
            Release();
        }
    }
}