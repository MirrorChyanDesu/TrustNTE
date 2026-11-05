using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace TrustNTE.Helpers
{
    public class TransExtension : MarkupExtension
    {
        public string Key { get; set; }

        public TransExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding
            {
                Source = TranslationService.Instance,
                Path = new PropertyPath(nameof(TranslationService.RefreshTrigger)),
                Converter = new TransConverter(),
                ConverterParameter = Key,
                Mode = BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}