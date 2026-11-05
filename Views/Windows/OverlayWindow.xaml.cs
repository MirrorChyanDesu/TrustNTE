using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

// 消除 Color 的命名冲突
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace TrustNTE.Views
{
    public partial class OverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public OverlayWindow()
        {
            InitializeComponent();
            LogConsole.MaxVisibleLines = 10;
            this.SourceInitialized += OverlayWindow_SourceInitialized;
        }

        private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        public void EnqueueLine(string line) { LogConsole.EnqueueLine(line); }

        public void SetBackgroundOpacity(double opacity)
        {
            byte a = (byte)(Math.Clamp(opacity, 0, 1) * 255);
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(a, 0x12, 0x12, 0x12));
        }

        public void SetTextOpacity(double opacity)
        {
            LogConsole.Opacity = Math.Clamp(opacity, 0, 1);
        }

        //     恢复你原版的直接赋值，不带任何 DPI 魔法
        public void ShowMask(double x, double y, double width, double height)
        {
            this.Left = x;
            this.Top = y;
            this.Width = width;
            this.Height = height;

            if (!this.IsVisible) this.Show();
        }

        public void HideMask()
        {
            if (this.IsVisible) this.Hide();
        }
    }
}