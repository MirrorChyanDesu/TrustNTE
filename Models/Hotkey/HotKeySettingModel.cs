using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Models
{
    /// <summary>
    /// 快捷键配置模型，支持树形结构
    /// </summary>
    public partial class HotKeySettingModel : ObservableObject
    {
        /// <summary>
        /// 功能名称
        /// </summary>
        public string FunctionName { get; init; } = string.Empty;

        /// <summary>
        /// 配置文件中的属性名
        /// </summary>
        public string ConfigPropertyName { get; init; } = string.Empty;

        /// <summary>
        /// 是否为目录（父级文件夹）
        /// </summary>
        public bool IsDirectory { get; init; }

        /// <summary>
        /// 子节点集合
        /// </summary>
        public ObservableCollection<HotKeySettingModel> Children { get; } = new();

        /// <summary>
        /// 当前绑定的快捷键
        /// </summary>
        [ObservableProperty]
        private HotKey _hotKey = HotKey.None;

        /// <summary>
        /// 快捷键类型
        /// </summary>
        [ObservableProperty]
        private HotKeyTypeEnum _hotKeyType = HotKeyTypeEnum.None;

        /// <summary>
        /// 是否是按住生效（长按）功能，如：自动飘逸。全局热键无法实现，只能键鼠监听。
        /// </summary>
        public bool IsHold { get; set; } = false;

        /// <summary>
        /// 是否允许使用全局热键（默认允许，长按功能设为 false）
        /// </summary>
        public bool AllowGlobal { get; set; } = true;

        /// <summary>
        /// 触发动作（按下）
        /// </summary>
        public Action<object?, EventArgs>? OnKeyDownAction { get; set; }

        /// <summary>
        /// ★ 新增：长按功能松开时的动作（仅 IsHold=true 的项使用）
        /// </summary>
        public Action? OnKeyReleasedAction { get; set; }

        /// <summary>
        /// 类型显示名称
        /// </summary>
        public string HotKeyTypeName => HotKeyType switch
        {
            HotKeyTypeEnum.GlobalRegister => "全局热键",
            HotKeyTypeEnum.KeyboardMonitor => "键鼠监听",
            _ => "无"
        };

        // ================= ★ 大牛 XAML 需要的计算属性 =================

        /// <summary>
        /// 长按类功能名显示"（长按生效）"后缀
        /// </summary>
        public string DisplayName => IsHold ? $"{FunctionName}（长按生效）" : FunctionName;

        /// <summary>
        /// 类型按钮的悬浮提示：可切/不可切给不同文案
        /// </summary>
        public string TypeToolTip => AllowGlobal
            ? "点击切换：键鼠监听 ⇄ 全局热键"
            : "长按生效的功能不支持全局热键（全局热键没有 KeyUp 事件）";

        // ==============================================================

        /// <summary>
        /// 构造函数：创建目录节点
        /// </summary>
        public HotKeySettingModel(string functionName)
        {
            FunctionName = functionName;
            IsDirectory = true;
        }

        /// <summary>
        /// 构造函数：创建功能节点
        /// </summary>
        public HotKeySettingModel(string functionName, string configPropertyName, string hotkeyStr, string typeStr, Action<object?, EventArgs>? onKeyDown = null)
        {
            FunctionName = functionName;
            ConfigPropertyName = configPropertyName;
            IsDirectory = false;
            OnKeyDownAction = onKeyDown;

            HotKey = HotKey.Parse(hotkeyStr);
            HotKeyType = Enum.TryParse<HotKeyTypeEnum>(typeStr, out var type) ? type : HotKeyTypeEnum.None;
        }

        /// <summary>
        /// ★ 核心补丁：当 HotKeyType 发生变化时，强制通知 UI 刷新 HotKeyTypeName（按钮文字）
        /// </summary>
        partial void OnHotKeyTypeChanged(HotKeyTypeEnum value)
        {
            OnPropertyChanged(nameof(HotKeyTypeName));
        }

        /// <summary>
        /// 注册全局热键
        /// </summary>
        public void RegisterHotKey(IHotKeyService service)
        {
            if (IsDirectory || HotKey.IsEmpty || HotKeyType != HotKeyTypeEnum.GlobalRegister) return;
            service.RegisterHotKey(ConfigPropertyName, HotKey, () => OnKeyDownAction?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>
        /// 注销全局热键
        /// </summary>
        public void UnRegisterHotKey(IHotKeyService service)
        {
            if (IsDirectory) return;
            service.UnregisterHotKey(ConfigPropertyName);
        }
    }
}