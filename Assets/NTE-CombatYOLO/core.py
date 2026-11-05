import win32gui
import numpy as np
import cv2
from ultralytics import YOLO
import ctypes
import win32ui
from PIL import Image

# 尝试导入高级截图库
try:
    import dxcam
    dxcam_available = True
except ImportError:
    dxcam_available = False

try:
    from windows_capture import WindowsCapture, Frame, InternalCaptureControl
    wgc_available = True
except ImportError:
    wgc_available = False


class GameVisionCore:
    def __init__(self, model_path="Nte_RecognizeEnemy.pt"):
        self.model = YOLO(model_path)
        self.hwnd = None
        self.capture_mode = 0  # 0: PrintWindow, 1: DXcam, 2: WGC
        
        self.dxcam_cam = None
        self.wgc_capture = None
        self.wgc_latest_frame = None
        
        # 自动寻找游戏窗口
        def callback(hwnd, extra):
            if win32gui.IsWindowVisible(hwnd):
                title = win32gui.GetWindowText(hwnd)
                cls = win32gui.GetClassName(hwnd)
                if ("异环" in title or "UnrealWindow" in cls) and win32gui.GetClientRect(hwnd)[2] > 100:
                    self.hwnd = hwnd
        
        win32gui.EnumWindows(callback, None)
        if not self.hwnd:
            raise Exception("找不到游戏窗口！请确认游戏是否已打开。")

    def set_capture_mode(self, mode):
        """切换截图引擎"""
        self.capture_mode = mode
        # 切换时清理旧的相机资源，防止冲突
        if self.dxcam_cam:
            self.dxcam_cam.stop()
            self.dxcam_cam = None
        if self.wgc_capture:
            self.wgc_capture = None
            self.wgc_latest_frame = None

    # ---------------- 截图引擎 1: PrintWindow (传统) ----------------
    def _capture_printwindow(self):
        rect = win32gui.GetClientRect(self.hwnd)
        width, height = rect[2], rect[3]
        try:
            hwnd_dc = win32gui.GetWindowDC(self.hwnd)
            mfc_dc = win32ui.CreateDCFromHandle(hwnd_dc)
            save_dc = mfc_dc.CreateCompatibleDC()
            bitmap = win32ui.CreateBitmap()
            bitmap.CreateCompatibleBitmap(mfc_dc, width, height)
            save_dc.SelectObject(bitmap)

            ctypes.windll.user32.PrintWindow(self.hwnd, save_dc.GetSafeHdc(), 3)
            bits = bitmap.GetBitmapBits(True)
            img = Image.frombuffer("RGB", (width, height), bits, "raw", "BGRX", 0, 1)
            frame = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)

            win32gui.DeleteObject(bitmap.GetHandle())
            save_dc.DeleteDC()
            mfc_dc.DeleteDC()
            win32gui.ReleaseDC(self.hwnd, hwnd_dc)
            return frame
        except:
            return None

    # ---------------- 截图引擎 2: DXcam (极速) ----------------
    def _capture_dxcam(self):
        if not dxcam_available: return self._capture_printwindow()
        try:
            if not self.dxcam_cam:
                rect = win32gui.GetClientRect(self.hwnd)
                pt = win32gui.ClientToScreen(self.hwnd, (0, 0))
                region = (pt[0], pt[1], pt[0] + rect[2], pt[1] + rect[3])
                self.dxcam_cam = dxcam.create(output_color="BGR", region=region)
                self.dxcam_cam.start(target_fps=60)
            
            frame = self.dxcam_cam.get_latest_frame()
            if frame is None: return self._capture_printwindow()
            return frame
        except:
            return self._capture_printwindow()

    # ---------------- 截图引擎 3: WGC (真后台) ----------------
    def _capture_wgc(self):
        if not wgc_available: return self._capture_printwindow()
        try:
            if not self.wgc_capture:
                self.wgc_capture = WindowsCapture(cursor_capture=False, draw_border=False, window_hwnd=self.hwnd)
                
                @self.wgc_capture.event
                def on_frame_arrived(frame: Frame, capture_control: InternalCaptureControl):
                    arr = np.array(frame.frame_buffer)
                    if arr.shape[2] == 4:
                        arr = cv2.cvtColor(arr, cv2.COLOR_BGRA2BGR)
                    self.wgc_latest_frame = arr
                
                self.wgc_capture.start_free_threaded()
                # 给捕捉线程一点启动时间
                import time
                time.sleep(0.5)
                
            if self.wgc_latest_frame is None:
                return self._capture_printwindow()
            return self.wgc_latest_frame
        except:
            return self._capture_printwindow()

    # ---------------- 统一分发和识别 ----------------
    def capture_and_detect(self):
        if self.capture_mode == 1:
            frame = self._capture_dxcam()
        elif self.capture_mode == 2:
            frame = self._capture_wgc()
        else:
            frame = self._capture_printwindow()

        if frame is None:
            return np.zeros((720, 1280, 3), dtype=np.uint8)

        # YOLO 推理
        results = self.model(frame, device=0, conf=0.25, verbose=False)

        annotated_frame = frame.copy()
        for r in results:
            boxes = r.boxes
            for box in boxes:
                x1, y1, x2, y2 = map(int, box.xyxy[0])
                conf = float(box.conf[0])
                label = f"LV {conf:.2f}"
                cv2.rectangle(annotated_frame, (x1, y1), (x2, y2), (0, 0, 255), 2)
                cv2.putText(annotated_frame, label, (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 255), 2)

        return annotated_frame