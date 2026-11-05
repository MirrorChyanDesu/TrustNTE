using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;

namespace TrustNTE.Helpers
{
    public static class ImageFadeBehavior
    {
        public static readonly DependencyProperty EnableFadeProperty =
            DependencyProperty.RegisterAttached("EnableFade", typeof(bool), typeof(ImageFadeBehavior), new PropertyMetadata(false, OnEnableFadeChanged));

        public static void SetEnableFade(DependencyObject element, bool value) => element.SetValue(EnableFadeProperty, value);
        public static bool GetEnableFade(DependencyObject element) => (bool)element.GetValue(EnableFadeProperty);

        private static void OnEnableFadeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.Image image)
            {
                if ((bool)e.NewValue)
                {
                    image.TargetUpdated += Image_TargetUpdated;
                }
                else
                {
                    image.TargetUpdated -= Image_TargetUpdated;
                }
            }
        }

        //    复 Nullability 警告：将 sender 改为 object?
        private static void Image_TargetUpdated(object? sender, DataTransferEventArgs e)
        {
            if (sender is System.Windows.Controls.Image image)
            {
                var animation = new DoubleAnimation
                {
                    From = 0.2,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                image.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        }
    }
}