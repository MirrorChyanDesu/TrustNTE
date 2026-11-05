using System.Windows;
using System.Windows.Controls;

namespace TrustNTE.Views.Controls
{
    public partial class SettingItem : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingItem), new PropertyMetadata(string.Empty));
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingItem), new PropertyMetadata(string.Empty));
        public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public static new readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(SettingItem), new PropertyMetadata(null));
        public new object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public SettingItem()
        {
            InitializeComponent();
        }
    }
}