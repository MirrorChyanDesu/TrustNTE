using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class MapMatchService : IMapMatchService, IDisposable
    {
        private readonly ILogService _logService;

        private Mat? _bigMapDescriptors;
        private KeyPoint[]? _bigMapKeypoints;
        private readonly AKAZE _akaze;
        private readonly BFMatcher _matcher;

        private int _bigMapWidth;
        private int _bigMapHeight;
        private Mat? _bigMapEdge;

        private readonly string _debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "MapDebug");

        public MapMatchService(ILogService logService)
        {
            _logService = logService;
            _akaze = AKAZE.Create();
            _matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);

            Directory.CreateDirectory(_debugDir);
            InitializeBigMap();
        }

        private void InitializeBigMap()
        {
            string mapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Map.png");
            if (!File.Exists(mapPath)) mapPath = @"D:\TrustNTE\Assets\Map.png";

            if (!File.Exists(mapPath)) return;

            try
            {
                using var bigMapOrigin = Cv2.ImRead(mapPath, ImreadModes.Grayscale);
                if (bigMapOrigin.Empty()) return;

                _bigMapWidth = bigMapOrigin.Width;
                _bigMapHeight = bigMapOrigin.Height;

                _bigMapEdge = new Mat();
                Cv2.GaussianBlur(bigMapOrigin, _bigMapEdge, new OpenCvSharp.Size(3, 3), 0);
                Cv2.Canny(_bigMapEdge, _bigMapEdge, 30, 100);

                _bigMapDescriptors = new Mat();
                _akaze.DetectAndCompute(_bigMapEdge, null, out _bigMapKeypoints, _bigMapDescriptors);

                _logService.LogInfo($"[地图匹配] 初始化完毕，大地图特征点: {_bigMapKeypoints?.Length ?? 0} 个。");
            }
            catch (Exception ex)
            {
                _logService.LogError($"[地图匹配] 初始化大地图异常: {ex.Message}");
            }
        }

        public Point? MatchMiniMapToBigMap(Mat fullScreenFrame, int centerX, int centerY, int radius)
        {
            if (_bigMapDescriptors == null || _bigMapKeypoints == null || _bigMapDescriptors.Empty() || _bigMapEdge == null)
                return null;
            if (fullScreenFrame == null || fullScreenFrame.Empty())
                return null;

            try
            {
                int x = centerX - radius;
                int y = centerY - radius;
                int width = radius * 2;
                int height = radius * 2;

                if (x < 0 || y < 0 || x + width > fullScreenFrame.Width || y + height > fullScreenFrame.Height)
                    return null;

                using var miniMapRoi = new Mat(fullScreenFrame, new Rect(x, y, width, height));
                using var grayMat = new Mat();
                Cv2.CvtColor(miniMapRoi, grayMat, ColorConversionCodes.BGR2GRAY);

                int r = width / 2;

                // 提亮暗部细节
                using var clahe = Cv2.CreateCLAHE(clipLimit: 3.0, tileGridSize: new OpenCvSharp.Size(8, 8));
                clahe.Apply(grayMat, grayMat);

                using var edgeMat = new Mat();
                Cv2.GaussianBlur(grayMat, edgeMat, new OpenCvSharp.Size(3, 3), 0);
                Cv2.Canny(edgeMat, edgeMat, 15, 50); // 调低 Canny 阈值，抓取更多暗部地形线

                // =================    正 Mask 大小 =================
                // 1. 盖死中心箭头和视野扇形：半径缩小到 55 (完美对应你图5的蓝圈)
                Cv2.Circle(edgeMat, new Point(r, r), 55, Scalar.Black, -1);

                // 2. 盖死右上角相机：缩小到 65x65
                Cv2.Rectangle(edgeMat, new Rect(width - 65, 0, 65, 65), Scalar.Black, -1);

                // 3. 盖死最外圈渐变边框：往内缩 10 像素
                Cv2.Circle(edgeMat, new Point(r, r), r - 10, Scalar.Black, 10);
                // ==================================================

                Cv2.ImWrite(Path.Combine(_debugDir, "1_mini_edge_clean.png"), edgeMat);

                using var miniMapDescriptors = new Mat();
                _akaze.DetectAndCompute(edgeMat, null, out var miniMapKeypoints, miniMapDescriptors);

                if (miniMapDescriptors.Empty() || miniMapKeypoints == null || miniMapKeypoints.Length < 10)
                {
                    _logService.LogInfo($"[地图匹配] 小地图特征点太少: {miniMapKeypoints?.Length ?? 0} 个");
                    return null;
                }

                var matches = _matcher.KnnMatch(miniMapDescriptors, _bigMapDescriptors, k: 2);

                var goodMatches = new List<DMatch>();
                foreach (var match in matches)
                {
                    if (match.Length == 2 && match[0].Distance < match[1].Distance * 0.75)
                    {
                        goodMatches.Add(match[0]);
                    }
                }

                if (goodMatches.Count < 8)
                {
                    _logService.LogInfo($"[地图匹配] 强匹配点太少: {goodMatches.Count} 个，放弃。");
                    SaveDebugImage(edgeMat, goodMatches, miniMapKeypoints, new Point2f(0, 0), false);
                    return null;
                }

                var miniMapPoints = goodMatches.Select(m => miniMapKeypoints[m.QueryIdx].Pt).ToArray();
                var bigMapPoints = goodMatches.Select(m => _bigMapKeypoints[m.TrainIdx].Pt).ToArray();

                using var homography = Cv2.FindHomography(
                    InputArray.Create(miniMapPoints),
                    InputArray.Create(bigMapPoints),
                    HomographyMethods.Ransac,
                    5.0,
                    null);

                if (homography == null || homography.Empty())
                {
                    SaveDebugImage(edgeMat, goodMatches, miniMapKeypoints, new Point2f(0, 0), false);
                    return null;
                }

                using var centerPointMat = new Mat(1, 1, MatType.CV_32FC2);
                centerPointMat.Set(0, 0, new Point2f(radius, radius));

                using var transformedMat = new Mat();
                Cv2.PerspectiveTransform(centerPointMat, transformedMat, homography);

                var resultPoint = transformedMat.Get<Point2f>(0, 0);

                if (resultPoint.X >= 0 && resultPoint.X <= _bigMapWidth &&
                    resultPoint.Y >= 0 && resultPoint.Y <= _bigMapHeight)
                {
                    SaveDebugImage(edgeMat, goodMatches, miniMapKeypoints, resultPoint, true);
                    return new Point((int)resultPoint.X, (int)resultPoint.Y);
                }
                else
                {
                    SaveDebugImage(edgeMat, goodMatches, miniMapKeypoints, resultPoint, false);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"[地图匹配] 匹配过程异常: {ex.Message}");
            }

            return null;
        }

        private void SaveDebugImage(Mat miniEdge, List<DMatch> goodMatches, KeyPoint[] miniKps, Point2f finalResult, bool isSuccess)
        {
            try
            {
                double scale = 800.0 / _bigMapWidth;
                int bigMapW = (int)(_bigMapWidth * scale);
                int bigMapH = (int)(_bigMapHeight * scale);

                using var bigMapDebug = new Mat();
                Cv2.Resize(_bigMapEdge!, bigMapDebug, new OpenCvSharp.Size(bigMapW, bigMapH));
                using var bigMapColor = new Mat();
                Cv2.CvtColor(bigMapDebug, bigMapColor, ColorConversionCodes.GRAY2BGR);

                using var miniMapColor = new Mat();
                Cv2.CvtColor(miniEdge, miniMapColor, ColorConversionCodes.GRAY2BGR);

                using var combined = new Mat(Math.Max(miniEdge.Height, bigMapH), miniEdge.Width + bigMapW, MatType.CV_8UC3, Scalar.Black);

                miniMapColor.CopyTo(new Mat(combined, new Rect(0, 0, miniEdge.Width, miniEdge.Height)));
                bigMapColor.CopyTo(new Mat(combined, new Rect(miniEdge.Width, 0, bigMapW, bigMapH)));

                var rand = new Random();
                foreach (var m in goodMatches)
                {
                    var pt1 = miniKps[m.QueryIdx].Pt;
                    var pt2 = _bigMapKeypoints![m.TrainIdx].Pt;
                    var scaledPt2 = new Point((int)(pt2.X * scale + miniEdge.Width), (int)(pt2.Y * scale));

                    var color = new Scalar(rand.Next(256), rand.Next(256), rand.Next(256));
                    Cv2.Line(combined, new Point((int)pt1.X, (int)pt1.Y), scaledPt2, color, 1);
                }

                if (isSuccess)
                {
                    var finalPointOnCanvas = new Point((int)(finalResult.X * scale + miniEdge.Width), (int)(finalResult.Y * scale));
                    Cv2.Circle(combined, finalPointOnCanvas, 10, new Scalar(0, 0, 255), -1);
                }

                Cv2.ImWrite(Path.Combine(_debugDir, "2_match_lines.png"), combined);
            }
            catch { }
        }

        public void Dispose()
        {
            _akaze?.Dispose();
            _matcher?.Dispose();
            _bigMapDescriptors?.Dispose();
            _bigMapEdge?.Dispose();
        }
    }
}