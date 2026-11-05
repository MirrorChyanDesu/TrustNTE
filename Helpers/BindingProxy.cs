using System.Windows;

namespace TrustNTE.Helpers
{
    /// <summary>
    /// 用于解决 WPF 弹出层（如 ContextMenu）无法通过 FindAncestor 找到父级 DataContext 的辅助类。
    /// 继承自 <see cref="Freezable"/> 以便在弹出层中被 StaticResource 引用。
    /// </summary>
    public class BindingProxy : Freezable
    {
        /// <summary>
        /// 数据属性，绑定源。
        /// </summary>
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(
                nameof(Data),
                typeof(object),
                typeof(BindingProxy),
                new UIPropertyMetadata(null));

        /// <summary>
        /// 数据属性访问器。
        /// </summary>
        public object? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        /// <inheritdoc/>
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }
    }
}