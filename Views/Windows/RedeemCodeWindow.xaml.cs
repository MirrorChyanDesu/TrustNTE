using System;
using System.Windows;

namespace TrustNTE.Views
{
    public partial class RedeemCodeWindow : Wpf.Ui.Controls.FluentWindow
    {
        public RedeemCodeWindow()
        {
            InitializeComponent();

            this.Closed += (s, e) => (DataContext as IDisposable)?.Dispose();
        }
    }
}