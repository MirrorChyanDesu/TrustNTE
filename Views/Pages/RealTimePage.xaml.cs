using System.Windows.Controls;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    public partial class RealTimePage : UserControl
    {
        public RealTimePage(RealTimePageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}