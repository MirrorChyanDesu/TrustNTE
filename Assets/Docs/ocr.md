# 📄 OCR 模型说明

本项目使用 **RapidOCRSharpOnnx + PP-OCRv6 tiny** 模型。

### 📂 模型文件位置

程序目录 `Assets/NTE-Ocr_Models/`：

- `det.onnx`（文字检测模型）
- `rec.onnx`（文字识别模型）
- `ppocrv6_tiny_dict.txt`（识别字典）

### 🔧 替换模型

将以上三个文件替换为目标版本后，重启软件即可生效。

### 🎯 UID 测试

点击【测试区域】按钮，系统会截取左下角 UID 区域（3,1051，宽高 193x27）执行 OCR。

> **注意**：请确保游戏分辨率 1920x1080，且 UID 未被任何 UI 遮挡。
