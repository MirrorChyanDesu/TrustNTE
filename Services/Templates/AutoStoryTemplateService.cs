using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    /// <summary>
    /// 自动剧情图集模板服务。加载 Assets\Nte_AutoStory 目录下的图集（png + json），
    /// 将整图按坐标切块并按尾部编号归一化分组，供剧情触发器在指定判断区域内做模板匹配。
    /// </summary>
    public class AutoStoryTemplateService : IDisposable
    {
        /// <summary>
        /// 剧情 UI 模板匹配的默认相似度阈值。
        /// </summary>
        public const double MatchThreshold = 0.8;

        private readonly ILogService _logService;
        private readonly Dictionary<string, List<Mat>> _templates = new();

        public AutoStoryTemplateService(ILogService logService)
        {
            _logService = logService;
            LoadTemplates();
        }

        /// <summary>
        /// 判断指定分组是否存在可用模板。
        /// </summary>
        /// <param name="groupKey">图集分组键（已去除尾部编号）。</param>
        /// <returns>存在至少一张模板返回 true。</returns>
        public bool HasTemplate(string groupKey)
        {
            return _templates.TryGetValue(groupKey, out var list) && list.Count > 0;
        }

        /// <summary>
        /// 在指定判断区域内匹配图集分组中的任意一张模板，组内任意一张命中即成功。
        /// </summary>
        /// <param name="fullFrame">归一化后的全屏帧（1920x1080）。</param>
        /// <param name="searchRect">判断区域（全图坐标系）。</param>
        /// <param name="groupKey">图集分组键（已去除尾部编号）。</param>
        /// <param name="threshold">匹配相似度阈值。</param>
        /// <returns>命中时返回在全图坐标系下的矩形框，未命中返回 null。</returns>
        public Rect? MatchAny(Mat fullFrame, Rect searchRect, string groupKey, double threshold = MatchThreshold)
        {
            if (!_templates.TryGetValue(groupKey, out var templates) || templates.Count == 0)
                return null;

            // 判断区域先与帧边界求交集，防止越界
            Rect actualRect = searchRect.Intersect(new Rect(0, 0, fullFrame.Width, fullFrame.Height));
            if (actualRect.Width <= 0 || actualRect.Height <= 0)
                return null;

            using var roiMat = new Mat(fullFrame, actualRect);
            using var grayRoi = new Mat();
            if (roiMat.Channels() > 1)
                Cv2.CvtColor(roiMat, grayRoi, ColorConversionCodes.BGR2GRAY);
            else
                roiMat.CopyTo(grayRoi);

            foreach (var template in templates)
            {
                if (template.Width > grayRoi.Width || template.Height > grayRoi.Height)
                    continue;

                using var result = new Mat();
                Cv2.MatchTemplate(grayRoi, template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

                if (maxVal >= threshold)
                {
                    // 换算为全图绝对坐标
                    return new Rect(actualRect.X + maxLoc.X, actualRect.Y + maxLoc.Y, template.Width, template.Height);
                }
            }

            return null;
        }

        private void LoadTemplates()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nte_AutoStory");
            if (!Directory.Exists(baseDir))
            {
                _logService.LogError($"[自动剧情] 找不到图集文件夹: {baseDir}");
                return;
            }

            string? imgPath = Directory.GetFiles(baseDir, "*.png").FirstOrDefault();
            string? jsonPath = Directory.GetFiles(baseDir, "*.json").FirstOrDefault();
            if (imgPath == null || jsonPath == null)
            {
                _logService.LogError("[自动剧情] 图集文件夹内缺少 png 或 json 文件！");
                return;
            }

            try
            {
                using var spriteSheet = Cv2.ImRead(imgPath, ImreadModes.Grayscale);
                if (spriteSheet.Empty())
                {
                    _logService.LogError("[自动剧情] 图集整图读取失败。");
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                using var doc = JsonDocument.Parse(json);
                int count = 0;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    string fullName = prop.Name;
                    var rectEl = prop.Value;
                    int x = rectEl.GetProperty("x").GetInt32();
                    int y = rectEl.GetProperty("y").GetInt32();
                    int w = rectEl.GetProperty("w").GetInt32();
                    int h = rectEl.GetProperty("h").GetInt32();

                    if (x + w > spriteSheet.Width || y + h > spriteSheet.Height)
                    {
                        _logService.LogWarning($"[自动剧情] 模板 {fullName} 坐标越界，已跳过。");
                        continue;
                    }

                    // [   改位置] 归一化分组键与 FishingTemplateService 的 \d+$ 不同：
                    // 本图集编号命名存在 Nte_StorySkip_1（带下划线）与 Nte_PressVTrackTask1（不带）两种，
                    // 采用 [_]?\d+$ 统一去除"可选下划线 + 尾部数字"，避免产生 Nte_StorySkip_ 这类脏组键
                    string baseName = Regex.Replace(fullName, @"[_]?\d+$", "");

                    using var roi = new Mat(spriteSheet, new Rect(x, y, w, h));
                    if (!_templates.TryGetValue(baseName, out var list))
                    {
                        list = new List<Mat>();
                        _templates[baseName] = list;
                    }
                    list.Add(roi.Clone());
                    count++;
                }

                _logService.LogInfo($"[自动剧情] 图集加载成功，共加载 {_templates.Count} 组 / {count} 张模板。");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[自动剧情] 图集解析异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            foreach (var list in _templates.Values)
            {
                foreach (var mat in list)
                    mat.Dispose();
            }
            _templates.Clear();
        }
    }
}
