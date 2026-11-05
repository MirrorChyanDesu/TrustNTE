using Wpf.Ui.Controls;

namespace TrustNTE.Views.Windows
{
    public partial class AutoMusicDocWindow : FluentWindow
    {
        public AutoMusicDocWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}