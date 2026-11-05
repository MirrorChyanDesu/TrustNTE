using System;
using System.Windows;
using System.Windows.Media.Imaging;
using TrustNTE.ViewModels;
using TrustNTE.ViewModels.Pages.TaskModules;
using Wpf.Ui.Controls;

namespace TrustNTE.Views.Windows
{
    public partial class AutoMusicDebugWindow : FluentWindow
    {
        private AutoMusicViewModel? _vm;
        private bool _isDragging;
        private int _dragLaneIndex = -1;

        public AutoMusicDebugWindow()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                if (DataContext is AutoMusicDebugWindowViewModel dvm) _vm = dvm.Owner;
            };
        }

        private void Preview_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (_vm == null) return;
            double delta = e.Delta > 0 ? 0.05 : -0.05;
            _vm.Config.DebugFontScale = Math.Clamp(_vm.Config.DebugFontScale + delta, 0.3, 1.5);
        }

        private void Preview_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm == null || Preview.Source is not BitmapSource bmp) return;

            var pt = e.GetPosition(Preview);
            var framePt = ControlToFrame(pt, bmp);
            var points = new[] { _vm.Config.RoiD, _vm.Config.RoiF, _vm.Config.RoiJ, _vm.Config.RoiK };

            for (int i = 0; i < 4; i++)
            {
                double dx = framePt.X - points[i].X * bmp.PixelWidth;
                double dy = framePt.Y - points[i].Y * bmp.PixelHeight;

                double triggerRadius = Math.Max(_vm.Config.ObserveRadius * 3, 30);

                if (Math.Sqrt(dx * dx + dy * dy) < triggerRadius)
                {
                    _dragLaneIndex = i;
                    _isDragging = true;
                    Preview.CaptureMouse();

                    System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Hand;
                    break;
                }
            }
        }

        private void Preview_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging || _vm == null || Preview.Source is not BitmapSource bmp) return;

            var framePt = ControlToFrame(e.GetPosition(Preview), bmp);
            double nx = Math.Clamp(framePt.X / bmp.PixelWidth, 0.0, 1.0);
            double ny = Math.Clamp(framePt.Y / bmp.PixelHeight, 0.0, 1.0);

            switch (_dragLaneIndex)
            {
                case 0: _vm.Config.RoiD.X = nx; _vm.Config.RoiD.Y = ny; break;
                case 1: _vm.Config.RoiF.X = nx; _vm.Config.RoiF.Y = ny; break;
                case 2: _vm.Config.RoiJ.X = nx; _vm.Config.RoiJ.Y = ny; break;
                case 3: _vm.Config.RoiK.X = nx; _vm.Config.RoiK.Y = ny; break;
            }
        }

        private void Preview_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = false;
            _dragLaneIndex = -1;
            Preview.ReleaseMouseCapture();

            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        private System.Windows.Point ControlToFrame(System.Windows.Point p, BitmapSource bmp)
        {
            double ctrlW = Preview.ActualWidth, ctrlH = Preview.ActualHeight;
            double bmpW = bmp.PixelWidth, bmpH = bmp.PixelHeight;
            if (bmpW == 0 || bmpH == 0 || ctrlW == 0 || ctrlH == 0) return p;

            double scale = Math.Min(ctrlW / bmpW, ctrlH / bmpH);
            double renderW = bmpW * scale, renderH = bmpH * scale;
            double offX = (ctrlW - renderW) / 2, offY = (ctrlH - renderH) / 2;

            return new System.Windows.Point((p.X - offX) / scale, (p.Y - offY) / scale);
        }
    }
}