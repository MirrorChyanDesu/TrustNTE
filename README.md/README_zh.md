\# TrustNTE (简体中文)



\*\*🌐 Language:\*\* \[🇨🇳 简体中文](README\_zh.md) | \[🇺🇸 English](README\_en.md) | \[🇯🇵 日本語](README\_ja.md) | \[🇰🇷 한국어](README\_ko.md)



TrustNTE 是一款基于 WPF 和 OpenCV 开发的高级游戏自动化辅助工具（针对《异环》）。本项目采用了现代化的多线程架构、计算机视觉识别、OpenVINO 深度学习推理以及任务调度系统，旨在提供极致丝滑、稳定、安全的挂机体验。



\## ✨ 核心功能

\*   \*\*视觉识别\*\*：基于 OpenCV 模板匹配与颜色空间（HSV/BGR）分析。

\*   \*\*深度学习\*\*：集成 OpenVINO YOLO 模型与 RapidOCR 文字识别引擎。

\*   \*\*自动化任务\*\*：支持自动钓鱼、自动音游（4线程极速响应）、自动战斗、剧情跳过等。

\*   \*\*脚本调度\*\*：内置 V8 JavaScript 引擎，支持脚本与 Shell 命令混合调度。

\*   \*\*安全守护\*\*：全局卡键释放、掉线自动重连、崩溃自动重启、多任务强制互斥防止输入冲突。



\## 🛠️ 技术栈与 NuGet 依赖

本项目基于 \*\*.NET 8.0 (WPF)\*\* 开发，核心依赖以下 NuGet 包：



\### UI 与 MVVM

\*   `Wpf.Ui` (v3.x) - Fluent Design 现代化 UI 控件库。

\*   `CommunityToolkit.Mvvm` - 官方 MVVM 框架，用于数据绑定与命令实现。



\### 计算机视觉与深度学习

\*   `OpenCvSharp4` 及 `OpenCvSharp4.runtime.win` - OpenCV 的 C# 包装，用于图像处理。

\*   `Sdcb.OpenVINO` - 用于运行战斗 YOLO 目标检测模型。

\*   `RapidOCRSharpOnnx` - 高性能的轻量级 OCR 文字识别库。



\### 脚本与系统交互

\*   `Microsoft.ClearScript.V8` - 用于执行用户自定义的 JS 调度脚本。

\*   `Gma.System.MouseKeyHook` - 全局键鼠热键与宏录制监听。

\*   `Microsoft.Extensions.DependencyInjection` - 依赖注入容器。

\*   `Microsoft.Extensions.Http` - 用于网络请求（更新、兑换码）。



\## 🚀 编译与运行指南



\### 环境要求

\*   Windows 10/11 (64位)

\*   Visual Studio 2022

\*   .NET 8.0 SDK

\*   \*\*必须以管理员身份运行\*\*（用于全局按键注入与系统级 API 调用）。



\### 目录结构与资源

由于开源仓库不包含所有大体积模型，请确保以下目录存在正确文件：

\*   `Assets/NTE-CombatYOLO/`：YOLO 模型文件（.xml, .bin）。

\*   `Assets/NTE-Ocr\_Models/`：OCR 模型文件（det.onnx, rec.onnx 及字典）。

\*   `Assets/Nte\_Dll/NteRenderCore.dll`：底层渲染同步 C++ 库。



\### 编译

1\. 克隆仓库。

2\. 使用 Visual Studio 打开 `TrustNTE.slnx`。

3\. 还原 NuGet 包并生成解决方案。

4\. 编译生成 `TrustNTE.exe` 即可运行。



\## 📝 注意事项

\*   请确保杀毒软件或 Windows Defender 未拦截本程序，某些注入机制可能会被误报。

\*   在执行开机自启或全局按键时，UAC 权限提示是正常现象。

