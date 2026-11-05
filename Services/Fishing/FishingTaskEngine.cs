using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;
using TrustNTE.ViewModels;

namespace TrustNTE.Services.Fishing
{
    public class FishingTaskEngine
    {
        private readonly AutomationService _automationService;
        private readonly FishingSceneRecognizer _recognizer;
        private readonly FishingStateMachine _stateMachine;
        private readonly FishingBarController _barController;
        private readonly ILogService _logService;
        private readonly AutoFishingConfig _config;
        private readonly FishingRoiAnalyzer _roiAnalyzer;
        private readonly AutoFishingPreviewRenderer _previewRenderer;

        private CancellationTokenSource? _activeCts;

        public FishingTaskEngine(
            AutomationService automationService,
            FishingSceneRecognizer recognizer,
            FishingStateMachine stateMachine,
            FishingBarController barController,
            ILogService logService,
            AutoFishingConfig config,
            FishingRoiAnalyzer roiAnalyzer,
            AutoFishingPreviewRenderer previewRenderer)
        {
            _automationService = automationService;
            _recognizer = recognizer;
            _stateMachine = stateMachine;
            _barController = barController;
            _logService = logService;
            _config = config;
            _roiAnalyzer = roiAnalyzer;
            _previewRenderer = previewRenderer;
        }

        public void RequestStop()
        {
            try { _activeCts?.Cancel(); } catch { }
        }

        public async Task RunAsync(CancellationToken token, Func<AutoFishingPreviewViewModel?> previewVmProvider, Action<int> onFishCountChanged, Action onExit)
        {
            if (_automationService.CurrentHwnd == IntPtr.Zero)
            {
                _logService.LogError("[自动钓鱼] 无法启动：请先开启截图器！");
                return;
            }

            if (_config.ClearStatsOnStart)
            {
                _config.SCount = 0; _config.ACount = 0; _config.BCount = 0;
                onFishCountChanged?.Invoke(0);
                _logService.LogInfo("[自动钓鱼] 已自动清空上次统计数据");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _activeCts = cts;
            var localToken = cts.Token;

            _stateMachine.BindStop(() => { try { cts.Cancel(); } catch { } });

            _barController.Reset();
            _recognizer.Reset();
            _stateMachine.HardReset();

            DateTime lastFrameAvailableTime = DateTime.Now;
            bool isShutdownTriggered = false;

            try
            {
                while (!localToken.IsCancellationRequested)
                {
                    if (_config.LoopCount > 0 && (_config.SCount + _config.ACount + _config.BCount) >= _config.LoopCount)
                    {
                        _logService.LogInfo($"[自动钓鱼] 已达到设定的循环次数 {_config.LoopCount}，自动停止");
                        isShutdownTriggered = true;
                        break;
                    }

                    try
                    {
                        using var frame = _automationService.GetLatestFrame();
                        if (frame == null || frame.Empty())
                        {
                            if ((DateTime.Now - lastFrameAvailableTime).TotalSeconds > 5)
                            {
                                _logService.LogError("[自动钓鱼] 截图器未运行或无画面，钓鱼任务退出");
                                break;
                            }
                            await Task.Delay(50, localToken);
                            continue;
                        }

                        lastFrameAvailableTime = DateTime.Now;

                        var mainRoi = RoiBox.Parse(_config.ControlRoiStr).ToCvRect();
                        var (barA, barB) = _recognizer.AnalyzeBars(frame, mainRoi, _roiAnalyzer);
                        int delayMs = await _stateMachine.ProcessFrameAsync(frame, barA, barB, localToken);
                        var vm = previewVmProvider();
                        if (!localToken.IsCancellationRequested && vm != null)
                            _previewRenderer.Render(frame, mainRoi, barA.Rect, barB.Rect, vm);

                        await Task.Delay(delayMs, localToken);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logService.LogError($"[自动钓鱼] 单帧处理异常: {ex.Message}");
                        await Task.Delay(100, localToken);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logService.LogError($"[自动钓鱼] 任务异常退出: {ex.Message}"); }
            finally
            {
                _activeCts = null;
                _stateMachine.Teardown();
                onExit?.Invoke();

                if (isShutdownTriggered && _config.ShutdownAfterFinish)
                {
                    _logService.LogWarning("[自动钓鱼] 触发完成后关机，系统将在 60 秒后关机！");
                    Process.Start(new ProcessStartInfo("shutdown", "/s /t 60") { CreateNoWindow = true, UseShellExecute = false });
                }
            }
        }
    }
}