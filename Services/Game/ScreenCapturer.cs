using OpenCvSharp;
using System;
using System.Runtime.InteropServices;

namespace TrustNTE.Services.Game
{
    public class ScreenCapturer
    {
        // ================= 基础 Win32 API =================
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits, ref BITMAPINFO lpbmi, uint usage);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        // 屏幕直读新增 API
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes; public ushort biBitCount;
            public uint biCompression; public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public uint biClrUsed; public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)] private struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public uint bmiColors; }

        // 线程本地内存池，彻底消除高频 new byte[] 带来的 GC 卡顿
        [ThreadStatic] private static byte[]? _pixScratch;

        // ================= 核心逻辑 =================

        public static string NormalizeMode(string? raw) => raw switch
        {
            null or "" => "PrintWindow",
            var s when s.Contains("WGC") => "WGC",
            var s when s.Contains("DXGI") => "Screen",    // 把 DXGI 的坑位让给屏幕直读
            var s when s.Contains("BitBlt") => "BitBlt",
            var s when s.Contains("DWM") => "DWM",
            _ => "PrintWindow"
        };

        public Mat? Capture(IntPtr hWnd, string mode)
        {
            try
            {
                Mat? mat = mode switch
                {
                    "BitBlt" => CaptureByBitBlt(hWnd),
                    "Screen" => CaptureByScreenRegion(hWnd),
                    "WGC" => CaptureByPrintWindow(hWnd, 3),
                    "DWM" => CaptureByPrintWindow(hWnd, 0),
                    _ => CaptureByPrintWindow(hWnd, 3)
                };

                //        兜底：如果抓到的是空图，或者由于全屏独占抓失败了，强制降级用 PrintWindow 再试一次
                if (mat == null || mat.Empty())
                {
                    return CaptureByPrintWindow(hWnd, 3);
                }
                return mat;
            }
            catch { return null; }
        }

        private Mat? CaptureByScreenRegion(IntPtr hWnd)
        {
            if (!GetClientRect(hWnd, out RECT rc)) return null;
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            if (w <= 0 || h <= 0) return null;

            var pt = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hWnd, ref pt)) return null;

            IntPtr deskDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(deskDC);
            IntPtr bmp = CreateCompatibleBitmap(deskDC, w, h);
            IntPtr old = SelectObject(memDC, bmp);

            Mat? result = null;
            GCHandle handle = default;

            try
            {
                //        核心   ：加入 CAPTUREBLT 标志 (0x40000000)
                // 防止无边框全屏被其他窗口遮挡导致截出黑屏
                if (BitBlt(memDC, 0, 0, w, h, deskDC, pt.X, pt.Y, 0x00CC0020 | 0x40000000))
                    result = ExtractMatFromBitmap(memDC, bmp, w, h, ref handle);
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
                SelectObject(memDC, old);
                DeleteObject(bmp);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, deskDC);
            }
            return result;
        }

        private Mat? CaptureByBitBlt(IntPtr hWnd)
        {
            if (!GetClientRect(hWnd, out RECT rect)) return null;
            int width = rect.Right - rect.Left; int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return null;

            IntPtr hWndDC = GetDC(hWnd);
            IntPtr mfcDC = CreateCompatibleDC(hWndDC);
            IntPtr saveBitMap = CreateCompatibleBitmap(hWndDC, width, height);
            IntPtr hOld = SelectObject(mfcDC, saveBitMap);

            Mat? result = null;
            GCHandle handle = default;

            try
            {
                if (BitBlt(mfcDC, 0, 0, width, height, hWndDC, 0, 0, 0x00CC0020))
                    result = ExtractMatFromBitmap(mfcDC, saveBitMap, width, height, ref handle);
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
                SelectObject(mfcDC, hOld);
                DeleteObject(saveBitMap);
                DeleteDC(mfcDC);
                ReleaseDC(hWnd, hWndDC);
            }
            return result;
        }

        private Mat? CaptureByPrintWindow(IntPtr hWnd, uint flags)
        {
            if (!GetClientRect(hWnd, out RECT rect)) return null;
            int width = rect.Right - rect.Left; int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return null;

            IntPtr hWndDC = GetWindowDC(hWnd);
            IntPtr mfcDC = CreateCompatibleDC(hWndDC);
            IntPtr saveBitMap = CreateCompatibleBitmap(hWndDC, width, height);
            IntPtr hOld = SelectObject(mfcDC, saveBitMap);

            Mat? result = null;
            GCHandle handle = default;

            try
            {
                if (PrintWindow(hWnd, mfcDC, flags))
                    result = ExtractMatFromBitmap(mfcDC, saveBitMap, width, height, ref handle);
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
                SelectObject(mfcDC, hOld);
                DeleteObject(saveBitMap);
                DeleteDC(mfcDC);
                ReleaseDC(hWnd, hWndDC);
            }
            return result;
        }

        private Mat? ExtractMatFromBitmap(IntPtr mfcDC, IntPtr saveBitMap, int width, int height, ref GCHandle handle)
        {
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width; bmi.bmiHeader.biHeight = -height;
            bmi.bmiHeader.biPlanes = 1; bmi.bmiHeader.biBitCount = 32; bmi.bmiHeader.biCompression = 0;

            int need = width * height * 4;
            byte[] pixels = (_pixScratch != null && _pixScratch.Length == need)
                ? _pixScratch : (_pixScratch = new byte[need]);

            int res = GetDIBits(mfcDC, saveBitMap, 0, (uint)height, pixels, ref bmi, 0);

            if (res > 0)
            {
                handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                IntPtr ptr = handle.AddrOfPinnedObject();

                using (var tempMat = Mat.FromPixelData(height, width, MatType.CV_8UC4, ptr, width * 4))
                {
                    var result = new Mat();
                    Cv2.CvtColor(tempMat, result, ColorConversionCodes.BGRA2BGR);
                    return result;
                }
            }
            return null;
        }
    }
}