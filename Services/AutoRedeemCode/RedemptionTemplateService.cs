using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenCvSharp;
// [   复][CS0104] 项目全局引入 System.Drawing，Size/Rect 有二义性，显式别名锁定 OpenCvSharp
using Size = OpenCvSharp.Size;
using Rect = OpenCvSharp.Rect;

namespace TrustNTE.Services.AutoRedeemCode
{
    /// <summary>
    /// 一键兑换图集模板服务。
    /// 资源：Assets\Ntes_OneClickRdmCode\Ntes_OneClickRdmCode.png + 同名 .json
    /// 分组规则与 AutoStoryTemplateService 一致：去掉 Nte_ 前缀后，按 [_]?\d+$ 归一化组键，
    /// 例如 Nte_GoToRedemptionCode1~4 → 组 GoToRedemptionCode（4 变体）。
    /// 匹配阈值 0.8（与 AutoStory/Fishing 同源常量）。
    /// </summary>
    public sealed class RedemptionTemplateService
    {
        private const string AssetDir = "Assets/Ntes_OneClickRdmCode";
        private const string AtlasPng = "Ntes_OneClickRdmCode.png";
        private const string AtlasJson = "Ntes_OneClickRdmCode.json";
        private const double MatchThreshold = 0.8;

        private readonly object _initLock = new();
        private readonly Dictionary<string, Mat> _templates = new();
        private readonly Dictionary<string, List<string>> _groups = new();
        private bool _initialized;

        /// <summary>JSON 中单个切片的几何信息（键与 JSON 原文小写 x/y/w/h 一致）</summary>
        private sealed class AtlasItem
        {
            public int x { get; set; }
            public int y { get; set; }
            public int w { get; set; }
            public int h { get; set; }
        }

        /// <summary>
        /// 组键归一化：Nte_GoToRedemptionCode1 → GoToRedemptionCode
        /// 与 AutoStoryTemplateService 同规则 [_]?\d+$。
        /// </summary>
        private static string NormalizeGroupKey(string rawKey)
        {
            var stem = rawKey.StartsWith("Nte_") ? rawKey.Substring(4) : rawKey;
            return Regex.Replace(stem, @"[_]?\d+$", string.Empty);
        }

        /// <summary>懒加载图集：首次使用时切图并缓存（双检锁）</summary>
        public void EnsureLoaded()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;

                var baseDir = Path.Combine(AppContext.BaseDirectory, AssetDir);
                var pngPath = Path.Combine(baseDir, AtlasPng);
                var jsonPath = Path.Combine(baseDir, AtlasJson);

                if (!File.Exists(pngPath) || !File.Exists(jsonPath))
                    throw new FileNotFoundException($"一键兑换图集缺失：{pngPath} / {jsonPath}");

                using var atlas = Cv2.ImRead(pngPath, ImreadModes.Color);
                if (atlas.Empty())
                    throw new InvalidDataException($"一键兑换图集读取失败：{pngPath}");

                var dict = JsonSerializer.Deserialize<Dictionary<string, AtlasItem>>(File.ReadAllText(jsonPath));
                if (dict == null || dict.Count == 0)
                    throw new InvalidDataException($"一键兑换图集 JSON 为空：{jsonPath}");

                foreach (var kv in dict)
                {
                    var it = kv.Value;
                    if (it.w <= 0 || it.h <= 0) continue;
                    var rect = new Rect(it.x, it.y, it.w, it.h);
                    if (rect.X < 0 || rect.Y < 0 || rect.Right > atlas.Width || rect.Bottom > atlas.Height) continue;

                    // 必须 Clone：atlas using 释放后切片 Mat 失效
                    var tpl = new Mat(atlas, rect).Clone();
                    _templates[kv.Key] = tpl;

                    var group = NormalizeGroupKey(kv.Key);
                    if (!_groups.TryGetValue(group, out var list))
                        _groups[group] = list = new List<string>();
                    list.Add(kv.Key);
                }

                _initialized = true;
            }
        }

        /// <summary>
        /// 在给定场景（通常为已裁剪的 ROI 区域）内对指定组的所有变体做 MatchTemplate，
        /// 取最高分且 ≥ 阈值的命中。center 为场景内局部坐标。
        /// [   复][CS8825] matchedKey 改为可空注解
        /// </summary>
        public bool TryMatchAny(Mat scene, string groupKey, out Point center, out string? matchedKey, double? threshold = null)
        {
            EnsureLoaded();
            center = default;
            matchedKey = null;
            var thr = threshold ?? MatchThreshold;
            if (scene.Empty() || !_groups.TryGetValue(groupKey, out var keys)) return false;

            double best = -1;
            string? bestKey = null;
            Point bestLoc = default;
            Size bestSize = default;

            foreach (var key in keys)
            {
                if (!_templates.TryGetValue(key, out var tpl)) continue;
                if (scene.Width < tpl.Width || scene.Height < tpl.Height) continue;

                using var res = new Mat();
                Cv2.MatchTemplate(scene, tpl, res, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(res, out _, out var maxVal, out _, out var maxLoc);
                if (maxVal > best)
                {
                    best = maxVal;
                    bestKey = key;
                    bestLoc = maxLoc;
                    bestSize = tpl.Size();
                }
            }

            if (bestKey is null || best < thr) return false;
            center = new Point(bestLoc.X + bestSize.Width / 2, bestLoc.Y + bestSize.Height / 2);
            matchedKey = bestKey;
            return true;
        }

        /// <summary>释放全部模板 Mat</summary>
        public void Dispose()
        {
            lock (_initLock)
            {
                foreach (var m in _templates.Values) m.Dispose();
                _templates.Clear();
                _groups.Clear();
                _initialized = false;
            }
        }
    }
}
