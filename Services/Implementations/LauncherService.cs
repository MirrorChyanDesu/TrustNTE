using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Services.Game;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class LauncherService
    {
        private const string LauncherTitle = "NTEGame";
        private const string LauncherClass = "Qt51517QWindowOwnDC";

        // 假设这是启动器按钮所在的整体 ROI 区域（1080P）
        private readonly Rect _btnRoi = new Rect(986, 606, 1251 - 986, 665 - 606);

        private readonly ILogService _logService;
        private readonly IInputService _inputService;

        private readonly WindowFinder _windowFinder = new();
        private readonly ScreenCapturer _screenCapturer = new();

        // 使用字典保存切出来的图集模板
        private readonly Dictionary<string, Mat> _launcherTemplates = new();

        public LauncherService(ILogService logService, IInputService inputService)
        {
            _logService = logService;
            _inputService = inputService;

            LoadLauncherTemplates();
        }

        private void LoadLauncherTemplates()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nte_Launcher");
            if (!Directory.Exists(baseDir))
            {
                _logService.LogError($"找不到启动器图集文件夹: {baseDir}");
                return;
            }

            string? imgPath = Directory.GetFiles(baseDir, "*.png").FirstOrDefault();
            string? jsonPath = Directory.GetFiles(baseDir, "*.json").FirstOrDefault();

            if (imgPath == null || jsonPath == null)
            {
                _logService.LogError($"启动器图集文件夹内缺少 png 或 json 文件！");
                return;
            }

            try
            {
                using var sourceMat = Cv2.ImRead(imgPath, ImreadModes.Grayscale);
                if (sourceMat.Empty()) return;

                string json = File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(json);

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    string name = prop.Name;
                    var rectEl = prop.Value;
                    int x = rectEl.GetProperty("x").GetInt32();
                    int y = rectEl.GetProperty("y").GetInt32();
                    int w = rectEl.GetProperty("w").GetInt32();
                    int h = rectEl.GetProperty("h").GetInt32();

                    using var roi = new Mat(sourceMat, new Rect(x, y, w, h));
                    _launcherTemplates[name] = roi.Clone();
                }
                _logService.LogInfo($"成功加载启动器图集，共切出 {_launcherTemplates.Count} 个模板。");
            }
            catch (Exception ex)
            {
                _logService.LogError($"解析启动器图集失败: {ex.Message}");
            }
        }

        public async Task<bool> StartAndEnterGameAsync(string? customLauncherPath, CancellationToken token = default)
        {
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).Any())
            {
                _logService.Log("异环游戏主进程已在运行，跳过启动器。");
                return true;
            }

            var launcherProc = Process.GetProcessesByName("NTEGame").FirstOrDefault();
            IntPtr hwnd = IntPtr.Zero;

            if (launcherProc != null)
            {
                _logService.Log("检测到启动器正在后台运行，正在尝试唤醒...");
                hwnd = NativeMethods.FindWindow(LauncherClass, LauncherTitle);

                if (hwnd != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
                    NativeMethods.SetForegroundWindow(hwnd);
                    await Task.Delay(500, token);
                }
            }
            else
            {
                string? launchPath = customLauncherPath;
                if (string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath))
                {
                    launchPath = RegistryHelper.FindLauncherPath();
                    if (!string.IsNullOrWhiteSpace(launchPath))
                        _logService.Log($"🔍 探查成功！从注册表找到启动器路径: {launchPath}");
                }

                if (string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath))
                {
                    _logService.LogError("❌ 未能找到有效的启动器路径。");
                    return false;
                }

                _logService.Log($"正在启动启动器: {launchPath}");
                Process.Start(new ProcessStartInfo { FileName = launchPath, UseShellExecute = true });
                _logService.Log("等待启动器窗口...");
            }

            if (hwnd == IntPtr.Zero)
            {
                for (int i = 0; i < 120; i++)
                {
                    if (token.IsCancellationRequested) return false;
                    await Task.Delay(500, token);
                    hwnd = NativeMethods.FindWindow(LauncherClass, LauncherTitle);
                    if (hwnd != IntPtr.Zero) break;
                }
            }

            if (hwnd == IntPtr.Zero)
            {
                _logService.LogError("未能找到启动器窗口！");
                return false;
            }

            _logService.Log($"成功绑定沙盒窗口句柄: {hwnd}");
            NativeMethods.SetForegroundWindow(hwnd);
            await Task.Delay(600, token);

            long startTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            int clickCooldown = 0;

            while (!token.IsCancellationRequested)
            {
                // 超时放宽到 10 分钟
                if (DateTimeOffset.Now.ToUnixTimeSeconds() - startTime > 10 * 60)
                {
                    _logService.LogError("等待超时（超过10分钟），停止等待。");
                    return false;
                }

                // 如果启动器被最小化或者不在前台，强行把它拽出来
                if (NativeMethods.IsIconic(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
                {
                    _logService.Log("检测到启动器缩下去了，强行唤醒并置顶...");
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
                    NativeMethods.SetForegroundWindow(hwnd);
                    await Task.Delay(500, token);
                }

                using Mat? screenMat = _screenCapturer.Capture(hwnd, "PrintWindow");
                if (screenMat == null || screenMat.Empty() || screenMat.Width < _btnRoi.Right || screenMat.Height < _btnRoi.Bottom)
                {
                    await Task.Delay(500, token);
                    continue;
                }

                using var cropMat = new Mat(screenMat, _btnRoi);

                //  [新增] 将截图区域转为灰度图，以匹配模板的通道数
                using var grayCropMat = new Mat();
                Cv2.CvtColor(cropMat, grayCropMat, ColorConversionCodes.BGR2GRAY);

                //  [   改] 传入灰度图进行检测
                bool isStart = CheckTemplateMatch(grayCropMat, "start_btn", 0.8, out Point startPos);
                bool isResume = CheckTemplateMatch(grayCropMat, "resume_btn", 0.8, out Point resumePos);
                bool isUpdate = CheckTemplateMatch(grayCropMat, "update_btn", 0.8, out Point updatePos);

                if (clickCooldown <= 0)
                {
                    //  [   改] 优先级拦截：必须优先保证点“开始游戏”
                    if (isStart)
                    {
                        _logService.Log($"检测到【开始游戏】状态，已发送精准点击进入游戏！");
                        NativeMethods.SetForegroundWindow(hwnd);
                        await Task.Delay(100, token);
                        _inputService.SendBackgroundMouseClick(hwnd, startPos.X, startPos.Y, true);
                        clickCooldown = 5;
                    }
                    else if (isResume)
                    {
                        _logService.Log($"检测到【继续下载】状态，已发送点击。");
                        NativeMethods.SetForegroundWindow(hwnd);
                        await Task.Delay(100, token);
                        _inputService.SendBackgroundMouseClick(hwnd, resumePos.X, resumePos.Y, true);
                        clickCooldown = 5;
                    }
                    else if (isUpdate)
                    {
                        _logService.Log($"检测到【开始更新】状态，已发送点击。");
                        NativeMethods.SetForegroundWindow(hwnd);
                        await Task.Delay(100, token);
                        _inputService.SendBackgroundMouseClick(hwnd, updatePos.X, updatePos.Y, true);
                        clickCooldown = 5;
                    }
                }

                if (clickCooldown > 0) clickCooldown--;

                var runningGame = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameConstants.GameExe)).FirstOrDefault();
                if (runningGame != null)
                {
                    _logService.Log("检测到游戏客户端已启动，准备切换游戏窗口...");
                    await Task.Delay(3000, token);
                    return true;
                }

                await Task.Delay(500, token);
            }

            return false;
        }

        //  [   改] 辅助匹配方法，返回真实的屏幕坐标
        private bool CheckTemplateMatch(Mat cropMat, string templateName, double threshold, out Point matchLocation)
        {
            matchLocation = new Point(-1, -1);
            if (!_launcherTemplates.TryGetValue(templateName, out var template) || template.Empty())
                return false;

            using var result = new Mat();
            Cv2.MatchTemplate(cropMat, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            if (maxVal >= threshold)
            {
                // 计算出模板在【全屏】下的实际中心坐标
                int templateCenterX = maxLoc.X + template.Width / 2;
                int templateCenterY = maxLoc.Y + template.Height / 2;

                // 加上 ROI 的偏移量，得到真实的屏幕坐标
                matchLocation = new Point(_btnRoi.X + templateCenterX, _btnRoi.Y + templateCenterY);
                return true;
            }

            return false;
        }
    }
}