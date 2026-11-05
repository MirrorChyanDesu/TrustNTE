using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TrustNTE.Models.Dispatcher;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Dispatcher
{
    public class DispatcherConfigService
    {
        private readonly ILogService _logService;
        private readonly string _configPath;

        private static readonly object _fileLock = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public DispatcherConfigService(ILogService logService)
        {
            _logService = logService;

            // 【   改点】统一存放到软件根目录的 Data 文件夹下
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _configPath = Path.Combine(dir, "dispatcher_config.json");
        }

        public ObservableCollection<ScriptGroup> Load()
        {
            if (!File.Exists(_configPath)) return new ObservableCollection<ScriptGroup>();

            try
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<ObservableCollection<ScriptGroup>>(json, _jsonOptions)
                       ?? new ObservableCollection<ScriptGroup>();
            }
            catch (Exception ex)
            {
                _logService.LogError($"[调度器] 配置文件损坏，加载失败: {ex.Message}");
                try { File.Copy(_configPath, _configPath + ".bak", true); } catch { }
                return new ObservableCollection<ScriptGroup>();
            }
        }

        public void Save(ObservableCollection<ScriptGroup> groups)
        {
            try
            {
                var json = JsonSerializer.Serialize(groups, _jsonOptions);
                string tempPath = _configPath + ".tmp";

                Task.Run(() =>
                {
                    try
                    {
                        lock (_fileLock)
                        {
                            File.WriteAllText(tempPath, json);
                            File.Move(tempPath, _configPath, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"[调度器] 后台保存配置失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"[调度器] 序列化配置失败: {ex.Message}");
            }
        }
    }
}