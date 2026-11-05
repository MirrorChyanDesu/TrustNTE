using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using TrustNTE.Helpers;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services.Interfaces;
using Size = OpenCvSharp.Size;
using Rect = OpenCvSharp.Rect;


namespace TrustNTE.Services.AutoRedeemCode
{
    /// <summary>
    /// 一键兑换核心状态机（Singleton）。
    /// 归一化约定：
    ///   帧侧：AutomationService 主循环已通过 NteRenderContextManager.SyncNteRenderFrame
    ///         将帧同步为 1080P 标准帧，GetLatestFrame() 返回的即该标准帧（已加锁、已 Clone）；
    ///   输入侧：所有 1080P 坐标经 AutomationService.ClickLogical（内部 TranslateNtePointer）
    ///         翻译后下发，禁止直接调 INteInteractionService.NteClick 传原始坐标。
    /// 本服务内 ROI/坐标一律为 1920×1080 绝对像素，不做任何二次缩放。
    ///
    /// [版本说明·三路结算版] 合并六项变更：
    ///   ① 引擎未启动时「弹窗询问用户」，不自动启动；
    ///   ② 输入态占位锚点先验证、后点击输入框；循环内无 ESC 误操作风险；
    ///   ③ 锚点失败自动保存现场快照到 Logs\RedeemDebug\；
    ///   ④ 兑换码经 WM_CHAR 逐字符直注（NteTextInjector），大小写/数字/多语言通吃；
    ///   ⑤ 成功闭环：以 Nte_RedeemSuccess 弹窗为唯一成功判定，命中后 ESC 关闭；
    ///   ⑥ [本轮] 三路结算：确认点击后单帧三裁剪轮询，区分
    ///      兑换成功 / 兑奖码已被使用 / 兑奖码无效，各走独立分支——
    ///      成功 → ESC → 下一码回到第一步；
    ///      已使用/无效 → 延迟 2s → 下一码直达输入步（停留本页跳过导航）；
    ///      未知 → 快照 → 下一码回到第一步（ESC 导航链自清理未知弹层）。
    /// </summary>
    public sealed class RedemptionService : IRedemptionService, IDisposable
    {
        // ============ 图集组键常量 ============
        // [对齐点★Template] 组键 = 模板名去 "Nte_" 前缀、去尾部序号。
        // 本轮新增组（图集 Ntes_OneClickRdmCode.json 已含）：
        //   Nte_RedemptionCodeUsed    → "RedemptionCodeUsed"（无序号，去前缀即可）
        //   Nte_InvalidRedemptionCode → "InvalidRedemptionCode"（同上）
        private const string GrpRedeemCodePage = "RedeemCodePage";              // Page1/Page2
        private const string GrpGoTo = "GoToRedemptionCode";                    // GoTo1~4（状态锚点）
        private const string GrpRedirect = "RedemptionCodeRedirect";            // 设置页跳转按钮
        private const string GrpPleaseEnter = "PleaseEnterRedemptionCode";      // 输入框占位
        private const string GrpConfirm = "RedemptionCodeConfirmation";         // 兑换码确认按钮
        private const string GrpRedeemSuccess = "RedeemSuccess";                // 兑换成功弹窗
        private const string GrpRedeemUsed = "RedemptionCodeUsed";              // 兑奖码已被使用（本轮新增）
        private const string GrpInvalidCode = "InvalidRedemptionCode";          // 兑奖码无效/已失效（本轮新增）

        // ============ 按键对齐区 ============
        // [对齐点★Key] 统一走项目封装 Helpers\KeyCode（a就是a），全量键码表维护在 KeyCode.cs。
        // 本流程只用 2 个键：Esc（导航+关成功弹窗）+ Backspace（清空输入框）。
        private const int KeyEsc = KeyCode.Esc;
        private const int KeyBackspace = KeyCode.Backspace;

        // ============ 结算判定常量（DetectOutcomeAsync 返回值） ============
        private const string OutcomeSuccess = "Success";   // 兑换成功
        private const string OutcomeUsed = "Used";      // 兑奖码已被使用
        private const string OutcomeInvalid = "Invalid";   // 兑奖码无效/已失效
        private const string OutcomeUnknown = "Unknown";   // 超时未捕获任何已知结算弹窗

