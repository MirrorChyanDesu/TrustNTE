using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.ViewModels.Pages
{
    public partial class StartPageViewModel
    {
        private bool _isCaptureExpanderExpanded = false;

        public bool IsCaptureExpanderExpanded
        {
            get => _isCaptureExpanderExpanded;
            set => SetProperty(ref _isCaptureExpanderExpanded, value);
        }

        /// <summary>
        /// 统一处理来自 Markdown 文档或其它组件的内部跳转指令
        /// </summary>
        public void HandleAppNavigation(string target)
        {
            switch (target)
            {
                case "goto-interval":
                case "goto-engine":
                    // 如果目标是抓帧间隔或截图引擎，就把截图设置面板撑开
                    IsCaptureExpanderExpanded = true;
                    break;

                case "goto-mouse":
                    // 如果以后有独立的鼠标面板，可以在这里展开
                    // IsMouseExpanderExpanded = true;
                    break;
            }
        }
    }
}