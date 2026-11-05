using System;
using System.Threading.Tasks;
using System.Windows;
using TrustNTE.Services.Interfaces;
using Wpf.Ui.Controls;

namespace TrustNTE.Services.Implementations
{
    public class DialogService : IDialogService
    {
        public async Task ShowInfoAsync(string title, string content)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                Owner = Application.Current.MainWindow
            }.ShowDialogAsync();
        }

        public async Task<bool> ShowConfirmAsync(string title, string content)
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                Owner = Application.Current.MainWindow
            };
            var result = await box.ShowDialogAsync();
            return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
        }
    }
}