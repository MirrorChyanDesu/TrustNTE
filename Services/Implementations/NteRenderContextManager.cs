using OpenCvSharp;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TrustNTE.Services
{
    /// <summary>
    /// 异环（NTE）渲染上下文管理器。
    /// </summary>
    public sealed class NteRenderContextManager
    {
        public const int NteBaseWidth = 1920;
        public const int NteBaseHeight = 1080;

        public int NteCurrentWidth { get; private set; } = NteBaseWidth;
        public int NteCurrentHeight { get; private set; } = NteBaseHeight;

        public bool IsNteStandard => NteCurrentWidth == NteBaseWidth && NteCurrentHeight == NteBaseHeight;
        public bool IsNteAbnormalRatio => Math.Abs((double)NteCurrentWidth / NteCurrentHeight - 16.0 / 9.0) > 0.01;

        static NteRenderContextManager()
        {
            NativeLibrary.SetDllImportResolver(typeof(NteRenderContextManager).Assembly, ResolveNativeLibrary);
        }

        private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName == "NteRenderCore.dll")
            {
                string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Nte_Dll", "NteRenderCore.dll");
                if (File.Exists(dllPath) && NativeLibrary.TryLoad(dllPath, out IntPtr handle))
                {
                    return handle;
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("NteRenderCore.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NteProcessRenderBuffer(
            IntPtr src, int srcW, int srcH,
            IntPtr dst, int dstW, int dstH, int channels);

        public void UpdateNteContext(int w, int h)
        {
            NteCurrentWidth = w;
            NteCurrentHeight = h;
        }

        public Mat SyncNteRenderFrame(Mat frame)
        {
            if (frame.Width == NteBaseWidth && frame.Height == NteBaseHeight)
                return frame.Clone();

            try
            {
                int channels = frame.Channels();
                Mat srcMat = frame;

                if (!frame.IsContinuous()) srcMat = frame.Clone();

                var syncedFrame = new Mat(NteBaseHeight, NteBaseWidth, srcMat.Type());
                NteProcessRenderBuffer(srcMat.Data, srcMat.Width, srcMat.Height, syncedFrame.Data, NteBaseWidth, NteBaseHeight, channels);

                if (srcMat != frame) srcMat.Dispose();

                return syncedFrame;
            }
            catch (DllNotFoundException)
            {
                // C++ 核心 DLL 缺失，退化为 C# OpenCV 双线性缩放
                var resized = new Mat();
                Cv2.Resize(frame, resized, new OpenCvSharp.Size(NteBaseWidth, NteBaseHeight));
                return resized;
            }
            catch (Exception)
            {
                return frame.Clone();
            }
        }

        public (int Px, int Py) TranslateNtePointer(int x, int y)
        {
            int px = (int)Math.Round(x * (double)NteCurrentWidth / NteBaseWidth);
            int py = (int)Math.Round(y * (double)NteCurrentHeight / NteBaseHeight);
            return (Math.Clamp(px, 0, NteCurrentWidth - 1), Math.Clamp(py, 0, NteCurrentHeight - 1));
        }
    }
}