# TrustNTE (English)

**🌐 Language:** [🇨🇳 简体中文](README_zh.md) | [🇺🇸 English](README_en.md) | [🇯🇵 日本語](README_ja.md) | [🇰🇷 한국어](README_ko.md)

TrustNTE is an advanced game automation tool (targeting "NTE") built with WPF and OpenCV. It features a modern multi-threaded architecture, computer vision, OpenVINO deep learning inference, and a task scheduling system, designed to provide a smooth, stable, and safe AFK experience.

## ✨ Features
*   **Computer Vision**: Template matching and color space (HSV/BGR) analysis via OpenCV.
*   **Deep Learning**: Integrated OpenVINO YOLO model for combat and RapidOCR for text recognition.
*   **Automation**: Auto-fishing, Auto-rhythm game (4-thread instant response), auto-combat, story skipping, etc.
*   **Task Dispatcher**: Built-in V8 JavaScript engine to schedule scripts and shell commands.
*   **Safety & Daemon**: Global key-release safety, auto-reconnect on disconnect, auto-restart on crash, and strict task mutual exclusion to prevent input conflicts.

## 🛠️ Tech Stack & NuGet Dependencies
Based on **.NET 8.0 (WPF)**, with the following core NuGet packages:

### UI & MVVM
*   `Wpf.Ui` (v3.x) - Fluent Design UI controls.
*   `CommunityToolkit.Mvvm` - Standard MVVM framework for data binding.

### Vision & AI
*   `OpenCvSharp4` & `OpenCvSharp4.runtime.win` - C# wrapper for OpenCV.
*   `Sdcb.OpenVINO` - For running the YOLO object detection model.
*   `RapidOCRSharpOnnx` - High-performance lightweight OCR library.

### Scripting & System
*   `Microsoft.ClearScript.V8` - For executing custom JS scheduler scripts.
*   `Gma.System.MouseKeyHook` - Global keyboard/mouse hotkeys and macro recording.
*   `Microsoft.Extensions.DependencyInjection` - DI container.
*   `Microsoft.Extensions.Http` - For network requests (updates, redeem codes).

## 🚀 Build & Run Guide

### Prerequisites
*   Windows 10/11 (64-bit)
*   Visual Studio 2022
*   .NET 8.0 SDK
*   **Must run as Administrator** (required for global input injection and system-level APIs).

### Directory & Assets
Ensure the following directories contain the necessary files (large models are usually not included in the repo):
*   `Assets/NTE-CombatYOLO/`: YOLO model files (.xml, .bin).
*   `Assets/NTE-Ocr_Models/`: OCR models (det.onnx, rec.onnx, dict).
*   `Assets/Nte_Dll/NteRenderCore.dll`: C++ rendering sync library.

### Build
1. Clone the repository.
2. Open `TrustNTE.slnx` in Visual Studio.
3. Restore NuGet packages and build the solution.
4. Run the generated `TrustNTE.exe`.

## 📝 Notes
*   Please ensure your antivirus or Windows Defender does not block the program, as some injection mechanisms might trigger false positives.
*   UAC prompts are normal when enabling auto-start or injecting global inputs.