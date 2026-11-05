using System;
using System.Windows;
using TrustNTE.ViewModels;
using Wpf.Ui.Controls;

namespace TrustNTE.Views
{
    public partial class LogWindow : FluentWindow
    {
        private readonly LogWindowViewModel _viewModel;

        public LogWindow(LogWindowViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            foreach (var log in _viewModel.Logs)
            {
                MainConsole.EnqueueLine(log);
            }
        }

        // 供外部 WindowService 调用的接收日志接口
        public void ReceiveLog(string message)
        {
            MainConsole.EnqueueLine(message);
        }

        // 清空界面按钮
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Logs.Clear();
            MainConsole.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}