using System.Windows.Controls;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    public partial class StartPage : UserControl
    {
        public StartPage(StartPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}