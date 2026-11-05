using System;
using TrustNTE.ViewModels;
using Wpf.Ui.Controls;

namespace TrustNTE.Views.Windows
{
    /// <summary>
    /// 通用文本输入对话框。
    /// </summary>
    public partial class PromptDialog : FluentWindow
    {
        /// <summary>
        /// 初始化 <see cref="PromptDialog"/> 实例。
        /// </summary>
        /// <param name="viewModel">对话框的 ViewModel。</param>
        public PromptDialog(PromptDialogViewModel viewModel)
        {
            // [模块A]    改原因：构造函数改为接收 ViewModel，DataContext 由外部注入
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
        }

        // [模块A]    改原因：原 BtnConfirm_Click / BtnCancel_Click 逻辑已迁入 VM，
        // View 仅订阅 CloseRequested 将结果写回 Window.DialogResult。
        private void OnCloseRequested(object? sender, EventArgs e)
        {
            if (sender is PromptDialogViewModel vm)
            {
                DialogResult = vm.DialogResult;
            }
        }
    }
}