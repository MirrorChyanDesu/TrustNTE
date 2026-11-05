using System.Windows;
using System.Windows.Input;
using TrustNTE.Models;

namespace TrustNTE.Views.Controls
{
    public class HotKeyTextBox : Wpf.Ui.Controls.TextBox
    {
        public static readonly DependencyProperty HotKeyProperty =
            DependencyProperty.Register(nameof(HotKey), typeof(HotKey), typeof(HotKeyTextBox),
                new FrameworkPropertyMetadata(HotKey.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotKeyChanged));

        public HotKey HotKey
        {
            get => (HotKey)GetValue(HotKeyProperty);
            set => SetValue(HotKeyProperty, value);
        }

        private static void OnHotKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotKeyTextBox textBox)
            {
                textBox.Text = (e.NewValue as HotKey)?.ToString() ?? "< None >";
            }
        }

        public HotKeyTextBox()
        {
            IsReadOnly = true;
            Focusable = true;
            Text = "< None >";
            TextAlignment = TextAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;

            // 强制使用暗色背景和白色字体
            this.SetResourceReference(BackgroundProperty, "ControlFillColorDefaultBrush");
            this.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
            this.BorderThickness = new Thickness(0);
            this.Padding = new Thickness(8, 4, 8, 4);

            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.Delete || e.Key == Key.Back || e.Key == Key.Escape)
            {
                HotKey = HotKey.None;
                return;
            }

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            HotKey = new HotKey { Key = e.Key, Modifiers = Keyboard.Modifiers };
        }
    }
}