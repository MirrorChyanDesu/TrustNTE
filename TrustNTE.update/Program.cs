using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace TrustNTE.Update
{
    class Program
    {
        private static readonly string[] SkipFileNames = { "config.json", "TrustNTE.update.exe", "TrustNTE.update.dll", "TrustNTE.update.deps.json", "TrustNTE.update.runtimeconfig.json", "TrustNTE.update.pdb" };
        private static readonly string[] SkipDirNames = { "Logs", "log" };

        private static Mutex? _mutex;
        public static void ReleaseMutex() => _mutex?.Dispose();

        public static string? GithubUrl { get; private set; }
        public static string? TencentUrl { get; private set; }
        public static string? ExpectedSha256 { get; private set; }
        public static string SelectedUrl { get; set; } = "";

        public static async Task Start(string[] args)
        {
            _mutex = new Mutex(true, @"Local\TrustNTE_Update_Lock", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("更新程序已经在运行中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }

            Log("Updater 启动。参数：" + string.Join(" ", args));

            try
            {
                var p = ParseArgs(args);
                GithubUrl = p.GithubUrl ?? p.DownloadUrl;
                TencentUrl = p.TencentUrl;
                ExpectedSha256 = p.Sha256;
                SelectedUrl = GithubUrl ?? TencentUrl ?? "";

                if (string.IsNullOrWhiteSpace(SelectedUrl))
                {
                    Log("未提供下载链接。");
                    MessageBox.Show("未获取到有效的更新地址。", "更新错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                var win = new UpdateWindow();
                Application.Current.MainWindow = win;
                win.Show();

                win.InitSources(GithubUrl, TencentUrl, (url) => SelectedUrl = url);

                win.UpdateStatus("正在等待主程序退出...");
                await WaitMainExitedAsync(p.RestartFromPid);

                if (string.IsNullOrWhiteSpace(TencentUrl))
                {
                    // 👇 修复阻断编译错误：调用公开的 BeginUpdate
                    win.BeginUpdate();
                }
                else
                {
                    win.UpdateStatus("请选择安装源后点击更新");
                }
            }
            catch (Exception ex)
            {
                Log("初始化致命错误：" + ex);
                MessageBox.Show($"初始化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        public static async Task<bool> StartUpdateAsync(UpdateWindow win)
        {
            // 👇 提前声明 tempRoot，防止 catch 中无法清理
            string tempRoot = Path.Combine(AppContext.BaseDirectory, $"_update_{Guid.NewGuid():N}");
            try
            {
                string zipPath = Path.Combine(tempRoot, "update.zip");
                string extractDir = Path.Combine(tempRoot, "extract");
                string backupDir = Path.Combine(tempRoot, "backup");
                Directory.CreateDirectory(tempRoot);

                win.UpdateStatus("正在连接服务器...");
                try
                {
                    await DownloadAsync(SelectedUrl, zipPath, win);
                }
                catch (Exception ex) when (SelectedUrl == GithubUrl && !string.IsNullOrWhiteSpace(TencentUrl))
                {
                    Log($"GitHub 下载失败，尝试切换腾讯云: {ex.Message}");
                    win.UpdateStatus("GitHub 下载失败，正在切换腾讯云...");
                    SelectedUrl = TencentUrl!;
                    await DownloadAsync(SelectedUrl, zipPath, win);
                }

                if (!string.IsNullOrWhiteSpace(ExpectedSha256))
                {
                    win.UpdateStatus("正在校验文件完整性...");
                    string actual = ComputeSha256(zipPath);
                    if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("文件校验失败，更新包可能已损坏。");
                }

                win.UpdateStatus("正在解压更新包...");
                win.UpdateProgress(40);
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                string sourceDir = NormalizeExtractRoot(extractDir);
                if (!File.Exists(Path.Combine(sourceDir, "TrustNTE.exe")))
                    throw new Exception("更新包内未找到 TrustNTE.exe，无法替换。");

                win.UpdateStatus("正在替换程序文件...");
                win.UpdateProgress(60);
                bool ok = ReplaceFiles(sourceDir, AppContext.BaseDirectory, backupDir);
                if (!ok)
                {
                    Log("部分文件替换失败，开始回滚...");
                    Rollback(backupDir, AppContext.BaseDirectory);
                    throw new Exception("部分文件替换失败，已尝试回滚。");
                }

                try { Directory.Delete(tempRoot, true); } catch { }

                win.UpdateStatus("更新完成，即将启动...");
                win.UpdateProgress(100);
                await Task.Delay(1000);
                RestartMain();
                Application.Current.Shutdown();
                return true;
            }
            catch (Exception ex)
            {
                Log("更新失败：" + ex);
                win.UpdateStatus($"更新失败：{ex.Message}", isError: true);
                // 👇 修复垃圾残留：失败时删除临时目录
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
                return false;
            }
        }

        private sealed class Params
        {
            public int? RestartFromPid;
            public string? DownloadUrl;
            public string? GithubUrl;
            public string? TencentUrl;
            public string? Sha256;
        }

        private static Params ParseArgs(string[] args)
        {
            var p = new Params();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--restart-from-pid=", StringComparison.Ordinal) && int.TryParse(a[(a.IndexOf('=') + 1)..], out int pid1)) p.RestartFromPid = pid1;
                else if (a.StartsWith("--url=", StringComparison.Ordinal)) p.DownloadUrl = a[(a.IndexOf('=') + 1)..];
                else if (a.StartsWith("--gh-url=", StringComparison.Ordinal)) p.GithubUrl = a[(a.IndexOf('=') + 1)..];
                else if (a.StartsWith("--tx-url=", StringComparison.Ordinal)) p.TencentUrl = a[(a.IndexOf('=') + 1)..];
                else if (a.StartsWith("--sha256=", StringComparison.Ordinal)) p.Sha256 = a[(a.IndexOf('=') + 1)..];
            }
            return p;
        }

        private static async Task WaitMainExitedAsync(int? pid)
        {
            // 👇 修复超时丢失：加入 15 秒超时机制
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            if (pid.HasValue)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid.Value);
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch { }
                return;
            }
            foreach (var p in Process.GetProcessesByName("TrustNTE"))
            {
                try { using (p) await p.WaitForExitAsync(cts.Token); } catch { }
            }
        }

        private static async Task DownloadAsync(string url, string dest, UpdateWindow win)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TrustNTE-Updater/1.0");

            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new Exception("服务器上找不到该文件 (404)。");
                throw new Exception($"服务器拒绝连接 ({(int)resp.StatusCode})");
            }

            long? total = resp.Content.Headers.ContentLength;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(dest);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer, 0, n);
                read += n;
                if (total is > 0)
                {
                    int pct = (int)(read * 100 / total.Value);
                    win.UpdateStatus($"正在下载 ({read / 1024 / 1024}MB / {total / 1024 / 1024}MB)...");
                    win.UpdateProgress(pct * 0.4);
                }
            }
            Log($"下载完成: {dest}");
        }

        private static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs));
        }

        private static string NormalizeExtractRoot(string root)
        {
            var dirs = Directory.GetDirectories(root);
            var files = Directory.GetFiles(root);
            if (dirs.Length == 1 && files.Length == 0) return dirs[0];
            return root;
        }

        private static bool ReplaceFiles(string srcDir, string dstDir, string backupDir)
        {
            Directory.CreateDirectory(backupDir);
            bool allOk = true;
            foreach (var srcFile in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(srcDir, srcFile);
                string fileName = Path.GetFileName(rel);
                if (SkipFileNames.Any(s => string.Equals(s, fileName, StringComparison.OrdinalIgnoreCase)) ||
                    SkipDirNames.Any(d => string.Equals(rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0], d, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string dstFile = Path.Combine(dstDir, rel);
                string tmp = dstFile + ".new";
                try
                {
                    string? dir = Path.GetDirectoryName(dstFile);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    if (File.Exists(dstFile))
                    {
                        string bakFile = Path.Combine(backupDir, rel);
                        string? bakDir = Path.GetDirectoryName(bakFile);
                        if (!string.IsNullOrEmpty(bakDir)) Directory.CreateDirectory(bakDir);
                        File.Copy(dstFile, bakFile, true);
                    }

                    File.Copy(srcFile, tmp, true);
                    if (File.Exists(dstFile))
                    {
                        try { File.Replace(tmp, dstFile, null, true); }
                        catch (IOException) { File.Copy(tmp, dstFile, true); File.Delete(tmp); }
                    }
                    else File.Move(tmp, dstFile);
                }
                catch (Exception ex)
                {
                    Log($"替换失败: {rel} -> {ex.Message}");
                    allOk = false;
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }
            return allOk;
        }

        private static void Rollback(string backupDir, string dstDir)
        {
            if (!Directory.Exists(backupDir)) return;
            foreach (var bakFile in Directory.EnumerateFiles(backupDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(backupDir, bakFile);
                string dstFile = Path.Combine(dstDir, rel);
                try { File.Copy(bakFile, dstFile, true); } catch (Exception ex) { Log($"回滚失败: {rel} -> {ex.Message}"); }
            }
        }

        public static void RestartMain()
        {
            var mainExe = Path.Combine(AppContext.BaseDirectory, "TrustNTE.exe");
            if (!File.Exists(mainExe)) return;
            try { Process.Start(new ProcessStartInfo(mainExe, $"--restart-from-pid={Environment.ProcessId}") { UseShellExecute = true, WorkingDirectory = AppContext.BaseDirectory }); } catch { }
        }

        private static void Log(string msg)
        {
            try
            {
                string logDir = Path.Combine(AppContext.BaseDirectory, "log");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, $"update_{DateTime.Now:yyyyMMdd_HHmmss}.log"), $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }
    }
}