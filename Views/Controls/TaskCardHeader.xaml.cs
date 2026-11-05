using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace TrustNTE.Views.Controls
{
    public partial class TaskCardHeader : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(TaskCardHeader), new PropertyMetadata(string.Empty));
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(TaskCardHeader), new PropertyMetadata(string.Empty));
        public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(SymbolRegular), typeof(TaskCardHeader), new PropertyMetadata(SymbolRegular.TaskListSquareLtr24));
        public SymbolRegular Icon { get => (SymbolRegular)GetValue(IconProperty); set => SetValue(IconProperty, value); }

        public static readonly DependencyProperty IconImageProperty = DependencyProperty.Register(nameof(IconImage), typeof(ImageSource), typeof(TaskCardHeader), new PropertyMetadata(null));
        public ImageSource IconImage { get => (ImageSource)GetValue(IconImageProperty); set => SetValue(IconImageProperty, value); }

        public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(TaskCardHeader), new PropertyMetadata(null));
        public object HeaderContent { get => GetValue(HeaderContentProperty); set => SetValue(HeaderContentProperty, value); }

        public TaskCardHeader()
        {
            InitializeComponent();
        }
    }
}