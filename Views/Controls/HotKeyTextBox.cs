using System.Windows;
using System.Windows.Input;
using TrustNTE.Models;
using TrustNTE.Services;
using TrustNTE.Services.Implementations;

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

            this.SetResourceReference(BackgroundProperty, "ControlFillColorDefaultBrush");
            this.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
            this.BorderThickness = new Thickness(0);
            this.Padding = new Thickness(8, 4, 8, 4);

            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            // ★ 修复：按住 Alt 时 WPF 把 e.Key 报成 Key.System，真实键在 SystemKey
            //   原代码直接取 e.Key，导致 Alt 单按被录成 "Alt+System" 垃圾绑定
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Delete || key == Key.Back || key == Key.Escape)
            {
                HotKey = HotKey.None;
                return;
            }

            // 修饰键单按、Tab、IME 中间态：吞掉但不提交
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                     or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
                     or Key.None or Key.System or Key.Tab
                     or Key.ImeProcessed or Key.DeadCharProcessed)
            {
                return;
            }

            HotKey = new HotKey { Key = key, Modifiers = Keyboard.Modifiers };
        }

        // ★ 录入期间挂起全部全局热键：RegisterHotKey 的键会被系统截走（WM_HOTKEY），
        //   不挂起的话，"已被全局热键占用的键"永远到不了录入框——这正是
        //   你之前"设置 F3 没响应也没弹窗直接变空"的根因
        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            HotKeyCaptureGate.Begin();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            HotKeyCaptureGate.End();
        }
    }
}
