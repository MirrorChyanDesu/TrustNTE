using System;
using System.Collections.Concurrent;
using System.Diagnostics; //     用于判断是否在开发环境
using System.IO;
using System.Linq;
using System.Text.RegularExpressions; //     用于正则脱敏
using System.Threading.Tasks;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Interfaces.Implementations
{
    public class LogService : ILogService
    {
        public event Action<string>? OnLogReceived;
        public bool IsEnabled { get; set; } = true;

        private readonly string _logDir;
        private readonly ConcurrentQueue<string> _fileQueue = new();
        private readonly object _fileLock = new();

        private const long MaxFileSize = 20 * 1024 * 1024;
        private const int MaxFileCount = 14;
        //连发折叠：2 秒内连续相同的（级别+消息）只打第一条，收尾补 ×N 统计
        private const int RepeatWindowSeconds = 2;
        private readonly object _dedupeLock = new();
        private string? _lastKey;
        private string _lastMessage = "";
        private string _lastLevel = "INFO";
        private int _repeatCount;
        private DateTime _lastRepeatUtc;


        //     隐私脱敏正则：匹配 C:\Users\ 后面跟着的任意文件夹名（用户名），直到下一个斜杠
        private static readonly Regex UserPathRegex = new Regex(@"C:\\Users\\[^\\]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public LogService()
        {
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);

            Task.Run(ProcessFileQueueAsync);
        }

        public void Log(string message) => LogInfo(message);

        //     分级落盘：如果是玩家直接双击 exe（没有附加调试器），直接忽略 Debug 日志
        public void LogDebug(string message)
        {
            if (!Debugger.IsAttached) return;
            WriteLog("DEBUG", message, false);
        }

        public void LogInfo(string message) => WriteLog("INFO", message, true);
        public void LogWarning(string message) => WriteLog("WARN", message, true);
        public void LogError(string message) => WriteLog("ERROR", message, true);

        private void WriteLog(string level, string message, bool pushToUi)
        {
            if (!IsEnabled) return;

            //     1. 隐私脱敏：将敏感路径打码，防止玩家发截图泄露真实姓名
            string safeMessage = UserPathRegex.Replace(message, @"C:\Users\***");

            //     2. [A13] 连发折叠判定（锁内只做纯判定，不做 IO/事件回调，防重入死锁）
            string? pendingSummary = null;   // 换话题时需先补发的上一轮 ×N 统计
            string summaryLevel = "INFO";
            bool suppressed = false;

            lock (_dedupeLock)
            {
                string key = level + "|" + safeMessage;
                DateTime nowUtc = DateTime.UtcNow;

                if (key == _lastKey && (nowUtc - _lastRepeatUtc).TotalSeconds < RepeatWindowSeconds)
                {
                    // 同话题 2 秒内连发 → 折叠，只计数
                    _repeatCount++;
                    _lastRepeatUtc = nowUtc;
                    suppressed = true;
                }
                else
                {
                    // 换话题或超时 → 结算上一轮（出现过 ≥2 次才补统计）
                    if (_repeatCount > 1)
                    {
                        pendingSummary = $"{_lastMessage}（连续 ×{_repeatCount}）";
                        summaryLevel = _lastLevel;
                    }

                    _lastKey = key;
                    _lastMessage = safeMessage;
                    _lastLevel = level;
                    _repeatCount = 1;
                    _lastRepeatUtc = nowUtc;
                }
            }

            if (suppressed) return;

            // 先补上一轮的 ×N 统计，再打本条（顺序不能反）
            if (pendingSummary != null) Emit(summaryLevel, pendingSummary, pushToUi);
            Emit(level, safeMessage, pushToUi);
        }

        //     [A13] 统一出口：入文件队列 + 推 UI（原 WriteLog 尾部逻辑原样搬来）
        private void Emit(string level, string safeMessage, bool pushToUi)
        {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _fileQueue.Enqueue($"[{time}] [{level}] {safeMessage}");

            if (pushToUi)
            {
                OnLogReceived?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {safeMessage}");
            }
        }

        //    由 3 秒巡检线程调用：连发停止后没有新日志来"结账"，这里兜底补 ×N
        private void FlushExpiredRepeat()
        {
            string? summary = null;
            lock (_dedupeLock)
            {
                if (_repeatCount > 1 && (DateTime.UtcNow - _lastRepeatUtc).TotalSeconds >= RepeatWindowSeconds)
                {
                    summary = $"{_lastMessage}（连续 ×{_repeatCount}）";
                    _repeatCount = 1; // 结算完保留 _lastKey：相同消息再来时重新开一轮
                }
            }
            if (summary != null) Emit(_lastLevel, summary, true);
        }


        private async Task ProcessFileQueueAsync()
        {
            while (true)
            {
                try
                {
                    if (!_fileQueue.IsEmpty)
                    {
                        WriteQueueToFile();
                    }
                    FlushExpiredRepeat();   //连发停止且无新日志时，这里补 ×N 统计
                }

                catch { }
                await Task.Delay(3000);
            }
        }

        public void Flush()
        {
            try
            {
                WriteQueueToFile();
            }
            catch { }
        }

        private void WriteQueueToFile()
        {
            lock (_fileLock)
            {
                string currentFile = GetCurrentLogFile();
                using (var sw = new StreamWriter(currentFile, true))
                {
                    while (_fileQueue.TryDequeue(out var line))
                    {
                        sw.WriteLine(line);
                    }
                }
            }
        }

        private string GetCurrentLogFile()
        {
            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            var files = Directory.GetFiles(_logDir, $"Log_{dateStr}_*.txt").OrderByDescending(f => f).ToList();

            string? currentFile = files.FirstOrDefault();

            if (currentFile == null || new FileInfo(currentFile).Length > MaxFileSize)
            {
                currentFile = Path.Combine(_logDir, $"Log_{dateStr}_{DateTime.Now:HHmmss}.txt");
                CleanOldLogs();
            }
            return currentFile;
        }

        private void CleanOldLogs()
        {
            try
            {
                var allFiles = Directory.GetFiles(_logDir, "Log_*.txt").OrderByDescending(f => f).ToList();
                if (allFiles.Count > MaxFileCount)
                {
                    for (int i = MaxFileCount; i < allFiles.Count; i++)
                    {
                        File.Delete(allFiles[i]);
                    }
                }
            }
            catch { }
        }
    }
}