using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace TrustNTE.Helpers
{
    public class LanguageChangedMessage
    {
        public string NewLanguage { get; set; } = string.Empty;
    }

    public class TranslationService : INotifyPropertyChanged
    {
        public static TranslationService Instance { get; } = new();

        private Dictionary<string, string> _dictionary = new();
        private string _currentLanguage = "简体中文";

        // 用于解决多线程读写 missing_keys.txt 的冲突
        private static readonly object _fileLock = new();
        private static readonly HashSet<string> _recordedMissingKeys = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string RefreshTrigger => string.Empty;

        public string this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key)) return key;
                if (_currentLanguage == "简体中文") return key;

                if (_dictionary.TryGetValue(key, out var value))
                {
                    return value;
                }

                RecordMissingKey(key);
                return key;
            }
        }

        private TranslationService()
        {
            WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) =>
            {
                LoadLanguage(m.NewLanguage);
            });
        }

        public void LoadLanguage(string lang)
        {
            _currentLanguage = lang;
            _dictionary.Clear();

            Debug.WriteLine($"[Lang] 正在尝试切换语言: {lang}");

            if (lang != "简体中文")
            {
                string langCode = lang switch
                {
                    "English" => "en",
                    "繁體中文" => "zh-Hant",
                    "日本語" => "ja",
                    "한국어" => "ko",
                    _ => "en"
                };

                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Translations", $"{langCode}.json");

                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            _dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                            Debug.WriteLine($"[Lang] ✅ 成功加载 {langCode}.json，包含 {_dictionary.Count} 个词条");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Lang] ❌ JSON 解析失败: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[Lang] ⚠️ 找不到语言文件: {path}");
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RefreshTrigger)));
            Debug.WriteLine($"[Lang] 已通知 UI 刷新");
        }

        private void RecordMissingKey(string key)
        {
#if DEBUG
            // 1. 内存去重：如果已经记录过了，直接跳过，不产生海量线程
            lock (_fileLock)
            {
                if (_recordedMissingKeys.Contains(key)) return;
                _recordedMissingKeys.Add(key);
            }

            // 2. 后台异步写入，加锁防止 IO 冲突
            Task.Run(() =>
            {
                try
                {
                    var stackTrace = new StackTrace(true);
                    var frame = stackTrace.GetFrame(2);
                    if (frame != null)
                    {
                        var fileName = frame.GetFileName();
                        var lineNumber = frame.GetFileLineNumber();
                        string log = $"[{DateTime.Now:HH:mm:ss}] [{_currentLanguage}] Missing key: '{key}' | File: {Path.GetFileName(fileName)}:{lineNumber}";
                        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "missing_keys.txt");

                        lock (_fileLock)
                        {
                            File.AppendAllText(logPath, log + Environment.NewLine);
                        }
                    }
                }
                catch { }
            });
#endif
        }
    }
}