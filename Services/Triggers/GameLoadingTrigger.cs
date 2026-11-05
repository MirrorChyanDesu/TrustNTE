using OpenCvSharp;
using System;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    public class GameLoadingTrigger : ITaskTrigger, IDisposable
    {
        public string Name => "游戏加载";
        public TriggerClass Class => TriggerClass.Guardian; 

        public bool IsEnabled { get; set; } = true;
        public Func<int, int, (int, int)>? CoordMapper { get; set; }

        private readonly AutomationService _automation;
        private readonly ILogService _logService;
        private readonly GameSupervisorService _supervisor;
        private readonly NteGameStateTemplateService _tpl; 
        private DateTime _triggerStartTime = DateTime.Now;

        // 与守护层同步的 UID 区域
        private static readonly Rect UidRoi = new(1, 1060, 34, 20);

        public GameLoadingTrigger(
            AutomationService automation,
            ILogService logService,
            IConfigService configService,
            IInputService inputService,
            INteInteractionService nteInteractionService,
            GameSupervisorService supervisor,
            NteGameStateTemplateService tpl,
            IntPtr hwnd)
        {
            _automation = automation;
            _logService = logService;
            _supervisor = supervisor;
            _tpl = tpl; 
        }

        public void DrawDebug(Mat drawMat) { }

        public void OnCapture(Mat resizedMat, double scale)
        {
            if (!IsEnabled) return;

            if ((DateTime.Now - _triggerStartTime).TotalMinutes >= 5)
            {
                _logService.LogError("⚠️ 等待进入游戏超时（5分钟），请检查游戏是否卡在登录界面！");
                IsEnabled = false;
                return;
            }

            // 双通道解锁：守护层 State 或者 自己匹配 UID，任一即解锁
            bool unlocked = _supervisor.State == SupervisorState.Running
                         || _tpl.MatchAny(resizedMat, UidRoi, "uid", 0.75) != null;

            if (unlocked)
            {
                _logService.Log("✅ 游戏就绪！已成功进入主界面，【实时触发】已全面开启");
                IsEnabled = false;
                _automation.EnableCombatTriggers();
            }
        }

        public void Dispose() { }
    }
}