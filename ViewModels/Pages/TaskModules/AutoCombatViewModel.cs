using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;
using TrustNTE.Services.Triggers;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoCombatViewModel : TaskModuleViewModelBase
    {
        private readonly IGameService _gameService;
        private readonly IInputService _inputService;
        private readonly AutomationService _automationService;
        private readonly INteInteractionService _nteInteractionService;
        private readonly IDialogService _dialogService;
        private CancellationTokenSource? _taskCts;
        private bool _isTargetLocked = false;

        public AutoCombatConfig Config { get; }

        public AutoCombatViewModel(
            IConfigService configService,
            ILogService logService,
            IGameService gameService,
            IInputService inputService,
            AutomationService automationService,
            INteInteractionService nteInteractionService,
            IDialogService dialogService)
            : base(logService, configService, "自动战斗")
        {
            _gameService = gameService;
            _inputService = inputService;
            _automationService = automationService;
            _nteInteractionService = nteInteractionService;
            _dialogService = dialogService;

            // [Phase 1] 持久化由基类接管
            Config = BindConfig(c => c.Combat, (main, cfg) => main.Combat = cfg);
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            if (_automationService.CurrentHwnd == IntPtr.Zero)
            {
                _logService.LogError("无法启动自动战斗：请先开启截图器。");
                return;
            }

            // [架构修复] 统一走引擎侧 YoloModelProvider（单实例、进游戏才加载、失败自动重试），
            // 消除 VM 自建第二份模型（双份内存 + 首启阻塞 + 绕过加载门控）。
            var yolo = _automationService.YoloProvider;
            if (yolo.State == YoloModelProvider.YoloLoadState.Failed)
            {
                _logService.LogError($"[自动战斗] YOLO 模型加载失败，本次无法执行：{yolo.StatusText}（重开截图引擎进游戏后会自动重试）");
                return;
            }
            if (yolo.State == YoloModelProvider.YoloLoadState.NotStarted)
            {
                yolo.EnsureStartLoading();
                _logService.Log("[自动战斗] YOLO 模型后台加载中，就绪前不索敌（不会误点）");
            }

            _taskCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var localToken = _taskCts.Token;
            _isTargetLocked = false;

            try
            {
                while (!localToken.IsCancellationRequested)
                {
                    using var frame = _gameService.CaptureWindow();
                    if (frame == null || frame.Empty())
                    {
                        await Task.Delay(20, localToken);
                        continue;
                    }

                    // 模型未就绪时 Detect 返回 null → 按无目标处理，静默等待加载完成
                    var targets = yolo.Detect(frame, (float)Config.ConfidenceThreshold);
                    if (targets != null && targets.Count > 0)
                    {
                        var target = targets.OrderBy(t =>
                            Math.Abs(t.Rect.X + t.Rect.Width / 2 - frame.Width / 2)
                          + Math.Abs(t.Rect.Y + t.Rect.Height / 2 - frame.Height / 2)).First();

                        int screenX = target.Rect.X + target.Rect.Width / 2;
                        int screenY = target.Rect.Y + target.Rect.Height / 2;

                        if (!_isTargetLocked)
                        {
                            // 锁定目标也走全局点击路由（中键锁定暂用右/中键，视具体游戏而定）
                            ExecuteSmartClick(screenX, screenY, isLeft: false);
                            _logService.Log($"已锁定目标: ({screenX}, {screenY})");
                            _isTargetLocked = true;
                        }

                        await PerformCombatActions(localToken);
                    }
                    else
                    {
                        _isTargetLocked = false;
                    }

                    await Task.Delay(100, localToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logService.LogError($"战斗异常: {ex.Message}");
            }
            finally
            {
                _taskCts?.Dispose();
                _taskCts = null;
            }
        }

        private async Task PerformCombatActions(CancellationToken token)
        {
            //  使用 KeyCode，并接入 SmartKeyPress 驱动路由
            SmartKeyPress(KeyCode.Q, 50);
            await Task.Delay(200, token);

            SmartKeyPress(KeyCode.E, 50);
            await Task.Delay(200, token);

            // 平A（960,540 = 1080P 中心点）
            ExecuteSmartClick(960, 540, isLeft: true);
            await Task.Delay(200, token);
        }

        // 全局点击路由（[Phase 1] 读配置走基类 _configService）
        private void ExecuteSmartClick(int x, int y, bool isLeft = true)
        {
            var config = _configService.Load();
            string driveType = config.MouseDriveType ?? "物理点击";
            if (driveType == "通用点击")
                _inputService.SendPhysicalClickWithRestore(_automationService.CurrentHwnd, x, y);
            else if (driveType == "后台点击")
                _inputService.SendBackgroundMouseClick(_automationService.CurrentHwnd, x, y, isLeft);
            else
                _nteInteractionService.NteClick(_automationService.CurrentHwnd, x, y, isLeft);
        }

        // 全局按键路由
        private void SmartKeyPress(int vk, int holdMs)
        {
            var config = _configService.Load();
            string driveType = config.MouseDriveType ?? "物理点击";
            if (driveType == "物理点击")
            {
                Win32Input.KeyDown(vk);
                Thread.Sleep(holdMs);
                Win32Input.KeyUp(vk);
            }
            else if (driveType == "后台点击")
            {
                _inputService.SendKeyPress(_automationService.CurrentHwnd, vk, holdMs);
            }
            else
            {
                _nteInteractionService.NteKeyPress(_automationService.CurrentHwnd, vk, holdMs);
            }
        }

        // [Phase 1] Dispose 整体删除：基类统一解绑配置订阅
    }
}
