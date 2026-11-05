using System;
using System.ComponentModel;
using System.Windows;
using TrustNTE.ViewModels;
using TrustNTE.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace TrustNTE.Views
{
    public partial class MainWindow : FluentWindow, INavigationWindow
    {
        private readonly INavigationService _navigationService;
        private bool _isExitRequested = false;

        public MainWindow(INavigationService navigationService, INavigationViewPageProvider pageProvider, MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            _navigationService = navigationService;
            RootNavigation.SetPageProviderService(pageProvider);
            _navigationService.SetNavigationControl(RootNavigation);

            this.Loaded += MainWindow_Loaded;
            this.StateChanged += MainWindow_StateChanged;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= MainWindow_Loaded;
            _navigationService.Navigate(typeof(StartPage));

            if (DataContext is MainWindowViewModel vm)
            {
                await vm.RunStartupTasksAsync();
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                MaxIcon.Symbol = SymbolRegular.SquareMultiple24;
            else
                MaxIcon.Symbol = SymbolRegular.Square24;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExitRequested)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as IDisposable)?.Dispose();
            base.OnClosed(e);
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e) => this.Hide();

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            _isExitRequested = true;
            Application.Current.Shutdown();
        }

        // ═══ 兑换码 Toast 关闭 ═══
        private void RedeemToastClose_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsRedeemInfoBarOpen = false;
            }
        }

        private void OnNotifyIconLeftDoubleClick(object sender, RoutedEventArgs e) => ShowWindow();

        private void OnShowWindowClick(object sender, RoutedEventArgs e) => ShowWindow();

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            _isExitRequested = true;
            Application.Current.Shutdown();
        }

        public void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        public INavigationView GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => _navigationService.Navigate(pageType);
        public void SetPageService(INavigationViewPageProvider pageProvider) => RootNavigation.SetPageProviderService(pageProvider);
        public void SetServiceProvider(IServiceProvider serviceProvider) { }
        public void CloseWindow() => Close();
    }
}