        /// <summary>用户确认启动后，等待引擎绑定游戏窗口的上限</summary>
        private const int EngineStartTimeoutMs = 30_000;

        /// <summary>Backspace 清空连发次数（覆盖常见兑换码长度上限；空框下多余按击无副作用）</summary>
        private const int ClearInputStrokes = 24;

        private readonly SemaphoreSlim _runLock = new(1, 1);
        private readonly RedemptionTemplateService _templates;
        private readonly AutomationService _automation;    // 具体类，命名空间 TrustNTE.Services
        private readonly INteInteractionService _nte;
        private readonly IConfigService _configService;

        public bool IsRunning => _runLock.CurrentCount == 0;

        public RedemptionService(
            RedemptionTemplateService templates,
            AutomationService automation,
            INteInteractionService nte,
            IConfigService configService)
        {
            _templates = templates;
            _automation = automation;
            _nte = nte;
            _configService = configService;
        }

        /// <summary>锚点检测结果（async 方法不得带 out，改为结果对象）</summary>
        private sealed class MatchResult
        {
            public bool Found;
            public Point Center;
            public string? MatchedKey;
        }

        // ================================================================
        // 主流程
        // ================================================================
        public async Task<RedemptionRunResult> RunAsync(IReadOnlyList<string> codes, IProgress<string> progress, CancellationToken ct)
        {
            var result = new RedemptionRunResult { Total = codes.Count };
            var cfg = _configService.Load().AutoRedeem;   // [对齐点★AppConfig] 属性名如不同只改此处

            // 全局单飞：非阻塞抢锁，失败直接退出（任务页与窗口按钮共用同一实例）
            if (!await _runLock.WaitAsync(0, ct))
            {
                progress.Report("[一键兑换]已有兑换任务在执行中，本次请求忽略");
                result.Aborted = true;
                return result;
            }

            var codeRegex = string.IsNullOrWhiteSpace(cfg.CodeRegex) ? null : new Regex(cfg.CodeRegex);
            var validCodes = new List<string>();
            foreach (var raw in codes)
            {
                var code = (raw ?? string.Empty).Trim();
                if (code.Length == 0) continue;
                if (codeRegex != null && !codeRegex.IsMatch(code))
                {
                    progress.Report($"[一键兑换]跳过非法兑换码：{code}");
                    continue;
                }
                validCodes.Add(code);
            }
            result.Total = validCodes.Count;
            if (validCodes.Count == 0)
            {
                progress.Report("[一键兑换]无有效兑换码，任务结束");
                _runLock.Release();
                return result;
            }

            try
            {
                // ---------- S0 前置：引擎就绪检查（未启动 → 弹窗询问，不自动启动） ----------
                var hwnd = await EnsureEngineReadyAsync(progress, ct);
                if (hwnd == IntPtr.Zero)
                {
                    result.Aborted = true;
                    progress.Report("[一键兑换]任务终止：截图引擎未启动或启动超时");
                    return result;
                }

                // ---------- S7 逐码执行：每码完整闭环 ----------
                // 流程：导航(按需) → 输入态校验 → 点输入框 → Backspace清空 → WM_CHAR注入
                //       → 确认点击 → 三路结算（成功/已使用/无效/未知）→ 按分支决定下一码起点
                int index = 0;
                bool navigateNeeded = true;   // 首码必走导航（内部含快查，已在页面则直通）
                foreach (var code in validCodes)
                {
                    ct.ThrowIfCancellationRequested();
                    index++;
                    progress.Report($"[一键兑换]── 第 {index}/{result.Total} 个码：{code} ──");

                    // ---- 步骤1：导航到兑换码页面（成功/未知 后走完整导航；已使用/无效 后跳过） ----
                    if (navigateNeeded)
                    {
                        if (!await NavigateToRedeemPageAsync(hwnd, cfg, ct, progress))
                            throw new RedemptionFlowException($"第 {index} 个码导航失败：未能进入兑奖码页面");
                    }

                    // ---- 步骤2：输入态校验（占位锚点独立短超时；未命中走告警兜底不中断） ----
                    var input = await TryWaitTemplateAsync(
                        GrpPleaseEnter, cfg.RoiPleaseEnterRedemptionCode, cfg.PlaceholderTimeoutMs, hwnd, cfg, ct, progress);
                    if (!input.Found)
                    {
                        await SaveDebugSnapshotAsync($"PleaseEnter超时_第{index}个码", cfg, progress);
                        var pageStill = await TryWaitTemplateAsync(
                            GrpRedeemCodePage, cfg.RoiRedeemCodePage, 2000, hwnd, cfg, ct, progress);
                        if (!pageStill.Found)
                        {
                            await SaveDebugSnapshotAsync($"页面消失_第{index}个码", cfg, progress);
                            progress.Report("[一键兑换]未找到输入框锚点且兑奖码页面已消失，终止剩余兑换码");
                            break;
                        }
                        progress.Report("[一键兑换]警告：未检测到输入框占位锚点，但兑奖码页面仍在，继续输入");
                    }
                    else
                    {
                        progress.Report("[一键兑换]找到输入兑奖码输入框");
                    }

                    // ---- 步骤3：点输入框（聚焦）→ Backspace 清残留 → WM_CHAR 逐字符直注 ----
                    await ClickClientAsync(hwnd, cfg.ClickInputX, cfg.ClickInputY, ct);
                    await ClearInputAsync(hwnd, ct);
                    await NteTextInjector.InjectTextAsync(hwnd, code, cfg.TextInjectDelayMs, ct);
                    await Task.Delay(cfg.PasteDelayMs, ct);

                    // ---- 步骤4：确认按钮 → 点击图片中心（提交） ----
                    var confirm = await TryWaitTemplateAsync(
                        GrpConfirm, cfg.RoiRedemptionCodeConfirmation, cfg.NavTimeoutMs, hwnd, cfg, ct, progress);
                    if (!confirm.Found)
                    {
                        await SaveDebugSnapshotAsync($"确认超时_{code}", cfg, progress);
                        result.Failed++;
                        result.FailedCodes.Add(code);
                        progress.Report($"[一键兑换]未找到确认按钮，判定失败：{code}");
                        navigateNeeded = true;   // 未知界面状态：下一码走 ESC 导航链自清理
                        continue;
                    }
                    await ClickClientAsync(hwnd, confirm.Center.X, confirm.Center.Y, ct);
                    progress.Report("[一键兑换]兑换完成");

                    // ---- 步骤5：三路结算检测（成功 / 已被使用 / 无效 / 未知） ----
                    var outcome = await DetectOutcomeAsync(hwnd, cfg, ct, progress);
                    switch (outcome)
                    {
                        case OutcomeSuccess:
                            // 1. 兑换成功获取奖励：ESC 关弹窗，下一码回到第一步
                            progress.Report("[一键兑换]成功兑换奖励");
                            result.Success++;
                            await PressEscAsync(hwnd, ct);
                            await Task.Delay(cfg.EscDelayMs, ct);
                            navigateNeeded = true;
                            break;

                        case OutcomeUsed:
                            // 2. 兑奖码被使用：延迟 2s，下一码直达输入步（停留本页）
                            progress.Report("[一键兑换]兑奖码已被使用");
                            result.Failed++;
                            result.FailedCodes.Add(code);
                            await Task.Delay(cfg.OutcomeDismissDelayMs, ct);
                            navigateNeeded = false;
                            break;

                        case OutcomeInvalid:
                            // 3. 兑奖码无效：延迟 2s，下一码直达输入步（停留本页）
                            progress.Report("[一键兑换]兑奖码无效或已失效");
                            result.Failed++;
                            result.FailedCodes.Add(code);
                            await Task.Delay(cfg.OutcomeDismissDelayMs, ct);
                            navigateNeeded = false;
                            break;

                        default: // OutcomeUnknown
                            // 未知结算状态：存快照，下一码回到第一步（ESC 导航链顺带清理未知弹层）
                            await SaveDebugSnapshotAsync($"结算未知_{code}", cfg, progress);
                            result.Failed++;
                            result.FailedCodes.Add(code);
                            progress.Report($"[一键兑换]确认后未捕获已知结算弹窗，判定失败：{code}");
                            navigateNeeded = true;
                            break;
                    }
                }

                progress.Report($"[一键兑换]任务结束：共 {result.Total}，成功 {result.Success}，失败 {result.Failed}");
                return result;
            }
            catch (RedemptionFlowException ex)
            {
                progress.Report($"[一键兑换]流程中断：{ex.Message}");
                return result;
            }
            finally
            {
                // ---------- S8 兜底归位：ESC×2 回主界面 ----------
                try
                {
                    // 归位用当前句柄实时读取（引擎可能已重启换句柄）；取消语义下用 None 确保执行
                    var hwndFinally = _automation.CurrentHwnd;
                    if (hwndFinally != IntPtr.Zero)
                    {
                        await PressEscAsync(hwndFinally, CancellationToken.None);
                        await Task.Delay(800, CancellationToken.None);
                        await PressEscAsync(hwndFinally, CancellationToken.None);
                    }
                }
                catch { /* 归位失败不抛出，避免掩盖原始异常 */ }
                _runLock.Release();
            }
        }

