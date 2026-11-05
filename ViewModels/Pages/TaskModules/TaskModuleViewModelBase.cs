using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public abstract partial class TaskModuleViewModelBase : ObservableObject, IDisposable, TaskRuntimeRegistry.ITaskRuntime
    {
        protected readonly ILogService _logService;
        protected readonly IConfigService _configService; // [Phase 1] 配置服务上移到基类
        protected readonly string _taskName;

        private CancellationTokenSource? _cts;
        private Task? _execTask;

        // 互斥协调器（可选依赖：App 未注册时为 null，自动跳过清场）
        // [Phase 5 待办] 服务定位器改构造注入，本阶段不动
        private TaskLaneCoordinator? _coordinator;
        private TaskLaneCoordinator? Coordinator => _coordinator ??= App.ServiceProvider?.GetService<TaskLaneCoordinator>();

        // [Phase 1] 配置绑定登记：绑定的配置实例 + 写回委托
        private readonly List<INotifyPropertyChanged> _boundConfigs = new();
        private readonly List<Action<AppConfig>> _configWriteBacks = new();

        public string TaskName => _taskName;

        [ObservableProperty]
        private bool _isRunning;

        protected TaskModuleViewModelBase(ILogService logService, IConfigService configService, string taskName)
        {
            _logService = logService;
            _configService = configService;
            _taskName = taskName;
        }

        protected abstract Task ExecuteTaskAsync(CancellationToken token);

        // ================================================================
        // [Phase 1] 配置持久化收口
        // ================================================================

        /// <summary>
        /// 绑定模块配置并接管持久化：属性变化 → Load 主配置 → 写回 → Save。
        /// 写盘防抖由 ConfigService 内部 500ms 处理，行为与原各模块手写 handler 完全一致。
        /// 子类需要额外联动（校验/刷新）时，override <see cref="OnConfigChanged"/>。
        /// </summary>
        protected TConfig BindConfig<TConfig>(Func<AppConfig, TConfig> selector, Action<AppConfig, TConfig> writeBack)
            where TConfig : INotifyPropertyChanged
        {
            var config = selector(_configService.Load());
            config.PropertyChanged += OnConfigPropertyChangedCore;
            _boundConfigs.Add(config);
            _configWriteBacks.Add(main => writeBack(main, config));
            return config;
        }

        private void OnConfigPropertyChangedCore(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                var main = _configService.Load();
                foreach (var writeBack in _configWriteBacks) writeBack(main);
                _configService.Save(main);
            }
            catch (Exception ex)
            {
                // [Phase 1] 原各模块 handler 无兜底，异常会直接打进事件链；现落地日志
                _logService.LogError($"[{_taskName}] 配置持久化失败: {ex.Message}");
            }
            OnConfigChanged(e);
        }

        /// <summary>配置变更回调（持久化完成后触发）。子类做联动刷新/校验，不要再重复保存。</summary>
        protected virtual void OnConfigChanged(PropertyChangedEventArgs e) { }

        // ================================================================
        // 启动 / 停止（原逻辑原样保留，Phase 1 未动一行）
        // ================================================================

        /// <summary>UI 按钮"开始"：允许清场（优先启动为准）。</summary>
        [RelayCommand]
        private Task Start() => StartCoreAsync(preemptOthers: true);

        /// <summary>
        /// 统一启动入口。
        /// preemptOthers=true：用户手动启动 → 先停掉调度器分组与其它用户任务（优先启动为准）；
        /// preemptOthers=false：守护恢复（TryStart）→ 不清场，允许多任务按账本并行恢复。
        /// </summary>
        private async Task<bool> StartCoreAsync(bool preemptOthers)
        {
            if (IsRunning) return false;

            // 等上一个任务彻底收尾（防手速：Stop 后立即 Start 的 CTS 竞态）
            var prev = _execTask;
            if (prev != null)
            {
                try { await prev.WaitAsync(TimeSpan.FromSeconds(1)); } catch { }
            }
            if (IsRunning) return false;

            IsRunning = true; // 先占位，防双击

            try
            {
                // "优先启动为准"：清掉其它在跑的任务与调度器分组
                if (preemptOthers && Coordinator != null)
                {
                    try { await Coordinator.PreemptForUserTaskAsync(); }
                    catch (Exception ex) { _logService.LogWarning($"[{_taskName}] 启动前清场失败（忽略继续）: {ex.Message}"); }
                }

                var cts = new CancellationTokenSource(); // 局部变量，杜绝 Dispose 竞态
                _cts = cts;
                _logService.Log($"▶ 开始执行任务：{_taskName}");

                _execTask = Task.Run(async () =>
                {
                    TaskRuntimeRegistry.Register(this);
                    try
                    {
                        await ExecuteTaskAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logService.Log($"■ 任务已取消：{_taskName}");
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"❌ 任务出错：{_taskName} - {ex.Message}");
                    }
                    finally
                    {
                        TaskRuntimeRegistry.Unregister(this);
                        // 真正的退出才切 UI
                        System.Windows.Application.Current?.Dispatcher.Invoke(() => { IsRunning = false; });
                        cts.Dispose(); // 只释放自己的
                        if (ReferenceEquals(_cts, cts)) _cts = null; // 只清自己的
                    }
                });

                return true;
            }
            catch
            {
                IsRunning = false;
                throw;
            }
        }

        [RelayCommand]
        private async Task Stop()
        {
            if (!IsRunning || _cts == null) return;

            _logService.Log($"⏳ 正在停止任务：{_taskName}...");
            _cts.Cancel(); // 只发信号，给底层 3 秒体面时间

            var task = _execTask;
            if (task != null)
            {
                try { await task.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (TimeoutException) { _logService.LogWarning($"[{_taskName}] 停止超时，强制切段"); }
                catch (OperationCanceledException) { }
            }
        }

        public async Task StopAsync(TimeSpan timeout)
        {
            _cts?.Cancel();
            if (_execTask != null)
                try { await _execTask.WaitAsync(timeout); } catch { }
        }

        /// <summary>守护恢复入口：不清场（允许多任务按账本并行恢复）。</summary>
        public bool TryStart()
        {
            if (IsRunning) return false;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return false;

            return dispatcher.Invoke(() =>
            {
                if (IsRunning) return false;
                try
                {
                    _ = StartCoreAsync(preemptOthers: false).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _logService.LogError($"[{_taskName}] 恢复任务失败: {t.Exception?.InnerException?.Message}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                    return true;
                }
                catch { return false; }
            });
        }

        public virtual void Dispose()
        {
            //  统一解绑配置订阅（原先散落在 12 个模块的 Dispose 里）
            foreach (var config in _boundConfigs)
                config.PropertyChanged -= OnConfigPropertyChangedCore;
            _boundConfigs.Clear();
            _configWriteBacks.Clear();

            _cts?.Cancel();
        }
    }
}
