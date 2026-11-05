using System.Threading.Tasks;

namespace TrustNTE.Services.Interfaces
{
    /// <summary>
    /// 通用对话框服务。
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 显示信息提示对话框。
        /// </summary>
        /// <param name="title">标题。</param>
        /// <param name="content">内容。</param>
        Task ShowInfoAsync(string title, string content);

        /// <summary>
        /// 显示确认对话框。
        /// </summary>
        /// <param name="title">标题。</param>
        /// <param name="content">内容。</param>
        /// <returns>true 表示用户点击了确认，false 表示取消或关闭。</returns>
        Task<bool> ShowConfirmAsync(string title, string content);
    }

    /// <summary>
    /// 文件对话框服务。
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// 弹出保存文件对话框。
        /// </summary>
        /// <param name="filter">文件筛选器。</param>
        /// <param name="defaultName">默认文件名。</param>
        /// <param name="initialDir">初始目录，为 null 或空时使用系统默认目录。</param>
        /// <returns>用户选择的完整文件路径；取消则返回 null。</returns>
        // initialDir 参数改为可选，供 DispatcherPageViewModel 导出分组时不强制指定目录使用；
        string? PickSaveFile(string filter, string defaultName, string? initialDir = null);

        /// <summary>
        /// 弹出打开文件对话框。
        /// </summary>
        /// <param name="filter">文件筛选器。</param>
        /// <param name="initialDir">初始目录，为 null 或空时使用系统默认目录。</param>
        /// <returns>用户选择的完整文件路径；取消则返回 null。</returns>
        // PickOpenFile，供 DispatcherPageViewModel 导入分组使用，
        // 避免 ViewModel 直接引用 Microsoft.Win32.OpenFileDialog
        string? PickOpenFile(string filter, string? initialDir = null);
    }

    /// <summary>
    /// 系统外壳服务。
    /// </summary>
    public interface IShellService
    {
        /// <summary>
        /// 用资源管理器打开指定文件夹。
        /// </summary>
        /// <param name="path">文件夹路径。</param>
        void OpenFolder(string path);
    }
}