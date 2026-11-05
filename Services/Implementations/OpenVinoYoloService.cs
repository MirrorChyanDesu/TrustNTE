using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Sdcb.OpenVINO;

namespace TrustNTE.Services.Implementations
{
    public class YoloResult
    {
        public Rect Rect { get; set; }
        public float Confidence { get; set; }
    }

    public class OpenVinoYoloService : IDisposable
    {
        private readonly CompiledModel _compiledModel;
        private readonly InferRequest _inferRequest;
        private readonly Shape _inputShape;

        public OpenVinoYoloService(string modelPath, string device = "CPU")
        {
            if (!File.Exists(modelPath)) throw new FileNotFoundException($"YOLO 模型不存在: {modelPath}");

            using Model rawModel = OVCore.Shared.ReadModel(modelPath);
            _inputShape = rawModel.Inputs.Primary.Shape;
            _compiledModel = OVCore.Shared.CompileModel(rawModel, device);
            _inferRequest = _compiledModel.CreateInferRequest();
        }

        public List<YoloResult> Detect(Mat frame, double confidenceThreshold = 0.65)
        {
            var results = new List<YoloResult>();

            int inputHeight = (int)_inputShape[2];
            int inputWidth = (int)_inputShape[3];

            using Mat blob = CvDnn.BlobFromImage(frame, 1.0 / 255.0, new OpenCvSharp.Size(inputWidth, inputHeight), new Scalar(0, 0, 0), true, false);

            float[] blobData = new float[blob.Total()];
            Marshal.Copy(blob.Data, blobData, 0, blobData.Length);

            using (Tensor input = Tensor.FromArray<float>(blobData, new Shape(1, 3, inputHeight, inputWidth)))
            {
                _inferRequest.Inputs.Primary = input;
            }

            _inferRequest.Run();

            using Tensor output = _inferRequest.Outputs.Primary;
            ReadOnlySpan<float> data = output.GetData<float>();
            var shape = output.Shape;

            // [   复] 自动判断输出格式是 [1, 84, 8400] 还是 [1, 8400, 84]
            bool isTransposed = shape[1] < shape[2];
            int numAnchors = isTransposed ? (int)shape[2] : (int)shape[1];
            int numClasses = isTransposed ? (int)shape[1] - 4 : (int)shape[2] - 4;

            float scaleX = (float)frame.Width / inputWidth;
            float scaleY = (float)frame.Height / inputHeight;

            for (int i = 0; i < numAnchors; i++)
            {
                float maxScore = 0;
                int maxClass = -1;
                for (int c = 0; c < numClasses; c++)
                {
                    // 根据是否转置，使用不同的读取方式
                    float score = isTransposed ? data[(c + 4) * numAnchors + i] : data[i * (numClasses + 4) + c + 4];
                    if (score > maxScore) { maxScore = score; maxClass = c; }
                }

                if (maxScore >= confidenceThreshold)
                {
                    float cx = isTransposed ? data[0 * numAnchors + i] : data[i * (numClasses + 4) + 0];
                    float cy = isTransposed ? data[1 * numAnchors + i] : data[i * (numClasses + 4) + 1];
                    float w = isTransposed ? data[2 * numAnchors + i] : data[i * (numClasses + 4) + 2];
                    float h = isTransposed ? data[3 * numAnchors + i] : data[i * (numClasses + 4) + 3];

                    int x = (int)((cx - w / 2) * scaleX);
                    int y = (int)((cy - h / 2) * scaleY);
                    int width = (int)(w * scaleX);
                    int height = (int)(h * scaleY);

                    x = Math.Max(0, x);
                    y = Math.Max(0, y);
                    width = Math.Min(frame.Width - x, width);
                    height = Math.Min(frame.Height - y, height);

                    if (width > 20 && height > 20)
                    {
                        results.Add(new YoloResult { Rect = new Rect(x, y, width, height), Confidence = maxScore });
                    }
                }
            }

            return NMS(results, 0.45);
        }

        private List<YoloResult> NMS(List<YoloResult> boxes, double iouThreshold)
        {
            var result = new List<YoloResult>();
            var sorted = boxes.OrderByDescending(b => b.Rect.Width * b.Rect.Height).ToList();
            while (sorted.Count > 0)
            {
                var current = sorted[0];
                result.Add(current);
                sorted.RemoveAt(0);
                sorted.RemoveAll(b => Iou(current.Rect, b.Rect) > iouThreshold);
            }
            return result;
        }

        private double Iou(Rect a, Rect b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            int w = Math.Max(0, x2 - x1);
            int h = Math.Max(0, y2 - y1);
            int inter = w * h;
            int union = a.Width * a.Height + b.Width * b.Height - inter;
            return union == 0 ? 0 : (double)inter / union;
        }

        public void Dispose()
        {
            _inferRequest.Dispose();
            _compiledModel.Dispose();
        }
    }
}