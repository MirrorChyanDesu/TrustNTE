using System;
using System.Windows;
using TrustNTE.ViewModels;
using Wpf.Ui.Controls;

namespace TrustNTE.Views.Windows
{
    public partial class CombatDebugWindow : FluentWindow
    {
        public CombatDebugWindow()
        {
            InitializeComponent();
            this.Closed += OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            if (DataContext is CombatDebugWindowViewModel vm)
            {
                vm.NotifyClosed();
            }
        }
    }
}