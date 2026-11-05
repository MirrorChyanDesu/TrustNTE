using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using TrustNTE.Services;
using TrustNTE.ViewModels;

namespace TrustNTE.Helpers
{
    /// <summary>
    /// 钓鱼预览渲染器。负责 OpenCV 绘制、缩放、跨线程 UI 派发及节流保护。
    /// </summary>
    public class AutoFishingPreviewRenderer
    {
        private const int PreviewIntervalMs = 100;
        private const int MaxWidth = 960;

        private int _isPreviewUpdating = 0;
        private DateTime _lastPreviewUpdate = DateTime.MinValue;
        private DateTime _lastGuardCheck = DateTime.MinValue;
        private WriteableBitmap? _fishingBitmap;

        /// <summary>
        /// 渲染并推送到 UI。
        /// </summary>
        public void Render(Mat frame, OpenCvSharp.Rect mainRoi, OpenCvSharp.Rect? aRect, OpenCvSharp.Rect? bRect, AutoFishingPreviewViewModel? previewVm)
        {
            if (previewVm == null) return;
            if ((DateTime.Now - _lastPreviewUpdate).TotalMilliseconds < PreviewIntervalMs) return;

            if (Interlocked.Exchange(ref _isPreviewUpdating, 1) == 1)
            {
                if ((DateTime.Now - _lastGuardCheck).TotalMilliseconds > 1000)
                {
                    _lastGuardCheck = DateTime.Now;
                    Interlocked.Exchange(ref _isPreviewUpdating, 0);
                    System.Diagnostics.Debug.WriteLine("[钓鱼预览] 检测到 UI 队列阻塞，已强制解锁");
                }
                return;
            }

            _lastPreviewUpdate = DateTime.Now;
            _lastGuardCheck = DateTime.Now;

            Mat snapshot;
            try
            {
                snapshot = frame.Clone();
            }
            catch
            {
                Interlocked.Exchange(ref _isPreviewUpdating, 0);
                return;
            }

            var roiCopy = mainRoi;

            _ = Task.Run(() =>
            {
                Mat? finalMat = null;
                try
                {
                    using var drawMat = snapshot;

                    Cv2.Rectangle(drawMat, roiCopy, Scalar.Orange, 2);
                    Cv2.PutText(drawMat, "Control ROI", new Point(roiCopy.X, roiCopy.Y - 5), HersheyFonts.HersheySimplex, 0.6, Scalar.Orange, 1);

                    if (aRect.HasValue)
                    {
                        Cv2.Rectangle(drawMat, aRect.Value, Scalar.Lime, 2);
                        Cv2.PutText(drawMat, "A", new Point(aRect.Value.X, aRect.Value.Y - 5), HersheyFonts.HersheySimplex, 0.6, Scalar.Lime, 1);
                    }
                    if (bRect.HasValue)
                    {
                        Cv2.Rectangle(drawMat, bRect.Value, Scalar.Yellow, 2);
                        Cv2.PutText(drawMat, "B", new Point(bRect.Value.X, bRect.Value.Y + bRect.Value.Height + 15), HersheyFonts.HersheySimplex, 0.6, Scalar.Yellow, 1);
                    }

                    finalMat = new Mat();
                    if (drawMat.Width > MaxWidth)
                    {
                        int h = (int)((double)MaxWidth / drawMat.Width * drawMat.Height);
                        Cv2.Resize(drawMat, finalMat, new OpenCvSharp.Size(MaxWidth, h));
                    }
                    else
                    {
                        drawMat.CopyTo(finalMat);
                    }

                    var mat = finalMat;
                    finalMat = null;

                    var app = Application.Current;
                    if (app == null)
                    {
                        mat.Dispose();
                        Interlocked.Exchange(ref _isPreviewUpdating, 0);
                        return;
                    }

                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var bmp = WriteableBitmapHelper.Update(ref _fishingBitmap, mat);
                            if (bmp != null && !ReferenceEquals(previewVm.PreviewImage, bmp))
                            {
                                previewVm.PreviewImage = bmp;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[钓鱼预览] UI更新失败: {ex.Message}");
                        }
                        finally
                        {
                            mat.Dispose();
                            Interlocked.Exchange(ref _isPreviewUpdating, 0);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    finalMat?.Dispose();
                    Interlocked.Exchange(ref _isPreviewUpdating, 0);
                    System.Diagnostics.Debug.WriteLine($"[钓鱼预览] 后台处理失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 清理位图状态。
        /// </summary>
        public void Reset()
        {
            _fishingBitmap = null;
        }
    }
}