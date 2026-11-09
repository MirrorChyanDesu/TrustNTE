using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class ConfigService : IConfigService
    {
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        // ══════════════════════════════════════════════════════════════
        // [   ·音游漏键] 锁拆分（原版单把 _fileLock 同时罩住 读缓存/写缓存/序列化/文件IO）
        //
        // 病根：IInputRouter.CurrentDrive → Load() 在【每次按键/点击】都会调用；
        //       原版 Save 的后台写盘（序列化+写文件+Move）持同一把锁 →
        //       运行中改任意配置，写盘期间按键线程被阻塞数毫秒~数十毫秒 → 音游必漏键。
        //
        // 拆分后：
        //   _loadLock  —— 仅保护缓存引用的读写（纳秒级，无任何 IO）
        //   _writeLock —— 仅后台写盘任务之间串行（输入路径完全不碰）
        //   输入热路径 Load()：缓存命中时 volatile 读，零锁零阻塞
        // ══════════════════════════════════════════════════════════════
        private static readonly object _loadLock = new object();
        private static readonly object _writeLock = new object();
        private static readonly object _updateLock = new object();
        private static AppConfig? _cachedConfig;

        private readonly object _debounceLock = new object();
        private CancellationTokenSource? _saveDebounceCts;

        public AppConfig Load()
        {
            // 快路径：缓存已建立 → 直接返回（按键热路径，零锁）
            var cached = Volatile.Read(ref _cachedConfig);
            if (cached != null) return cached;

            // 慢路径：仅进程启动后首次走磁盘
            lock (_loadLock)
            {
                cached = Volatile.Read(ref _cachedConfig);
                if (cached != null) return cached;

                try
                {
                    if (File.Exists(_configPath))
                    {
                        string json = File.ReadAllText(_configPath);
                        var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                        // 反序列化后归一化剧情字符串配置（兼容旧版历史值）
                        NormalizeStoryConfig(config);
                        Volatile.Write(ref _cachedConfig, config);
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigService] 读取配置失败: {ex.Message}");
                }

                var fallback = new AppConfig();
                Volatile.Write(ref _cachedConfig, fallback);
                return fallback;
            }
        }

        public void Save(AppConfig config)
        {
            // 缓存引用原子替换（纳秒级持锁，无 IO —— 按键线程可忽略）
            lock (_loadLock)
            {
                Volatile.Write(ref _cachedConfig, config);
            }

            // 防抖写盘
            lock (_debounceLock)
            {
                // 只 Cancel 不 Dispose：Task.Delay 的延迟回调
                // 可能在 Dispose 之后才响应取消，访问已释放 token 会抛 ObjectDisposedException
                _saveDebounceCts?.Cancel();
                _saveDebounceCts = new CancellationTokenSource();
                var token = _saveDebounceCts.Token;

                Task.Delay(500, token).ContinueWith(t =>
                {
                    if (t.IsCanceled) return;
                    SaveToFile(config); // 序列化 + 文件 IO，只在后台写盘线程间串行
                }, TaskScheduler.Default);
            }
        }

        private void SaveToFile(AppConfig config)
        {
            lock (_writeLock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                    string tempPath = _configPath + ".tmp";
                    // 原子写入：先写临时文件再替换，防写一半断电损坏（原逻辑保留）
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, _configPath, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigService] 保存配置失败: {ex.Message}");
                }
            }
        }

        public void Update(Action<AppConfig> mutateAction)
        {
            lock (_updateLock)
            {
                var config = Load();
                mutateAction(config);
                Save(config);
            }
        }

        private static void NormalizeStoryConfig(AppConfig config)
        {
            if (config.AutoDialogueOptionStrategy != "第一个" && config.AutoDialogueOptionStrategy != "第二个")
            {
                config.AutoDialogueOptionStrategy = "第一个";
            }
            if (config.AutoStoryLogMode != "单项输出" && config.AutoStoryLogMode != "双线输出")
            {
                config.AutoStoryLogMode = "单项输出";
            }
        }
    }
}
