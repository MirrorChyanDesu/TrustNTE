using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Input;
using TrustNTE.Helpers;
using TrustNTE.Models.TaskConfigs;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages.TaskModules
{
    public partial class AutoMusicViewModel : TaskModuleViewModelBase
    {
        // Win32 提权定时器，用于将系统定时器精度提升至 1ms
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint uPeriod);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint uPeriod);

        private sealed class AnchorCfg
        {
            public string Name { get; init; } = "";
            public string Tag { get; init; } = "";
            public string[] Templates { get; init; } = Array.Empty<string>();
            public Rect Roi { get; init; }
            public double Thr { get; init; } = 0.80;
        }

        private static readonly AnchorCfg[] Anchors =
        {
            new() { Name = "开始演奏", Tag = "Start",  Templates = new[] { "Nte_StartPerformance1", "Nte_StartPerformance2" }, Roi = new Rect(1546, 986, 63, 46) },
            new() { Name = "演奏中",   Tag = "Playing", Templates = new[] { "Nte_Performing" }, Roi = new Rect(42, 54, 37, 40) },
            new() { Name = "演奏收益", Tag = "Income", Templates = new[] { "Nte_PerformanceIncome" }, Roi = new Rect(952, 544, 74, 59) }
        };

        private readonly Dictionary<string, Mat> _atlasTemplates = new();
        private static readonly string[] LaneNames = { "D", "F", "J", "K" };
        private readonly Dictionary<string, LaneState> _lanes = new() { { "D", new() }, { "F", new() }, { "J", new() }, { "K", new() } };

        private class LaneState { public bool Armed = true; public DateTime LastFire = DateTime.MinValue; }

        // 颜色配置静态字段
        private static readonly Scalar[] BgLows = { new(197, 217, 225), new(203, 218, 225), new(202, 218, 225), new(198, 216, 225) };
        private static readonly Scalar[] BgHighs = { new(255, 255, 255), new(255, 255, 255), new(255, 255, 255), new(255, 255, 255) };
        private static readonly Scalar[] C1Lows = { new(225, 219, 57), new(104, 189, 225), new(81, 101, 225), new(1, 0, 0) };
        private static readonly Scalar[] C1Highs = { new(255, 255, 117), new(164, 249, 255), new(141, 161, 255), new(61, 60, 60) };
        private static readonly Scalar[] C2Lows = { new(0, 0, 0), new(4, 4, 4), new(0, 0, 0), new(225, 113, 225) };
        private static readonly Scalar[] C2Highs = { new(59, 59, 59), new(64, 64, 64), new(60, 60, 59), new(255, 173, 255) };

        private readonly object _stateLock = new();
        private int _frameCounter;
        private int _isUiRendering = 0;
        private readonly double[] _lastDark = new double[4];

        private readonly IGameService _gameService;
        private readonly IWindowService _windowService;
        private readonly IInputRouter _router;
        private readonly AutomationService _automationService;
        private readonly IDialogService _dialogService;

        private CancellationTokenSource? _taskCts;
        private CancellationTokenSource? _keyWorkerCts;

        private readonly Channel<int>[] _keyChannels = Enumerable.Range(0, 4)
            .Select(_ => Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true }))
            .ToArray();

        private AutoMusicDebugWindowViewModel? _debugVm;
        private System.Windows.Threading.DispatcherTimer? _debugPreviewTimer;
        private int[] _keyMap = new int[4];

        private readonly Mat[] _bgM = { new(), new(), new(), new() };
        private readonly Mat[] _hsvM = { new(), new(), new(), new() };
        private readonly Mat[] _darkM = { new(), new(), new(), new() };
        private readonly Mat[] _c1M = { new(), new(), new(), new() };
        private readonly Mat[] _c2M = { new(), new(), new(), new() };

        public AutoMusicConfig Config { get; }
        public AutoMusicRoiViewModel RoiVm { get; }

        public AutoMusicViewModel(
            IConfigService configService, ILogService logService, IGameService gameService,
            IWindowService windowService, IInputRouter router, AutomationService automationService, IDialogService dialogService)
             : base(logService, configService, "自动音游")
        {
            _gameService = gameService; _windowService = windowService;
            _router = router; _automationService = automationService; _dialogService = dialogService;

            // [Phase 1] 持久化由基类 BindConfig 接管（属性变化 → Load → 写回 → Save，500ms 防抖）
            Config = BindConfig(c => c.Music, (main, cfg) => main.Music = cfg);
            RoiVm = new AutoMusicRoiViewModel(Config);

            LoadAtlas();
        }

        private void LoadAtlas()
        {
            try
            {
                string imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nte_StarSign", "Nte_StarSign.png");
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nte_StarSign", "Nte_StarSign.json");
                if (!File.Exists(imgPath) || !File.Exists(jsonPath)) return;

                using var atlas = Cv2.ImRead(imgPath, ImreadModes.Grayscale);
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var rectEl = prop.Value;
                    int x = rectEl.GetProperty("x").GetInt32();
                    int y = rectEl.GetProperty("y").GetInt32();
                    int w = rectEl.GetProperty("w").GetInt32();
                    int h = rectEl.GetProperty("h").GetInt32();

                    using var roi = new Mat(atlas, new Rect(x, y, w, h));
                    _atlasTemplates[prop.Name] = roi.Clone();
                }
                _logService.LogInfo($"[音游] 状态图集加载完成，共 {_atlasTemplates.Count} 个模板");
            }
            catch (Exception ex) { _logService.LogError($"[音游] 图集加载失败: {ex.Message}"); }
        }

        [RelayCommand]
        private void OpenLogicDoc()
        {
            TrustNTE.Views.Windows.DocViewerWindow.ShowDoc("音游参数配置指南", "music-guide.md");
        }

        [RelayCommand]
        private void ResetSettings()
        {
            var defaults = new AutoMusicConfig();

            Config.DarkVHi = defaults.DarkVHi;
            Config.MinFireGapMs = defaults.MinFireGapMs;
            Config.KeyDownTimeMs = defaults.KeyDownTimeMs;
            Config.ObserveRadius = defaults.ObserveRadius;

            Config.HitPixelCount = defaults.HitPixelCount;
            Config.ResetPixelCount = defaults.ResetPixelCount;
            Config.ForceResetMs = defaults.ForceResetMs;

            Config.RoiD.X = defaults.RoiD.X; Config.RoiD.Y = defaults.RoiD.Y;
            Config.RoiF.X = defaults.RoiF.X; Config.RoiF.Y = defaults.RoiF.Y;
            Config.RoiJ.X = defaults.RoiJ.X; Config.RoiJ.Y = defaults.RoiJ.Y;
            Config.RoiK.X = defaults.RoiK.X; Config.RoiK.Y = defaults.RoiK.Y;

            _logService.LogInfo("[音游] 判定参数与圆心坐标已恢复默认设置。");
        }

        [RelayCommand]
        private void OpenDebugWindow()
        {
            if (_debugVm == null)
            {
                _debugVm = new AutoMusicDebugWindowViewModel();
                _debugVm.DebugWindowClosed += (s, e) => StopDebugPreview();
                _debugVm.Owner = this;
            }
            _windowService.ShowAutoMusicDebugWindow(_debugVm);
            if (!IsRunning) StartDebugPreview();
        }

        private void StartDebugPreview()
        {
            StopDebugPreview();
            if (_automationService.CurrentHwnd == IntPtr.Zero) return;

            _debugPreviewTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _debugPreviewTimer.Tick += (s, e) =>
            {
                if (IsRunning || _debugVm == null) return;
                using var frame = _gameService.CaptureWindow();
                if (frame != null && !frame.Empty())
                {
                    UpdateDebugUI(frame);
                }
            };
            _debugPreviewTimer.Start();
        }

        private void StopDebugPreview()
        {
            _debugPreviewTimer?.Stop();
            _debugPreviewTimer = null;
        }

        protected override async Task ExecuteTaskAsync(CancellationToken token)
        {
            if (_automationService.CurrentHwnd == IntPtr.Zero) { _windowService.ShowStartErrorWindow(); return; }

            StopDebugPreview();
            _taskCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var localToken = _taskCts.Token;

            timeBeginPeriod(1);

            _keyMap[0] = KeyInterop.VirtualKeyFromKey(Enum.TryParse<Key>(Config.KeyD, true, out var kd) ? kd : Key.D);
            _keyMap[1] = KeyInterop.VirtualKeyFromKey(Enum.TryParse<Key>(Config.KeyF, true, out var kf) ? kf : Key.F);
            _keyMap[2] = KeyInterop.VirtualKeyFromKey(Enum.TryParse<Key>(Config.KeyJ, true, out var kj) ? kj : Key.J);
            _keyMap[3] = KeyInterop.VirtualKeyFromKey(Enum.TryParse<Key>(Config.KeyK, true, out var kk) ? kk : Key.K);

            StartKeyWorkers(localToken);
            ResetStates();

            DateTime lastShotAt = DateTime.MinValue;

            try
            {
                _logService.Log("音游全自动循环已启动...");
                int done = 0;
                while (!localToken.IsCancellationRequested && (Config.LoopCount == 0 || done < Config.LoopCount))
                {
                    var (frame, shotAt) = _gameService.GetLatestFrameWithTimestamp();
                    if (frame == null || frame.Empty()) { frame?.Dispose(); await Task.Delay(1); continue; }

                    if (shotAt == lastShotAt) { frame.Dispose(); await Task.Delay(1); continue; }
                    lastShotAt = shotAt;

                    using (frame)
                    {
                        if (FindAnchor(frame, Anchors[2]) is { } income)
                        {
                            _logService.LogInfo($"【日志输出】[键鼠 {ModeTag()}] 演奏收益 → 按 ESC 退出");
                            _router.KeyPress(_automationService.CurrentHwnd, KeyCode.Esc, 50);
                            await Task.Delay(1000, localToken);
                            done++;
                            continue;
                        }

                        if (FindAnchor(frame, Anchors[0]) is { } start)
                        {
                            if (Config.AutoClickStart)
                            {
                                _logService.LogInfo($"【日志输出】[键鼠 {ModeTag()}] 点击开始演奏");
                                _router.ClickLogical(_automationService.CurrentHwnd, start.X, start.Y);
                            }
                            await Task.Delay(800, localToken);
                            continue;
                        }

                        if (FindAnchor(frame, Anchors[1]) != null)
                        {
                            _logService.LogInfo($"【日志输出】[键鼠 {ModeTag()}] 演奏中 → 进入 DFJK 判定 (第 {done + 1} 局)");
                            while (!localToken.IsCancellationRequested)
                            {
                                var (f2, t2) = _gameService.GetLatestFrameWithTimestamp();
                                if (f2 != null)
                                {
                                    using (f2)
                                    {
                                        if (t2 == lastShotAt) { await Task.Delay(1, localToken); continue; }
                                        lastShotAt = t2;

                                        if (FindAnchor(f2, Anchors[2]) != null) break;
                                        TickDetection(f2, t2);
                                        UpdateDebugUI(f2);
                                    }
                                }
                                await Task.Delay(1, localToken);
                            }
                            continue;
                        }
                    }
                    await Task.Delay(1, localToken);
                }
                _logService.Log($"循环结束，共完成 {done} 局");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logService.LogError($"音游异常: {ex.Message}"); }
            finally
            {
                timeEndPeriod(1);
                _taskCts?.Dispose(); _taskCts = null;
                _keyWorkerCts?.Cancel(); _keyWorkerCts?.Dispose(); _keyWorkerCts = null;
            }
        }

        private Point? FindAnchor(Mat frame, AnchorCfg cfg)
        {
            var actual = cfg.Roi.Intersect(new Rect(0, 0, frame.Width, frame.Height));
            if (actual.Width <= 0 || actual.Height <= 0) return null;
            using var scene = new Mat(frame, actual);
            using var sceneGray = new Mat();
            Cv2.CvtColor(scene, sceneGray, ColorConversionCodes.BGR2GRAY);

            foreach (var tplName in cfg.Templates)
            {
                if (!_atlasTemplates.TryGetValue(tplName, out var tpl)) continue;
                if (tpl.Width > scene.Width || tpl.Height > scene.Height) continue;

                using var res = new Mat();
                Cv2.MatchTemplate(sceneGray, tpl, res, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out Point maxLoc);
                if (maxVal >= cfg.Thr)
                    return new Point(actual.X + maxLoc.X + tpl.Width / 2, actual.Y + maxLoc.Y + tpl.Height / 2);
            }
            return null;
        }

        private void TickDetection(Mat frame, DateTime shotAt)
        {
            int fh = frame.Rows;
            int fw = frame.Cols;
            var points = new[] { Config.RoiD, Config.RoiF, Config.RoiJ, Config.RoiK };

            lock (_stateLock)
            {
                for (int i = 0; i < 4; i++)
                {
                    var pt = points[i];
                    int cx = (int)(pt.X * fw);
                    int cy = (int)(pt.Y * fh);
                    int r = (int)(Config.ObserveRadius * ((double)fw / 1920.0));

                    int x1 = Math.Max(0, cx - r);
                    int y1 = Math.Max(0, cy - r);
                    int w = Math.Min(fw - x1, r * 2);
                    int h = Math.Min(fh - y1, r * 2);
                    if (w <= 0 || h <= 0) continue;

                    using var roi = new Mat(frame, new Rect(x1, y1, w, h));
                    int totalPixels = w * h;

                    Cv2.InRange(roi, BgLows[i], BgHighs[i], _bgM[i]);
                    int bgPixels = Cv2.CountNonZero(_bgM[i]);
                    double bgRatio = bgPixels / (double)totalPixels;

                    Cv2.CvtColor(roi, _hsvM[i], ColorConversionCodes.BGR2HSV);
                    Cv2.InRange(_hsvM[i], new Scalar(0, 0, 0), new Scalar(180, 255, Config.DarkVHi), _darkM[i]);
                    int darkPixels = Cv2.CountNonZero(_darkM[i]);

                    Cv2.InRange(roi, C1Lows[i], C1Highs[i], _c1M[i]);
                    int c1Pixels = Cv2.CountNonZero(_c1M[i]);

                    Cv2.InRange(roi, C2Lows[i], C2Highs[i], _c2M[i]);
                    int c2Pixels = Cv2.CountNonZero(_c2M[i]);

                    string laneName = LaneNames[i];
                    var lane = _lanes[laneName];
                    _lastDark[i] = darkPixels / (double)totalPixels;

                    if (lane.Armed)
                    {
                        bool isDarkHit = darkPixels >= Config.HitPixelCount;
                        bool isC1Hit = c1Pixels >= Config.HitPixelCount;
                        bool isC2Hit = c2Pixels >= Config.HitPixelCount;

                        if (isDarkHit || isC1Hit || isC2Hit)
                        {
                            if ((shotAt - lane.LastFire).TotalMilliseconds >= Config.MinFireGapMs)
                            {
                                lane.Armed = false;
                                lane.LastFire = shotAt;
                                _keyChannels[i].Writer.TryWrite(_keyMap[i]);
                            }
                        }
                    }

                    if (!lane.Armed)
                    {
                        bool noteStillThere = darkPixels >= Config.ResetPixelCount ||
                                              c1Pixels >= Config.ResetPixelCount ||
                                              c2Pixels >= Config.ResetPixelCount;

                        if (bgRatio > 0.60 && !noteStillThere)
                        {
                            lane.Armed = true;
                        }
                        else if ((shotAt - lane.LastFire).TotalMilliseconds > Config.ForceResetMs)
                        {
                            lane.Armed = true;
                        }
                    }
                }
            }
        }

        private void UpdateDebugUI(Mat frame)
        {
            if (!Config.EnableDebugLog || _debugVm == null) return;
            if (++_frameCounter % 8 != 0) return;

            if (Interlocked.Exchange(ref _isUiRendering, 1) == 0)
            {
                Mat debugMat = frame.Clone();
                double fs = Config.DebugFontScale;

                string info = $"HitPx:{Config.HitPixelCount} RstPx:{Config.ResetPixelCount} Force:{Config.ForceResetMs} V{Config.DarkVHi} G{Config.MinFireGapMs} H{Config.KeyDownTimeMs} r{Config.ObserveRadius} Fs{fs:F2}";
                Cv2.PutText(debugMat, info, new Point(10, (int)(30 * fs)), HersheyFonts.HersheySimplex, fs, Scalar.White, 1);

                foreach (var a in Anchors)
                {
                    var rc = new Rect((int)(a.Roi.X * frame.Width / 1920.0), (int)(a.Roi.Y * frame.Height / 1080.0),
                                      (int)(a.Roi.Width * frame.Width / 1920.0), (int)(a.Roi.Height * frame.Height / 1080.0));
                    Cv2.Rectangle(debugMat, rc, Scalar.Yellow, 1);
                    Cv2.PutText(debugMat, a.Tag, new Point(rc.X, rc.Y - 4), HersheyFonts.HersheySimplex, fs, Scalar.Yellow, 1);
                }

                var points = new[] { Config.RoiD, Config.RoiF, Config.RoiJ, Config.RoiK };
                for (int i = 0; i < 4; i++)
                {
                    int cx = (int)(points[i].X * frame.Width);
                    int cy = (int)(points[i].Y * frame.Height);
                    int r = (int)(Config.ObserveRadius * ((double)frame.Width / 1920.0));

                    Scalar color = _lanes[LaneNames[i]].Armed ? Scalar.Lime : Scalar.Red;
                    Cv2.Circle(debugMat, new Point(cx, cy), r, color, 1);
                    Cv2.PutText(debugMat, $"{LaneNames[i]} {_lastDark[i]:F2}", new Point(cx - 10, cy - r - 5), HersheyFonts.HersheySimplex, fs, color, 1);
                }

                _ = Task.Run(() =>
                {
                    try { _debugVm?.UpdatePreviewImage(ImageHelper.MatToBitmapSource(debugMat)); }
                    finally { debugMat.Dispose(); Interlocked.Exchange(ref _isUiRendering, 0); }
                });
            }
        }

        private void StartKeyWorkers(CancellationToken token)
        {
            _keyWorkerCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var vk in _keyChannels[idx].Reader.ReadAllAsync(_keyWorkerCts.Token))
                        {
                            var hwnd = _automationService.CurrentHwnd;
                            try
                            {
                                _router.KeyDown(hwnd, vk);
                                await Task.Delay(Config.KeyDownTimeMs, _keyWorkerCts.Token);
                            }
                            finally
                            {
                                try { _router.KeyUp(hwnd, vk); } catch { }
                            }

                            _logService.LogInfo($"【日志输出】[键鼠 {ModeTag()}] 击发按键 {LaneNames[idx]}");
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { _logService.LogError($"按键线程异常: {ex.Message}"); }
                }, _keyWorkerCts.Token);
            }
        }

        private string ModeTag() => _router.CurrentDrive switch
        {
            InputDriveType.Physical => "前台→物理点击",
            InputDriveType.Trick => "后台→通用点击",
            _ => "后台→后台点击",
        };

        private void ResetStates()
        {
            lock (_stateLock)
            {
                foreach (var lane in _lanes.Values) { lane.Armed = true; lane.LastFire = DateTime.MinValue; }
                _frameCounter = 0;
                for (int i = 0; i < 4; i++)
                {
                    while (_keyChannels[i].Reader.TryRead(out _)) { }
                }
            }
        }

        public override void Dispose()
        {
            StopDebugPreview();
            _taskCts?.Cancel(); _taskCts?.Dispose(); _taskCts = null;
            _keyWorkerCts?.Cancel(); _keyWorkerCts?.Dispose(); _keyWorkerCts = null;
            _debugVm = null;

            for (int i = 0; i < 4; i++)
            {
                _bgM[i].Dispose(); _hsvM[i].Dispose(); _darkM[i].Dispose(); _c1M[i].Dispose(); _c2M[i].Dispose();
            }

            base.Dispose();
        }
    }
}
