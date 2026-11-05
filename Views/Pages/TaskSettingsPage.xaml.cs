using System.Windows.Controls;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    public partial class TaskSettingsPage : UserControl
    {
        public TaskSettingsPage(TaskSettingsPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}