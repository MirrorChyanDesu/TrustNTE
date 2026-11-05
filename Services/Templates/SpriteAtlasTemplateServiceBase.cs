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
    /// 通用图集模板服务基类：加载 Assets\{文件夹} 下的 png + json，
    /// 整图按坐标切块、按 "可选下划线+尾部数字" 归一化分组。
    /// [性能·本轮] 跨组匹配新增「命中记忆」：提示停留画面期间每帧只比对 1 张模板，
    /// 稳态匹配成本从 全量 N 张/次 → 1 张/次（记忆失效时回落一次全量扫描再重建）。
    /// [归一化] 匹配一律针对 1080P 标准帧（主循环 SyncNteRenderFrame 已保证）。
    /// </summary>
    public abstract class SpriteAtlasTemplateServiceBase : IDisposable
    {
        public const double MatchThreshold = 0.8;

        private readonly Dictionary<string, List<Mat>> _templates = new();
        private readonly ILogService _logService;
        private readonly string _folderName;
        private readonly string _tag;

        // ---- 命中记忆（仅 Dispatch 单线程访问，无需加锁）----
        private string? _memoKey;      // 上次命中的组键
        private int _memoIndex;        // 上次命中的模板序号（组内）

        protected SpriteAtlasTemplateServiceBase(string folderName, string logTag, ILogService logService)
        {
            _folderName = folderName;
            _tag = logTag;
            _logService = logService;
            LoadTemplates();
        }

        public bool HasTemplate(string groupKey)
            => _templates.TryGetValue(groupKey, out var list) && list.Count > 0;

        /// <summary>指定区域内匹配指定组（同 AutoStoryTemplateService.MatchAny）。</summary>
        public Rect? MatchAny(Mat fullFrame, Rect searchRect, string groupKey, double threshold = MatchThreshold)
        {
            if (!_templates.TryGetValue(groupKey, out var templates) || templates.Count == 0) return null;
            Rect actual = searchRect.Intersect(new Rect(0, 0, fullFrame.Width, fullFrame.Height));
            if (actual.Width <= 0 || actual.Height <= 0) return null;

            using var roiMat = new Mat(fullFrame, actual);
            using var grayRoi = Preprocess(roiMat);
            foreach (var template in templates)
            {
                if (MatchOne(template, grayRoi, threshold, out var loc))
                    return new Rect(actual.X + loc.X, actual.Y + loc.Y, template.Width, template.Height);
            }
            return null;
        }

        /// <summary>
        /// 跨组匹配：所有组的所有模板在指定区域内扫一遍，任意一张命中即返回。
        /// [性能] 优先复用上次命中的模板（记忆路径，1 张成本），未命中才全量扫描；
        /// 全量命中后重建记忆，全量未命中则清除记忆。
        /// 适用于"出现任意已知物品名/按钮就触发"类需求（粉爪拾取、月卡领取）。
        /// </summary>
        public Rect? MatchAnyAcrossAllGroups(Mat fullFrame, Rect searchRect, out string matchedGroup, double threshold = MatchThreshold)
        {
            matchedGroup = string.Empty;
            Rect actual = searchRect.Intersect(new Rect(0, 0, fullFrame.Width, fullFrame.Height));
            if (actual.Width <= 0 || actual.Height <= 0) return null;

            using var roiMat = new Mat(fullFrame, actual);
            using var grayRoi = Preprocess(roiMat);

            // 1) 记忆路径：上次命中的模板还在画面上 → 1 张模板成本直接返回
            if (_memoKey != null &&
                _templates.TryGetValue(_memoKey, out var memoList) &&
                _memoIndex >= 0 && _memoIndex < memoList.Count)
            {
                var t = memoList[_memoIndex];
                if (MatchOne(t, grayRoi, threshold, out var loc))
                {
                    matchedGroup = _memoKey;
                    return new Rect(actual.X + loc.X, actual.Y + loc.Y, t.Width, t.Height);
                }
            }
            _memoKey = null;   // 记忆失效，回落全量

            // 2) 全量扫描（命中后重建记忆）
            foreach (var kv in _templates)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    var t = kv.Value[i];
                    if (MatchOne(t, grayRoi, threshold, out var loc))
                    {
                        matchedGroup = kv.Key;
                        _memoKey = kv.Key;
                        _memoIndex = i;
                        return new Rect(actual.X + loc.X, actual.Y + loc.Y, t.Width, t.Height);
                    }
                }
            }
            return null;
        }

        /// <summary>单张模板匹配。命中返回 true + 匹配左上角坐标（ROI 局部系）。</summary>
        private static bool MatchOne(Mat template, Mat grayRoi, double threshold, out Point loc)
        {
            loc = default;
            if (template.Width > grayRoi.Width || template.Height > grayRoi.Height) return false;
            using var result = new Mat();
            Cv2.MatchTemplate(grayRoi, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);
            if (maxVal >= threshold) { loc = maxLoc; return true; }
            return false;
        }

        private static Mat Preprocess(Mat roi)
        {
            var gray = new Mat();
            if (roi.Channels() > 1) Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
            else roi.CopyTo(gray);
            return gray;
        }

        private void LoadTemplates()
        {
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", _folderName);
            if (!Directory.Exists(baseDir))
            {
                _logService.LogError($"[{_tag}] 找不到图集文件夹: {baseDir}");
                return;
            }
            string? imgPath = Directory.GetFiles(baseDir, "*.png").FirstOrDefault();
            string? jsonPath = Directory.GetFiles(baseDir, "*.json").FirstOrDefault();
            if (imgPath == null || jsonPath == null)
            {
                _logService.LogError($"[{_tag}] 图集文件夹内缺少 png 或 json 文件！");
                return;
            }
            try
            {
                using var spriteSheet = Cv2.ImRead(imgPath, ImreadModes.Grayscale);
                if (spriteSheet.Empty()) { _logService.LogError($"[{_tag}] 图集整图读取失败。"); return; }

                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                int count = 0;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    // [体检项] "图层 N" 是 PSD 导出残留名，拒绝加载并告警——在切图工具里改回物品名后自动生效
                    if (prop.Name.StartsWith("图层"))
                    {
                        _logService.LogWarning($"[{_tag}] 模板 \"{prop.Name}\" 为未命名图层残留，已跳过。请在图集中重命名。");
                        continue;
                    }
                    var el = prop.Value;
                    int x = el.GetProperty("x").GetInt32();
                    int y = el.GetProperty("y").GetInt32();
                    int w = el.GetProperty("w").GetInt32();
                    int h = el.GetProperty("h").GetInt32();
                    if (x + w > spriteSheet.Width || y + h > spriteSheet.Height)
                    {
                        _logService.LogWarning($"[{_tag}] 模板 {prop.Name} 坐标越界，已跳过。");
                        continue;
                    }
                    string baseName = Regex.Replace(prop.Name, @"[_]?\d+$", "");
                    using var roi = new Mat(spriteSheet, new Rect(x, y, w, h));
                    if (!_templates.TryGetValue(baseName, out var list))
                        _templates[baseName] = list = new List<Mat>();
                    list.Add(roi.Clone());
                    count++;
                }
                _logService.LogInfo($"[{_tag}] 图集加载成功，共 {_templates.Count} 组 / {count} 张模板。");
            }
            catch (Exception ex) { _logService.LogError($"[{_tag}] 图集解析异常: {ex.Message}"); }
        }

        public void Dispose()
        {
            foreach (var list in _templates.Values)
                foreach (var mat in list) mat.Dispose();
            _templates.Clear();
        }
    }


    /// <summary>月卡领取图集（Assets\MonthlyCard）。</summary>
    public sealed class MonthlyCardTemplateService : SpriteAtlasTemplateServiceBase
    {
        public MonthlyCardTemplateService(ILogService logService) : base("MonthlyCard", "月卡奖励", logService) { }
    }

    /// <summary>游戏状态图集（Assets\Nte_GameState）：启动器按钮/进入游戏/掉线重试/UID。</summary>
    public sealed class NteGameStateTemplateService : SpriteAtlasTemplateServiceBase
    {
        public NteGameStateTemplateService(ILogService logService)
            : base("Nte_GameState", "游戏状态", logService) { }
    }

}
