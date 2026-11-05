using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using TrustNTE.Helpers;
using TrustNTE.Models;
using TrustNTE.Models.Update;
using TrustNTE.Services;
using TrustNTE.Services.Interfaces;
using Wpf.Ui.Appearance;
using CommunityToolkit.Mvvm.Messaging;

namespace TrustNTE.ViewModels.Pages
{
    /// <summary>
    /// 设置页的 ViewModel。
    /// </summary>
    public partial class SettingsPageViewModel : ObservableObject, IDisposable
    {
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly UpdateService _updateService;
        private readonly IStartupService _startupService;
        private readonly IDialogService _dialogService;
        private readonly IWindowService _windowService;

        /// <summary>
        /// 当前配置实例。
        /// </summary>
        public AppConfig Config { get; set; }

        /// <summary>
        /// 界面语言选项。
        /// </summary>
        public string[] Languages { get; } = { "简体中文", "繁體中文", "English", "日本語", "한국어" };

        /// <summary>
        /// 主题选项。
        /// </summary>
        public string[] Themes { get; } = { "浅色", "深色", "跟随系统" };

        /// <summary>
        /// 开机自启是否已启用。
        /// </summary>
        [ObservableProperty]
        private bool _isAutoStartEnabled;

        private bool _isInitializing;
        private bool _isUpdatingToggle;

        /// <summary>
        /// 初始化 <see cref="SettingsPageViewModel"/> 实例。
        /// </summary>
        public SettingsPageViewModel(
            IConfigService configService,
            ILogService logService,
            UpdateService updateService,
            IStartupService startupService,
            IDialogService dialogService,
            IWindowService windowService)
        {
            _configService = configService;
            _logService = logService;
            _updateService = updateService;
            _startupService = startupService;
            _dialogService = dialogService;
            _windowService = windowService;

            Config = _configService.Load();
            Config.PropertyChanged += Config_PropertyChanged;

            _isInitializing = true;
            IsAutoStartEnabled = _startupService.IsEnabled();
            _isInitializing = false;
        }

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _configService.Save(Config);

            if (e.PropertyName == nameof(AppConfig.UiLanguage))
            {
                WeakReferenceMessenger.Default.Send(new LanguageChangedMessage { NewLanguage = Config.UiLanguage });
            }
            // [模块F]    改原因：将原 SettingsPage.xaml.cs 中的主题应用逻辑迁入 ViewModel，
            // 由配置属性变化驱动，消除 View 直接订阅 Model 的违规
            else if (e.PropertyName == nameof(AppConfig.AppTheme))
            {
                ApplyTheme(Config.AppTheme);
            }
            //     当“自启后自动挂机”开关改变时，如果自启任务已经创建，则重写一遍让参数生效
            else if (e.PropertyName == nameof(AppConfig.AutoStartEngineOnLogin))
            {
                if (_startupService.IsEnabled())
                {
                    _startupService.Disable();
                    _startupService.Enable();
                    _logService.Log("[设置] 自启模式已切换，已重新写入计划任务。");
                }
            }
        }

        private void ApplyTheme(string themeName)
        {
            try
            {
                if (themeName == "浅色")
                {
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                }
                else if (themeName == "深色")
                {
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                }
                else
                {
                    ApplicationThemeManager.Apply(ApplicationTheme.Unknown);
                }
            }
            catch { }
        }

        partial void OnIsAutoStartEnabledChanged(bool value)
        {
            if (_isInitializing || _isUpdatingToggle) return;

            _isUpdatingToggle = true;
            try
            {
                if (value)
                {
                    if (!_startupService.Enable())
                    {
                        IsAutoStartEnabled = false;
                        _ = _dialogService.ShowInfoAsync("开启失败", "开启自启失败，可能被安全软件拦截或权限不足。");
                    }
                }
                else
                {
                    if (!_startupService.Disable())
                    {
                        IsAutoStartEnabled = true;
                        _ = _dialogService.ShowInfoAsync("关闭失败", "关闭自启失败，可能被安全软件拦截。");
                    }
                }

                Config.IsAutoStartEnabled = IsAutoStartEnabled;
                _configService.Save(Config);
            }
            finally
            {
                _isUpdatingToggle = false;
            }
        }

        [RelayCommand]
        private async Task CheckUpdateAsync()
        {
            try
            {
                var result = await _updateService.CheckUpdateAsync(
                    new UpdateOption { Trigger = UpdateTrigger.Manual, Channel = UpdateChannel.Stable });
                await HandleUpdateResultAsync(result);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowInfoAsync("检查更新失败", ex.Message);
            }
        }

        [RelayCommand]
        private async Task CheckBetaUpdateAsync()
        {
            bool confirmed = await _dialogService.ShowConfirmAsync(
                "警告",
                "测试版非常不稳定，请谨慎选择更新！\n\n是否继续检查测试版？");
            if (!confirmed) return;

            try
            {
                var result = await _updateService.CheckUpdateAsync(
                    new UpdateOption { Trigger = UpdateTrigger.Manual, Channel = UpdateChannel.Alpha });
                await HandleUpdateResultAsync(result);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowInfoAsync("检查更新失败", ex.Message);
            }
        }

        [RelayCommand]
        private async Task ShowAboutAsync()
        {
            string aboutText = "TrustNTE\n\n" +
                               "版本：后续更新\n" +
                               "项目地址：后续更新\n" +
                               "教程地址：后续更新\n" +
                               "开源协议：MIT License\n" +
                               "帮助文档：后续更新\n\n" +
                               "薄荷 · 解放双手！开源免费！为爱发电";
            await _dialogService.ShowInfoAsync("关于 TrustNTE", aboutText);
        }

        private async Task HandleUpdateResultAsync(UpdateCheckResult result)
        {
            if (!result.NeedPrompt)
            {
                if (result.ShowUpToDateMessage)
                {
                    await _dialogService.ShowInfoAsync("提示", $"当前已是最新版本！({result.CurrentVersion})");
                }
                return;
            }

            var vm = new CheckUpdateWindowViewModel(result, _updateService, _configService, _dialogService);
            _windowService.ShowUpdateWindow(vm);
        }

        public void Dispose()
        {
            Config.PropertyChanged -= Config_PropertyChanged;
        }
    }
}