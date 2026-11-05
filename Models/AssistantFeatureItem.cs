using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace TrustNTE.Models
{
    public partial class AssistantFeatureItem : ObservableObject
    {
        // 唯一标识
        public string Key { get; init; } = string.Empty;
        
        // 标题
        public string Title { get; init; } = string.Empty;
        
        // 描述
        public string Description { get; init; } = string.Empty;
        
        // 是否为占位模块（暂时不可用的功能）
        public bool IsPlaceholder { get; init; }
        
        // 是否启用（真实功能用）
        [ObservableProperty] 
        private bool _isEnabled;

        // 状态变更事件，通知 ViewModel 进行配置保存
        public event EventHandler? StateChanged;

        partial void OnIsEnabledChanged(bool value)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}