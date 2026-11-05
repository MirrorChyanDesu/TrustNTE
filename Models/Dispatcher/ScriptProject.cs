using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace TrustNTE.Models.Dispatcher
{
    public enum ScriptType
    {
        Javascript,
        KeyMouse,
        Shell
    }

    public partial class ScriptProject : ObservableObject
    {
        [ObservableProperty] private string _id = Guid.NewGuid().ToString();
        [ObservableProperty] private string _name = "新建任务";
        [ObservableProperty] private ScriptType _type = ScriptType.Javascript;
        [ObservableProperty] private string _content = string.Empty;
        [ObservableProperty] private bool _isEnabled = true;
        
        [ObservableProperty] private int _timeoutSeconds = 30;

        [ObservableProperty]
        [property: JsonIgnore]
        private string _status = "等待中";

        [ObservableProperty]
        [property: JsonIgnore]
        private bool _isRunning;
    }

    public partial class ScriptGroup : ObservableObject
    {
        [ObservableProperty] private string _id = Guid.NewGuid().ToString();
        [ObservableProperty] private string _name = "默认分组";
        [ObservableProperty] private bool _isEnabled = true;
        
        public ObservableCollection<ScriptProject> Projects { get; set; } = new();
    }
}