        // ================================================================
        // 导航链（成功/未知 后执行；内部快查可直通）
        // ================================================================

        /// <summary>
        /// 导航到兑换码页面。返回 false = 导航失败（已存快照），调用方终止任务。
        /// 步骤：快查当前页 → ESC → 找前往兑换码锚点（GoTo1~4，ESC 重试）→ 点设置入口(1794,183)
        ///       → 找兑奖码跳转（超时回点设置入口一次）→ 点击其中心 → 等待兑换码页面。
        /// </summary>
        private async Task<bool> NavigateToRedeemPageAsync(
            IntPtr hwnd, AutoRedeemConfig cfg, CancellationToken ct, IProgress<string> progress)
        {
            // 1. 快查：是否已在兑换码页面（直达输入步路径之外的所有情况都先经过这里）
            var quick = await TryWaitTemplateAsync(
                GrpRedeemCodePage, cfg.RoiRedeemCodePage, cfg.AnchorTimeoutMs, hwnd, cfg, ct, progress);
            if (quick.Found)
            {
                progress.Report("[一键兑换]已在兑奖码页面");
                return true;
            }

            // 2. ESC → 延迟 1s
            await PressEscAsync(hwnd, ct);
            await Task.Delay(cfg.EscDelayMs, ct);

            // 3. 找设置页状态锚点（GoToRedemptionCode 1~4 变体），未找到 ESC 重试
            var foundMenu = false;
            for (int attempt = 0; attempt <= Math.Max(0, cfg.EscRetryMax); attempt++)
            {
                if (attempt > 0)
                {
                    if (cfg.EnableDebugLog) progress.Report($"[一键兑换]第 {attempt} 次 ESC 重试查找设置页锚点");
                    await PressEscAsync(hwnd, ct);
                    await Task.Delay(cfg.EscDelayMs, ct);
                }
                var menu = await TryWaitTemplateAsync(
                    GrpGoTo, cfg.RoiGoToRedemptionCode, cfg.AnchorTimeoutMs, hwnd, cfg, ct, progress);
                foundMenu = menu.Found;
                if (foundMenu) break;
            }
            if (!foundMenu)
            {
                await SaveDebugSnapshotAsync("GoTo锚点超时", cfg, progress);
                return false;
            }
            progress.Report("[一键兑换]找到设置页面");

            // 4. 点击设置入口（坐标走 Config）→ 延迟 2s
            await ClickClientAsync(hwnd, cfg.ClickSettingsX, cfg.ClickSettingsY, ct);
            await Task.Delay(cfg.ClickSettingsDelayMs, ct);

            // 5. 找跳转按钮 → 点击图片中心（超时回点设置入口一次）
            var redirect = await TryWaitTemplateAsync(
                GrpRedirect, cfg.RoiRedemptionCodeRedirect, cfg.NavTimeoutMs, hwnd, cfg, ct, progress);
            if (!redirect.Found)
            {
                if (cfg.EnableDebugLog) progress.Report("[一键兑换]未找到跳转按钮，回点设置入口重试一次");
                await ClickClientAsync(hwnd, cfg.ClickSettingsX, cfg.ClickSettingsY, ct);
                await Task.Delay(cfg.ClickSettingsDelayMs, ct);
                redirect = await TryWaitTemplateAsync(
                    GrpRedirect, cfg.RoiRedemptionCodeRedirect, cfg.NavTimeoutMs, hwnd, cfg, ct, progress);
                if (!redirect.Found)
                {
                    await SaveDebugSnapshotAsync("Redirect超时", cfg, progress);
                    return false;
                }
            }
            await ClickClientAsync(hwnd, redirect.Center.X, redirect.Center.Y, ct);

            // 6. 等待兑换码页面
            var page = await TryWaitTemplateAsync(
                GrpRedeemCodePage, cfg.RoiRedeemCodePage, cfg.NavTimeoutMs, hwnd, cfg, ct, progress);
            if (!page.Found)
            {
                await SaveDebugSnapshotAsync("RedeemPage超时", cfg, progress);
                return false;
            }
            progress.Report("[一键兑换]找到兑奖码页面");
            return true;
        }

