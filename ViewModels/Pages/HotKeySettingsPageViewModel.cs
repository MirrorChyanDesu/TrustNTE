using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gma.System.MouseKeyHook.HotKeys;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using TrustNTE.Models;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages
{
    public partial class HotKeySettingsPageViewModel : ObservableObject, IDisposable
    {
        private readonly IHotKeyService _hotKeyService;
        private readonly IDialogService _dialogService;
        private readonly IConfigService _configService;
        private readonly AutomationService _automationService;
        private readonly ILogService _logService;
        private readonly IDispatcherService _dispatcherService;

        private readonly Dictionary<string, HotKey> _acceptedHotKeys = new();
        private readonly HashSet<string> _rollingBackHotKeyProperties = new();

        private bool _isInitializing = true;

        public AppConfig Config { get; set; }

        [ObservableProperty]
        private ObservableCollection<HotKeySettingModel> _hotKeys = new();

        private readonly Dictionary<string, string> _nteKeyConflicts = new()
        {
            // [游戏内]  F1-11 冲突提示
            { "F1", "异环探索指南" },
            { "F2", "异环历练奖赏" },
            { "F3", "异环斯卡布罗集市" },
            { "F4", "异环活动页面" },
            { "F5", "异环都市大亨" },
            { "F7", "异环隐藏主界面" },
            { "F8", "异环抓拍" },
            { "F9", "异环拍照" },
            { "F11", "异环全屏/窗口切换" },
            // [模块音游]  DFJK 冲突提示
            { "D", "异环超强音 D 键音符" },
            { "F", "异环超强音 F 键音符" },
            { "J", "异环超强音 J 键音符" },
            { "K", "异环超强音 K 键音符" }
        };

        public HotKeySettingsPageViewModel(
            IHotKeyService hotKeyService,
            IDialogService dialogService,
            IConfigService configService,
            AutomationService automationService,
            ILogService logService,
            IDispatcherService dispatcherService)
        {
            _hotKeyService = hotKeyService;
            _dialogService = dialogService;
            _configService = configService;
            _automationService = automationService;
            _logService = logService;
            _dispatcherService = dispatcherService;

            Config = _configService.Load();
            if (Config.HotKeyConfig == null) Config.HotKeyConfig = new HotKeyConfig();

            BuildHotKeySettingModelList();

            var list = GetAllNonDirectoryHotkey(HotKeys);
            foreach (var model in list)
            {
                _acceptedHotKeys[model.ConfigPropertyName] = model.HotKey;
                model.RegisterHotKey(_hotKeyService);

                model.PropertyChanged += (sender, e) =>
                {
                    if (sender is HotKeySettingModel m)
                    {
                        if (_isInitializing) return;

                        if (e.PropertyName == nameof(HotKeySettingModel.HotKey))
                        {
                            if (_rollingBackHotKeyProperties.Remove(m.ConfigPropertyName))
                            {
                                UpdateHotKeyConfig(m);
                            }
                            else
                            {
                                var newHotKey = m.HotKey;
                                var previousHotKey = _acceptedHotKeys.GetValueOrDefault(m.ConfigPropertyName, HotKey.None);
                                UpdateHotKeyConfig(m);
                                _acceptedHotKeys[m.ConfigPropertyName] = newHotKey;
                                ShowGameKeyBindingConflictWarning(m, newHotKey, previousHotKey);
                            }
                        }
                        else if (e.PropertyName == nameof(HotKeySettingModel.HotKeyType))
                        {
                            m.HotKey = HotKey.None;
                            var pi = Config.HotKeyConfig.GetType().GetProperty(m.ConfigPropertyName + "Type", BindingFlags.Public | BindingFlags.Instance);
                            if (pi != null && pi.CanWrite) pi.SetValue(Config.HotKeyConfig, m.HotKeyType.ToString());
                        }

                        RemoveDuplicateHotKey(m);
                        m.UnRegisterHotKey(_hotKeyService);
                        m.RegisterHotKey(_hotKeyService);
                        _configService.Save(Config);
                    }
                };
            }

            _isInitializing = false;
        }

        private void UpdateHotKeyConfig(HotKeySettingModel model)
        {
            var pi = Config.HotKeyConfig.GetType().GetProperty(model.ConfigPropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (pi != null && pi.CanWrite)
            {
                pi.SetValue(Config.HotKeyConfig, model.HotKey.IsEmpty ? "" : model.HotKey.ToString());
            }
        }

        private void ShowGameKeyBindingConflictWarning(HotKeySettingModel model, HotKey newHotKey, HotKey previousHotKey)
        {
            if (newHotKey.IsEmpty) return;
            string keyStr = newHotKey.ToString();

            if (newHotKey.Modifiers != ModifierKeys.None) return;

            if (_nteKeyConflicts.TryGetValue(keyStr, out var gameActionName))
            {
                string message = model.HotKeyType == HotKeyTypeEnum.GlobalRegister
                    ? $"你使用的是【全局独占】模式。\n设置该键后，异环将无法接收 {keyStr} 键。\n\n这意味着你的游戏功能【{gameActionName}】将失效！\n\n确定要覆盖吗？"
                    : $"你使用的是【底层监听】模式。\n按下 {keyStr} 时，会导致游戏内的【{gameActionName}】和软件功能同时触发，极易引起混乱！\n\n确定要使用吗？";

                _dispatcherService.BeginInvoke(() => _ = HandleHotKeyConflictAsync(model, newHotKey, previousHotKey, message));
            }
        }

        private async Task HandleHotKeyConflictAsync(
            HotKeySettingModel model,
            HotKey newHotKey,
            HotKey previousHotKey,
            string message)
        {
            try
            {
                bool confirm = await _dialogService.ShowConfirmAsync("快捷键冲突提醒", message);
                if (!confirm && model.HotKey == newHotKey)
                {
                    _acceptedHotKeys[model.ConfigPropertyName] = previousHotKey;
                    _rollingBackHotKeyProperties.Add(model.ConfigPropertyName);
                    model.HotKey = previousHotKey;
                }
            }
            catch
            {
            }
        }

        private void RemoveDuplicateHotKey(HotKeySettingModel current)
        {
            if (current.HotKey.IsEmpty) return;
            var list = GetAllNonDirectoryHotkey(HotKeys);
            foreach (var m in list)
            {
                if (m.HotKey.IsEmpty) continue;
                if (m.ConfigPropertyName != current.ConfigPropertyName && m.HotKey.Equals(current.HotKey))
                {
                    m.HotKey = HotKey.None;
                }
            }
        }

        public static List<HotKeySettingModel> GetAllNonDirectoryHotkey(IEnumerable<HotKeySettingModel> modelList)
        {
            var list = new List<HotKeySettingModel>();
            foreach (var model in modelList)
            {
                if (!model.IsDirectory) list.Add(model);
                list.AddRange(GetAllNonDirectoryChildren(model));
            }
            return list;
        }

        public static List<HotKeySettingModel> GetAllNonDirectoryChildren(HotKeySettingModel model)
        {
            var result = new List<HotKeySettingModel>();
            if (model.Children.Count == 0) return result;
            foreach (var child in model.Children)
            {
                if (!child.IsDirectory) result.Add(child);
                result.AddRange(GetAllNonDirectoryChildren(child));
            }
            return result;
        }

        private void BuildHotKeySettingModelList()
        {
            var rootModel = new HotKeySettingModel(
                "启动停止 TrustNTE",
                nameof(Config.HotKeyConfig.StartCaptureHotkey),
                Config.HotKeyConfig.StartCaptureHotkey,
                Config.HotKeyConfig.StartCaptureHotkeyType,
                (_, _) =>
                {
                    if (_automationService.CurrentHwnd == IntPtr.Zero) _automationService.Start();
                    else _automationService.Stop();
                }
            );
            HotKeys.Add(rootModel);

            var systemDir = new HotKeySettingModel("系统控制");
            systemDir.Children.Add(new HotKeySettingModel(
                "停止当前自动化任务",
                nameof(Config.HotKeyConfig.CancelTaskHotkey),
                Config.HotKeyConfig.CancelTaskHotkey,
                Config.HotKeyConfig.CancelTaskHotkeyType,
                (_, _) =>
                {
                    _automationService.Stop();
                    _logService.Log("快捷键触发：停止自动化任务");
                }
            ));
            systemDir.Children.Add(new HotKeySettingModel(
                "游戏截图",
                nameof(Config.HotKeyConfig.TakeScreenshotHotkey),
                Config.HotKeyConfig.TakeScreenshotHotkey,
                Config.HotKeyConfig.TakeScreenshotHotkeyType,
                (_, _) => { _logService.Log("快捷键触发：游戏截图 (TODO)"); }
            ));
            systemDir.Children.Add(new HotKeySettingModel(
                "打开日志窗口",
                nameof(Config.HotKeyConfig.OpenLogHotkey),
                Config.HotKeyConfig.OpenLogHotkey,
                Config.HotKeyConfig.OpenLogHotkeyType,
                (_, _) => { _logService.Log("快捷键触发：打开日志窗口 (TODO)"); }
            ));
            systemDir.Children.Add(new HotKeySettingModel(
                "打开实时预览窗口",
                nameof(Config.HotKeyConfig.OpenPreviewHotkey),
                Config.HotKeyConfig.OpenPreviewHotkey,
                Config.HotKeyConfig.OpenPreviewHotkeyType,
                (_, _) => { _logService.Log("快捷键触发：打开实时预览窗口 (TODO)"); }
            ));
            HotKeys.Add(systemDir);

            var realTimeDir = new HotKeySettingModel("实时任务开关");
            realTimeDir.Children.Add(new HotKeySettingModel(
                "自动拾取开关",
                nameof(Config.HotKeyConfig.AutoLootHotkey),
                Config.HotKeyConfig.AutoLootHotkey,
                Config.HotKeyConfig.AutoLootHotkeyType,
                (_, _) =>
                {
                    Config.AutoLootEnabled = !Config.AutoLootEnabled;
                    _configService.Save(Config);
                    _logService.Log($"快捷键触发：自动拾取已{(Config.AutoLootEnabled ? "开启" : "关闭")}");
                }
            ));
            realTimeDir.Children.Add(new HotKeySettingModel(
                "自动剧情开关",
                nameof(Config.HotKeyConfig.AutoDialogueHotkey),
                Config.HotKeyConfig.AutoDialogueHotkey,
                Config.HotKeyConfig.AutoDialogueHotkeyType,
                (_, _) =>
                {
                    Config.AutoDialogueEnabled = !Config.AutoDialogueEnabled;
                    _configService.Save(Config);
                    _logService.Log($"快捷键触发：自动剧情已{(Config.AutoDialogueEnabled ? "开启" : "关闭")}");
                }
            ));
            realTimeDir.Children.Add(new HotKeySettingModel(
                "自动战斗开关",
                nameof(Config.HotKeyConfig.AutoCombatHotkey),
                Config.HotKeyConfig.AutoCombatHotkey,
                Config.HotKeyConfig.AutoCombatHotkeyType,
                (_, _) =>
                {
                    Config.AutoCombatEnabled = !Config.AutoCombatEnabled;
                    _configService.Save(Config);
                    _logService.Log($"快捷键触发：自动战斗已{(Config.AutoCombatEnabled ? "开启" : "关闭")}");
                }
            ));
            HotKeys.Add(realTimeDir);
        }

        [RelayCommand]
        private void SwitchTypeAsync(HotKeySettingModel model)
        {
            if (model == null) return;

            model.HotKeyType = model.HotKeyType switch
            {
                HotKeyTypeEnum.None => HotKeyTypeEnum.KeyboardMonitor,
                HotKeyTypeEnum.KeyboardMonitor => HotKeyTypeEnum.GlobalRegister,
                HotKeyTypeEnum.GlobalRegister => HotKeyTypeEnum.None,
                _ => HotKeyTypeEnum.None
            };
        }

        public void Dispose()
        {
            var list = GetAllNonDirectoryHotkey(HotKeys);
            foreach (var m in list)
            {
                m.UnRegisterHotKey(_hotKeyService);
            }
        }
    }
}