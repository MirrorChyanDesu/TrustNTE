using System;
using System.Windows;
using System.Windows.Navigation;
using Wpf.Ui;
using TrustNTE.Views.Pages;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Windows
{
    public partial class StartErrorWindow : Wpf.Ui.Controls.FluentWindow
    {
        public StartErrorWindow()
        {
            InitializeComponent();
        }

        private void GotoCapture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var startPageVm = App.ServiceProvider?.GetService(typeof(StartPageViewModel)) as StartPageViewModel;
                startPageVm?.HandleAppNavigation("goto-interval"); // goto-interval 和 goto-engine 会触发 IsCaptureExpanderExpanded = true

                var navService = App.ServiceProvider?.GetService(typeof(INavigationService)) as INavigationService;
                navService?.Navigate(typeof(StartPage));

                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartErrorWindow] 跳转失败: {ex.Message}");
                this.Close(); 
            }
        }
    }
}