        // ================================================================
        // 三路结算检测（本轮新增核心）
        // ================================================================

        /// <summary>
        /// 确认点击后的结算判定：同一轮询周期内取一帧、三 ROI 各裁剪比对，
        /// 命中即返回（优先级 成功 &gt; 已使用 &gt; 无效；实际界面互斥，仅理论兜底）。
        /// 单帧三裁剪比串行等三个超时快得多；200ms 周期足够捕获停留秒级的提示弹窗。
        /// 超时预算复用 AfterConfirmDelayMs（原"结算动画等待"5100ms，语义升级为结算检测窗口；
        /// 网络慢导致弹窗晚到时调大该配置即可，不改代码）。
        /// 返回 OutcomeSuccess / OutcomeUsed / OutcomeInvalid / OutcomeUnknown。
        /// </summary>
        private async Task<string> DetectOutcomeAsync(
            IntPtr hwnd, AutoRedeemConfig cfg, CancellationToken ct, IProgress<string> progress)
        {
            var roiSuccess = ParseRoi(cfg.RoiRedeemSuccess);
            var roiUsed = ParseRoi(cfg.RoiRedeemCodeUsed);
            var roiInvalid = ParseRoi(cfg.RoiInvalidRedemptionCode);

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < cfg.AfterConfirmDelayMs)
            {
                ct.ThrowIfCancellationRequested();

                using var frame = _automation.GetLatestFrame();
                if (frame != null && !frame.Empty())
                {
                    // ① 兑换成功
                    using (var scene = SafeCrop(frame, roiSuccess))
                    {
                        if (_templates.TryMatchAny(scene, GrpRedeemSuccess, out var local, out var key))
                        {
                            LogHit(progress, cfg, key, roiSuccess, local);
                            return OutcomeSuccess;
                        }
                    }
                    // ② 兑奖码已被使用
                    using (var scene = SafeCrop(frame, roiUsed))
                    {
                        if (_templates.TryMatchAny(scene, GrpRedeemUsed, out var local, out var key))
                        {
                            LogHit(progress, cfg, key, roiUsed, local);
                            return OutcomeUsed;
                        }
                    }
                    // ③ 兑奖码无效/已失效
                    using (var scene = SafeCrop(frame, roiInvalid))
                    {
                        if (_templates.TryMatchAny(scene, GrpInvalidCode, out var local, out var key))
                        {
                            LogHit(progress, cfg, key, roiInvalid, local);
                            return OutcomeInvalid;
                        }
                    }
                }
                await Task.Delay(200, ct);
            }
            return OutcomeUnknown;
        }

