using System.Windows;
using System.Windows.Input;
using TrustNTE.ViewModels;
using Wpf.Ui.Controls;

// [   复] 强制使用 WPF 的别名，彻底避开 WinForms 和 OpenCV 的污染
using WpfPoint = System.Windows.Point;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace TrustNTE.Views.Windows
{
    public partial class AutoFishingPreviewWindow : FluentWindow
    {
        public AutoFishingPreviewWindow()
        {
            InitializeComponent();
            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (OverlayCanvas != null)
            {
                OverlayCanvas.MouseLeftButtonDown += OverlayCanvas_MouseLeftButtonDown;
                OverlayCanvas.MouseMove += OverlayCanvas_MouseMove;
                OverlayCanvas.MouseLeftButtonUp += OverlayCanvas_MouseLeftButtonUp;
                OverlayCanvas.LostMouseCapture += OverlayCanvas_LostMouseCapture;

                OverlayCanvas.MouseRightButtonDown += (s, ev) =>
                {
                    if (DataContext is AutoFishingPreviewViewModel vm && vm.IsSelecting)
                    {
                        vm.CancelSelection();
                        OverlayCanvas.ReleaseMouseCapture();
                        ev.Handled = true;
                    }
                };
            }
        }

        private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AutoFishingPreviewViewModel vm)
            {
                // 此处 e.GetPosition 返回的是 System.Windows.Point，直接传给 vm
                vm.StartSelection(e.GetPosition(PreviewImg), PreviewImg.ActualWidth, PreviewImg.ActualHeight);
                if (vm.IsSelecting)
                {
                    OverlayCanvas.CaptureMouse();
                }
            }
        }

        private void OverlayCanvas_MouseMove(object sender, WpfMouseEventArgs e)
        {
            if (DataContext is AutoFishingPreviewViewModel vm)
            {
                vm.UpdateSelection(e.GetPosition(PreviewImg), PreviewImg.ActualWidth, PreviewImg.ActualHeight);
            }
        }

        private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AutoFishingPreviewViewModel vm)
            {
                vm.EndSelection(e.GetPosition(PreviewImg), PreviewImg.ActualWidth, PreviewImg.ActualHeight);
                OverlayCanvas.ReleaseMouseCapture();
            }
        }

        private void OverlayCanvas_LostMouseCapture(object sender, WpfMouseEventArgs e)
        {
            if (DataContext is AutoFishingPreviewViewModel vm)
            {
                vm.CancelSelection();
            }
        }
    }
}