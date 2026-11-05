using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrustNTE.Services;

namespace TrustNTE.ViewModels
{
    public partial class CombatDebugWindowViewModel : ObservableObject
    {
        [ObservableProperty] private BitmapSource? _previewImage;
        public event Action? WindowClosed;

        private int _isRendering = 0;

        public CombatDebugWindowViewModel()
        {
            var service = App.ServiceProvider.GetService(typeof(AutomationService)) as AutomationService;
            if (service != null)
            {
                service.OnCombatDebugFrameCaptured += OnFrameCaptured;
            }
        }

        private void OnFrameCaptured(BitmapSource bmp)
        {
            if (Interlocked.Exchange(ref _isRendering, 1) == 1) return;

            Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                try
                {
                    PreviewImage = bmp;
                }
                finally
                {
                    Interlocked.Exchange(ref _isRendering, 0);
                }
            }));
        }

        public void NotifyClosed()
        {
            var service = App.ServiceProvider.GetService(typeof(AutomationService)) as AutomationService;
            if (service != null)
            {
                service.OnCombatDebugFrameCaptured -= OnFrameCaptured;
            }
            WindowClosed?.Invoke();
        }
    }
}