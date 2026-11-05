using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using TrustNTE.Helpers;
using TrustNTE.Models;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel
    {
        public ObservableCollection<string> Logs { get; } = new();

        //     暴露给界面用于颜色绑定
        public AppConfig Config => _config;

        [ObservableProperty] private bool _isLogEnabled = true;
        [ObservableProperty] private string _logWindowBtnText = TranslationService.Instance["打开日志"];
        [ObservableProperty] private double _maskOpacity = 0.1;
        [ObservableProperty] private double _maskTextOpacity = 1.0;

        public int MaskPositionIndex
        {
            get => _config.MaskPosition;
            set
            {
                if (_config.MaskPosition != value)
                {
                    _config.MaskPosition = value;
                    OnPropertyChanged();
                    SaveCurrentConfig();
                }
            }
        }

        partial void OnMaskOpacityChanged(double value)
        {
            SaveCurrentConfig();
            _windowService?.SetOverlayBackgroundOpacity(value);
        }

        partial void OnMaskTextOpacityChanged(double value)
        {
            SaveCurrentConfig();
            _windowService?.SetOverlayTextOpacity(value);
        }

        partial void OnIsLogEnabledChanged(bool value) => _logService.IsEnabled = value;

        private void OnLogColorChanged()
        {
            LogPalette.UpdateFromConfig(_config);
            SaveCurrentConfig();
            OnPropertyChanged(nameof(Config));
        }

        [RelayCommand]
        private void ResetLogColors()
        {
            LogPalette.ResetToDefault(_config);
            OnLogColorChanged();
        }

        [RelayCommand]
        private void PickColor(string colorPropertyName)
        {
            using var dialog = new ColorDialog();
            dialog.FullOpen = true;

            try
            {
                var currentHex = GetColorPropertyValue(colorPropertyName);
                dialog.Color = System.Drawing.ColorTranslator.FromHtml(currentHex);
            }
            catch { }

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                SetColorPropertyValue(colorPropertyName, hex);
                OnLogColorChanged();
            }
        }

        private string GetColorPropertyValue(string propName)
        {
            var prop = _config.GetType().GetProperty(propName);
            return prop?.GetValue(_config)?.ToString() ?? "#FFFFFF";
        }

        private void SetColorPropertyValue(string propName, string hex)
        {
            var prop = _config.GetType().GetProperty(propName);
            prop?.SetValue(_config, hex);
        }
    }
}