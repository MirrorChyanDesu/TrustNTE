using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    public partial class HotKeySettingsPage : UserControl
    {
        public HotKeySettingsPage(HotKeySettingsPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        // 整行点击 = 展开/收起。e.Handled=true 屏蔽 TreeViewItem 原生的“双击才切换 + 单击选中”
        private void HeaderRow_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.ClickCount > 1) return;

            var fe = sender as FrameworkElement;
            var tvi = fe?.TemplatedParent as TreeViewItem;
            if (tvi != null && tvi.HasItems)
                tvi.IsExpanded = !tvi.IsExpanded;
        }
    }
}