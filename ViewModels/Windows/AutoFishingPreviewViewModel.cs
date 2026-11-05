using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// [   复] 强制使用 WPF 的 Point 和 Rect，防止被全局的 OpenCvSharp.Point 干扰
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace TrustNTE.ViewModels
{
    public partial class AutoFishingPreviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private ImageSource? _previewImage;

        [ObservableProperty]
        private double _originalFrameWidth = 1920;

        [ObservableProperty]
        private double _originalFrameHeight = 1080;

        [ObservableProperty]
        private WpfRect _selectionRect = new(0, 0, 0, 0);

        [ObservableProperty]
        private bool _isSelecting = false;

        [ObservableProperty]
        private string _mousePosText = "鼠标移入可查看坐标，按住左键拖拽框选 ROI";

        private readonly Action<string> _onRoiSelected;
        private WpfPoint? _dragStart;

        public AutoFishingPreviewViewModel(Action<string> onRoiSelected)
        {
            _onRoiSelected = onRoiSelected;
        }

        public void StartSelection(WpfPoint mousePos, double ctrlW, double ctrlH)
        {
            if (PreviewImage is not BitmapSource bmp) return;
            if (bmp.PixelWidth == 0 || bmp.PixelHeight == 0 || ctrlW <= 0 || ctrlH <= 0) return;

            double scale = Math.Min(ctrlW / bmp.PixelWidth, ctrlH / bmp.PixelHeight);
            double renderW = bmp.PixelWidth * scale;
            double renderH = bmp.PixelHeight * scale;
            double offX = (ctrlW - renderW) / 2;
            double offY = (ctrlH - renderH) / 2;

            if (mousePos.X < offX || mousePos.X > offX + renderW ||
                mousePos.Y < offY || mousePos.Y > offY + renderH)
            {
                return;
            }

            _dragStart = mousePos;
            IsSelecting = true;
            SelectionRect = new WpfRect(mousePos, mousePos);
        }

        public void UpdateSelection(WpfPoint mousePos, double ctrlW, double ctrlH)
        {
            if (PreviewImage == null)
            {
                MousePosText = "等待任务启动后显示画面...";
                return;
            }

            if (PreviewImage is not BitmapSource bmp) return;

            var origPt = ControlToOriginal(mousePos, ctrlW, ctrlH, bmp);
            int ox = (int)Math.Clamp(origPt.X, 0, OriginalFrameWidth);
            int oy = (int)Math.Clamp(origPt.Y, 0, OriginalFrameHeight);
            MousePosText = $"原始坐标: X={ox}, Y={oy}";

            if (_dragStart != null && IsSelecting)
            {
                SelectionRect = new WpfRect(_dragStart.Value, mousePos);
            }
        }

        public void EndSelection(WpfPoint mousePos, double ctrlW, double ctrlH)
        {
            if (PreviewImage is not BitmapSource bmp || _dragStart == null) return;

            var r = SelectionRect;
            IsSelecting = false;
            _dragStart = null;

            var p1 = ControlToOriginal(new WpfPoint(r.X, r.Y), ctrlW, ctrlH, bmp);
            var p2 = ControlToOriginal(new WpfPoint(r.Right, r.Bottom), ctrlW, ctrlH, bmp);

            int x1 = (int)Math.Clamp(Math.Min(p1.X, p2.X), 0, OriginalFrameWidth);
            int y1 = (int)Math.Clamp(Math.Min(p1.Y, p2.Y), 0, OriginalFrameHeight);
            int x2 = (int)Math.Clamp(Math.Max(p1.X, p2.X), 0, OriginalFrameWidth);
            int y2 = (int)Math.Clamp(Math.Max(p1.Y, p2.Y), 0, OriginalFrameHeight);

            int w = x2 - x1;
            int h = y2 - y1;

            if (w < 10 || h < 10) return;

            _onRoiSelected?.Invoke($"{x1},{y1},{w},{h}");
        }

        public void CancelSelection()
        {
            if (!IsSelecting) return;
            IsSelecting = false;
            _dragStart = null;
            SelectionRect = new WpfRect(0, 0, 0, 0);
            MousePosText = "框选已取消";
        }

        private WpfPoint ControlToOriginal(WpfPoint ctrlPt, double ctrlW, double ctrlH, BitmapSource bmp)
        {
            double bmpW = bmp.PixelWidth;
            double bmpH = bmp.PixelHeight;

            if (bmpW == 0 || bmpH == 0 || ctrlW == 0 || ctrlH == 0) return ctrlPt;

            double scale = Math.Min(ctrlW / bmpW, ctrlH / bmpH);
            double offsetX = (ctrlW - bmpW * scale) / 2;
            double offsetY = (ctrlH - bmpH * scale) / 2;

            double bx = (ctrlPt.X - offsetX) / scale;
            double by = (ctrlPt.Y - offsetY) / scale;

            return new WpfPoint(bx * OriginalFrameWidth / bmpW, by * OriginalFrameHeight / bmpH);
        }
    }
}