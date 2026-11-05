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
        /// 触发动作
        /// </summary>
        public Action<object?, EventArgs>? OnKeyDownAction { get; set; }

        /// <summary>
        /// 类型显示名称
        /// </summary>
        public string HotKeyTypeName => HotKeyType switch
        {
            HotKeyTypeEnum.GlobalRegister => "全局热键",
            HotKeyTypeEnum.KeyboardMonitor => "键鼠监听",
            _ => "无"
        };

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