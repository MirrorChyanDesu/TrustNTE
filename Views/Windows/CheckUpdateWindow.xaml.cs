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
        public CheckUpdateWindow(CheckUpdateWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += OnCloseRequested;
            viewModel.CloseApplicationRequested += OnCloseApplicationRequested;
        }

        private void OnCloseRequested(object? sender, EventArgs e)
        {
            if (sender is CheckUpdateWindowViewModel vm)
            {
                DialogResult = vm.DialogResult;
            }
        }

        private void OnCloseApplicationRequested(object? sender, EventArgs e)
        {
            DialogResult = true;
            Application.Current.Shutdown();
        }
    }
}