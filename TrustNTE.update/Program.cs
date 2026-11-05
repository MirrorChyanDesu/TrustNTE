using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TrustNTE.Update
{
    class Program
    {
        private static readonly string[] SkipFileNames =
        {
            "config.json",
            "TrustNTE.update.exe",
            "TrustNTE.update.dll",
            "TrustNTE.update.deps.json",
            "TrustNTE.update.runtimeconfig.json",
            "TrustNTE.update.pdb"
        };
        private static readonly string[] SkipDirNames = { "Logs", "log" };

        static async Task<int> Main(string[] args)
        {
            using var mutex = new Mutex(true, @"Local\TrustNTE_Update_Lock", out bool createdNew);
            if (!createdNew) return 1;

            string logDir = Path.Combine(AppContext.BaseDirectory, "log");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, $"update_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            void Log(string msg)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
            }

            Log("Updater 启动。参数：" + string.Join(" ", args));

            try
            {
                var p = ParseArgs(args);

                await WaitMainExited(p.RestartFromPid, Log);

                if (p.DryRun)
                {
                    Log("DryRun 模式，跳过下载与替换。");
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(p.DownloadUrl))
                {
                    Log("未提供 --url，无法更新。");
                    return 2;
                }

                string tempRoot = Path.Combine(AppContext.BaseDirectory, $"_update_{Guid.NewGuid():N}");
                string zipPath = Path.Combine(tempRoot, "update.zip");
                string extractDir = Path.Combine(tempRoot, "extract");
                string backupDir = Path.Combine(tempRoot, "backup");
                Directory.CreateDirectory(tempRoot);

                Log($"下载：{p.DownloadUrl}");
                await DownloadAsync(p.DownloadUrl, zipPath, Log);

                if (!string.IsNullOrWhiteSpace(p.Sha256))
                {
                    string actual = ComputeSha256(zipPath);
                    Log($"SHA256 计算完成：{actual}");
                    if (!string.Equals(actual, p.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"SHA256 不匹配，期望：{p.Sha256}");
                        return 3;
                    }
                }

                Log("解压中...");
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                string sourceDir = NormalizeExtractRoot(extractDir);
                if (!File.Exists(Path.Combine(sourceDir, "TrustNTE.exe")))
                {
                    Log("更新包内未找到 TrustNTE.exe，放弃替换。");
                    return 4;
                }

                bool ok = ReplaceFiles(sourceDir, AppContext.BaseDirectory, backupDir, Log);
                if (!ok)
                {
                    Log("替换存在失败，开始回滚...");
                    Rollback(backupDir, AppContext.BaseDirectory, Log);
                    return 5;
                }

                try { Directory.Delete(tempRoot, true); } catch { }

                Log("更新完成，准备重启主程序...");
                RestartMain(Log);
                return 0;
            }
            catch (Exception ex)
            {
                Log("致命错误：" + ex);
                return 99;
            }
            finally
            {
                Log("Updater 退出。");
            }
        }

        // ---------- 参数 ----------

        private sealed class Params
        {
            public int? RestartFromPid;
            public string? DownloadUrl;
            public string? Sha256;
            public bool DryRun;
        }

        private static Params ParseArgs(string[] args)
        {
            var p = new Params();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--restart-from-pid=", StringComparison.Ordinal) &&
                    int.TryParse(a[(a.IndexOf('=') + 1)..], out int pid1))
                {
                    p.RestartFromPid = pid1;
                }
                else if (a.StartsWith("--url=", StringComparison.Ordinal))
                {
                    p.DownloadUrl = a[(a.IndexOf('=') + 1)..];
                }
                else if (a == "--url" && i + 1 < args.Length)
                {
                    p.DownloadUrl = args[++i];
                }
                else if (a.StartsWith("--sha256=", StringComparison.Ordinal))
                {
                    p.Sha256 = a[(a.IndexOf('=') + 1)..];
                }
                else if (a == "--sha256" && i + 1 < args.Length)
                {
                    p.Sha256 = args[++i];
                }
                else if (a == "--dry-run")
                {
                    p.DryRun = true;
                }
            }
            return p;
        }

        // ---------- 等主程序退出 ----------

        private static async Task WaitMainExited(int? pid, Action<string> log)
        {
            if (pid.HasValue)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid.Value);
                    log($"等待 PID={pid.Value}（{proc.ProcessName}）退出...");
                    if (!proc.WaitForExit(15000))
                        log("警告：主程序 15 秒内未退出，继续尝试替换。");
                }
                catch (ArgumentException) { log($"PID={pid.Value} 已不存在。"); }
                catch (Exception ex) { log("等待主程序异常：" + ex.Message); }
                await Task.CompletedTask;
                return;
            }

            foreach (var p in Process.GetProcessesByName("TrustNTE"))
            {
                try { p.WaitForExit(30000); } catch { }
            }
            await Task.CompletedTask;
        }

        // ---------- 下载 ----------

        private static async Task DownloadAsync(string url, string dest, Action<string> log)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TrustNTE-Updater/1.0");

            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            long? total = resp.Content.Headers.ContentLength;
            log($"Content-Length：{total?.ToString() ?? "未知"}");

            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(dest);

            var buffer = new byte[81920];
            long read = 0;
            int lastLogPct = -10;
            int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer, 0, n);
                read += n;
                if (total is > 0)
                {
                    int pct = (int)(read * 100 / total.Value);
                    if (pct - lastLogPct >= 10)
                    {
                        log($"下载进度 {pct}%");
                        lastLogPct = pct;
                    }
                }
            }
            log($"下载完成，共 {read} 字节。");
        }

        private static string ComputeSha256(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(fs));
        }

        // ---------- 解压根目录 ----------

        private static string NormalizeExtractRoot(string root)
        {
            var dirs = Directory.GetDirectories(root);
            var files = Directory.GetFiles(root);
            if (dirs.Length == 1 && files.Length == 0) return dirs[0];
            return root;
        }

        // ---------- 替换 / 回滚 ----------

        private static bool ReplaceFiles(string srcDir, string dstDir, string backupDir, Action<string> log)
        {
            Directory.CreateDirectory(backupDir);
            bool allOk = true;

            foreach (var srcFile in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(srcDir, srcFile);
                string fileName = Path.GetFileName(rel);

                if (SkipFileNames.Any(s => string.Equals(s, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    log($"跳过：{rel}");
                    continue;
                }
                var firstSeg = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                if (SkipDirNames.Any(d => string.Equals(d, firstSeg, StringComparison.OrdinalIgnoreCase)))
                {
                    log($"跳过：{rel}");
                    continue;
                }

                string dstFile = Path.Combine(dstDir, rel);
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

                    string tmp = dstFile + ".new";
                    File.Copy(srcFile, tmp, true);
                    if (File.Exists(dstFile))
                    {
                        try { File.Replace(tmp, dstFile, null, true); }
                        catch (IOException) { File.Copy(tmp, dstFile, true); File.Delete(tmp); }
                    }
                    else
                    {
                        File.Move(tmp, dstFile);
                    }

                    log($"替换：{rel}");
                }
                catch (Exception ex)
                {
                    log($"替换失败：{rel} -> {ex.Message}");
                    allOk = false;
                }
            }
            return allOk;
        }

        private static void Rollback(string backupDir, string dstDir, Action<string> log)
        {
            if (!Directory.Exists(backupDir)) return;
            foreach (var bakFile in Directory.EnumerateFiles(backupDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(backupDir, bakFile);
                string dstFile = Path.Combine(dstDir, rel);
                try
                {
                    File.Copy(bakFile, dstFile, true);
                    log($"回滚：{rel}");
                }
                catch (Exception ex) { log($"回滚失败：{rel} -> {ex.Message}"); }
            }
        }

        // ---------- 重启 ----------

        private static void RestartMain(Action<string> log)
        {
            var mainExe = Path.Combine(AppContext.BaseDirectory, "TrustNTE.exe");
            if (!File.Exists(mainExe)) { log("未找到 TrustNTE.exe，无法重启。"); return; }

            try
            {
                Process.Start(new ProcessStartInfo(mainExe, $"--restart-from-pid={Environment.ProcessId}")
                {
                    UseShellExecute = true,
                    WorkingDirectory = AppContext.BaseDirectory
                });
                log("已拉起主程序。");
            }
            catch (Exception ex) { log("重启主程序失败：" + ex.Message); }
        }
    }
}