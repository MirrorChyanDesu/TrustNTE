using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    public sealed class NteCombatAtlasService : IDisposable
    {
        public sealed record AtlasHit(Rect Rect, string Group);

        private sealed class Tpl
        {
            public string Group = "";
            public Mat Bgr = new();
            public Mat Mask = new();
        }

        private readonly ILogService _log;
        private readonly List<Tpl> _tpls = new();
        private readonly Dictionary<string, int> _cursor = new();
        private static readonly float[] Scales = { 0.85f, 1.0f, 1.2f };

        public bool IsReady => _tpls.Count > 0;

        public NteCombatAtlasService(ILogService log) => _log = log;

        public void Load(string dir)
        {
            int alphaCnt = 0;
            foreach (var f in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
            {
                using var raw = Cv2.ImRead(f, ImreadModes.Unchanged);
                if (raw.Empty()) continue;

                string group = ResolveGroup(dir, f);

                using var alpha = new Mat();
                using var bgr = new Mat();
                switch (raw.Channels())
                {
                    case 1:   // 灰度白字模板
                        Cv2.CvtColor(raw, bgr, ColorConversionCodes.GRAY2BGR);
                        alpha.Create(raw.Rows, raw.Cols, MatType.CV_8UC1);
                        alpha.SetTo(Scalar.All(255));
                        break;

                    case 2:   // 灰度+alpha（极大概率是这批被丢的）
                        using (var g = new Mat())
                        {
                            Cv2.ExtractChannel(raw, g, 0);
                            Cv2.CvtColor(g, bgr, ColorConversionCodes.GRAY2BGR);
                        }
                        Cv2.ExtractChannel(raw, alpha, 1);
                        alphaCnt++;
                        break;

                    case 3:   // 彩色
                        raw.CopyTo(bgr);
                        alpha.Create(raw.Rows, raw.Cols, MatType.CV_8UC1);
                        alpha.SetTo(Scalar.All(255));
                        break;

                    case 4:   // 彩色+alpha
                        Cv2.ExtractChannel(raw, alpha, 3);
                        Cv2.CvtColor(raw, bgr, ColorConversionCodes.BGRA2BGR);
                        alphaCnt++;
                        break;

                    default: continue;
                }

                foreach (var s in Scales)
                {
                    var b = new Mat(); var m = new Mat();
                    Cv2.Resize(bgr, b, default, s, s, InterpolationFlags.Linear);
                    Cv2.Resize(alpha, m, default, s, s, InterpolationFlags.Linear);
                    Cv2.Threshold(m, m, 128, 255, ThresholdTypes.Binary);
                    _tpls.Add(new Tpl { Group = group, Bgr = b, Mask = m });
                }
            }

            if (_tpls.Count == 0) { _log.LogError($"[战斗] 图集目录无有效模板: {dir}"); return; }

            int groups = _tpls.Select(t => t.Group).Distinct().Count();
            _log.LogInfo($"[战斗] 图集(alpha掩码) 加载成功：{groups} 组 / {_tpls.Count / Scales.Length} 张模板" +
                         $"（透明 {alphaCnt} 张 × {Scales.Length} 尺度 = {_tpls.Count} 个匹配体）");
        }

        private static string ResolveGroup(string root, string file)
        {
            var parent = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(file)));
            var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
            return !string.IsNullOrEmpty(parent) && parent != rootName
                ? parent
                : Path.GetFileNameWithoutExtension(file).Split('_')[0];
        }

        public AtlasHit? MatchInGroup(Mat frameBgr, double threshold)
        {
            if (!IsReady || frameBgr.Empty()) return null;

            foreach (var g in _tpls.Select(t => t.Group).Distinct())
            {
                var list = _tpls.Where(t => t.Group == g).ToList();
                _cursor.TryGetValue(g, out int cur);

                for (int k = 0; k < list.Count; k++)
                {
                    var t = list[(cur + k) % list.Count];
                    if (MatchOne(frameBgr, t, threshold) is Rect r)
                    {
                        _cursor[g] = list.IndexOf(t);
                        return new AtlasHit(r, g);
                    }
                }
                _cursor[g] = (cur + 1) % list.Count;
            }
            return null;
        }

        private static Rect? MatchOne(Mat frame, Tpl t, double threshold)
        {
            if (t.Bgr.Width > frame.Width || t.Bgr.Height > frame.Height) return null;

            using var res = new Mat();
            //    复2：换成 CCoeffNormed，带 mask 匹配
            Cv2.MatchTemplate(frame, t.Bgr, res, TemplateMatchModes.CCoeffNormed, t.Mask);
            Cv2.MinMaxLoc(res, out _, out double max, out _, out Point loc);

            return max >= threshold ? new Rect(loc.X, loc.Y, t.Bgr.Width, t.Bgr.Height) : null;
        }

        public void Dispose()
        {
            foreach (var t in _tpls) { t.Bgr.Dispose(); t.Mask.Dispose(); }
            _tpls.Clear();
        }
    }
}