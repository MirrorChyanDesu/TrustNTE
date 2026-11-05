using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TrustNTE.ViewModels
{
    public partial class MapMatchDebugWindowViewModel : ObservableObject
    {
        [ObservableProperty] private BitmapImage? _mapImage;
        [ObservableProperty] private double _markerX;
        [ObservableProperty] private double _markerY;
        [ObservableProperty] private bool _isMarkerVisible;

        [ObservableProperty] private string _statusText = "等待匹配...";

        public MapMatchDebugWindowViewModel()
        {
            _ = LoadMapAsync();
        }

        private async Task LoadMapAsync()
        {
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Map.png");
            if (!File.Exists(mapPath)) mapPath = @"D:\TrustNTE\Assets\Map.png";

            if (!File.Exists(mapPath))
            {
                StatusText = "找不到大地图文件";
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(mapPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // 防止文件占用
                    bitmap.EndInit();
                    bitmap.Freeze(); // 跨线程安全

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        MapImage = bitmap;
                    });
                });
            }
            catch (Exception ex)
            {
                StatusText = $"地图加载失败: {ex.Message}";
            }
        }

        public void UpdateMarker(double x, double y)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                MarkerX = x;
                MarkerY = y;
                IsMarkerVisible = true;
                StatusText = $"当前坐标: X={x:F0}, Y={y:F0}";
            });
        }

        public void ClearMarker()
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsMarkerVisible = false;
                StatusText = "特征不足，匹配失败";
            });
        }
    }
}