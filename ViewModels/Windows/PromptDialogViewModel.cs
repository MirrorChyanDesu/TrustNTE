using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace TrustNTE.ViewModels
{
    /// <summary>
    /// PromptDialog 的 ViewModel，承载通用文本输入对话框的数据和命令。
    /// </summary>
    public partial class PromptDialogViewModel : ObservableObject
    {
        /// <summary>
        /// 对话框标题，显示在 TitleBar 上。
        /// </summary>
        [ObservableProperty]
        private string _windowTitle;

        /// <summary>
        /// 输入框上方的提示文本。
        /// </summary>
        [ObservableProperty]
        private string _promptText;

        /// <summary>
        /// 用户在输入框中编辑的内容。
        /// </summary>
        [ObservableProperty]
        private string _inputText;

        /// <summary>
        /// 关闭对话框时使用的结果值。true 表示确认，false 表示取消，null 表示未做决定。
        /// </summary>
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// 当 ViewModel 请求关闭对话框时触发，由 View 订阅并设置 DialogResult。
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// 初始化 <see cref="PromptDialogViewModel"/> 实例。
        /// </summary>
        /// <param name="title">对话框标题。</param>
        /// <param name="prompt">输入框上方提示文本。</param>
        /// <param name="initialText">输入框初始内容。</param>
        public PromptDialogViewModel(string title, string prompt, string initialText = "")
        {
            _windowTitle = title;
            _promptText = prompt;
            _inputText = initialText;
        }

        /// <summary>
        /// 确认按钮命令：设置 DialogResult 为 true 并请求关闭。
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            DialogResult = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 取消按钮命令：设置 DialogResult 为 false 并请求关闭。
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}