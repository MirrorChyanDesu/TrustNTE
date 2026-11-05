using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Triggers
{
    /// <summary>
    /// "Lv" 轮廓指纹匹配器（对齐 ok-nte CombatCheck.find_lv 四层结构）：
    /// ① NteVisionUtils.IsolateLvToWhite（白|灰|红三路，模板与运行时同管线）
    /// ② 轮廓指纹粗筛（solidity/重心/宽高比）
    /// ③ 32×32 归一化 IoU 精验（尺度免疫核心）
    /// ④ L+V 空间配对（V 在 L 右侧、Y 平齐、配对区白色占比≥0.82）
    /// 返回坐标为 1080P 标准帧绝对坐标，只准喂 ClickLogical。
    /// </summary>
    public sealed class LvShapeMatcher : IDisposable
    {
        private const int NormSize = 32;
        // 1080P 搜索区：x 0.154~0.907, y 0~0.70
        private const int RoiX = 296, RoiY = 0, RoiW = 1444, RoiH = 756;

        // 源码原值面积窗口（参考分辨率 2560，随帧宽等比缩放）。
        // 实测调参口：远处小怪漏检 → MinAreaFactor 0.8→0.6；神话怪漏检 → MaxAreaFactor 1.5→1.8。
        private const double RefLetterPx = 15.0, RefLetterPxMax = 20.0;
        private const double MinAreaFactor = 0.8, MaxAreaFactor = 1.5;

        private readonly ILogService _log;
        private readonly Mat _lNorm = new(), _vNorm = new();
        private (double Solidity, double Cx, double Cy, double Aspect) _lFp, _vFp;
        public bool IsReady { get; private set; }

        public LvShapeMatcher(ILogService log) => _log = log;

        /// <summary>
        /// lv_shape.png 必须裁【普通怪】的 Lv.60，L/V 两字母完整、不粘连。
        /// 裁成传说怪大字 → 普通怪 IoU 崩掉，指纹路全废。
        /// </summary>
        public bool BuildTemplate(Mat lvCropBgr)
        {
            IsReady = false;
            using var bin = NteVisionUtils.IsolateLvToWhite(lvCropBgr);   // 与运行时同一管线
            Cv2.FindContours(bin, out var cnts, out _, RetrievalModes.External,
                             ContourApproximationModes.ApproxSimple);
            var valid = cnts
                .Where(c => { var r = Cv2.BoundingRect(c); return r.Width > 2 && r.Height > 2; })
                .OrderBy(c => Cv2.BoundingRect(c).X).ToArray();
            if (valid.Length < 2)
            {
                _log.LogError("[战斗] Lv指纹构建失败：白色轮廓不足2个(需L+V)，请站普通怪前重新截图裁 Lv.60 存 lv_shape.png");
                return false;
            }
            (_lFp, var l) = Extract(valid[0]); l.CopyTo(_lNorm); l.Dispose();
            (_vFp, var v) = Extract(valid[1]); v.CopyTo(_vNorm); v.Dispose();
            IsReady = true;

            // 自检：L 有空腔 solidity 应 ~0.45~0.6，V 应 ~0.3~0.45；两值过近 → 裁错或粘连
            bool lBad = _lFp.Solidity < 0.35 || _lFp.Solidity > 0.75;
            bool vBad = _vFp.Solidity < 0.20 || _vFp.Solidity > 0.60;
            bool tooClose = Math.Abs(_lFp.Solidity - _vFp.Solidity) < 0.05;
            string warn = (lBad || vBad || tooClose) ? " [数值异常] 请确认裁的是普通怪 Lv.60 且 L/V 未粘连！" : "";
            _log.LogInfo($"[战斗] Lv指纹构建成功: L(solid={_lFp.Solidity:F2},aspect={_lFp.Aspect:F2}) " +
                         $"V(solid={_vFp.Solidity:F2},aspect={_vFp.Aspect:F2}){warn}");
            return true;
        }

        /// <summary>运行时检测：返回怪物头顶 Lv 配对框（1080P 帧绝对坐标），未命中 null。frame 需为 1080P 标准帧。</summary>
        public Rect? FindLv(Mat frame)
        {
            if (!IsReady) return null;
            using var roi = new Mat(frame, new Rect(RoiX, RoiY, RoiW, RoiH));
            using var bin = NteVisionUtils.IsolateLvToWhite(roi);

            double scale = frame.Width / 2560.0;                          // 与源码同基准
            double minArea = Math.Pow(RefLetterPx * scale, 2) * MinAreaFactor;
            double maxArea = Math.Pow(RefLetterPxMax * scale, 2) * MaxAreaFactor;

            var lCands = new List<(Rect B, double S)>();
            var vCands = new List<(Rect B, double S)>();
            Cv2.FindContours(bin, out var cnts, out _, RetrievalModes.External,
                             ContourApproximationModes.ApproxSimple);

            foreach (var cnt in cnts)
            {
                var b = Cv2.BoundingRect(cnt);
                double bboxArea = (double)b.Width * b.Height;
                if (bboxArea < minArea || bboxArea > maxArea) continue;   // 源码面积窗口

                var fp = Fingerprint(cnt, b);
                double aspect = b.Width / (double)Math.Max(b.Height, 1);
                if (Near(fp, _lFp))
                {
                    double iou = ContourIou(_lNorm, cnt, b);
                    if (_lFp.Aspect * 0.6 < aspect && aspect < _lFp.Aspect * 1.5 && iou > 0.5)
                        lCands.Add((b, iou));
                }
                else if (Near(fp, _vFp))
                {
                    double iou = ContourIou(_vNorm, cnt, b);
                    if (_vFp.Aspect * 0.6 < aspect && aspect < _vFp.Aspect * 1.5 && iou > 0.5)
                        vCands.Add((b, iou));
                }
            }

            // L-V 配对：V 在 L 右侧、Y 平齐
            Rect? best = null; double bestConf = 0;
            foreach (var l in lCands)
                foreach (var v in vCands)
                {
                    int gap = v.B.X - (l.B.X + l.B.Width);
                    if (gap < -(int)(l.B.Width * 0.5) || gap > l.B.Height * 1.5) continue;
                    if (Math.Abs(v.B.Y - l.B.Y) > l.B.Height * 0.5) continue;
                    double conf = (l.S + v.S) / 2;
                    if (conf < 0.7) continue;

                    // ROI 局部坐标 + 偏移还原为帧绝对坐标（漏了这步点击整体偏左 296px）
                    var pair = Rect.FromLTRB(l.B.X + RoiX, Math.Min(l.B.Y, v.B.Y) + RoiY,
                                             v.B.X + v.B.Width + RoiX, Math.Max(l.B.Bottom, v.B.Bottom) + RoiY);
                    var local = new Rect(l.B.X, Math.Min(l.B.Y, v.B.Y), pair.Width, pair.Height);
                    if (WhiteRatio(bin, local) < 0.82) continue;   // 源码配对区占比阈值
                    if (conf > bestConf) { bestConf = conf; best = pair; }
                }
            return best;
        }

        public void Dispose() { _lNorm.Dispose(); _vNorm.Dispose(); }

        // ===== helpers =====
        private static bool Near((double S, double X, double Y, double A) a, (double S, double X, double Y, double A) b)
            => Math.Abs(a.S - b.S) < 0.15 && Math.Abs(a.X - b.X) < 0.15 && Math.Abs(a.Y - b.Y) < 0.15;

        private static (double, double, double, double) Fingerprint(Point[] cnt, Rect b)
        {
            double solidity = Cv2.ContourArea(cnt) / Math.Max((double)b.Width * b.Height, 1);
            var m = Cv2.Moments(cnt);
            double cx = m.M00 > 0 ? (m.M10 / m.M00 - b.X) / b.Width : 0.5;
            double cy = m.M00 > 0 ? (m.M01 / m.M00 - b.Y) / b.Height : 0.5;
            return (solidity, cx, cy, b.Width / (double)Math.Max(b.Height, 1));
        }

        private static Mat RenderNormalized(Point[] cnt, Rect b)
        {
            var img = new Mat(NormSize, NormSize, MatType.CV_8UC1, Scalar.All(0));
            var shifted = cnt.Select(p => new Point(
                (p.X - b.X) * (NormSize - 1) / Math.Max(b.Width - 1, 1),
                (p.Y - b.Y) * (NormSize - 1) / Math.Max(b.Height - 1, 1))).ToArray();
            Cv2.DrawContours(img, new[] { shifted }, -1, Scalar.All(255), -1);   // 填充渲染
            return img;
        }

        private static double ContourIou(Mat tpl, Point[] cnt, Rect b)
        {
            using var cand = RenderNormalized(cnt, b);
            using var i = new Mat();
            using var u = new Mat();
            Cv2.BitwiseAnd(tpl, cand, i); Cv2.BitwiseOr(tpl, cand, u);
            int ic = Cv2.CountNonZero(i), uc = Cv2.CountNonZero(u);
            return uc > 0 ? (double)ic / uc : 0;
        }

        private static ((double, double, double, double), Mat) Extract(Point[] cnt)
        {
            var b = Cv2.BoundingRect(cnt);
            return (Fingerprint(cnt, b), RenderNormalized(cnt, b));
        }

        private static double WhiteRatio(Mat bin, Rect r)
        {
            using var m = bin[r];
            return Cv2.CountNonZero(m) / (double)Math.Max(r.Width * r.Height, 1);
        }
    }
}
