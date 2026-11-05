import sys
import cv2
from PySide6.QtWidgets import QApplication, QMainWindow, QLabel, QVBoxLayout, QHBoxLayout, QWidget, QPushButton, QComboBox
from PySide6.QtCore import QTimer, Qt
from PySide6.QtGui import QImage, QPixmap
from core import GameVisionCore


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("异环 - LV识别助手")
        self.resize(1280, 720)

        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        layout = QVBoxLayout(central_widget)

        # ================= 顶部控制栏 =================
        top_layout = QHBoxLayout()
        
        # 截图引擎下拉框
        self.combo_capture = QComboBox()
        self.combo_capture.addItem("PrintWindow (传统,可能黑屏)", 0)
        self.combo_capture.addItem("DXcam (极速前台)", 1)
        self.combo_capture.addItem("WGC (真后台抓取)", 2)
        self.combo_capture.currentIndexChanged.connect(self.on_capture_mode_changed)
        top_layout.addWidget(QLabel("截图引擎:"))
        top_layout.addWidget(self.combo_capture)

        top_layout.addStretch() # 占位符

        # 开始按钮
        self.start_btn = QPushButton("开始识别")
        self.start_btn.clicked.connect(self.toggle_detection)
        top_layout.addWidget(self.start_btn)

        layout.addLayout(top_layout)

        # ================= 图像显示区 =================
        self.image_label = QLabel("等待启动...")
        self.image_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(self.image_label)

        # ================= 初始化 Core =================
        try:
            self.core = GameVisionCore(model_path="Nte_RecognizeEnemy.pt")
        except Exception as e:
            self.image_label.setText(str(e))
            self.core = None

        self.is_running = False
        self.timer = QTimer()
        self.timer.timeout.connect(self.update_frame)

    def on_capture_mode_changed(self):
        """下拉框切换事件"""
        if self.core:
            mode = self.combo_capture.currentData()
            self.core.set_capture_mode(mode)
            print(f"已切换截图引擎为: {self.combo_capture.currentText()}")

    def toggle_detection(self):
        if not self.core:
            return

        if self.is_running:
            self.timer.stop()
            self.start_btn.setText("开始识别")
            self.is_running = False
        else:
            self.timer.start(30)  # ~33 FPS
            self.start_btn.setText("停止识别")
            self.is_running = True

    def update_frame(self):
        try:
            frame = self.core.capture_and_detect()
            rgb_image = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            h, w, ch = rgb_image.shape
            bytes_per_line = ch * w
            qt_image = QImage(rgb_image.data, w, h, bytes_per_line, QImage.Format_RGB888)
            self.image_label.setPixmap(QPixmap.fromImage(qt_image).scaled(
                self.image_label.width(), self.image_label.height(), Qt.KeepAspectRatio))
        except Exception as e:
            print(f"UI更新错误: {e}")
            self.timer.stop()


if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec())