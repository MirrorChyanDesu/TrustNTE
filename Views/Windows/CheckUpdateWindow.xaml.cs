using System;
using System.Windows;
using TrustNTE.ViewModels;
using Wpf.Ui.Controls;

namespace TrustNTE.Views.Windows
{
    /// <summary>
    /// 更新提示窗口。
    /// </summary>
    public partial class CheckUpdateWindow : FluentWindow
    {
        /// <summary>
        /// 初始化 <see cref="CheckUpdateWindow"/> 实例。
        /// </summary>
        /// <param name="viewModel">窗口的 ViewModel。</param>
        // [模块E]    改原因：构造函数由原 (string, string, string?, UpdateOption, ...) 改为接收 CheckUpdateWindowViewModel，
        // 删除 WindowTitle / ReleaseNote 属性、BtnXxx_Click 事件处理器、ExecuteUpdate 业务逻辑，全部迁入 VM
        public CheckUpdateWindow(CheckUpdateWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
            viewModel.CloseApplicationRequested += OnCloseApplicationRequested;
        }

        // [模块E]    改原因：VM 请求普通关闭时，转发到 Window.DialogResult，这是纯 UI 生命周期操作
        private void OnCloseRequested(object? sender, EventArgs e)
        {
            if (sender is CheckUpdateWindowViewModel vm)
            {
                DialogResult = vm.DialogResult;
            }
        }

        // [模块E]    改原因：VM 请求关闭应用时（更新器已成功启动），关闭窗口并退出应用，
        // 这是 App 生命周期操作，属于 View 的职责范围
        private void OnCloseApplicationRequested(object? sender, EventArgs e)
        {
            DialogResult = true;
            Application.Current.Shutdown();
        }
    }
}