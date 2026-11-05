using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace TrustNTE.Helpers
{
    public static class ElevationHelper
    {
        public static bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void EnsureElevated()
        {
            if (!IsRunningAsAdmin())
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = Process.GetCurrentProcess().MainModule?.FileName,
                        Verb = "runas" // 触发 UAC
                    };

                    Process.Start(processInfo);

                    System.Windows.Application.Current.Shutdown();
                }
                catch (Exception)
                {
                    //    复套娃报错
                    System.Windows.MessageBox.Show("此程序必须【以管理员身份运行】才能正常使用自动化功能！\n\n请右键点击软件图标，选择“以管理员身份运行”。",
                                    "权限被拒绝", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Windows.Application.Current.Shutdown();
                }
            }
        }
    }
}