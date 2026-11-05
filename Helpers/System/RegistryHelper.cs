using Microsoft.Win32;
using System;
using System.IO;

namespace TrustNTE.Helpers
{
    public static class RegistryHelper
    {
        public static string? FindLauncherPath()
        {
            string[] uninstallKeys =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            try
            {
                foreach (var keyPath in uninstallKeys)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        string displayName = subKey.GetValue("DisplayName")?.ToString() ?? "";
                        string installLocation = subKey.GetValue("InstallLocation")?.ToString() ?? "";
                        string uninstallString = subKey.GetValue("UninstallString")?.ToString() ?? "";

                        string combined = (displayName + installLocation + uninstallString).ToLower();
                        // 过滤条件
                        if (!combined.Contains("neverness") && !combined.Contains("nte")) continue;

                        // 尝试从安装目录推导
                        if (!string.IsNullOrEmpty(installLocation))
                        {
                            foreach (var exe in GameConstants.LauncherExes)
                            {
                                string path1 = Path.Combine(installLocation, "NTELauncher", exe);
                                string path2 = Path.Combine(installLocation, exe);
                                if (File.Exists(path1)) return path1;
                                if (File.Exists(path2)) return path2;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 加入了权限提取器不会报错！
            }
            return null;
        }
    }
}