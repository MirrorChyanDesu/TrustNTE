using System.Windows.Controls;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    public partial class DispatcherPage : UserControl
    {
        public DispatcherPage(DispatcherPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}