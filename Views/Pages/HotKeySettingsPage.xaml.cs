using System.Windows.Controls;
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
    }
}