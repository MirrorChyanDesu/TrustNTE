using System.Windows;
using Application = System.Windows.Application;

namespace TrustNTE.Update
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await Program.Start(e.Args);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Program.ReleaseMutex();
            base.OnExit(e);
        }
    }
}