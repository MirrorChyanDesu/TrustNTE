using CommunityToolkit.Mvvm.ComponentModel; // 必须加
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Threading;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel
    {
        [ObservableProperty] private string _currentCarouselImage = "/Assets/TrustNTE.png";
        private DispatcherTimer? _carouselTimer;
        private int _carouselIndex = 0;
        private readonly string[] _carouselImages = new[]
        {
            "/Assets/TrustNTE.png", "/Assets/TrustNTE1.png", "/Assets/TrustNTE2.png", "/Assets/TrustNTE3.png"
        };

        public double Dot0Opacity => _carouselIndex == 0 ? 1.0 : 0.4;
        public double Dot1Opacity => _carouselIndex == 1 ? 1.0 : 0.4;
        public double Dot2Opacity => _carouselIndex == 2 ? 1.0 : 0.4;
        public double Dot3Opacity => _carouselIndex == 3 ? 1.0 : 0.4;

        private void InitializeCarousel()
        {
            _carouselTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _carouselTimer.Tick += (s, e) =>
            {
                _carouselIndex = (_carouselIndex + 1) % _carouselImages.Length;
                CurrentCarouselImage = _carouselImages[_carouselIndex];
                OnPropertyChanged(nameof(Dot0Opacity));
                OnPropertyChanged(nameof(Dot1Opacity));
                OnPropertyChanged(nameof(Dot2Opacity));
                OnPropertyChanged(nameof(Dot3Opacity));
            };
            _carouselTimer.Start();
        }

        private void DisposeCarousel()
        {
            _carouselTimer?.Stop();
        }

        [RelayCommand]
        private void DotClick(string indexStr)
        {
            if (int.TryParse(indexStr, out int index) && index >= 0 && index < _carouselImages.Length)
            {
                _carouselIndex = index;
                CurrentCarouselImage = _carouselImages[_carouselIndex];
                OnPropertyChanged(nameof(Dot0Opacity));
                OnPropertyChanged(nameof(Dot1Opacity));
                OnPropertyChanged(nameof(Dot2Opacity));
                OnPropertyChanged(nameof(Dot3Opacity));
                _carouselTimer?.Stop();
                _carouselTimer?.Start();
            }
        }
    }
}