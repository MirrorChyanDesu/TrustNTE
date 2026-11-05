using System.Windows.Controls;
using TrustNTE.ViewModels.Pages;

namespace TrustNTE.Views.Pages
{
    /// <summary>
    /// 设置页。
    /// </summary>
    public partial class SettingsPage : UserControl
    {
        /// <summary>
        /// 初始化 <see cref="SettingsPage"/> 实例。
        /// </summary>
        /// <param name="viewModel">页面 ViewModel。</param>
        public SettingsPage(SettingsPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}