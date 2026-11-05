# TrustNTE (한국어)

**🌐 Language:** [🇨🇳 简体中文](README_zh.md) | [🇺🇸 English](README_en.md) | [🇯🇵 日本語](README_ja.md) | [🇰🇷 한국어](README_ko.md)

TrustNTE는 WPF와 OpenCV를 기반으로 개발된 고급 게임 자동화 도구(NTE 타겟)입니다. 최신 멀티스레드 아키텍처, 컴퓨터 비전, OpenVINO 딥러닝 추론 및 작업 스케줄링 시스템을 채택하여 부드럽고 안정적이며 안전한 방치형 플레이 환경을 제공합니다.

## ✨ 핵심 기능
*   **비전 인식**: OpenCV 템플릿 매칭 및 색 공간(HSV/BGR) 분석.
*   **딥러닝**: OpenVINO YOLO 모델과 RapidOCR 텍스트 인식 엔진 통합.
*   **자동화 작업**: 자동 낚시, 자동 리듬 게임(4스레드 초고속 반응), 자동 전투, 스토리 스킵 등.
*   **스케줄러**: V8 JavaScript 엔진 내장, 스크립트 및 쉘 명령어 실행 지원.
*   **안전 보호**: 글로벌 키 해제, 연결 끊김 시 자동 재연결, 충돌 시 자동 재시작, 입력 충돌을 방지하는 엄격한 작업 상호 배제.

## 🛠️ 기술 스택 및 NuGet 종속성
**.NET 8.0 (WPF)** 기반으로 하며, 다음 핵심 NuGet 패키지에 종속됩니다:

### UI 및 MVVM
*   `Wpf.Ui` (v3.x) - Fluent Design UI 컨트롤.
*   `CommunityToolkit.Mvvm` - 데이터 바인딩을 위한 MVVM 프레임워크.

### 컴퓨터 비전 및 AI
*   `OpenCvSharp4` 및 `OpenCvSharp4.runtime.win` - OpenCV의 C# 래퍼.
*   `Sdcb.OpenVINO` - YOLO 객체 감지 모델 실행용.
*   `RapidOCRSharpOnnx` - 고성능 경량 OCR 라이브러리.

### 스크립트 및 시스템
*   `Microsoft.ClearScript.V8` - 사용자 정의 JS 스케줄러 스크립트 실행용.
*   `Gma.System.MouseKeyHook` - 글로벌 키보드/마우스 단축키 및 매크로 기록.
*   `Microsoft.Extensions.DependencyInjection` - DI 컨테이너.
*   `Microsoft.Extensions.Http` - 네트워크 요청용.

## 🚀 빌드 및 실행 가이드

### 환경 요구 사항
*   Windows 10/11 (64비트)
*   Visual Studio 2022
*   .NET 8.0 SDK
*   **관리자 권한으로 실행해야 합니다** (글로벌 입력 주입 및 시스템 API 호출용).

### 디렉토리 및 리소스
대용량 모델 파일은 저장소에 포함되어 있지 않으므로 다음 디렉토리에 파일을 배치하십시오:
*   `Assets/NTE-CombatYOLO/`: YOLO 모델 파일 (.xml, .bin).
*   `Assets/NTE-Ocr_Models/`: OCR 모델 (det.onnx, rec.onnx, 딕셔너리).
*   `Assets/Nte_Dll/NteRenderCore.dll`: C++ 렌더링 동기화 라이브러리.

### 빌드
1. 저장소를 클론합니다.
2. Visual Studio에서 `TrustNTE.slnx`를 엽니다.
3. NuGet 패키지를 복원하고 솔루션을 빌드합니다.
4. 생성된 `TrustNTE.exe`를 실행합니다.

## 📝 참고 사항
*   일부 주입 메커니즘이 오탐지될 수 있으므로 백신 프로그램의 차단을 해제하십시오.
*   자동 시작 또는 글로벌 입력 활성화 시 UAC 프롬프트가 표시되는 것은 정상입니다.