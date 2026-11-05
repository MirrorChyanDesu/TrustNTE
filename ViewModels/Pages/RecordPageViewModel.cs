using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Threading;
using TrustNTE.Models.Macro;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;
using TrustNTE.Services.Macro;

namespace TrustNTE.ViewModels.Pages
{
    public partial class RecordPageViewModel : ObservableObject, IDisposable
    {
        private readonly AutomationService _automationService;
        private readonly IDialogService _dialogService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IShellService _shellService;
        private readonly DispatcherTimer _timer;
        private IMacroSession? _currentSession;

        [ObservableProperty] private bool _isRecording;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private string _statusText = "准备就绪";
        [ObservableProperty] private string _recordTime = "00:00:00";

        public RecordPageViewModel(
            AutomationService automationService,
            IDialogService dialogService,
            IFileDialogService fileDialogService,
            IShellService shellService)
        {
            _automationService = automationService;
            _dialogService = dialogService;
            _fileDialogService = fileDialogService;
            _shellService = shellService;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (s, e) =>
            {
                if (IsRecording && _currentSession != null)
                {
                    var ts = TimeSpan.FromMilliseconds(_currentSession.CurrentDuration);
                    RecordTime = ts.ToString(@"hh\:mm\:ss");
                    StatusText = $"正在录制... (事件数: {_currentSession.EventCount})";
                }
            };
        }

        [RelayCommand]
        private void OpenDirectory()
        {
            //    改：路径改为 Data/Macros
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Macros");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _shellService.OpenFolder(dir);
        }

        [RelayCommand]
        private async Task ToggleRecordAsync()
        {
            if (!IsRecording)
            {
                if (_automationService.CurrentHwnd == IntPtr.Zero)
                {
                    await _dialogService.ShowInfoAsync("无法录制", "请先前往【启动】页面，开启截图器并绑定游戏窗口！");
                    return;
                }

                _currentSession = new MacroSession(_automationService.CurrentHwnd);
                IsRecording = true;
                _timer.Start();
            }
            else
            {
                StopRecording();
                StatusText = "录制已停止，请点击保存";
            }
        }

        private void StopRecording()
        {
            IsRecording = false;
            _timer.Stop();
            _currentSession?.Dispose();
        }

        [RelayCommand]
        private async Task SaveRecordAsync()
        {
            if (IsRecording) StopRecording();

            if (_currentSession == null || _currentSession.EventCount == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "没有录制的数据。");
                return;
            }


            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Macros");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string? filePath = _fileDialogService.PickSaveFile("宏文件 (*.json)|*.json", $"Macro_{DateTime.Now:yyyyMMdd_HHmmss}.json", dir);

            if (filePath != null)
            {
                if (!_automationService.TryGetClientRegion(out var gx, out var gy, out var gw, out var gh, out var dpi))
                {
                    await _dialogService.ShowInfoAsync("无法保存", "请先绑定游戏窗口");
                    return;
                }

                var info = new MacroInfo { X = gx, Y = gy, Width = gw, Height = gh, RecordDpi = dpi };
                var mergedMacro = _currentSession.GetMergedMacro(info);

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                // 流式序列化防止双倍内存
                using var fs = File.Create(filePath);
                await JsonSerializer.SerializeAsync(fs, mergedMacro, jsonOptions);

                StatusText = "保存成功！";
                await _dialogService.ShowInfoAsync("成功", "宏已保存！");
                _currentSession.Dispose();
                _currentSession = null;
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            StopRecording();
        }
    }
}