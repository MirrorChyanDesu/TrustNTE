using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using TrustNTE.ViewModels.Pages.TaskModules;   //     Owner 类型需要

namespace TrustNTE.ViewModels
{
    public partial class AutoMusicDebugWindowViewModel : ObservableObject
    {
        [ObservableProperty] private BitmapSource? _previewImage;
        public ObservableCollection<string> Logs { get; } = new();
        public event EventHandler? DebugWindowClosed;

        public AutoMusicViewModel? Owner { get; set; }

        public void UpdatePreviewImage(BitmapSource bmp)
        {
            Application.Current?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                PreviewImage = bmp;
            }));
        }

        public void AddLog(string message)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                Logs.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                if (Logs.Count > 50) Logs.RemoveAt(0);
            }));
        }

        public void NotifyWindowClosed()
        {
            DebugWindowClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}
