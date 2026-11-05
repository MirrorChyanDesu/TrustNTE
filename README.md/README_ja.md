# TrustNTE (日本語)

**🌐 Language:** [🇨🇳 简体中文](README_zh.md) | [🇺🇸 English](README_en.md) | [🇯🇵 日本語](README_ja.md) | [🇰🇷 한국어](README_ko.md)

TrustNTE は、WPF と OpenCV を使用して開発された高度なゲーム自動化ツール（「NTE」向け）です。モダンなマルチスレッドアーキテクチャ、コンピュータビジョン、OpenVINO ディープラーニング推論、タスクスケジューリングシステムを採用しており、スムーズで安定した安全な放置プレイ環境を提供します。

## ✨ 主な機能
*   **画像認識**: OpenCV によるテンプレートマッチングと色空間 (HSV/BGR) 解析。
*   **ディープラーニング**: OpenVINO YOLO モデルと RapidOCR によるテキスト認識。
*   **自動化タスク**: 自動釣り、自動リズムゲーム（4スレッドによる超高速応答）、自動戦闘、ストーリースキップなど。
*   **タスクスケジューラー**: V8 JavaScript エンジンを内蔵し、スクリプトとシェルコマンドの実行をサポート。
*   **安全機能**: グローバルキーリリース、切断時の自動再接続、クラッシュ時の自動再起動、入力競合を防ぐ厳格なタスク排他制御。

## 🛠️ 技術スタックと NuGet 依存関係
**.NET 8.0 (WPF)** をベースにしており、以下の主要な NuGet パッケージに依存しています：

### UI / MVVM
*   `Wpf.Ui` (v3.x) - Fluent Design 対応の UI コントロール。
*   `CommunityToolkit.Mvvm` - データバインディング用の MVVM フレームワーク。

### 画像処理と AI
*   `OpenCvSharp4` ＆ `OpenCvSharp4.runtime.win` - OpenCV の C# ラッパー。
*   `Sdcb.OpenVINO` - YOLO 物体検出モデルの実行用。
*   `RapidOCRSharpOnnx` - 高速な軽量 OCR ライブラリ。

### スクリプトとシステム
*   `Microsoft.ClearScript.V8` - カスタム JS スクリプトの実行用。
*   `Gma.System.MouseKeyHook` - グローバルホットキーとマクロ記録用。
*   `Microsoft.Extensions.DependencyInjection` - 依存性注入コンテナ。
*   `Microsoft.Extensions.Http` - ネットワークリクエスト用。

## 🚀 ビルドと実行ガイド

### 動作環境
*   Windows 10/11 (64ビット)
*   Visual Studio 2022
*   .NET 8.0 SDK
*   **管理者として実行する必要があります**（グローバル入力の注入とシステム API の呼び出しに必要）。

### ディレクトリとアセット
リポジトリに大きなモデルファイルが含まれていないため、以下のファイルを配置してください：
*   `Assets/NTE-CombatYOLO/`: YOLO モデルファイル (.xml, .bin)。
*   `Assets/NTE-Ocr_Models/`: OCR モデル (det.onnx, rec.onnx, 辞書)。
*   `Assets/Nte_Dll/NteRenderCore.dll`: C++ レンダリング同期ライブラリ。

### ビルド
1. リポジトリをクローンします。
2. Visual Studio で `TrustNTE.slnx` を開きます。
3. NuGet パッケージを復元し、ソリューションをビルドします。
4. 生成された `TrustNTE.exe` を実行します。

## 📝 注意事項
*   一部のインジェクション機能が誤検知される可能性があるため、アンチウイルスソフトのブロックを解除してください。
*   自動起動やグローバル入力の有効化時に UAC プロンプトが表示されるのは正常です。