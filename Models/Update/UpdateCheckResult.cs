namespace TrustNTE.Models.Update
{
    /// <summary>
    /// 检查更新后的结构化结果，供调用方决定如何展示。
    /// </summary>
    public sealed class UpdateCheckResult
    {
        /// <summary>
        /// 远端最新版本号。
        /// </summary>
        public string RemoteVersion { get; init; } = string.Empty;

        /// <summary>
        /// 当前本地版本号。
        /// </summary>
        public string CurrentVersion { get; init; } = string.Empty;

        /// <summary>
        /// 远端发布的更新说明。
        /// </summary>
        public string ReleaseNote { get; init; } = string.Empty;

        public string? TencentUrl { get; init; }
        /// <summary>
        /// 更新包下载地址。
        /// </summary>
        public string? DownloadUrl { get; init; }

        /// <summary>
        /// 更新包 SHA256 校验值。
        /// </summary>
        public string? Sha256 { get; init; }

        /// <summary>
        /// 触发本次检查的选项。
        /// </summary>
        public UpdateOption Option { get; init; } = new();

        /// <summary>
        /// 是否需要向用户弹窗提示。false 表示已是最新或用户已忽略该版本。
        /// </summary>
        public bool NeedPrompt { get; init; }

        /// <summary>
        /// 是否需要向用户显示"已是最新版本"信息。仅在手动触发且无新版本时为 true。
        /// </summary>
        public bool ShowUpToDateMessage { get; init; }
    }
}