using System;
using System.Threading;
using System.Threading.Tasks;

namespace TrustNTE.Services.Interfaces
{
    public interface IModelDownloadService
    {
        Task<bool> DownloadAndExtractAsync(string url, string targetDir, IProgress<double> progress, CancellationToken token = default);
    }
}