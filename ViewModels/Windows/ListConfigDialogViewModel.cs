using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels
{
    /// <summary>
    /// ListConfigDialog 的 ViewModel，承载黑白名单编辑对话框的数据和命令。
    /// </summary>
    public partial class ListConfigDialogViewModel : ObservableObject
    {
        private const string WhiteModePrompt =
            "【白名单模式】\n默认拾取列表中的内容，未列出的物品不会拾取。\n示例：\n驾驶\n上车\n乘坐";

        private const string BlackModePrompt =
            "【黑名单模式】\n会拾取所有内容，但排除列表中的物品。\n示例：\n早雾\n娜娜莉\n安魂曲";

        private const string HelpContent =
            "1. 每行一条记录，回车键换行。\n" +
            "2. 点击右下角的 × 图标会【清空全部内容】。\n" +
            "3. 如果只想删除某一行，请使用退格键 (Backspace) 或删除键 (Delete)。\n\n" +
            "【白名单模式】：\n只会拾取列表中出现的物品或交互。\n\n" +
            "【黑名单模式】：\n会拾取所有物品，但会跳过列表中出现的物品。\n\n" +
            "点击保存后，窗口自动关闭并立即按照新的名单规则执行。";

        private readonly IDialogService _dialogService;

        private string _whiteText;
        private string _blackText;

        /// <summary>
        /// 当前是否处于白名单编辑模式。
        /// </summary>
        [ObservableProperty]
        private bool _isWhiteMode = true;

        /// <summary>
        /// 当前模式对应的提示文本，显示在输入框上方。
        /// </summary>
        [ObservableProperty]
        private string _promptText = WhiteModePrompt;

        /// <summary>
        /// 输入框中的文本内容。
        /// </summary>
        [ObservableProperty]
        private string _inputText = string.Empty;

        /// <summary>
        /// 确认后返回给调用方的白名单文本。
        /// </summary>
        public string FinalWhiteText { get; private set; } = string.Empty;

        /// <summary>
        /// 确认后返回给调用方的黑名单文本。
        /// </summary>
        public string FinalBlackText { get; private set; } = string.Empty;

        /// <summary>
        /// 关闭对话框时使用的结果值。true 表示确认，false 表示取消。
        /// </summary>
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// 当 ViewModel 请求关闭对话框时触发，由 View 订阅并设置 DialogResult。
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// 初始化 <see cref="ListConfigDialogViewModel"/> 实例。
        /// </summary>
        /// <param name="currentMode">当前配置的模式名称。</param>
        /// <param name="whiteText">白名单初始文本。</param>
        /// <param name="blackText">黑名单初始文本。</param>
        /// <param name="dialogService">用于显示帮助对话框的服务。</param>
        public ListConfigDialogViewModel(
            string currentMode,
            string whiteText,
            string blackText,
            IDialogService dialogService)
        {
            _whiteText = whiteText ?? string.Empty;
            _blackText = blackText ?? string.Empty;
            _dialogService = dialogService;

            if (string.Equals(currentMode, "黑名单模式", StringComparison.Ordinal))
            {
                _isWhiteMode = false;
            }

            UpdateUi();
        }

        /// <summary>
        /// 切换到白名单模式。
        /// </summary>
        [RelayCommand]
        private void SwitchToWhite()
        {
            // 离开黑名单模式前，先保存黑名单编辑内容
            _blackText = InputText;
            IsWhiteMode = true;
            UpdateUi();
        }

        /// <summary>
        /// 切换到黑名单模式。
        /// </summary>
        [RelayCommand]
        private void SwitchToBlack()
        {
            // 离开白名单模式前，先保存白名单编辑内容
            _whiteText = InputText;
            IsWhiteMode = false;
            UpdateUi();
        }

        /// <summary>
        /// 确认命令：将当前输入写入备份字段，设置最终返回值并请求关闭。
        /// </summary>
        [RelayCommand]
        private void Confirm()
        {
            if (IsWhiteMode) _whiteText = InputText;
            else _blackText = InputText;

            FinalWhiteText = _whiteText;
            FinalBlackText = _blackText;

            DialogResult = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 取消命令：请求关闭且不   改最终返回值。
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 打开帮助对话框。
        /// </summary>
        [RelayCommand]
        private async Task ShowHelpAsync()
        {
            await _dialogService.ShowInfoAsync("黑白名单操作说明", HelpContent);
        }

        private void UpdateUi()
        {
            if (IsWhiteMode)
            {
                PromptText = WhiteModePrompt;
                InputText = _whiteText;
            }
            else
            {
                PromptText = BlackModePrompt;
                InputText = _blackText;
            }
        }
    }
}