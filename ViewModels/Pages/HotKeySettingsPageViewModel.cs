using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using TrustNTE.Models;
using TrustNTE.Services.Implementations;
using System.Diagnostics;
using System.IO;

namespace TrustNTE.ViewModels.Pages
{
    /// <summary>
    /// 快捷键设置页：纯壳。树、注册、落盘、路由全部由 HotkeyCoordinatorService 持有。
    /// </summary>
    public partial class HotKeySettingsPageViewModel : ObservableObject
    {
        private readonly HotkeyCoordinatorService _coordinator;

        /// <summary>同一棵树：显示/改键/切类型直接落在 Coordinator 的模型上</summary>
        public ObservableCollection<HotKeySettingModel> HotKeys => _coordinator.HotKeys;

        public HotKeySettingsPageViewModel(HotkeyCoordinatorService coordinator)
        {
            _coordinator = coordinator;
        }



        // 打开 HTML 说明文档
        [RelayCommand]
        private void OpenHotkeyDoc()
        {
            try
            {
                // ★ 大牛核心修复：相对程序根目录解析，打包后无论装在哪都能找到
                string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Docs", "Web", "Nte_Hotkey.html");

                if (!File.Exists(path))
                {
                    System.Windows.MessageBox.Show($"未找到说明文档：\n{path}\n请检查文件是否随软件打包。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // ★ 大牛核心修复：UseShellExecute = true 是调用系统默认浏览器的硬性要求
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"打开文档失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 两态互切：键鼠监听 ⇄ 全局热键，无限循环，不经过“无”。
        /// 已绑定的按键保留不变（ PropertyChanged → Coordinator 自动落盘+重注册）。
        /// </summary>
        [RelayCommand]
        private void SwitchType(HotKeySettingModel model)
        {
            if (model == null) return;

            // 长按功能锁死键鼠监听（按钮已禁用，此处双保险）
            if (!model.AllowGlobal)
            {
                model.HotKeyType = HotKeyTypeEnum.KeyboardMonitor;
                return;
            }

            model.HotKeyType = model.HotKeyType == HotKeyTypeEnum.KeyboardMonitor
                ? HotKeyTypeEnum.GlobalRegister
                : HotKeyTypeEnum.KeyboardMonitor;
        }
    }
}
