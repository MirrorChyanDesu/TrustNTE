using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OpenCvSharp;
using TrustNTE.Helpers;
using TrustNTE.Services;
using TrustNTE.Services.Implementations;
using TrustNTE.Services.Interfaces;
using TrustNTE.Services.Interfaces.Implementations;
using TrustNTE.ViewModels;
using TrustNTE.ViewModels.Pages;
using TrustNTE.Views;
using TrustNTE.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;
using TrustNTE.Services.Scripting;
using TrustNTE.Services.Dispatcher;
using TrustNTE.Services.Macro;
using TrustNTE.Services.Fishing;

namespace TrustNTE
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        // 区分 --start 和 --auto 的属性
        private static readonly HashSet<string> _loggedCrashKeys = new();
        public static bool IsAutoStartCommand { get; private set; } = false;  // --auto 或 --start：静默后台
        public static bool IsAutoStartEngine { get; private set; } = false;   // 仅 --start：静默并自动开启引擎

        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            try { System.IO.Directory.CreateDirectory(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs")); } catch { }


            _mutex = new Mutex(true, @"Local\TrustNTE_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show("TrustNTE 已经在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Environment.Exit(0);
                return;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ElevationHelper.EnsureElevated();

            base.OnStartup(e);

            // 分别解析参数
            if (e.Args.Contains("--auto"))
            {
                IsAutoStartCommand = true; // 静默待命
            }
            if (e.Args.Contains("--start"))
            {
                IsAutoStartCommand = true; // 静默隐藏
                IsAutoStartEngine = true;  // 并自动开启截图引擎
            }

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                args.SetObserved();
                var ex = args.Exception?.InnerException;
                if (ex is not TaskCanceledException && ex is not OperationCanceledException)
                {
                    Debug.WriteLine($"[全局拦截] 未处理的 Task 异常: {ex?.Message}");
                }
            };

            this.DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    if (args.Exception is TaskCanceledException || args.Exception is OperationCanceledException)
                    { args.Handled = true; return; }

                    string key = args.Exception.GetType().Name + "|" +
                                 (args.Exception.InnerException?.Message ?? args.Exception.Message);

                    // 只有第一次出现该异常时才写日志
                    if (_loggedCrashKeys.Add(key))
                    {
                        string logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                        System.IO.Directory.CreateDirectory(logDir);
                        System.IO.File.AppendAllText(
                            System.IO.Path.Combine(logDir, "ui_crash.log"),
                            $"[{DateTime.Now:HH:mm:ss}] {args.Exception}\n---\n");
                    }
                    args.Handled = true;
                }
                catch { args.Handled = true; }
            };

            try
            {
                WaitForUpdaterExit(e.Args);

                var services = new ServiceCollection();

                // 基础服务
                services.AddSingleton<IConfigService, ConfigService>();
                services.AddSingleton<IGameService, GameService>();
                services.AddSingleton<IInputService, InputService>();
                services.AddSingleton<INteInteractionService, NteInteractionService>();
                services.AddSingleton<ILogService, LogService>();
                services.AddSingleton<IOcrService, RapidOcrService>();
                services.AddSingleton<LauncherService>();
                services.AddSingleton<AutomationService>();
                services.AddSingleton<IAutomationContext>(sp => sp.GetRequiredService<AutomationService>());
                services.AddSingleton<UpdateService>();
                services.AddSingleton<ScriptEngineService>();
                services.AddSingleton<DispatcherConfigService>();
                services.AddSingleton<DispatcherTaskRunner>();
                services.AddSingleton<TaskLaneCoordinator>();
                services.AddSingleton<MacroPlayer>();
                services.AddSingleton<IWindowService, WindowService>();
                services.AddSingleton<IModelDownloadService, ModelDownloadService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IFileDialogService, FileDialogService>();
                services.AddSingleton<IShellService, ShellService>();
                services.AddSingleton<IMapMatchService, MapMatchService>();
                services.AddSingleton<TrustNTE.ViewModels.RedeemCodeWindowViewModel>();
                services.AddSingleton<IInputRouter, InputRouter>();
                services.AddSingleton<NteRenderContextManager>();
                services.AddSingleton<ICoordinateMapper, CoordinateMapper>();

                // 图集与守护
                services.AddSingleton<FishingTemplateService>();
                services.AddSingleton<AutoStoryTemplateService>();
                services.AddSingleton<NteGameStateTemplateService>();
                services.AddSingleton<GameSupervisorService>();

                // 钓鱼模块子服务
                services.AddTransient<FishingBarController>();
                services.AddTransient<AutoFishingPreviewRenderer>();

                services.AddHttpClient<IRedeemCodeService, RedeemCodeService>();
                services.AddSingleton<IRedeemWindowService, RedeemWindowService>();
                services.AddSingleton<IDispatcherService, DispatcherService>();
                services.AddSingleton<IClipboardService, ClipboardService>();
                services.AddSingleton<IStartupService, StartupService>();
                services.AddSingleton<LocalApiService>();

                // ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<StartPageViewModel>();
                services.AddSingleton<SettingsPageViewModel>();
                services.AddSingleton<RealTimePageViewModel>();
                services.AddSingleton<DispatcherPageViewModel>();
                services.AddSingleton<RecordPageViewModel>();
                services.AddSingleton<TaskSettingsPageViewModel>();
                services.AddSingleton<HotKeySettingsPageViewModel>();
                services.AddTransient<ScriptPageViewModel>();
                services.AddTransient<AssistantPageViewModel>();

                // Task Modules
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoCombatViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoFishingViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoHuntViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoRedeemViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoMusicViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoPawViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoApartmentViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoFountainViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoMarketViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoGiftViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoVolleyballHeartViewModel>();
                services.AddSingleton<TrustNTE.ViewModels.Pages.TaskModules.AutoPilgrimageViewModel>();

                // UI 视图
                services.AddNavigationViewPageProvider();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddTransient<StartPage>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<RealTimePage>();
                services.AddTransient<TaskSettingsPage>();
                services.AddTransient<DispatcherPage>();
                services.AddTransient<ScriptPage>();
                services.AddTransient<RecordPage>();
                services.AddTransient<AssistantPage>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<IHotKeyService, HotKeyService>();
                services.AddTransient<HotKeySettingsPage>();

                // 一键兑换
                services.AddSingleton<TrustNTE.Services.AutoRedeemCode.RedemptionTemplateService>();
                services.AddSingleton<TrustNTE.Services.AutoRedeemCode.IRedemptionService,
                                      TrustNTE.Services.AutoRedeemCode.RedemptionService>();

                ServiceProvider = services.BuildServiceProvider();

                // 装配任务车道互斥
                var laneCoordinator = ServiceProvider.GetRequiredService<TaskLaneCoordinator>();
                var automationSvc = ServiceProvider.GetRequiredService<AutomationService>();
                var dispatcherRunner = ServiceProvider.GetRequiredService<DispatcherTaskRunner>();

                laneCoordinator.Bind(dispatcherRunner);
                automationSvc.Triggers.ExtraBusyCheck = () => dispatcherRunner.IsRunningGroup;
                //     补丁 B：注入断线时的清场钩子
                automationSvc.Supervisor.OnLanesStop = () => dispatcherRunner.Stop();

                var configService = ServiceProvider.GetRequiredService<IConfigService>();
                var config = configService.Load();

                if (config.EnableLocalApi)
                {
                    var apiService = ServiceProvider.GetRequiredService<LocalApiService>();
                    apiService.Start();
                }

                TranslationService.Instance.LoadLanguage(config.UiLanguage);

                if (config.AppTheme == "浅色")
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                else if (config.AppTheme == "深色")
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                else
                    ApplicationThemeManager.Apply(ApplicationTheme.Unknown);

                MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();

                if (IsAutoStartCommand)
                {
                    mainWindow.Hide();
                }

                //     补丁 4：如果是 --start，自动开启截图引擎，并将日志写入文件
                if (IsAutoStartEngine)
                {
                    var logService = ServiceProvider.GetRequiredService<ILogService>();
                    try
                    {
                        automationSvc.Start();
                        logService.Log("[自启] 已自动开启截图引擎。");
                    }
                    catch (Exception ex)
                    {
                        logService.LogError($"[自启] 引擎启动失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"启动失败，错误信息：\n{ex.Message}\n\n堆栈：\n{ex.StackTrace}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void WaitForUpdaterExit(string[] args)
        {
            var arg = args.FirstOrDefault(x => x.StartsWith("--restart-from-pid=", StringComparison.Ordinal));
            if (arg == null) return;

            if (!int.TryParse(arg[(arg.IndexOf('=') + 1)..], out var pid)) return;

            try
            {
                using var oldProc = Process.GetProcessById(pid);
                if (oldProc.ProcessName != "TrustNTE.update") return;
                oldProc.WaitForExit(3000);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var autoService = ServiceProvider?.GetService<AutomationService>();

                // ① 幽灵守护收尸：IdleGuard + 引擎 + 派发器，一次性全发停止信号
                //    （_idleGuardCts 从创建起无人取消，只有这里能杀掉守护线程）
                autoService?.ShutdownForExit();

                // ② 任务先停（在引擎停止信号前，避免收尾期又被拉起）
                Task.Run(() => TaskRuntimeRegistry.StopAllAsync(TimeSpan.FromSeconds(2))).Wait(TimeSpan.FromSeconds(2.5));

                // ③ 其余组件发停止信号
                var taskRunner = ServiceProvider?.GetService<DispatcherTaskRunner>();
                taskRunner?.Stop();

                var apiService = ServiceProvider?.GetService<LocalApiService>();
                apiService?.Stop();

                // ④ 引擎停止信号（幂等：ShutdownForExit 已 Cancel 过，这里显式表达语义）
                autoService?.Stop();

                // ⑤ [退出生死线] 等引擎 finally 真正跑完再退。
                //    finally 顺序：StopCapture → 等业务闲 → ReleaseAllHeldKeys → ...
                //    没有这一行，进程死在等待窗口内，keyup 永远不会发出——
                //    游戏角色会按着方向键走到天涯海角。
                autoService?.WaitForExit(3000);

                // ⑥ 兜底：抬起所有按住的键（双保险，即使 ⑤ 超时也再抬一次）
                ServiceProvider?.GetService<IInputRouter>()?.ReleaseAllHeldKeys(IntPtr.Zero);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[退出异常] {ex.Message}"); }

            if (ServiceProvider?.GetService<ILogService>() is LogService logService) logService.Flush();

            // 容器 Dispose 会连带 Dispose 所有 singleton（含 StartPageViewModel：
            // 退订日志事件 + 销毁日志刷新 timer）
            if (ServiceProvider is IDisposable disposable) disposable.Dispose();

            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); }
                catch (ApplicationException) { }
                finally { _mutex.Dispose(); _mutex = null; }
            }

            base.OnExit(e);
        }
    }
}
