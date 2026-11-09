using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TrustNTE.Models;
using TrustNTE.ViewModels.Pages; // ★ 加上这个，用于 StartPageViewModel
using TrustNTE.ViewModels.Pages.TaskModules;

namespace TrustNTE.Services.Implementations
{
    public partial class HotkeyCoordinatorService
    {
        private void BuildTree()
        {
            // ── 根：启动停止 ──
            HotKeys.Add(new HotKeySettingModel("启动停止 TrustNTE", "StartStop", "", nameof(HotKeyTypeEnum.KeyboardMonitor),
                (_, _) => _dispatcherService.BeginInvoke(() =>
                {
                    if (_automationService.CurrentHwnd == IntPtr.Zero) _automationService.Start();
                    else _automationService.Stop();
                })));

            // ── 启动控制 ──
            var ctl = new HotKeySettingModel("启动控制");
            // ★ 接线 1：打开日志窗口
            ctl.Children.Add(Item("OpenLog", "打开日志", () =>
                _dispatcherService.BeginInvoke(() => LazyGet<StartPageViewModel>().OpenLogWindowCommand.Execute(null))));
            // ★ 接线 2：打开遮罩日志
            ctl.Children.Add(Item("OpenMaskLog", "打开遮罩日志", () =>
                _dispatcherService.BeginInvoke(() => LazyGet<StartPageViewModel>().ToggleMaskCommand.Execute(null))));
            // ★ 接线 3：打开预览窗口
            ctl.Children.Add(Item("OpenPreview", "打开预览窗口", () =>
                _dispatcherService.BeginInvoke(() => LazyGet<StartPageViewModel>().TogglePreviewWindowCommand.Execute(null))));
            HotKeys.Add(ctl);

            // ── 实时触发（按下 = 开关切换） ──
            var rt = new HotKeySettingModel("实时触发");
            AddToggle(rt, "AutoLoot", "自动拾取", c => c.AutoLootEnabled);
            AddToggle(rt, "AutoCombat", "自动战斗", c => c.AutoCombatEnabled);
            AddToggle(rt, "AutoStory", "自动剧情", c => c.AutoDialogueEnabled);
            AddToggle(rt, "DailyReward", "自动领取每日奖励", c => c.DailyRewardEnabled);
            HotKeys.Add(rt);

            // ── 独立任务（按下 = 没跑就启 / 跑着就停） ──
            var task = new HotKeySettingModel("独立任务");
            AddTask(task, "Fishing", "自动钓鱼", () => LazyGet<AutoFishingViewModel>());
            AddTask(task, "Music", "自动音游", () => LazyGet<AutoMusicViewModel>());
            AddTask(task, "Paw", "自动粉爪", () => LazyGet<AutoPawViewModel>());
            AddTask(task, "Apartment", "自动公寓", () => LazyGet<AutoApartmentViewModel>());
            AddTask(task, "Fountain", "喷泉签到", () => LazyGet<AutoFountainViewModel>());
            AddTask(task, "Market", "福荫集市", () => LazyGet<AutoMarketViewModel>());
            AddTask(task, "Gift", "羁绊礼物", () => LazyGet<AutoGiftViewModel>());
            AddTask(task, "Volleyball", "排球之心", () => LazyGet<AutoVolleyballHeartViewModel>());
            AddTask(task, "Redeem", "自动兑换码", () => LazyGet<AutoRedeemViewModel>());
            AddTask(task, "Hunt", "异象追猎", () => LazyGet<AutoHuntViewModel>());
            AddTask(task, "Pilgrimage", "异象巡礼", () => LazyGet<AutoPilgrimageViewModel>());
            HotKeys.Add(task);

            // ── 辅助操控 ──
            var assist = new HotKeySettingModel("辅助操控");
            AddHold(assist, "Drift", "自动飘逸", c => c.AutoDriftEnabled);
            for (int i = 2; i <= 5; i++)
                assist.Children.Add(new HotKeySettingModel($"待开发功能{i}", $"Assist{i}", "", "None"));
            HotKeys.Add(assist);

            // 从字典恢复持久化绑定
            foreach (var m in GetAllNonDirectoryHotkey(HotKeys)) RestoreOne(m);
        }

        // ────────────── 定义工厂 ──────────────

        private static HotKeySettingModel Item(string id, string name, Action onKeyDown,
            bool isHold = false, bool allowGlobal = true)
            => new(name, id, "", nameof(HotKeyTypeEnum.KeyboardMonitor), (_, _) => onKeyDown())
            { IsHold = isHold, AllowGlobal = allowGlobal };

        private void AddToggle(HotKeySettingModel parent, string id, string name,
            Expression<Func<AppConfig, bool>> prop)
        {
            var get = prop.Compile();
            var setter = CreateSetter(prop);
            parent.Children.Add(new HotKeySettingModel(name, id, "", nameof(HotKeyTypeEnum.KeyboardMonitor),
                (_, _) => _dispatcherService.BeginInvoke(() =>
                {
                    try
                    {
                        bool newVal = !get(Config);
                        setter(Config, newVal);
                        _configService.Save(Config);
                        _logService.Log($"[热键] {name} 已{(newVal ? "开启" : "关闭")}");
                    }
                    catch (Exception ex) { _logService.LogError($"[热键] {name} 切换失败: {ex.Message}"); }
                })));
        }

        private void AddHold(HotKeySettingModel parent, string id, string name,
            Expression<Func<AppConfig, bool>> prop)
        {
            var setter = CreateSetter(prop);
            void Set(bool v) => _dispatcherService.BeginInvoke(() =>
            {
                try { setter(Config, v); _configService.Save(Config); }
                catch (Exception ex) { _logService.LogError($"[热键] {name} 失败: {ex.Message}"); }
            });

            var item = new HotKeySettingModel(name, id, "", nameof(HotKeyTypeEnum.KeyboardMonitor),
                (_, _) => Set(true))
            { IsHold = true, AllowGlobal = false };
            item.OnKeyReleasedAction = () => Set(false);
            parent.Children.Add(item);
        }

        private void AddTask(HotKeySettingModel parent, string id, string name, Func<TaskModuleViewModelBase> getVm)
            => parent.Children.Add(new HotKeySettingModel(name, id, "", nameof(HotKeyTypeEnum.KeyboardMonitor),
                (_, _) => _dispatcherService.BeginInvoke(() => _ = ToggleTaskSafeAsync(getVm, name))));

        private async Task ToggleTaskSafeAsync(Func<TaskModuleViewModelBase> getVm, string name)
        {
            try
            {
                var vm = getVm();
                if (vm.IsRunning)
                {
                    await vm.StopCommand.ExecuteAsync(null);
                    _logService.Log($"[热键] 停止：{name}");
                }
                else
                {
                    await vm.StartCommand.ExecuteAsync(null);
                    if (vm.IsRunning) _logService.Log($"[热键] 启动：{name}");
                    else _logService.LogWarning($"[热键] {name} 未启动（可能刚停止还在收尾，稍后再按）");
                }
            }
            catch (Exception ex) { _logService.LogError($"[热键] {name} 启停失败: {ex.Message}"); }
        }

        private static Action<AppConfig, bool> CreateSetter(Expression<Func<AppConfig, bool>> prop)
        {
            var pi = (System.Reflection.PropertyInfo)((MemberExpression)prop.Body).Member;
            return (cfg, v) => pi.SetValue(cfg, v);
        }

        private static T LazyGet<T>() where T : class
            => App.ServiceProvider.GetRequiredService<T>();
    }
}