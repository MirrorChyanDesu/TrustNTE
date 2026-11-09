using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TrustNTE.Models;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public partial class HotkeyCoordinatorService : IDisposable
    {
        private readonly IHotKeyService _hotKeyService;
        private readonly IConfigService _configService;
        private readonly IDialogService _dialogService;
        private readonly IDispatcherService _dispatcherService;
        private readonly AutomationService _automationService;
        private readonly ILogService _logService;

        public AppConfig Config { get; set; }
        public ObservableCollection<HotKeySettingModel> HotKeys { get; } = new();

        private readonly Dictionary<string, HotKey> _acceptedHotKeys = new();
        private readonly HashSet<string> _rollingBackHotKeyProperties = new();
        private bool _isInitializing = true;

        private readonly Dictionary<string, string> _nteKeyConflicts = new()
        {
            { "F1", "异环探索指南" }, { "F2", "异环历练奖赏" }, { "F3", "异环斯卡布罗集市" }, { "F4", "异环活动页面" }, { "F5", "异环都市大亨" },
            { "F7", "异环隐藏主界面" }, { "F8", "异环抓拍" }, { "F9", "异环拍照" }, { "F11", "异环全屏/窗口切换" },
            { "D", "异环超强音 D 键音符" }, { "F", "异环超强音 F 键音符" }, { "J", "异环超强音 J 键音符" }, { "K", "异环超强音 K 键音符" }
        };

        public HotkeyCoordinatorService(
            IHotKeyService hotKeyService, IConfigService configService, IDialogService dialogService,
            IDispatcherService dispatcherService, AutomationService automationService, ILogService logService)
        {
            _hotKeyService = hotKeyService;
            _configService = configService;
            _dialogService = dialogService;
            _dispatcherService = dispatcherService;
            _automationService = automationService;
            _logService = logService;

            Config = _configService.Load();
            if (Config.HotKeyConfig == null) Config.HotKeyConfig = new HotKeyConfig();

            // ★ 录入闸门：录入框聚焦期间挂起/恢复全局热键注册
            HotKeyCaptureGate.Started += SuspendGlobalForCapture;
            HotKeyCaptureGate.Ended += ResumeGlobalAfterCapture;

            BuildTree();
            Initialize();
        }

        private void Initialize()
        {
            _hotKeyService.StartListening();
            _hotKeyService.OnKeyPressed += OnGlobalKeyPressed;
            _hotKeyService.OnKeyReleased += OnGlobalKeyReleased;

            var list = GetAllNonDirectoryHotkey(HotKeys);
            foreach (var model in list)
            {
                // ★ 老配置残留纠正：长按功能不允许全局热键（历史配置里可能是 GlobalRegister）
                if (!model.AllowGlobal && model.HotKeyType == HotKeyTypeEnum.GlobalRegister)
                {
                    model.HotKeyType = HotKeyTypeEnum.KeyboardMonitor;
                    UpdateHotKeyConfig(model);
                }

                _acceptedHotKeys[model.ConfigPropertyName] = model.HotKey;
                model.RegisterHotKey(_hotKeyService);

                model.PropertyChanged += (sender, e) =>
                {
                    if (sender is not HotKeySettingModel m) return;
                    if (_isInitializing) return;

                    if (e.PropertyName == nameof(HotKeySettingModel.HotKey))
                    {
                        if (_rollingBackHotKeyProperties.Remove(m.ConfigPropertyName))
                        {
                            // 回滚路径：只落盘，不再触发冲突/抢占（防二次弹窗）
                            UpdateHotKeyConfig(m);
                        }
                        else
                        {
                            var newHotKey = m.HotKey;
                            var previousHotKey = _acceptedHotKeys.GetValueOrDefault(m.ConfigPropertyName, HotKey.None);
                            UpdateHotKeyConfig(m);
                            _acceptedHotKeys[m.ConfigPropertyName] = newHotKey;

                            // ★ 后绑获胜：无条件立即抢占（不再被弹窗劫持）
                            var victim = RemoveDuplicateHotKey(m);

                            // 游戏键提醒：只是提醒，不阻塞绑定
                            WarnGameKeyConflictIfNeeded(m, newHotKey, previousHotKey, victim);
                        }
                    }
                    else if (e.PropertyName == nameof(HotKeySettingModel.HotKeyType))
                    {
                        UpdateHotKeyConfig(m);
                    }

                    m.UnRegisterHotKey(_hotKeyService);
                    m.RegisterHotKey(_hotKeyService);
                    // 注：UpdateHotKeyConfig 内部已 Save，这里不再重复落盘
                };
            }
            _isInitializing = false;
        }

        // ══════════ 录入闸门 ══════════

        private void SuspendGlobalForCapture()
        {
            foreach (var m in GetAllNonDirectoryHotkey(HotKeys))
                if (m.HotKeyType == HotKeyTypeEnum.GlobalRegister && !m.HotKey.IsEmpty)
                    m.UnRegisterHotKey(_hotKeyService);
        }

        private void ResumeGlobalAfterCapture()
        {
            foreach (var m in GetAllNonDirectoryHotkey(HotKeys))
                if (m.HotKeyType == HotKeyTypeEnum.GlobalRegister && !m.HotKey.IsEmpty)
                {
                    m.UnRegisterHotKey(_hotKeyService);
                    m.RegisterHotKey(_hotKeyService);
                }
        }

        // ══════════ 恢复持久化 ══════════

        private void RestoreOne(HotKeySettingModel m)
        {
            if (!Config.HotKeySettings.TryGetValue(m.ConfigPropertyName, out var it) || it.Key == 0) return;

            var key = KeyInterop.KeyFromVirtualKey(it.Key);

            // ★ 历史垃圾键（如 Alt 单按录成的 System）不恢复，并从配置中自清理
            if (key is Key.None or Key.System or Key.Tab or Key.ImeProcessed or Key.DeadCharProcessed)
            {
                Config.HotKeySettings.Remove(m.ConfigPropertyName);
                return;
            }

            m.HotKey = new HotKey { Key = key, Modifiers = (ModifierKeys)it.Modifiers };
            if (Enum.TryParse<HotKeyTypeEnum>(it.HotKeyType, out var t)) m.HotKeyType = t;
        }

        // ══════════ 钩子路由（录入期静音） ══════════

        private void OnGlobalKeyPressed(HotKey pressedHotKey)
        {
            if (HotKeyCaptureGate.IsCapturing) return;   // ★ 录入中不触发任何功能

            var list = GetAllNonDirectoryHotkey(HotKeys);
            foreach (var m in list)
            {
                if (m.HotKeyType == HotKeyTypeEnum.KeyboardMonitor && SameKey(m.HotKey, pressedHotKey))
                {
                    _dispatcherService.BeginInvoke(() => m.OnKeyDownAction?.Invoke(m, EventArgs.Empty));
                    break;
                }
            }
        }

        private void OnGlobalKeyReleased(HotKey releasedHotKey)
        {
            if (HotKeyCaptureGate.IsCapturing) return;   // ★ 同上

            var list = GetAllNonDirectoryHotkey(HotKeys);
            foreach (var m in list)
            {
                if (m.IsHold && m.HotKeyType == HotKeyTypeEnum.KeyboardMonitor && SameKey(m.HotKey, releasedHotKey))
                {
                    _dispatcherService.BeginInvoke(() => m.OnKeyReleasedAction?.Invoke());
                    break;
                }
            }
        }

        // ══════════ 树遍历 ══════════

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

        // ══════════ 落盘 ══════════

        private void UpdateHotKeyConfig(HotKeySettingModel m)
        {
            if (m.HotKey.IsEmpty) Config.HotKeySettings.Remove(m.ConfigPropertyName);
            else Config.HotKeySettings[m.ConfigPropertyName] = new HotKeyConfigItem
            {
                Id = m.ConfigPropertyName,
                Key = KeyInterop.VirtualKeyFromKey(m.HotKey.Key),
                Modifiers = (int)m.HotKey.Modifiers,
                HotKeyType = m.HotKeyType.ToString()
            };
            _configService.Save(Config);
        }

        // ══════════ 抢占（后绑获胜） ══════════

        /// <summary>清掉与本项冲突的旧绑定，返回被抢占的那一项（可能为 null）</summary>
        private HotKeySettingModel? RemoveDuplicateHotKey(HotKeySettingModel current)
        {
            if (current.HotKey.IsEmpty) return null;

            foreach (var m in GetAllNonDirectoryHotkey(HotKeys))
            {
                if (m.ConfigPropertyName == current.ConfigPropertyName) continue;
                if (m.HotKey.IsEmpty) continue;

                if (SameKey(m.HotKey, current.HotKey))
                {
                    _logService.LogWarning($"[快捷键] {FormatKey(current.HotKey)} 已改绑给【{current.FunctionName}】，【{m.FunctionName}】的原绑定已自动清空");
                    m.HotKey = HotKey.None;
                    return m;
                }
            }
            return null;
        }

        // ══════════ 游戏键提醒（事后、非阻塞） ══════════

        private void WarnGameKeyConflictIfNeeded(HotKeySettingModel model, HotKey newHotKey,
            HotKey previousHotKey, HotKeySettingModel? victim)
        {
            if (newHotKey.IsEmpty) return;
            if (newHotKey.Modifiers != ModifierKeys.None) return;
            if (!_nteKeyConflicts.TryGetValue(newHotKey.ToString(), out var gameName)) return;

            // 键鼠监听不吞键：日志提醒即可，不打断
            if (model.HotKeyType == HotKeyTypeEnum.KeyboardMonitor)
            {
                _logService.LogWarning($"[快捷键] {newHotKey} 与游戏功能【{gameName}】重合（底层监听）：按下时游戏与软件会同时触发");
                return;
            }

            // 全局独占会吞键：弹确认。注意绑定与抢占此时已生效，取消则整体还原
            _dispatcherService.BeginInvoke(() => _ = WarnGlobalAsync(model, newHotKey, previousHotKey, victim, gameName));
        }

        private async Task WarnGlobalAsync(HotKeySettingModel model, HotKey newHotKey,
            HotKey previousHotKey, HotKeySettingModel? victim, string gameName)
        {
            try
            {
                bool confirm = await _dialogService.ShowConfirmAsync("快捷键冲突提醒",
                    $"全局热键会独占 {newHotKey}，异环的【{gameName}】将无法收到该键。\n\n确定使用吗？");

                if (confirm) return;
                if (!SameKey(model.HotKey, newHotKey)) return;   // 弹窗期间用户又改了键，不动

                // 还原自己
                _acceptedHotKeys[model.ConfigPropertyName] = previousHotKey;
                _rollingBackHotKeyProperties.Add(model.ConfigPropertyName);
                model.HotKey = previousHotKey;

                // 还原被抢占的对方（走 rollback 分支：只落盘，不会反向弹窗/抢占）
                if (victim != null && victim.HotKey.IsEmpty)
                {
                    _acceptedHotKeys[victim.ConfigPropertyName] = newHotKey;
                    _rollingBackHotKeyProperties.Add(victim.ConfigPropertyName);
                    victim.HotKey = newHotKey;
                }
            }
            catch (Exception ex)
            {
                // 提醒失败不影响已完成的绑定与抢占
                _logService.LogError($"[快捷键] 冲突提醒失败: {ex.Message}");
            }
        }

        // ══════════ 工具 ══════════

        private static bool SameKey(HotKey a, HotKey b)
            => !a.IsEmpty && !b.IsEmpty && a.Key == b.Key && a.Modifiers == b.Modifiers;

        private static string FormatKey(HotKey hk)
            => (hk.Modifiers == ModifierKeys.None ? "" : $"{hk.Modifiers}+") + hk.Key;

        public void Dispose()
        {
            HotKeyCaptureGate.Started -= SuspendGlobalForCapture;
            HotKeyCaptureGate.Ended -= ResumeGlobalAfterCapture;

            _hotKeyService.OnKeyPressed -= OnGlobalKeyPressed;
            _hotKeyService.OnKeyReleased -= OnGlobalKeyReleased;
            foreach (var m in GetAllNonDirectoryHotkey(HotKeys)) m.UnRegisterHotKey(_hotKeyService);
            _hotKeyService.StopListening();
        }
    }
}
