using System.Diagnostics;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class ShellService : IShellService
    {
        public void OpenFolder(string path)
        {
            Process.Start("explorer.exe", path);
        }
    }
}