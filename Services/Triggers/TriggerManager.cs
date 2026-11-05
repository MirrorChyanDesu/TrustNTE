using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    public class TriggerManager
    {
        private readonly List<ITaskTrigger> _triggers = new();

        //     额外的互斥判定（App 装配时注入，如：调度器分组是否在跑）。
        //    返回 true = 视为"有任务在跑"，Reactive 触发器被跳过。
        //    与 TaskRuntimeRegistry.Count 一起构成完整互斥条件。
        public Func<bool>? ExtraBusyCheck { get; set; }

        public GameLoadingTrigger? LoadingTrigger { get; private set; }
        public CombatTrigger? CombatTrigger { get; private set; }
        public AutoLootTrigger? LootTrigger { get; private set; }
        public AutoDialogueTrigger? DialogueTrigger { get; private set; }
        public AutoCombatTrigger? AutoCombatTrigger { get; private set; }

        public PinkPawPickupTrigger? PinkPawTrigger { get; private set; }

        public MonthlyCardTrigger? MonthlyCardTrigger { get; private set; }

        public void InitializeAll(TriggerContext ctx)
        {
            ReleaseAllTriggers();
            _triggers.Clear();

            LoadingTrigger = new GameLoadingTrigger(
                ctx.Automation,
                ctx.Log,
                ctx.Config,
                ctx.Input,
                ctx.NteInteraction,
                ctx.Automation.Supervisor,
                ctx.Automation.GameStateTemplates,
                ctx.Hwnd)
            {
                CoordMapper = ctx.LegacyCoordMapper
            };

            CombatTrigger = new CombatTrigger(ctx.Log);

            LootTrigger = new AutoLootTrigger(ctx.Config, ctx.Input, ctx.Hwnd)
            {
                CoordMapper = ctx.LegacyCoordMapper
            };

            DialogueTrigger = new AutoDialogueTrigger(
                ctx.Config,
                ctx.Router,
                ctx.Log,
                ctx.StoryTemplates,
                ctx.Hwnd);

            //    （Provider 挂在 AutomationService 上，跨 InitializeAll 复用，模型不重复加载）
            AutoCombatTrigger = new AutoCombatTrigger(
                ctx.Config,
                ctx.Log,
                ctx.Router,
                ctx.Hwnd,
                ctx.YoloProvider!);

            PinkPawTrigger = new PinkPawPickupTrigger(
                ctx.Config, ctx.Input, ctx.NteInteraction, ctx.Log, ctx.Hwnd)
            {
                CoordMapper = ctx.LegacyCoordMapper
            };

            MonthlyCardTrigger = new MonthlyCardTrigger(
                ctx.Config, ctx.Input, ctx.NteInteraction, ctx.Log, ctx.Hwnd)
            {
                CoordMapper = ctx.LegacyCoordMapper
            };

            _triggers.Add(LoadingTrigger);
            _triggers.Add(CombatTrigger);
            _triggers.Add(LootTrigger);
            _triggers.Add(DialogueTrigger);
            _triggers.Add(AutoCombatTrigger);
            _triggers.Add(PinkPawTrigger);
            _triggers.Add(MonthlyCardTrigger);
        }

        public void EnableCombatTriggers(ILogService logService)
        {
            if (CombatTrigger != null) CombatTrigger.IsEnabled = true;
            if (LootTrigger != null) LootTrigger.IsEnabled = true;
            if (DialogueTrigger != null) DialogueTrigger.IsEnabled = true;
            if (AutoCombatTrigger != null) AutoCombatTrigger.IsEnabled = true;
            if (PinkPawTrigger != null) PinkPawTrigger.IsEnabled = true;
            if (MonthlyCardTrigger != null) MonthlyCardTrigger.IsEnabled = true;
            logService.Log("即将跳转实时触发任务");
        }

        //     增加 token 参数（默认值保持旧调用点兼容）
        public void Dispatch(Mat resizedMat, double scale, CancellationToken token = default)
        {
            //     停止/退出时：即刻止步，不再触发任何输入（P0-3）
            if (token.IsCancellationRequested) return;

            // 1. 检查当前是否有序列任务（用户级任务 / 调度器分组）在运行
            //        ExtraBusyCheck 补上调度器分组的覆盖
            bool sequenceRunning = TaskRuntimeRegistry.Count > 0
                || (ExtraBusyCheck?.Invoke() ?? false);

            foreach (var trigger in _triggers)
            {
                //     每个触发器派发前再查一次取消，停止时最长残留一个动作
                if (token.IsCancellationRequested) return;

                if (trigger.IsEnabled)
                {
                    // 2. 如果有任务在跑，且当前触发器是 Reactive（会抢输入的），直接跳过
                    if (sequenceRunning && trigger.Class == TriggerClass.Reactive)
                        continue;

                    trigger.OnCapture(resizedMat, scale);
                }
            }
        }

        public void DrawDebug(Mat drawMat)
        {
            foreach (var trigger in _triggers)
            {
                if (trigger.IsEnabled)
                {
                    trigger.DrawDebug(drawMat);
                }
            }
        }

        // 【YOLO 战斗视角专用】只绘制战斗触发器的调试层
        //   （Q 槽框 + 最近 YOLO 目标橙框），
        //   不画任务/监控/其他触发器的静态 ROI，避免满屏黄框干扰观察。
        //   不做 IsEnabled 门控：战斗开关关着时也能观察 Q 槽读数，便于调参。
        public void DrawCombatDebug(Mat drawMat)
        {
            AutoCombatTrigger?.DrawDebug(drawMat);
        }

        public void DisposeAll()
        {
            ReleaseAllTriggers();
            _triggers.Clear();
            LoadingTrigger = null;
            CombatTrigger = null;
            LootTrigger = null;
            DialogueTrigger = null;
            AutoCombatTrigger = null;
            PinkPawTrigger = null;
            MonthlyCardTrigger = null;
        }

        private void ReleaseAllTriggers()
        {
            foreach (var trigger in _triggers)
            {
                if (trigger is IDisposable disposable)
                {
                    try { disposable.Dispose(); }
                    catch { }
                }
            }
        }
    }
}
