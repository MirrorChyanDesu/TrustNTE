using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TrustNTE.Update
{
    public partial class UpdateWindow : Window
    {
        private bool _isUpdating = false;
        private bool _updateFailed = false;

        public UpdateWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating)
            {
                MessageBox.Show("正在更新中，请勿关闭！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            System.Windows.Application.Current.Shutdown();
        }

        private async void ActionBtn_Click(object sender, RoutedEventArgs e) => BeginUpdate();

        //     封装为公开行为，供 Program.cs 和事件处理器调用
        public async void BeginUpdate()
        {
            if (_isUpdating || _updateFailed) return;

            _isUpdating = true;
            ActionBtn.IsEnabled = false;
            SourcePanel.Visibility = Visibility.Collapsed;
            DownloadProgress.Visibility = Visibility.Visible;

            bool success = await Program.StartUpdateAsync(this);

            if (!success)
            {
                _isUpdating = false;
                _updateFailed = true;
                ActionBtn.Content = "启 动";
                ActionBtn.IsEnabled = true;
            }
        }

        public void InitSources(string? ghUrl, string? txUrl, Action<string> onSourceChanged)
        {
            if (!string.IsNullOrWhiteSpace(txUrl))
            {
                SourcePanel.Visibility = Visibility.Visible;
                if (!string.IsNullOrWhiteSpace(ghUrl))
                {
                    RbGithub.Checked += (s, e) => onSourceChanged(ghUrl);
                }
                else
                {
                    RbGithub.IsEnabled = false;
                    RbTencent.IsChecked = true;
                    onSourceChanged(txUrl);
                }
                RbTencent.Checked += (s, e) => onSourceChanged(txUrl);
            }
        }

        public void UpdateStatus(string message, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                StatusText.Foreground = isError ? new SolidColorBrush(Color.FromRgb(255, 82, 82)) : Brushes.White;
            });
        }

        public void UpdateProgress(double value)
        {
            Dispatcher.Invoke(() => DownloadProgress.Value = value);
        }
    }
}