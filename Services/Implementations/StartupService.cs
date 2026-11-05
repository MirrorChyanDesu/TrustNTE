using System;
using System.Diagnostics;
using System.IO;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class StartupService : IStartupService
    {
        private const string TaskName = "TrustNTE_AutoStart";
        private readonly ILogService _logService;
        private readonly IConfigService _configService;

        public StartupService(ILogService logService, IConfigService configService)
        {
            _logService = logService;
            _configService = configService;
        }

        /// <summary>    按配置决定自启模式：--start（自动挂机）/ --auto（静默待命）</summary>
        private string ModeArgument =>
            _configService.Load().AutoStartEngineOnLogin ? "--start" : "--auto";

        public bool IsEnabled()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks",
                        Arguments = $"/query /tn \"{TaskName}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Startup] 查询自启状态失败: {ex.Message}");
                return false;
            }
        }

        public bool Enable()
        {
            try
            {
                string exePath = Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                    return false;

                // /SC ONLOGON: 登录时触发
                // /RL HIGHEST: 最高权限运行，绕过 UAC 弹窗
                // /TR: 执行路径与参数（    模式参数按配置写入）
                string modeArg = ModeArgument;
                string arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" {modeArg}\" /SC ONLOGON /RL HIGHEST /F";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                //     先读完输出再等退出：避免输出缓冲填满时 WaitForExit 死锁
                string stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _logService.LogError($"[Startup] 创建计划任务失败。输出: {stdErr}");
                    return false;
                }
                _logService.Log($"[Startup] 自启已开启（模式：{modeArg}）");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Startup] 创建自启异常: {ex.Message}");
                return false;
            }
        }

        public bool Disable()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{TaskName}\" /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                string stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                //     幂等关闭：任务不存在也视为成功（否则关闭开关会误报失败）
                if (process.ExitCode != 0 && stdErr.Contains("不存在", StringComparison.OrdinalIgnoreCase) == false
                    && !stdErr.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                {
                    _logService.LogError($"[Startup] 删除计划任务失败。输出: {stdErr}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"[Startup] 删除自启异常: {ex.Message}");
                return false;
            }
        }
    }
}
