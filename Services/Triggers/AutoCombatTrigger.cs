using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using TrustNTE.Helpers;
using TrustNTE.Models;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    /// <summary>
    /// 自动战斗触发器（YOLO 纯视觉版）：
    /// - 索敌：仅 YOLO（自训 Nte_RecognizeEnemy 模型，识别怪头顶 Lv 标记），原 Lv 指纹/图集路已删
    /// - 置信度阈值(配置 AutoCombatConfidence)是唯一门槛，无任何二次几何验证
    ///   （旧菱形验证是旧模型遗物，会误杀新模型的横长 Lv 牌框，已删）
    /// - 锁定：鼠标中键单击（MiddleClickLogical，三通道驱动自动路由）
    /// - 终结技：Q 能量条亮度检测，就绪即发（替代固定节奏盲按）
    /// </summary>
    public sealed class AutoCombatTrigger : ITaskTrigger, IDisposable
    {
        public string Name => "自动战斗";
        public bool IsEnabled { get; set; }

        // ===== 依赖 =====
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly IInputRouter _router;
        private readonly IntPtr _hwnd;
        private readonly YoloModelProvider _yoloProvider;
        private readonly QEnergyDetector _qEnergy;

        public AutoCombatTrigger(IConfigService configService, ILogService logService,
                                 IInputRouter router, IntPtr hwnd, YoloModelProvider yoloProvider)
        {
            _configService = configService;
            _logService = logService;
            _router = router;
            _hwnd = hwnd;
            _yoloProvider = yoloProvider;
            _qEnergy = new QEnergyDetector(logService);
        }

        // ===== 状态机 =====
        private enum CombatPhase { Searching, Locked }
        private CombatPhase _phase = CombatPhase.Searching;
        private DateTime _lockHoldUntil = DateTime.MinValue;
        private DateTime _relockCooldownUntil = DateTime.MinValue;
        private const double LockHoldSeconds = 6.0;
        private const double RelockCooldownSeconds = 2.0;

        // ===== 节流 / 攻击 =====
        private DateTime _lastDetectTime = DateTime.MinValue;
        private DateTime _lastAttackTime = DateTime.MinValue;
        private DateTime _lastSwitchTime = DateTime.MinValue;
        private int _currentCharIndex;
        // 红线5：键码统一走 KeyCode 常量表，禁止 0x 魔法数字
        private static readonly int[] SwitchKeys = { KeyCode.D1, KeyCode.D2, KeyCode.D3, KeyCode.D4 };

        // ===== 诊断 =====
        private int _diagFrames, _diagYolo;
        private int _rawDbg;

        private const int DiagReportInterval = 120;
        private sealed record DebugMark(Rect Rect, string Source);
        private volatile DebugMark? _lastDebug;

        public void OnCapture(Mat resizedMat, double scale)
        {
            if (!IsEnabled) return;
            var config = _configService.Load();
            if (!config.AutoCombatEnabled) return;
            if ((DateTime.Now - _lastDetectTime).TotalMilliseconds < 100) return;
            _lastDetectTime = DateTime.Now;

            try
            {
                // Q 能量检测：UseUltimate 开启才跑，关闭零开销
                if (config.AutoCombatUseUltimate) _qEnergy.Analyze(resizedMat);

                var target = ScanTarget(resizedMat, config);
                RunStateMachine(target, config);
            }
            catch (Exception ex)
            {
                _logService.LogError($"[战斗] OnCapture 异常: {ex.Message}");
            }
            ReportDiag();
        }

        public void DrawDebug(Mat frame)
        {
            // Q 槽位框+亮度读数（不依赖锁定目标，放最前）
            if (_configService.Load().AutoCombatUseUltimate)
                _qEnergy.DrawDebug(frame);

            var mark = _lastDebug;
            if (mark == null) return;
            Cv2.Rectangle(frame, mark.Rect, Scalar.Orange, 2);
            Cv2.PutText(frame, $"{mark.Source} ({mark.Rect.X + mark.Rect.Width / 2},{mark.Rect.Y + mark.Rect.Height / 2})",
                new Point(mark.Rect.X, Math.Max(14, mark.Rect.Y - 6)),
                HersheyFonts.HersheySimplex, 0.55, Scalar.Orange, 1);
        }

        // ===== 索敌：纯 YOLO。置信度阈值（配置 AutoCombatConfidence）是唯一门槛 =====
        private (bool Has, Rect Rect, string Source) ScanTarget(Mat frame, AppConfig config)
        {
            if (_yoloProvider.State == YoloModelProvider.YoloLoadState.NotStarted)
                _yoloProvider.EnsureStartLoading();

            var dets = _yoloProvider.Detect(frame, (float)config.AutoCombatConfidence);
#if DEBUG
            if (++_rawDbg >= 30)
            {
                _rawDbg = 0;
                var best = dets != null && dets.Count > 0
                    ? $"首个: {dets[0].ClassName} conf={dets[0].Confidence:F2}"
                    : "无";
                _logService.LogDebug($"[YOLO裸数据] dets={(dets == null ? "null(模型未就绪)" : dets.Count.ToString())} " +
                                     $"frame={frame.Width}x{frame.Height} 阈值={config.AutoCombatConfidence} | {best}");
            }
#endif
            if (dets != null)
            {
                foreach (var d in dets.OrderByDescending<CombatYoloBox, float>(x => x.Confidence))
                {
                    if (!AcceptYoloClass(d.ClassName)) continue;

                    // 纯 YOLO：Detect 内部已按阈值过滤，这里不再二次设门槛。
                    // （旧版：>0.8 直收、0.25~0.8 过菱形几何验证——那是旧模型菱形标记的遗物，
                    //   新模型的横长 Lv 牌 aspect≈2.5 必然被它误杀，且 break 导致只验第一个候选）
                    _diagYolo++;
                    _lastDebug = new DebugMark(d.Rect, "YOLO");
                    return (true, d.Rect, "YOLO");
                }
            }
            return (false, default, "");
        }

        // 适配器 ClassName=null（YoloResult 无类别字段）→ null 放行；
        // 兼容未来适配器填入的 target / LV 类名
        private static bool AcceptYoloClass(string? className)
            => string.IsNullOrWhiteSpace(className)
               || className.Equals("target", StringComparison.OrdinalIgnoreCase)
               || className.Equals("LV", StringComparison.OrdinalIgnoreCase);

        private void RunStateMachine((bool Has, Rect Rect, string Source) target, AppConfig config)
        {
            var now = DateTime.Now;
            switch (_phase)
            {
                case CombatPhase.Searching:
                    if (!target.Has || now < _relockCooldownUntil) return;

                    // 模型识别的是怪头顶 Lv 标记 → 点其下方怪身
                    // （调试窗口若见橙框套的是怪身本体，把 +80 去掉改为点框中心）
                    int tx = target.Rect.X + target.Rect.Width / 2;
                    int ty = Math.Min(target.Rect.Y + target.Rect.Height + 80, 1080);

                    // 锁定 = 中键单击（1080P 逻辑坐标，内部归一化到当前视口）
                    _router.MiddleClickLogical(_hwnd, tx, ty);
                    _logService.LogInfo($"[战斗] 发现怪物({target.Source})，中键锁定 ({tx},{ty}) [{_router.CurrentDrive.ToConfigString()}]！");
                    _phase = CombatPhase.Locked;
                    _lockHoldUntil = now.AddSeconds(LockHoldSeconds);
                    _lastSwitchTime = DateTime.MinValue;
                    break;

                case CombatPhase.Locked:
                    if ((now - _lastAttackTime).TotalMilliseconds > 200)
                    {
                        _router.ClickLogical(_hwnd, 960, 540);
                        _lastAttackTime = now;
                    }

                    // Q 能量就绪即发（替代固定节奏盲按；NotifyCast 带冷却防读数回落期重复按）
                    if (config.AutoCombatUseUltimate && _qEnergy.IsReady && !_qEnergy.InCastCooldown)
                    {
                        _router.KeyPress(_hwnd, KeyCode.Q, 50);
                        _logService.LogDebug($"[战斗] Q就绪施放 (亮:{_qEnergy.LastBright:P0})");
                        _qEnergy.NotifyCast();
                    }

                    if ((now - _lastSwitchTime).TotalSeconds > 3)
                    {
                        _lastSwitchTime = now;
                        _router.KeyPress(_hwnd, KeyCode.E, 50);
                        _currentCharIndex = (_currentCharIndex + 1) % SwitchKeys.Length;
                        _router.KeyPress(_hwnd, SwitchKeys[_currentCharIndex], 50);
                    }
                    if (target.Has) _lockHoldUntil = now.AddSeconds(LockHoldSeconds);
                    if (now >= _lockHoldUntil)
                    {
                        _logService.LogInfo("[战斗] 目标丢失，自动脱战。");
                        _phase = CombatPhase.Searching;
                        _relockCooldownUntil = now.AddSeconds(RelockCooldownSeconds);
                    }
                    break;
            }
        }

        private void ReportDiag()
        {
            _diagFrames++;
            if (_diagFrames < DiagReportInterval) return;
            double total = _diagFrames;
            _logService.LogInfo(
                $"[战斗诊断] 近{DiagReportInterval}帧 | YOLO({_yoloProvider.StatusText}) {_diagYolo * 100 / total:F0}%");
            _diagFrames = 0; _diagYolo = 0;
        }

        public void Dispose()
        {
            // 红线3：QEnergyDetector 内部资源必须释放
            _qEnergy.Dispose();
        }
    }
}