        /// <summary>命中日志（与 TryWaitTemplateAsync 同格式，DebugLog 开关控制）</summary>
        private static void LogHit(IProgress<string> progress, AutoRedeemConfig cfg, string? key, Rect roi, Point local)
        {
            if (cfg.EnableDebugLog)
                progress.Report($"[一键兑换]命中模板 {key} @ ({roi.X + local.X},{roi.Y + local.Y}) ROI({roi.X},{roi.Y},{roi.Width},{roi.Height})");
        }

        // ================================================================
        // 引擎就绪（弹窗询问制）
        // ================================================================

        /// <summary>
        /// 确保截图引擎已启动并绑定游戏窗口。
        /// 未启动时先在 UI 线程弹窗询问用户，确认后才 Start 并轮询等待绑定。
        /// 返回 IntPtr.Zero 表示：用户拒绝启动 / 启动超时，调用方应终止任务。
        /// </summary>
        private async Task<IntPtr> EnsureEngineReadyAsync(IProgress<string> progress, CancellationToken ct)
        {
            // 1. 已启动：直接返回，不弹窗
            var hwnd = _automation.CurrentHwnd;
            if (hwnd != IntPtr.Zero) return hwnd;

            // 2. 未启动：弹窗询问（不自动启动）
            progress.Report("[一键兑换]截图引擎未启动，等待用户确认...");
            var confirmed = ConfirmStartEngineOnUi();
            if (!confirmed)
            {
                progress.Report("[一键兑换]已取消：用户选择不启动截图引擎");
                return IntPtr.Zero;
            }

            // 3. 用户确认：启动并等待绑定
            progress.Report("[一键兑换]正在启动截图引擎...");
            _automation.Start();

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < EngineStartTimeoutMs)
            {
                ct.ThrowIfCancellationRequested();
                hwnd = _automation.CurrentHwnd;
                if (hwnd != IntPtr.Zero)
                {
                    progress.Report("[一键兑换]引擎已启动并绑定游戏窗口");
                    return hwnd;
                }
                await Task.Delay(300, ct);
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// UI 线程弹窗：询问是否启动截图引擎。返回 true=用户确认启动。
        /// </summary>
        private bool ConfirmStartEngineOnUi()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app == null) return false;

