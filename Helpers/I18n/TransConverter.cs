using System;
using System.Globalization;
using System.Windows.Data;

namespace TrustNTE.Helpers // 确保这里的命名空间和你项目里其他 Helpers 文件的命名空间一致
{
    public class TransConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string key)
            {
                return TranslationService.Instance[key];
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}