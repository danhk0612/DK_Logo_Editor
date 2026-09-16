# ARCHITECTURE

## 기술 스택

- .NET 8
- WPF
- Windows desktop
- OpenRouter HTTP API
- 로컬 이미지 처리 라이브러리: ImageSharp 계열 우선 검토

## 핵심 설계

최종 결과를 AI가 통째로 다시 그리게 하지 않는다.

```text
Original Image
    │
    ├─ Background Analysis ─────────────┐
    │                                  │
    └─ Protected Logo Extraction       │
                                       ▼
                              Editable Background
                                       │
                                       ├─ OpenRouter analysis/generation
                                       │      └─ subtitle placement/style
                                       │
                                       ▼
                              Local Composition
                                  ├─ background
                                  ├─ subtitle layer
                                  └─ protected original logo
                                       │
                                       ▼
                                Exact Output Size
```

## 레이어 규칙

### Protected Logo Layer

원본 로고의 비배경 영역에서 추출한다.

허용:

- 전체 크기 변경
- 캔버스 내 위치 이동

금지:

- 내부 픽셀의 생성형 수정
- 색상 재해석
- 글자/심볼 재생성
- 형태 변경

### Background Layer

- 지정 색상 또는 완전 투명
- 보호 로고 레이어와 겹치는 픽셀을 수정하지 않는다.

### Subtitle Layer

- 부기명이 있을 때만 존재한다.
- OpenRouter의 이미지 이해/생성 결과를 이용해 위치와 시각적 구성을 결정한다.
- 원본 로고를 침범하거나 가리지 않도록 배치한다.

## 주요 모듈

```text
DKLogoEditor
├─ UI
│  ├─ MainWindow
│  └─ SettingsWindow
├─ Services
│  ├─ OpenRouterClient
│  ├─ ModelCatalogService
│  ├─ LogoAnalysisService
│  └─ LogoEditService
├─ Imaging
│  ├─ BackgroundDetector
│  ├─ LogoMaskBuilder
│  ├─ LogoLayerExtractor
│  ├─ CanvasLayoutEngine
│  ├─ ImageComposer
│  └─ ImageResizer
├─ Models
│  ├─ AppSettings
│  ├─ ImageModelPreset
│  ├─ EditRequest
│  └─ EditResult
└─ Storage
   └─ SettingsStore
```

## OpenRouter 역할

OpenRouter는 생성형/AI 판단이 필요한 부분에만 사용한다.

예:

- 원본 로고의 시각적 특성 분석
- 부기명의 적절한 위치 제안
- 부기명의 크기/정렬/간격/스타일 결정
- 필요한 경우 부기명 그래픽 생성

정확한 출력 크기, 투명도, 마스크, 원본 보호 합성은 로컬 코드가 책임진다.

## 출력 크기 처리

기본 출력은 200 × 60 px이다.

AI 입력 또는 중간 작업 이미지는 더 큰 해상도를 사용할 수 있으며, 최종 단계에서 지정 크기로 변환한다.

원본 로고는 부기명 공간 확보를 위해 출력 캔버스 안에서 축소/이동할 수 있다. 이때 종횡비를 유지한다.

## 설정 저장

저장 대상:

- OpenRouter API Key
- 기본 모델
- 커스텀 모델 목록

설정 저장 방식의 구체 구현은 T02 단계에서 결정한다.

## UI 원칙

### MainWindow

작업 흐름 외 설정을 최소화한다.

- 이미지 선택
- 배경
- 부기명
- 출력 크기
- 모델
- 실행
- 결과 저장
- 설정 진입

### SettingsWindow

- API Key
- 기본 모델
- 커스텀 모델 관리

## 실패 방지 원칙

AI 출력은 원본 로고의 신뢰 가능한 소스로 사용하지 않는다.

최종 로고 본체는 반드시 원본에서 추출한 보호 레이어를 사용한다. 이를 통해 AI가 로고를 미세하게 바꾸는 문제를 구조적으로 차단한다.
