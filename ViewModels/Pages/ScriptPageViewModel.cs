using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustNTE.ViewModels.Pages
{
    public partial class ScriptPageViewModel : ObservableObject
    {
        [ObservableProperty] private string _scriptContent = "// 在这里编写 JS 脚本\nlog.info('异环启动');\n支持C#API调用";

        public ScriptPageViewModel()
        {
        }
    }
}