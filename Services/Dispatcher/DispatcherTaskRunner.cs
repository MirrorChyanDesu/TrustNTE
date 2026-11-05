using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models.Dispatcher;
using TrustNTE.Services.Interfaces;
using TrustNTE.Services.Scripting;

namespace TrustNTE.Services.Dispatcher
{
    public class DispatcherTaskRunner
    {
        private readonly ScriptEngineService _scriptEngine;
        private readonly ILogService _logService;
        private readonly SemaphoreSlim _runnerLock = new(1, 1);
        private readonly object _sync = new();
        private CancellationTokenSource? _groupCts;
        private CancellationTokenSource? _currentTaskCts;
        private Process? _currentProcess;

        //     互斥协调器/TriggerManager 用：是否有分组正在执行
        private volatile bool _groupRunning;
        public bool IsRunningGroup => _groupRunning;

        public event Action<ScriptGroup, bool>? OnGroupStateChanged;
        public event Action<ScriptProject, string>? OnProjectStateChanged;

        public DispatcherTaskRunner(ScriptEngineService scriptEngine, ILogService logService)
        {
            _scriptEngine = scriptEngine;
            _logService = logService;
        }

        public async Task RunGroupAsync(ScriptGroup group)
        {
            if (!group.IsEnabled) return;

            // 并发调用的 UI 反馈
            if (!await _runnerLock.WaitAsync(0))
            {
                _logService.LogWarning("[调度器] 已有分组正在执行中，请先停止当前任务！");
                return;
            }

            _groupRunning = true; //    

            using var groupCts = new CancellationTokenSource();
            lock (_sync) _groupCts = groupCts;

            try
            {
                SafeInvoke(() => OnGroupStateChanged?.Invoke(group, true));
                _logService.Log($"[调度器] 开始执行分组: {group.Name}");

                foreach (var project in group.Projects.ToList())
                {
                    if (groupCts.IsCancellationRequested) break;
                    if (!project.IsEnabled) continue;

                    using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(groupCts.Token);
                    lock (_sync) _currentTaskCts = taskCts;

                    try
                    {
                        await ExecuteProjectAsync(project, taskCts.Token, groupCts.Token);
                    }
                    catch (OperationCanceledException) when (taskCts.IsCancellationRequested && !groupCts.IsCancellationRequested)
                    {
                        SafeInvoke(() => OnProjectStateChanged?.Invoke(project, "已跳过"));
                    }
                    catch (OperationCanceledException)
                    {
                        SafeInvoke(() => OnProjectStateChanged?.Invoke(project, "已停止"));
                        break;
                    }
                    catch (TimeoutException)
                    {
                        SafeInvoke(() => OnProjectStateChanged?.Invoke(project, "执行超时"));
                        _logService.LogWarning($"[调度器] 任务 {project.Name} 超时。");
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() => OnProjectStateChanged?.Invoke(project, "执行失败"));
                        _logService.LogError($"[调度器] 任务 {project.Name} 失败: {ex.Message}");
                    }
                    finally
                    {
                        lock (_sync) _currentTaskCts = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"[调度器] 分组执行严重异常: {ex.Message}");
            }
            finally
            {
                lock (_sync) { _currentTaskCts = null; _groupCts = null; }
                _groupRunning = false; //     先改状态再广播
                SafeInvoke(() => OnGroupStateChanged?.Invoke(group, false));
                _runnerLock.Release();
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            lock (_sync) cts = _groupCts;

            try { cts?.Cancel(); } catch { }
            try { _scriptEngine.StopScript(); } catch { }
            KillCurrentProcess();
        }

        public void SkipCurrent()
        {
            CancellationTokenSource? cts;
            lock (_sync) cts = _currentTaskCts;

            try { cts?.Cancel(); } catch { }
            try { _scriptEngine.StopScript(); } catch { }
            KillCurrentProcess();
        }

        private async Task ExecuteProjectAsync(ScriptProject project, CancellationToken taskToken, CancellationToken groupToken)
        {
            project.IsRunning = true;
            SafeInvoke(() => OnProjectStateChanged?.Invoke(project, "执行中..."));
            _logService.Log($"[调度器] 执行任务: {project.Name} ({project.Type})");

            try
            {
                switch (project.Type)
                {
                    case ScriptType.Javascript:
                        int timeoutMs = project.TimeoutSeconds > 0 ? project.TimeoutSeconds * 1000 : 30000;
                        await _scriptEngine.ExecuteScriptAsync(project.Content, timeoutMs, taskToken);
                        break;

                    case ScriptType.KeyMouse:
                        // 占位，以后接入键鼠回放
                        await Task.Delay(1000, taskToken);
                        break;

                    case ScriptType.Shell:
                        await ExecuteShellAsync(project, taskToken);
                        break;
                }

                SafeInvoke(() => OnProjectStateChanged?.Invoke(project, "已完成"));
            }
            finally
            {
                project.IsRunning = false;
            }
        }

        private async Task ExecuteShellAsync(ScriptProject project, CancellationToken taskToken)
        {
            // 捕获标准输出和错误输出
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {project.Content}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };

            // 实时把输出打到日志里
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) _logService.LogInfo($"[Shell 输出] {e.Data}");
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) _logService.LogError($"[Shell 错误] {e.Data}");
            };

            if (!process.Start())
                throw new InvalidOperationException("Shell 进程启动失败");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_sync) _currentProcess = process;

            try
            {
                await process.WaitForExitAsync(taskToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                //     无条件清理：正常退出时 HasExited=true 为安全 no-op；
                //    取消/超时时确保子进程树不残留
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                lock (_sync)
                {
                    if (_currentProcess == process) _currentProcess = null;
                }
            }
        }

        private void KillCurrentProcess()
        {
            Process? p;
            lock (_sync) p = _currentProcess;
            try { p?.Kill(entireProcessTree: true); } catch { }
        }

        private void SafeInvoke(Action action)
        {
            try { action(); }
            catch (Exception ex) { _logService.LogError($"[调度器] 事件广播引发异常: {ex.Message}"); }
        }
    }
}
