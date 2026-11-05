using System;
using System.Formats.Tar;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class ModelDownloadService : IModelDownloadService
    {
        private readonly ILogService _logService;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        public ModelDownloadService(ILogService logService)
        {
            _logService = logService;
        }

        public async Task<bool> DownloadAndExtractAsync(string url, string targetDir, IProgress<double> progress, CancellationToken token = default)
        {
            string tempTarPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tar");
            string tempExtractDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                _logService.Log($"开始下载模型: {url}");

                // 1. 流式下载
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                long readBytes = 0;

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                await using var fileStream = File.Create(tempTarPath);

                byte[] buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    readBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        progress?.Report((double)readBytes / totalBytes * 100);
                    }
                }

                fileStream.Close(); // 确保文件被释放

                // 2. 解压 .tar
                _logService.Log("下载完成，正在解压模型...");
                Directory.CreateDirectory(tempExtractDir);
                TarFile.ExtractToDirectory(tempTarPath, tempExtractDir, true);

                // 3. 寻找解压出来的子文件夹（因为官方包解压出来通常有一层 "PP-OCRv5_server_det_infer" 的壳）
                var dirs = Directory.GetDirectories(tempExtractDir);
                string sourceDir = dirs.Length == 1 ? dirs[0] : tempExtractDir;

                // 4. 移动到目标目录
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true); // 删掉旧模型
                }
                Directory.CreateDirectory(targetDir);

                // 复制所有文件
                foreach (var file in Directory.GetFiles(sourceDir))
                {
                    string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                _logService.Log($"✅ 模型已成功部署到: {targetDir}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"❌ 下载或解压失败: {ex.Message}");
                return false;
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(tempTarPath)) try { File.Delete(tempTarPath); } catch { }
                if (Directory.Exists(tempExtractDir)) try { Directory.Delete(tempExtractDir, true); } catch { }
            }
        }
    }
}