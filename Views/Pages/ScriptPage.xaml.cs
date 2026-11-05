using System.Windows.Controls;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    public partial class ScriptPage : UserControl
    {
        public ScriptPage(ScriptPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}