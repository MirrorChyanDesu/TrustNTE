using System.Windows.Controls;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    public partial class RecordPage : UserControl
    {
        public RecordPage(RecordPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}