                // 切回 UI 线程并阻塞等待结果
                return app.Dispatcher.Invoke(() =>
                {
                    // 使用全限定名，防止命名冲突，且必须加上 using Wpf.Ui.Controls; 如果在文件顶部没加的话
                    var msgBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "一键兑换",
                        Content = "截图引擎未启动，无法执行一键兑换。\n\n是否立即启动截图器？",
                        PrimaryButtonText = "立即启动",
                        CloseButtonText = "取消",
                        Owner = app.MainWindow
                    };

                    // 因为是在后台线程里，这里直接同步阻塞等用户点确认
                    var result = msgBox.ShowDialogAsync().GetAwaiter().GetResult();
                    return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
                });
            }
            catch
            {
                // UI 不可用（宿主正在关闭等）：按取消处理
                return false;
            }
        }

        // ================================================================
        // 原子操作封装
        // ================================================================

        /// <summary>
        /// 在超时窗口内循环检测模板锚点。
        /// 帧来源 = AutomationService.GetLatestFrame()：内部已加锁并返回 Clone，
        /// 直接持有使用、用完 Dispose，不再二次 Clone。
        /// </summary>
        private async Task<MatchResult> TryWaitTemplateAsync(
            string groupKey, string roiStr, int timeoutMs, IntPtr hwnd,
            AutoRedeemConfig cfg, CancellationToken ct, IProgress<string> progress)
        {
            var roi = ParseRoi(roiStr);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                ct.ThrowIfCancellationRequested();

                using var frame = _automation.GetLatestFrame();
                if (frame != null && !frame.Empty())
                {
                    using var scene = SafeCrop(frame, roi);
                    if (_templates.TryMatchAny(scene, groupKey, out var local, out var matchedKey))
                    {
                        var center = new Point(roi.X + local.X, roi.Y + local.Y);
                        if (cfg.EnableDebugLog)
                            progress.Report($"[一键兑换]命中模板 {matchedKey} @ ({center.X},{center.Y}) ROI({roi.X},{roi.Y},{roi.Width},{roi.Height})");
                        return new MatchResult { Found = true, Center = center, MatchedKey = matchedKey };
                    }
                }
                await Task.Delay(200, ct);
            }
            return new MatchResult();
        }

        /// <summary>按 ESC。签名：NteKeyPress(IntPtr hwnd, int virtualKey, int holdMs)</summary>
        private async Task PressEscAsync(IntPtr hwnd, CancellationToken ct)
        {
            _nte.NteKeyPress(hwnd, KeyEsc, 50);
            await Task.Delay(50, ct);
        }

        /// <summary>
        /// 清空输入框残留：连发 Backspace（单键注入，已验证路径）。
        /// ClearInputStrokes 次覆盖常见码长上限；空输入框下多余按击无副作用。
        /// </summary>
        private async Task ClearInputAsync(IntPtr hwnd, CancellationToken ct)
        {
            for (int i = 0; i < ClearInputStrokes; i++)
            {
                _nte.NteKeyPress(hwnd, KeyBackspace, 30);
                await Task.Delay(25, ct);
            }
        }

        /// <summary>
        /// 点击 1080P 基准坐标：经 AutomationService.ClickLogical（内部 TranslateNtePointer
        /// 做分辨率归一化平移），保证非 1080P 视口下落点正确。
        /// </summary>
        private async Task ClickClientAsync(IntPtr hwnd, int x, int y, CancellationToken ct)
        {
            _automation.ClickLogical(hwnd, x, y);
            await Task.Delay(80, ct);
        }

        /// <summary>
        /// 失败现场快照：把当前帧整幅存盘到 Logs\RedeemDebug\，用于离线判定锚点为何不命中。
        /// </summary>
        private async Task SaveDebugSnapshotAsync(string tag, AutoRedeemConfig cfg, IProgress<string> progress)
        {
            if (!cfg.EnableDebugSnapshots) return;
            try
            {
                using var frame = _automation.GetLatestFrame();
                if (frame == null || frame.Empty()) return;
                var dir = Path.Combine(AppContext.BaseDirectory, "Logs", "RedeemDebug");
                Directory.CreateDirectory(dir);
                foreach (var c in Path.GetInvalidFileNameChars()) tag = tag.Replace(c, '_');
                var file = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{tag}.png");
                Cv2.ImWrite(file, frame);
                progress.Report($"[一键兑换]已保存现场快照：{file}");
                await Task.CompletedTask;
            }
            catch { /* 快照失败不影响主流程 */ }
        }

        /// <summary>"x1,y1,x2,y2" → Rect；非法返回 default（宽高为 0，SafeCrop 会兜底为整帧）</summary>
        private static Rect ParseRoi(string roiStr)
        {
            if (string.IsNullOrWhiteSpace(roiStr)) return default;
            var parts = roiStr.Split(',');
            if (parts.Length != 4) return default;
            if (!int.TryParse(parts[0].Trim(), out var x1) || !int.TryParse(parts[1].Trim(), out var y1)
                || !int.TryParse(parts[2].Trim(), out var x2) || !int.TryParse(parts[3].Trim(), out var y2))
                return default;
            return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        /// <summary>安全裁剪：ROI 越界自动收缩到帧边界</summary>
        private static Mat SafeCrop(Mat frame, Rect roi)
        {
            if (roi.Width <= 0 || roi.Height <= 0) return frame.Clone();
            var x = Math.Max(0, roi.X);
            var y = Math.Max(0, roi.Y);
            var w = Math.Min(roi.Width, frame.Width - x);
            var h = Math.Min(roi.Height, frame.Height - y);
            if (w <= 0 || h <= 0) return frame.Clone();
            return new Mat(frame, new Rect(x, y, w, h)).Clone();
        }

        public void Dispose()
        {
            _runLock.Dispose();
            _templates.Dispose();
        }
    }
}
