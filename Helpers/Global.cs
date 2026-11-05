using System;

namespace TrustNTE.Helpers
{
    public static class Global
    {
        public static bool IsNewVersion(string remoteVersion, string localVersion)
        {
            try
            {
                string r = remoteVersion.TrimStart('v', 'V');
                string l = localVersion.TrimStart('v', 'V');
                var remoteParts = r.Split('.');
                var localParts = l.Split('.');
                int maxLen = Math.Max(remoteParts.Length, localParts.Length);
                for (int i = 0; i < maxLen; i++)
                {
                    int remoteNum = 0;
                    int localNum = 0;
                    if (i < remoteParts.Length) int.TryParse(remoteParts[i], out remoteNum);
                    if (i < localParts.Length) int.TryParse(localParts[i], out localNum);
                    if (remoteNum > localNum) return true;
                    if (remoteNum < localNum) return false;
                }
            }
            catch { }
            return false;
        }
    }
}