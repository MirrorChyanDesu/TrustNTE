using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TrustNTE.Models.Dispatcher;
using TrustNTE.Services;
using TrustNTE.Services.Dispatcher;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.ViewModels.Pages
{
    /// <summary>
    /// 调度器页的 ViewModel。
    /// </summary>
    public partial class DispatcherPageViewModel : ObservableObject, IDisposable
    {
        private const string ExportFilter = "TrustNTE 分组文件 (*.json)|*.json";

        private readonly DispatcherConfigService _configService;
        private readonly DispatcherTaskRunner _runner;
        private readonly IDialogService _dialogService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IWindowService _windowService;

        /// <summary>
        /// 所有配置分组。
        /// </summary>
        public ObservableCollection<ScriptGroup> Groups { get; } = new();

        /// <summary>
        /// 当前选中的分组。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedGroup))]
        private ScriptGroup? _selectedGroup;

        /// <summary>
        /// 当前选中的任务项。
        /// </summary>
        [ObservableProperty]
        private ScriptProject? _selectedProject;

        /// <summary>
        /// 是否存在选中的分组。
        /// </summary>
        public bool HasSelectedGroup => SelectedGroup != null;

        /// <summary>
        /// 初始化 <see cref="DispatcherPageViewModel"/> 实例。
        /// </summary>
        /// <param name="configService">调度器配置服务。</param>
        /// <param name="runner">调度器任务执行器。</param>
        /// <param name="dialogService">通用对话框服务。</param>
        /// <param name="fileDialogService">文件对话框服务。</param>
        /// <param name="windowService">窗口服务，用于显示重命名对话框。</param>
        // [模块D]    改原因：构造函数新增 IDialogService / IFileDialogService / IWindowService 三个依赖，
        // 用于替代原有的 new MessageBox / new FileDialog / new PromptDialog 调用
        public DispatcherPageViewModel(
            DispatcherConfigService configService,
            DispatcherTaskRunner runner,
            IDialogService dialogService,
            IFileDialogService fileDialogService,
            IWindowService windowService)
        {
            _configService = configService;
            _runner = runner;
            _dialogService = dialogService;
            _fileDialogService = fileDialogService;
            _windowService = windowService;

            var loaded = _configService.Load();
            foreach (var group in loaded) Groups.Add(group);

            _runner.OnProjectStateChanged += HandleProjectStateChanged;
            _runner.OnGroupStateChanged += HandleGroupStateChanged;
        }

        private void HandleProjectStateChanged(ScriptProject proj, string status)
        {
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() => proj.Status = status));
        }

        private void HandleGroupStateChanged(ScriptGroup group, bool isRunning) { }

        //     ================================================================
        //     "优先启动为准"：调度器启动分组前，先有界停掉
        //     用户级任务（钓鱼/音游等）与其它在跑的调度器分组。
        //     协调器未注册时静默跳过；清场失败不阻断启动。
        //     ================================================================
        private static async Task PreemptLanesAsync()
        {
            try
            {
                var coordinator = App.ServiceProvider?.GetService<TaskLaneCoordinator>();
                if (coordinator != null)
                    await coordinator.PreemptForUserTaskAsync();
            }
            catch { /* 清场失败不阻断启动 */ }
        }

        /// <summary>
        /// 新增分组。
        /// </summary>
        [RelayCommand]
        private void AddGroup()
        {
            var newGroup = new ScriptGroup { Name = $"新分组 {Groups.Count + 1}" };
            Groups.Add(newGroup);
            SelectedGroup = newGroup;
            Save();
        }

        /// <summary>
        /// 删除分组。
        /// </summary>
        /// <param name="group">要删除的分组。</param>
        [RelayCommand]
        private async Task DeleteGroup(ScriptGroup group)
        {
            if (group == null) return;

            // [模块D]    改原因：new Wpf.Ui.Controls.MessageBox 改为 IDialogService.ShowConfirmAsync
            bool confirmed = await _dialogService.ShowConfirmAsync(
                "删除分组",
                $"确定要删除分组【{group.Name}】吗？其中的任务也会被一并删除！");
            if (confirmed)
            {
                Groups.Remove(group);
                if (SelectedGroup == group) SelectedGroup = null;
                Save();
            }
        }

        /// <summary>
        /// 重命名分组。
        /// </summary>
        /// <param name="group">要重命名的分组。</param>
        [RelayCommand]
        private void RenameGroup(ScriptGroup group)
        {
            if (group == null) return;

            // [模块D]    改原因：new PromptDialog 改为 IWindowService.ShowPromptDialog，
            // 消除 ViewModel 对 View 的直接依赖
            var vm = new PromptDialogViewModel("重命名分组", "请输入新的分组名称：", group.Name);
            bool? result = _windowService.ShowPromptDialog(vm);
            if (result == true && !string.IsNullOrWhiteSpace(vm.InputText))
            {
                group.Name = vm.InputText;
                Save();
            }
        }

        /// <summary>
        /// 复制分组。
        /// </summary>
        /// <param name="group">要复制的分组。</param>
        [RelayCommand]
        private void CopyGroup(ScriptGroup group)
        {
            if (group == null) return;
            var json = JsonSerializer.Serialize(group);
            var newGroup = JsonSerializer.Deserialize<ScriptGroup>(json);
            if (newGroup != null)
            {
                newGroup.Id = Guid.NewGuid().ToString();
                newGroup.Name = $"{group.Name} - 副本";
                foreach (var p in newGroup.Projects) p.Id = Guid.NewGuid().ToString();
                Groups.Insert(Groups.IndexOf(group) + 1, newGroup);
                SelectedGroup = newGroup;
                Save();
            }
        }

        /// <summary>
        /// 导出分组配置到文件。
        /// </summary>
        /// <param name="group">要导出的分组。</param>
        [RelayCommand]
        private async Task ExportGroup(ScriptGroup group)
        {
            if (group == null) return;

            // [模块D]    改原因：new Microsoft.Win32.SaveFileDialog 改为 IFileDialogService.PickSaveFile
            string? filePath = _fileDialogService.PickSaveFile(ExportFilter, $"{group.Name}.json");
            if (filePath == null) return;

            try
            {
                string json = JsonSerializer.Serialize(group, new JsonSerializerOptions { WriteIndented = true });
                // [模块D]    改原因：File.WriteAllText 同步 IO 改为异步，避免大文件导出时阻塞 UI 线程
                await File.WriteAllTextAsync(filePath, json);
                // [模块D]    改原因：new MessageBox 改为 IDialogService.ShowInfoAsync
                await _dialogService.ShowInfoAsync("导出成功", "分组配置已成功导出！");
            }
            catch (Exception ex)
            {
                // [模块D]    改原因：new MessageBox 改为 IDialogService.ShowInfoAsync
                await _dialogService.ShowInfoAsync("导出失败", ex.Message);
            }
        }

        /// <summary>
        /// 从文件导入分组配置。
        /// </summary>
        [RelayCommand]
        private async Task ImportGroup()
        {
            // [模块D]    改原因：new Microsoft.Win32.OpenFileDialog 改为 IFileDialogService.PickOpenFile
            string? filePath = _fileDialogService.PickOpenFile(ExportFilter);
            if (filePath == null) return;

            try
            {
                // [模块D]    改原因：File.ReadAllText 同步 IO 改为异步，避免大文件导入时阻塞 UI 线程
                string json = await File.ReadAllTextAsync(filePath);
                var newGroup = JsonSerializer.Deserialize<ScriptGroup>(json);
                if (newGroup != null)
                {
                    newGroup.Id = Guid.NewGuid().ToString();
                    newGroup.Name += " (导入)";
                    Groups.Add(newGroup);
                    SelectedGroup = newGroup;
                    Save();
                }
            }
            catch (Exception ex)
            {
                // [模块D]    改原因：new MessageBox 改为 IDialogService.ShowInfoAsync
                await _dialogService.ShowInfoAsync("导入失败", $"文件格式不正确: {ex.Message}");
            }
        }

        /// <summary>
        /// 连续执行所有启用的分组。
        /// </summary>
        [RelayCommand]
        private async Task RunContinuous()
        {
            if (!Groups.Any()) return;
            await PreemptLanesAsync(); //     启动前清场：停用户任务 + 其它在跑的分组
            foreach (var group in Groups.ToList())
            {
                if (!group.IsEnabled) continue;
                await _runner.RunGroupAsync(group);
            }
        }

        /// <summary>
        /// 执行选中的分组。
        /// </summary>
        /// <param name="group">要执行的分组。</param>
        [RelayCommand]
        private async Task RunByGroup(ScriptGroup group)
        {
            if (group == null) return;
            await PreemptLanesAsync(); //     启动前清场：停用户任务 + 其它在跑的分组
            await _runner.RunGroupAsync(group);
        }

        /// <summary>
        /// 为分组新增一个任务项。
        /// </summary>
        /// <param name="group">要添加任务项的分组。</param>
        [RelayCommand]
        private void AddProject(ScriptGroup group)
        {
            if (group == null) return;
            var newProj = new ScriptProject { Name = $"新任务 {group.Projects.Count + 1}" };
            group.Projects.Add(newProj);
            SelectedProject = newProj;
            Save();
        }

        /// <summary>
        /// 删除任务项。
        /// </summary>
        /// <param name="project">要删除的任务项。</param>
        [RelayCommand]
        private void DeleteProject(ScriptProject project)
        {
            if (project == null || SelectedGroup == null) return;
            SelectedGroup.Projects.Remove(project);
            SelectedProject = null;
            Save();
        }

        /// <summary>
        /// 执行分组（非阻塞触发）。
        /// </summary>
        /// <param name="group">要执行的分组。</param>
        [RelayCommand]
        private async Task RunGroup(ScriptGroup group)
        {
            if (group == null) return;
            await PreemptLanesAsync(); //     启动前清场：停用户任务 + 其它在跑的分组
            _ = _runner.RunGroupAsync(group); // 分组本体仍为非阻塞触发
        }

        /// <summary>
        /// 停止当前执行。
        /// </summary>
        [RelayCommand]
        private void StopAll() => _runner.Stop();

        /// <summary>
        /// 跳过当前任务。
        /// </summary>
        [RelayCommand]
        private void SkipCurrent() => _runner.SkipCurrent();

        /// <summary>
        /// 显示更多功能占位提示。
        /// </summary>
        [RelayCommand]
        private async Task MoreActions()
        {
            // [模块D]    改原因：new MessageBox 改为 IDialogService.ShowInfoAsync
            await _dialogService.ShowInfoAsync("更多功能", "更多功能正在开发中，包括：一键清空任务、任务拖拽排序等。");
        }

        /// <summary>
        /// 显示分组设置占位提示。
        /// </summary>
        [RelayCommand]
        private async Task Settings()
        {
            // [模块D]    改原因：new MessageBox 改为 IDialogService.ShowInfoAsync
            await _dialogService.ShowInfoAsync("分组设置", "分组设置正在开发中，包括：循环次数、执行完毕后关机等。");
        }

        /// <summary>
        /// 保存分组配置。
        /// </summary>
        [RelayCommand]
        private void Save() => _configService.Save(Groups);

        /// <summary>
        /// 释放资源并解除事件订阅。
        /// </summary>
        public void Dispose()
        {
            _runner.OnProjectStateChanged -= HandleProjectStateChanged;
            _runner.OnGroupStateChanged -= HandleGroupStateChanged;
        }
    }
}
