using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TrustNTE.Services.AutoRedeemCode
{
    /// <summary>一键兑换运行结果</summary>
    public sealed class RedemptionRunResult
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
        public List<string> FailedCodes { get; } = new();
        /// <summary>true=因互斥/前置失败未进入流程</summary>
        public bool Aborted { get; set; }
    }

    /// <summary>流程导航失败（锚点超时等），由 finally 统一兜底归位</summary>
    public sealed class RedemptionFlowException : System.Exception
    {
        public RedemptionFlowException(string message) : base(message) { }
    }

    /// <summary>
    /// 一键兑换核心服务（Singleton）。
    /// 全局单飞：任务页"自动兑换码"与未来的兑换码窗口"一键兑换"按钮共用同一实例与信号量。
    /// </summary>
    public interface IRedemptionService
    {
        /// <summary>是否有兑换任务正在执行</summary>
        bool IsRunning { get; }

        /// <summary>
        /// 执行一键兑换流程。
        /// 日志通过 progress 回调（由 ViewModel 转发到任务页日志区）。
        /// 无论成功/失败/取消，finally 均清剪贴板并 ESC 归位。
        /// </summary>
        Task<RedemptionRunResult> RunAsync(IReadOnlyList<string> codes, IProgress<string> progress, CancellationToken ct);
    }
}
