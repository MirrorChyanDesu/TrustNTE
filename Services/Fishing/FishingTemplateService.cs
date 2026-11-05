using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    public class FishingTemplateService : IDisposable
    {
        private readonly ILogService _logService;
        private readonly Dictionary<string, List<Mat>> _templates = new();

        public FishingTemplateService(ILogService logService)
        {
            _logService = logService;
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nte_Fish");
            string jsonPath = Path.Combine(baseDir, "Nte_Fish.json");
            string imgPath = Path.Combine(baseDir, "Nte_Fish.png");

            if (!File.Exists(jsonPath) || !File.Exists(imgPath))
            {
                _logService.LogError($"[钓鱼] 找不到图集文件: {jsonPath} 或 {imgPath}");
                return;
            }

            try
            {
                using var spriteSheet = Cv2.ImRead(imgPath, ImreadModes.Grayscale);
                if (spriteSheet.Empty()) return;

                string json = File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                foreach (var prop in root.EnumerateObject())
                {
                    string fullName = prop.Name;
                    var rectEl = prop.Value;

                    int x = rectEl.GetProperty("x").GetInt32();
                    int y = rectEl.GetProperty("y").GetInt32();
                    int w = rectEl.GetProperty("w").GetInt32();
                    int h = rectEl.GetProperty("h").GetInt32();

                    string baseName = System.Text.RegularExpressions.Regex.Replace(fullName, @"\d+$", "");

                    if (x + w <= spriteSheet.Width && y + h <= spriteSheet.Height)
                    {
                        using var roi = new Mat(spriteSheet, new Rect(x, y, w, h));
                        if (!_templates.ContainsKey(baseName))
                        {
                            _templates[baseName] = new List<Mat>();
                        }
                        _templates[baseName].Add(roi.Clone());
                    }
                }
                _logService.LogInfo($"[钓鱼] 图集加载成功，共加载 {_templates.Count} 组模板。");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[钓鱼] 图集解析异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 匹配成功返回在全图坐标系下的具体矩形框，失败返回 null。
        /// </summary>
        public Rect? MatchAny(Mat fullFrame, Rect searchRect, string templateBaseName, double threshold = 0.8)
        {
            if (!_templates.ContainsKey(templateBaseName) || _templates[templateBaseName].Count == 0)
                return null;

            Rect actualRect = searchRect.Intersect(new Rect(0, 0, fullFrame.Width, fullFrame.Height));
            if (actualRect.Width <= 0 || actualRect.Height <= 0) return null;

            using var roiMat = new Mat(fullFrame, actualRect);
            using var grayRoi = new Mat();
            if (roiMat.Channels() > 1)
                Cv2.CvtColor(roiMat, grayRoi, ColorConversionCodes.BGR2GRAY);
            else
                roiMat.CopyTo(grayRoi);

            foreach (var template in _templates[templateBaseName])
            {
                if (template.Width > grayRoi.Width || template.Height > grayRoi.Height) continue;

                using var result = new Mat();
                Cv2.MatchTemplate(grayRoi, template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

                if (maxVal >= threshold)
                {
                    // 返回全图绝对坐标
                    return new Rect(actualRect.X + maxLoc.X, actualRect.Y + maxLoc.Y, template.Width, template.Height);
                }
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var list in _templates.Values)
            {
                foreach (var mat in list) mat.Dispose();
            }
            _templates.Clear();
        }
    }
}