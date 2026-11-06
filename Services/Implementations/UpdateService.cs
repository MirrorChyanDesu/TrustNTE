using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models.Update;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigService _configService;
        private const string NoticeUrl = "https://raw.githubusercontent.com/TrustNTE/TrustNTE/main/notice.json";

        public UpdateService(IConfigService configService)
        {
            _configService = configService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TrustNTE-Updater");
        }

        private static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null) return "0.0.1";
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public async Task<UpdateCheckResult> CheckUpdateAsync(UpdateOption option)
        {
            try
            {
                var (remoteVersion, releaseNote, downloadUrl, sha256) = await GetLatestVersionInfoAsync(option);
                string currentVersion = GetCurrentVersion();

                if (string.IsNullOrEmpty(remoteVersion))
                {
                    return new UpdateCheckResult
                    {
                        CurrentVersion = currentVersion,
                        Option = option,
                        NeedPrompt = false,
                        ShowUpToDateMessage = option.Trigger == UpdateTrigger.Manual
                    };
                }

                if (!Global.IsNewVersion(remoteVersion, currentVersion))
                {
                    return new UpdateCheckResult
                    {
                        RemoteVersion = remoteVersion,
                        CurrentVersion = currentVersion,
                        ReleaseNote = releaseNote ?? string.Empty,
                        DownloadUrl = downloadUrl,
                        Sha256 = sha256,
                        Option = option,
                        NeedPrompt = false,
                        ShowUpToDateMessage = option.Trigger == UpdateTrigger.Manual
                    };
                }

                bool needPrompt = true;
                if (option.Trigger == UpdateTrigger.Auto)
                {
                    var config = _configService.Load();
                    if (!string.IsNullOrEmpty(config.NotShowNewVersionNoticeEndVersion) &&
                        !Global.IsNewVersion(remoteVersion, config.NotShowNewVersionNoticeEndVersion))
                    {
                        needPrompt = false;
                    }
                }

                return new UpdateCheckResult
                {
                    RemoteVersion = remoteVersion,
                    CurrentVersion = currentVersion,
                    ReleaseNote = releaseNote ?? string.Empty,
                    DownloadUrl = downloadUrl,
                    Sha256 = sha256,
                    Option = option,
                    NeedPrompt = needPrompt,
                    ShowUpToDateMessage = false
                };
            }
            catch (TaskCanceledException)
            {
                // 捕获超时
                throw new TimeoutException("连接更新服务器超时，请检查网络连接。");
            }
        }

        private async Task<(string? version, string? releaseNote, string? downloadUrl, string? sha256)> GetLatestVersionInfoAsync(UpdateOption option)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(NoticeUrl);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                // 全员可见
                int gray = root.TryGetProperty("gray", out var grayProp) && grayProp.ValueKind == JsonValueKind.Number
                    ? grayProp.GetInt32()
                    : 100;

                if (gray < 100)
                {
                    string deviceId = DeviceIdHelper.GetDeviceId();
                    var hash = MD5.HashData(Encoding.UTF8.GetBytes(deviceId));
                    var bucket = BitConverter.ToUInt32(hash, 0) % 100;
                    if (bucket >= gray) return (null, null, null, null);
                }


                string version = root.GetProperty("version").GetString() ?? "";

                string releaseNote = "";
                if (root.TryGetProperty("release_note", out var noteProp))
                {
                    releaseNote = noteProp.GetString() ?? "";
                }

                string? downloadUrl = null;
                if (root.TryGetProperty("download_url_template", out var urlProp))
                {
                    downloadUrl = urlProp.GetString()?.Replace("{version}", version);
                }

                string? sha256 = null;
                if (root.TryGetProperty("sha256", out var shaProp) &&
                    shaProp.ValueKind == JsonValueKind.Object &&
                    shaProp.TryGetProperty(version, out var versionSha))
                {
                    sha256 = versionSha.GetString();
                }

                return (version, releaseNote, downloadUrl, sha256);
            }
            catch (TaskCanceledException)
            {
                // HttpClient 超时引发的 TaskCanceledException
                throw new TimeoutException("连接更新服务器超时，请检查网络连接。");
            }
        }

        public bool TryLaunchUpdater(string baseArgs, out string? error)
        {
            error = null;
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "TrustNTE.update.exe");
            if (!File.Exists(updaterPath))
            {
                error = "更新程序不存在，请重新下载完整安装包。";
                return false;
            }

            string fullArgs = $"{baseArgs} --restart-from-pid={Environment.ProcessId}";

            try
            {
                var psi = new ProcessStartInfo(updaterPath, fullArgs)
                {
                    UseShellExecute = false,
                    WorkingDirectory = AppContext.BaseDirectory
                };
                using var proc = Process.Start(psi);

                if (proc == null || proc.HasExited)
                {
                    error = "更新程序启动失败！";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"启动更新程序失败：{ex.Message}";
                return false;
            }
        }
    }
}