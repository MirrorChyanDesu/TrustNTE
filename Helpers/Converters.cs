using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Controls;
using TrustNTE.Helpers;

namespace TrustNTE.Helpers
{
    public class ButtonAppearanceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? ControlAppearance.Danger : ControlAppearance.Primary;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ButtonIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? SymbolRegular.Stop24 : SymbolRegular.Play24;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value?.ToString() == parameter?.ToString();
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return parameter?.ToString() ?? Binding.DoNothing;
            return Binding.DoNothing;
        }
    }

    public class BooleanToInverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool boolValue && boolValue) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (object)(value != null ? Visibility.Visible : Visibility.Collapsed);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (object)(value == null ? Visibility.Visible : Visibility.Collapsed);
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// 虚拟键码与英文字母/数字之间的双向转换器。
    /// 用于解决 AutoFishingConfig 里 int 类型按键无法直接绑定 UI 文本框的问题。
    /// </summary>
    public class KeyNameToVkConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int vk)
            {
                // 尝试把虚拟键码转成英文字符串（如 70 -> F）
                Key key = KeyInterop.KeyFromVirtualKey(vk);
                if (key >= Key.A && key <= Key.Z) return key.ToString();
                if (key >= Key.D0 && key <= Key.D9) return key.ToString().Replace("D", "");
                if (key == Key.Space) return "Space";
                return vk.ToString();
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string input = value?.ToString()?.Trim().ToUpper() ?? string.Empty;
            if (string.IsNullOrEmpty(input)) return Binding.DoNothing;

            // 如果用户直接输入了数字 (如 70)
            if (int.TryParse(input, out int resultInt)) return resultInt;

            // 如果用户输入了字母 (如 F)
            if (Enum.TryParse<Key>(input, out var key))
            {
                return KeyInterop.VirtualKeyFromKey(key);
            }
            if (input == "SPACE" || input == "空格") return 0x20;

            // 转换失败（用户乱输），保持原值不变
            return Binding.DoNothing;
        }
    }
}