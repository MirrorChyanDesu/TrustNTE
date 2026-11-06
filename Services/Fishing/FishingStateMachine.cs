using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Fishing
{
    public enum FishingState
    {
        Interact, Start, Cast, Control, Result, Observing,
        SellFish, SellCabin, SellConfirm, SellSuccessWait, RecoverFishing,
        BuySelectBait, BuySelectMax, BuyExecute, BuyPromptWait, BuyItemWait, BuyExitShop, EquipPanel
    }

    public class FishingStateMachine
    {
        private readonly AutoFishingConfig _config;
        private readonly IInputRouter _router;
        private readonly ILogService _logService;
        private readonly FishingTemplateService _templateService;
        private readonly FishingBarController _barController;
        private readonly AutomationService _automationService;
        private readonly FishingSceneRecognizer _recognizer;

        private Action? _requestStop;
        public void BindStop(Action stop) => _requestStop = stop;

        private FishingState _currentState = FishingState.Interact;
        private DateTime _stateEnterTime = DateTime.Now;
        private DateTime _lastBarSeenTime = DateTime.Now;
        private DateTime _recoverLastEsc = DateTime.MinValue;

        private const int BarMissTolerance = 5;
        private int _barMissCount = 0;

        private bool _soldFishOk = true;
        private bool _baitOk = true;
        private bool _needEquip = false;
        private int _sellAttempts, _buyAttempts, _equipAttempts;
        private bool _cabinTabLogged = false;
        private bool _sellSuccessEscPressed = false;
        private bool _recoverFlowLogged = false;
        private int _recoverEscCount = 0;
        private bool _hasCountedCurrentResult = false;
        private DateTime _lastFishCaughtTime = DateTime.MinValue;

        private string Tag => FishingLogTag.Of(_router);

        public FishingStateMachine(
            AutoFishingConfig config,
            IInputRouter router,
            ILogService logService,
            FishingTemplateService templateService,
            FishingBarController barController,
            AutomationService automationService,
            FishingSceneRecognizer recognizer)
        {
            _config = config;
            _router = router;
            _logService = logService;
            _templateService = templateService;
            _barController = barController;
            _automationService = automationService;
            _recognizer = recognizer;
        }

        public void HardReset()
        {
            _currentState = FishingState.Interact;
            _soldFishOk = true;
            _baitOk = true;
            _needEquip = false;
            _sellAttempts = _buyAttempts = _equipAttempts = 0;
            _cabinTabLogged = false;
            _sellSuccessEscPressed = false;
            _recoverFlowLogged = false;
            _recoverEscCount = 0;
            _recoverLastEsc = DateTime.MinValue;
            _hasCountedCurrentResult = false;
            _lastFishCaughtTime = DateTime.MinValue;
            _stateEnterTime = DateTime.Now;
            _lastBarSeenTime = DateTime.Now;
            _barMissCount = 0;
            SwitchState(FishingState.RecoverFishing);
        }

        public void Teardown() => _barController.Release();

        public async Task<int> ProcessFrameAsync(Mat frame, FishingRoiAnalyzer.ColorRectResult barA, FishingRoiAnalyzer.ColorRectResult barB, CancellationToken token)
        {
            FishingState realTimeState = _currentState;
            FishingSceneSnapshot snap = default;

            if (IsChainState(_currentState))
            {
            }
            else if (barA.Rect.HasValue && barB.Rect.HasValue)
            {
                realTimeState = FishingState.Control;
                _barMissCount = 0;
                _lastBarSeenTime = DateTime.Now;
            }
            else
            {
                _barMissCount++;
                bool isControlGracePeriod = _currentState == FishingState.Control
                    && (DateTime.Now - _stateEnterTime).TotalMilliseconds < 2500;

                if ((_barMissCount > BarMissTolerance || _currentState != FishingState.Control) && !isControlGracePeriod)
                {
                    snap = _recognizer.AnalyzeScene(frame, barA, barB);

                    if (snap.OverflowToast && _soldFishOk)
                    {
                        _soldFishOk = false;
                        _logService.LogWarning($"{Tag} 鱼舱爆仓");
                    }

                    if (snap.BaitEmptyToast)
                    {
                        if (_baitOk || _soldFishOk) _logService.LogWarning($"{Tag} 检测到鱼饵不足，流程重置：卖鱼 → 买饵");
                        _soldFishOk = false;
                        _baitOk = false;
                        realTimeState = FishingState.SellFish;
                    }
                    else
                    {
                        if (snap.HitFish) realTimeState = FishingState.Result;
                        else if (snap.HitCastRod) realTimeState = FishingState.Cast;
                        else if (snap.HitStart) realTimeState = FishingState.Start;
                        else if (snap.HitInteract) realTimeState = FishingState.Interact;

                        if (realTimeState == _currentState && _currentState == FishingState.Control &&
                            (DateTime.Now - _lastBarSeenTime).TotalSeconds > 5)
                        {
                            _logService.LogWarning($"{Tag} 控条超过 5 秒未恢复，回退到 Interact");
                            realTimeState = FishingState.Interact;
                        }
                    }
                }
            }

            if (realTimeState != _currentState) SwitchState(realTimeState);
            await ExecuteStateAsync(frame, barA, barB, token);
            return _currentState == FishingState.Control ? FishingSceneMap.ControlFrameDelayMs : FishingSceneMap.IdleFrameDelayMs;
        }

        private async Task ExecuteStateAsync(Mat frame, FishingRoiAnalyzer.ColorRectResult barA, FishingRoiAnalyzer.ColorRectResult barB, CancellationToken token)
        {
            int delay = Math.Max(_config.ActionDelayMs, 100);
            switch (_currentState)
            {
                case FishingState.Interact: await DoInteract(token, delay); break;
                case FishingState.Start: await DoStart(token, delay); break;
                case FishingState.Cast: await DoCast(token, delay); break;
                case FishingState.Control: _barController.Tick(barA, barB, _config, _barMissCount); break;
                case FishingState.Result: await DoResult(frame, token, delay); break;
                case FishingState.Observing: await Task.Delay(30, token); break;
                case FishingState.SellFish: await DoSellFish(token); break;
                case FishingState.SellCabin: await DoSellCabin(frame, token); break;
                case FishingState.SellConfirm: await DoSellConfirm(token); break;
                case FishingState.SellSuccessWait: await DoSellSuccessWait(frame, token); break;
                case FishingState.RecoverFishing: await DoRecoverFishing(frame, token); break;
                case FishingState.BuySelectBait: await DoBuySelectBait(frame, token); break;
                case FishingState.BuySelectMax: await DoBuySelectMax(frame, token); break;
                case FishingState.BuyExecute: await DoBuyExecute(frame, token); break;
                case FishingState.BuyPromptWait: await DoBuyPromptWait(frame, token); break;
                case FishingState.BuyItemWait: await DoBuyItemWait(frame, token); break;
                case FishingState.BuyExitShop: await DoBuyExitShop(frame, token); break;
                case FishingState.EquipPanel: await DoEquipPanel(frame, token); break;
            }
        }

        private void SwitchState(FishingState newState)
        {
            if (_currentState == newState) return;
            _logService.LogInfo($"{Tag} 状态切换: {_currentState} -> {newState}");
            if (_currentState == FishingState.Control) _barController.Release();

            _currentState = newState;
            _stateEnterTime = DateTime.Now;
            _barMissCount = 0;

            switch (newState)
            {
                case FishingState.Control: _lastBarSeenTime = DateTime.Now; _hasCountedCurrentResult = false; break;
                case FishingState.SellCabin: _cabinTabLogged = false; break;
                case FishingState.SellSuccessWait: _sellSuccessEscPressed = false; break;
                case FishingState.RecoverFishing: _recoverEscCount = 0; _recoverFlowLogged = false; _recoverLastEsc = DateTime.Now; break;
            }
        }

        private static bool IsChainState(FishingState s) =>
            s is FishingState.SellFish or FishingState.SellCabin or FishingState.SellConfirm or FishingState.SellSuccessWait or FishingState.RecoverFishing
            or FishingState.BuySelectBait or FishingState.BuySelectMax or FishingState.BuyExecute or FishingState.BuyPromptWait or FishingState.BuyItemWait or FishingState.BuyExitShop or FishingState.EquipPanel;

        private async Task DoInteract(CancellationToken token, int delay)
        {
            _logService.LogInfo($"{Tag} 发现钓鱼交互，按 F 键");
            _router.KeyPress(_automationService.CurrentHwnd, _config.CastKey, 50);
            _recognizer.MarkInteractPressed();
            SwitchState(FishingState.Start);
            await Task.Delay(delay, token);
        }

        private async Task DoStart(CancellationToken token, int delay)
        {
            _logService.LogInfo($"{Tag} 识别到开始钓鱼，点击左键");
            _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.StartRect.X + FishingSceneMap.StartRect.Width / 2, FishingSceneMap.StartRect.Y + FishingSceneMap.StartRect.Height / 2);
            SwitchState(FishingState.Cast);
            await Task.Delay(1200, token);
        }

        private async Task DoCast(CancellationToken token, int delay)
        {
            var hwnd = _automationService.CurrentHwnd;
            if (!_soldFishOk)
            {
                if (++_sellAttempts > 5) { _logService.LogError($"{Tag} 卖鱼流程连续多次未完成，请手动检查游戏界面后重启任务"); _requestStop?.Invoke(); return; }
                SwitchState(FishingState.SellFish);
                return;
            }

            if (!_baitOk)
            {
                if (++_buyAttempts > 5) { _logService.LogError($"{Tag} 购买鱼饵流程连续多次未完成，请手动检查游戏界面后重启任务"); _requestStop?.Invoke(); return; }
                _logService.LogInfo($"{Tag} 打开鱼饵面板购买");
                _router.KeyPress(hwnd, KeyCode.R, 50);
                SwitchState(FishingState.BuySelectBait);
                await Task.Delay(800, token);
                return;
            }

            if (_needEquip)
            {
                if (++_equipAttempts > 5) { _logService.LogError($"{Tag} 更换鱼饵流程连续多次未完成，请手动检查游戏界面后重启任务"); _requestStop?.Invoke(); return; }
                _logService.LogInfo($"{Tag} 打开鱼饵面板更换");
                _router.KeyPress(hwnd, KeyCode.E, 50);
                SwitchState(FishingState.EquipPanel);
                await Task.Delay(800, token);
                return;
            }

            _logService.LogInfo($"{Tag} 抛竿");
            _router.KeyPress(hwnd, _config.CastKey, 50);
            _recognizer.MarkCastPressed();
            SwitchState(FishingState.Control);
            await Task.Delay(delay, token);
        }

        private async Task DoResult(Mat frame, CancellationToken token, int delay)
        {
            bool foundFish = false;
            bool isTooSoon = (DateTime.Now - _lastFishCaughtTime).TotalSeconds < 3.0;

            if (_templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishS) != null)
            {
                if (!_hasCountedCurrentResult && !isTooSoon) { _logService.LogInfo($"{Tag} 成功钓到 S级 鱼！"); _config.SCount++; _hasCountedCurrentResult = true; _lastFishCaughtTime = DateTime.Now; }
                foundFish = true;
            }
            else if (_templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishA) != null)
            {
                if (!_hasCountedCurrentResult && !isTooSoon) { _logService.LogInfo($"{Tag} 成功钓到 A级 鱼！"); _config.ACount++; _hasCountedCurrentResult = true; _lastFishCaughtTime = DateTime.Now; }
                foundFish = true;
            }
            else if (_templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishB) != null)
            {
                if (!_hasCountedCurrentResult && !isTooSoon) { _logService.LogInfo($"{Tag} 成功钓到 B级 鱼！"); _config.BCount++; _hasCountedCurrentResult = true; _lastFishCaughtTime = DateTime.Now; }
                foundFish = true;
            }

            if (foundFish || (DateTime.Now - _stateEnterTime).TotalSeconds > 4)
            {
                if (!foundFish) _logService.LogWarning($"{Tag} 结算界面未识别到鱼类，强制按 F 退出");
                _router.KeyPress(_automationService.CurrentHwnd, _config.CastKey, 50);
                _recognizer.MarkCastPressed();
                SwitchState(FishingState.Cast);
                await Task.Delay(delay, token);
            }
        }

        private async Task DoSellFish(CancellationToken token)
        {
            _logService.LogInfo($"{Tag} 按Q→前往卖鱼");
            _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Q, 50);
            SwitchState(FishingState.SellCabin);
            await Task.Delay(1000, token);
        }

        private async Task DoSellCabin(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.NoFishRect, FishingSceneMap.TplNoFish) != null) { _logService.LogInfo($"{Tag} 没有鱼类回到钓鱼界面"); _soldFishOk = true; _sellAttempts = 0; _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(1000, token); return; }
            if (_templateService.MatchAny(frame, FishingSceneMap.OneClickSellRect, FishingSceneMap.TplOneClickSell) != null) { _logService.LogInfo($"{Tag} 一键卖鱼"); _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.OneClickSellRect.X + FishingSceneMap.OneClickSellRect.Width / 2, FishingSceneMap.OneClickSellRect.Y + FishingSceneMap.OneClickSellRect.Height / 2); _soldFishOk = true; _sellAttempts = 0; SwitchState(FishingState.SellConfirm); await Task.Delay(400, token); return; }
            if (_templateService.MatchAny(frame, FishingSceneMap.FishCabinRect, FishingSceneMap.TplFishCabin) != null) { if (!_cabinTabLogged) { _cabinTabLogged = true; _logService.LogInfo($"{Tag} 钓鱼仓库"); } _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.FishCabinRect.X + FishingSceneMap.FishCabinRect.Width / 2, FishingSceneMap.FishCabinRect.Y + FishingSceneMap.FishCabinRect.Height / 2); await Task.Delay(800, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 12) { _logService.LogWarning($"{Tag} 鱼舱界面未就绪，ESC自救"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(1000, token); }
        }

        private async Task DoSellConfirm(CancellationToken token)
        {
            if ((DateTime.Now - _stateEnterTime).TotalMilliseconds > FishingSceneMap.SellConfirmDelayMs) { _logService.LogInfo($"{Tag} 一键出售"); _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.SellConfirmX, FishingSceneMap.SellConfirmY); SwitchState(FishingState.SellSuccessWait); await Task.Delay(600, token); }
        }

        private async Task DoSellSuccessWait(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.SellSuccessRect, FishingSceneMap.TplSellSuccess) != null) { if (!_sellSuccessEscPressed) { _sellSuccessEscPressed = true; _logService.LogInfo($"{Tag} 出售成功"); _soldFishOk = true; _sellAttempts = 0; _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); await Task.Delay(3000, token); SwitchState(FishingState.RecoverFishing); } return; }
            if (!_sellSuccessEscPressed && (DateTime.Now - _stateEnterTime).TotalSeconds > 8) { _logService.LogWarning($"{Tag} 未检测到出售成功弹窗，ESC自救"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); }
        }

        private async Task DoRecoverFishing(Mat frame, CancellationToken token)
        {
            // ★ 修复：多尺度匹配气泡 + 粉色检测兜底
            bool hudVisible = _templateService.MatchAny(frame, FishingSceneMap.CastRodRect1, FishingSceneMap.TplCastRod) != null ||
                              _templateService.MatchAny(frame, FishingSceneMap.CastRodRect2, FishingSceneMap.TplCastRod) != null ||
                              _templateService.MatchAny(frame, FishingSceneMap.StartRect, FishingSceneMap.TplStartFishing) != null ||
                              _templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishS) != null ||
                              _templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishA) != null ||
                              _templateService.MatchAny(frame, FishingSceneMap.FishResultRect, FishingSceneMap.TplFishB) != null ||
                              _templateService.MatchAnyMultiScale(frame, FishingSceneMap.InteractRect, FishingSceneMap.TplFishingInteract) != null ||
                              _recognizer.CheckPinkBubble(frame, FishingSceneMap.InteractRect);

            if (hudVisible) { _logService.LogInfo($"{Tag} 已回到钓鱼界面"); SwitchState(FishingState.Observing); return; }
            if (!_recoverFlowLogged && _templateService.MatchAny(frame, FishingSceneMap.ReturnFlowRect, FishingSceneMap.TplReturnFlow) != null) { _recoverFlowLogged = true; _logService.LogInfo($"{Tag} 还在仓库前往钓鱼"); }
            if (_recoverEscCount < 6 && (DateTime.Now - _recoverLastEsc).TotalMilliseconds > 1500) { _recoverEscCount++; _recoverLastEsc = DateTime.Now; _logService.LogInfo($"{Tag} 第{_recoverEscCount}/6次ESC尝试返回钓鱼界面"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); await Task.Delay(500, token); return; }
            if (_recoverEscCount >= 6) { _logService.LogError($"{Tag} 多次ESC仍未能回到钓鱼界面，请手动返回钓鱼界面后重新启动任务"); _requestStop?.Invoke(); }
        }

        private async Task DoBuySelectBait(Mat frame, CancellationToken token)
        {
            var hit = _templateService.MatchAny(frame, FishingSceneMap.UniversalBaitRectA, FishingSceneMap.TplUniversalBait);
            if (hit == null) hit = _templateService.MatchAny(frame, FishingSceneMap.UniversalBaitRectB, FishingSceneMap.TplUniversalBait);
            if (hit != null) { _logService.LogInfo($"{Tag} 选择万能鱼饵"); _router.ClickLogical(_automationService.CurrentHwnd, hit.Value.X + hit.Value.Width / 2, hit.Value.Y + hit.Value.Height / 2); SwitchState(FishingState.BuySelectMax); await Task.Delay(600, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 8) { _logService.LogWarning($"{Tag} 未找到万能鱼饵，ESC自救重试"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(800, token); }
        }

        private async Task DoBuySelectMax(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.MaxBaitRect, FishingSceneMap.TplMaxBait) != null) { _logService.LogInfo($"{Tag} 选择最大值鱼饵"); _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.MaxBaitRect.X + FishingSceneMap.MaxBaitRect.Width / 2, FishingSceneMap.MaxBaitRect.Y + FishingSceneMap.MaxBaitRect.Height / 2); SwitchState(FishingState.BuyExecute); await Task.Delay(600, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 8) { _logService.LogWarning($"{Tag} 未找到最大值按钮，ESC自救重试"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(800, token); }
        }

        private async Task DoBuyExecute(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.PurchaseRect, FishingSceneMap.TplPurchase) != null) { _logService.LogInfo($"{Tag} 执行购买"); _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.PurchaseRect.X + FishingSceneMap.PurchaseRect.Width / 2, FishingSceneMap.PurchaseRect.Y + FishingSceneMap.PurchaseRect.Height / 2); SwitchState(FishingState.BuyPromptWait); await Task.Delay(800, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 8) { _logService.LogWarning($"{Tag} 未找到购买按钮，ESC自救重试"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(800, token); }
        }

        private async Task DoBuyPromptWait(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.PromptRect, FishingSceneMap.TplPurchasePrompt) != null) { _logService.LogInfo($"{Tag} 确认花费大价购买最大值鱼饵"); _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.PromptRect.X + FishingSceneMap.PromptRect.Width / 2, FishingSceneMap.PromptRect.Y + FishingSceneMap.PromptRect.Height / 2); SwitchState(FishingState.BuyItemWait); await Task.Delay(600, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 8) { _logService.LogWarning($"{Tag} 未出现购买提示，ESC自救重试"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(800, token); }
        }

        private async Task DoBuyItemWait(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.ItemObtainedRect, FishingSceneMap.TplItemObtained) != null) { _logService.LogInfo($"{Tag} 购买成功"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); _baitOk = true; _needEquip = true; _logService.LogInfo($"{Tag} 购买大量鱼饵"); SwitchState(FishingState.BuyExitShop); await Task.Delay(2000, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 6) { _logService.LogInfo($"{Tag} 购买数量不足50"); _baitOk = true; _needEquip = true; SwitchState(FishingState.BuyExitShop); await Task.Delay(500, token); }
        }

        private async Task DoBuyExitShop(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.TackleShopRect, FishingSceneMap.TplTackleShop) != null) { _logService.LogInfo($"{Tag} 退出渔具商店"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(2000, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 6) { _logService.LogInfo($"{Tag} 未检测到渔具商店标题，直接返回"); SwitchState(FishingState.RecoverFishing); await Task.Delay(500, token); }
        }

        private async Task DoEquipPanel(Mat frame, CancellationToken token)
        {
            if (_templateService.MatchAny(frame, FishingSceneMap.ConfirmChangeRect, FishingSceneMap.TplConfirmChange) != null) { _logService.LogInfo($"{Tag} 更换鱼饵"); _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.ConfirmChangeRect.X + FishingSceneMap.ConfirmChangeRect.Width / 2, FishingSceneMap.ConfirmChangeRect.Y + FishingSceneMap.ConfirmChangeRect.Height / 2); _needEquip = false; _equipAttempts = 0; _buyAttempts = 0; SwitchState(FishingState.RecoverFishing); await Task.Delay(1000, token); return; }
            if (_templateService.MatchAny(frame, FishingSceneMap.ChangeBaitBuyRect, FishingSceneMap.TplChangeBaitBuy) != null) { _logService.LogInfo($"{Tag} 没有鱼饵"); if (_buyAttempts >= 2) _logService.LogWarning($"{Tag} 疑似余额不足，无法购买鱼饵（继续重试中）"); _router.ClickLogical(_automationService.CurrentHwnd, FishingSceneMap.ChangeBaitBuyClickX, FishingSceneMap.ChangeBaitBuyClickY); _baitOk = false; _needEquip = false; _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(1000, token); return; }
            if ((DateTime.Now - _stateEnterTime).TotalSeconds > 8) { _logService.LogWarning($"{Tag} 鱼饵面板未出现，ESC自救重试"); _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50); SwitchState(FishingState.RecoverFishing); await Task.Delay(800, token); }
        }
    }
}