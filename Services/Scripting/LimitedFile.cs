using System;
using System.IO;

namespace TrustNTE.Services.Scripting
{
    public class LimitedFile
    {
        private readonly string _allowedBaseDir;

        public LimitedFile(string allowedBaseDir)
        {
            _allowedBaseDir = Path.GetFullPath(allowedBaseDir);
            if (!Directory.Exists(_allowedBaseDir))
            {
                Directory.CreateDirectory(_allowedBaseDir);
            }
        }

        public string ReadText(string relativePath)
        {
            try
            {
                string requestedPath = Path.GetFullPath(Path.Combine(_allowedBaseDir, relativePath));

                string relative = Path.GetRelativePath(_allowedBaseDir, requestedPath);
                if (relative.StartsWith("..") || Path.IsPathRooted(relative))
                {
                    throw new UnauthorizedAccessException($"[安全拦截] 非法的路径穿越尝试: {relativePath}");
                }

                if (!File.Exists(requestedPath))
                {
                    throw new FileNotFoundException($"文件不存在: {relativePath}");
                }

                return File.ReadAllText(requestedPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"读取文件失败: {ex.Message}", ex);
            }
        }
    }
}