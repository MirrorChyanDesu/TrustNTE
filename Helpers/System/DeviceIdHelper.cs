using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace TrustNTE.Helpers
{
    public static class DeviceIdHelper
    {
        private static string? _cachedId;

        public static string GetDeviceId()
        {
            if (_cachedId != null) return _cachedId;

            string raw = GetCpuId() + GetMacAddress() + Environment.MachineName;
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(raw));
            _cachedId = Convert.ToHexString(hash);
            return _cachedId;
        }

        private static string GetCpuId()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "UnknownCPU";
            }
            catch { return "UnknownCPU"; }
        }

        private static string GetMacAddress()
        {
            try
            {
                var mac = string.Empty;
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        mac += nic.GetPhysicalAddress().ToString();
                        break;
                    }
                }
                return string.IsNullOrEmpty(mac) ? "UnknownMAC" : mac;
            }
            catch { return "UnknownMAC"; }
        }
    }
}