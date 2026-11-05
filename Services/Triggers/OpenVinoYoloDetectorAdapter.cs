using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;
using TrustNTE.Services.Implementations;

namespace TrustNTE.Services.Triggers
{
    /// <summary>
    /// 把现有的 OpenVinoYoloService 包装成 ICombatYoloDetector（5 行适配）
    /// </summary>
    public class OpenVinoYoloDetectorAdapter : ICombatYoloDetector
    {
        private readonly OpenVinoYoloService _svc;

        public OpenVinoYoloDetectorAdapter(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"找不到 YOLO 模型文件: {modelPath}");

            _svc = new OpenVinoYoloService(modelPath);
        }

        public IReadOnlyList<CombatYoloBox>? Detect(Mat frame, float conf)
        {
            var results = _svc.Detect(frame, conf);
            if (results == null) return null;

            return results.Select(r => new CombatYoloBox(r.Rect, r.Confidence, "target")).ToList();
        }

        public void Dispose() => _svc.Dispose();
    }
}