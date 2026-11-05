using System;
using TrustNTE.ViewModels;
using Wpf.Ui.Controls;

namespace TrustNTE.Views.Windows
{
    /// <summary>
    /// 黑白名单配置对话框。
    /// </summary>
    public partial class ListConfigDialog : FluentWindow
    {
        /// <summary>
        /// 初始化 <see cref="ListConfigDialog"/> 实例。
        /// </summary>
        /// <param name="viewModel">对话框的 ViewModel。</param>
        public ListConfigDialog(ListConfigDialogViewModel viewModel)
        {
            // [模块A]    改原因：构造函数改为接收 ViewModel，DataContext 由外部注入
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
        }

        // [模块A]    改原因：原 BtnWhite_Click / BtnBlack_Click / BtnConfirm_Click /
        // BtnCancel_Click / BtnHelp_Click / UpdateUI 等方法已整体迁入 VM，
        // View 仅订阅 CloseRequested 将结果写回 Window.DialogResult。
        private void OnCloseRequested(object? sender, EventArgs e)
        {
            if (sender is ListConfigDialogViewModel vm)
            {
                DialogResult = vm.DialogResult;
            }
        }
    }
}