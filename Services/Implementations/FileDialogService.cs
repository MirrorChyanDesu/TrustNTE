using Microsoft.Win32;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    /// <summary>
    /// 文件对话框服务实现。
    /// </summary>
    public class FileDialogService : IFileDialogService
    {
        /// <inheritdoc/>
        public string? PickSaveFile(string filter, string defaultName, string? initialDir = null)
        {
            // [模块D]    改原因：initialDir 为空时不再设置 InitialDirectory，使用系统默认目录，
            // 与 Microsoft.Win32.SaveFileDialog 的默认行为一致
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultName,
            };
            if (!string.IsNullOrEmpty(initialDir))
            {
                dialog.InitialDirectory = initialDir;
            }
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        // [模块D]    改原因：新增实现，对应 IFileDialogService 的 PickOpenFile 方法
        /// <inheritdoc/>
        public string? PickOpenFile(string filter, string? initialDir = null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
            };
            if (!string.IsNullOrEmpty(initialDir))
            {
                dialog.InitialDirectory = initialDir;
            }
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}