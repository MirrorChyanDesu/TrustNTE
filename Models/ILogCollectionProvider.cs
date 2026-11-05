using System.Collections.ObjectModel;

namespace TrustNTE.Models
{
    /// <summary>
    /// 表示持有日志集合的 ViewModel 契约，供 OverlayWindow 等视图组件依赖抽象而不依赖具体类型。
    /// </summary>
    public interface ILogCollectionProvider
    {
        /// <summary>
        /// 日志集合。
        /// </summary>
        ObservableCollection<string> Logs { get; }
    }
}