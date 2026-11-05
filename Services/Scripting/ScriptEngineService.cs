using Microsoft.ClearScript.V8;
using Microsoft.ClearScript;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Scripting
{
    public class ScriptEngineService : IDisposable
    {
        private V8ScriptEngine? _engine;
        private readonly ILogService _logService;
        private readonly IInputService _inputService;
        private readonly string _scriptDir;
        
        private readonly SemaphoreSlim _executeLock = new(1, 1);
        private readonly object _engineLock = new();
        
        public ScriptEngineService(ILogService logService, IInputService inputService)
        {
            _logService = logService;
            _inputService = inputService;
            _scriptDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Scripts");
        }

        private void InitializeEngine()
        {
            _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);

            _engine.AddHostObject("log", new
            {
                info = new Action<string>(msg => _logService.LogInfo($"[JS] {msg}")),
                error = new Action<string>(msg => _logService.LogError($"[JS] {msg}")),
                debug = new Action<string>(msg => _logService.LogDebug($"[JS] {msg}"))
            });

            _engine.AddHostObject("file", new LimitedFile(_scriptDir));

            _engine.AddHostObject("sleep", new Func<int, Task>(async ms => 
            {
                await Task.Delay(ms);
            }));

            _engine.AddHostObject("automation", new
            {
                KeyPress = new Action<string>(key => _logService.LogInfo($"[JS] 请求按键: {key}")),
                Click = new Action<int, int>((x, y) => _logService.LogInfo($"[JS] 请求点击: ({x}, {y})"))
            });
        }

        /// <summary>
        /// 执行脚本，支持外部 Token 中断和超时中断
        /// </summary>
        public async Task<string> ExecuteScriptAsync(string scriptContent, int timeoutMs, CancellationToken externalToken = default)
        {
            await _executeLock.WaitAsync(externalToken);
            Task<object?>? executeTask = null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            if (timeoutMs > 0)
            {
                cts.CancelAfter(timeoutMs);
            }

            try
            {
                lock (_engineLock)
                {
                    InitializeEngine();
                }

                executeTask = Task.Run(() => (object?)_engine!.Evaluate(scriptContent), cts.Token);

                var completedTask = await Task.WhenAny(executeTask, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask != executeTask)
                {
                    lock (_engineLock) { _engine?.Interrupt(); }
                    if (externalToken.IsCancellationRequested)
                        throw new OperationCanceledException("脚本被外部中断", externalToken);
                    else
                        throw new TimeoutException("脚本执行超时。");
                }

                object? result = await executeTask;

                if (result is Task jsTask)
                {
                    var asyncCompletedTask = await Task.WhenAny(jsTask, Task.Delay(Timeout.Infinite, cts.Token));
                    if (asyncCompletedTask != jsTask)
                    {
                        if (externalToken.IsCancellationRequested)
                            throw new OperationCanceledException("脚本被外部中断", externalToken);
                        else
                            throw new TimeoutException("脚本异步等待超时。");
                    }
                    result = jsTask.GetType().GetProperty("Result")?.GetValue(jsTask);
                }

                return HandleJsResult(result);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                throw; 
            }
            catch (ScriptInterruptedException)
            {
                throw new OperationCanceledException("脚本收到中断信号。");
            }
            catch (Exception ex)
            {
                _logService.LogError($"❌ 脚本运行时异常: {ex.Message}");
                throw;
            }
            finally
            {
                if (executeTask != null && !executeTask.IsCompleted)
                {
                    lock (_engineLock) { _engine?.Interrupt(); }
                    try { await executeTask; } catch { }
                }

                lock (_engineLock)
                {
                    _engine?.Dispose();
                    _engine = null;
                }

                _executeLock.Release();
            }
        }

        /// <summary>
        /// 强制中断当前脚本（供调度器打断死循环使用）
        /// </summary>
        public void StopScript()
        {
            lock (_engineLock)
            {
                _engine?.Interrupt();
            }
        }

        private string HandleJsResult(object? jsResult)
        {
            if (jsResult == null) return string.Empty;
            if (jsResult is string || jsResult is int || jsResult is bool || jsResult is double)
                return jsResult.ToString()!;
            try
            {
                var json = _engine?.Script.JSON.stringify(jsResult);
                return json?.ToString() ?? "{}";
            }
            catch { return jsResult.ToString() ?? string.Empty; }
        }

        public void Dispose()
        {
            lock (_engineLock)
            {
                _engine?.Dispose();
                _engine = null;
            }
            _executeLock.Dispose();
        }
    }
}