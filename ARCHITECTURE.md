# DanceUp – Cấu trúc dự án Unity

Unity 2022.3 LTS · URP · uGUI + TextMeshPro · Android (API 28+). Toàn bộ code/asset của team nằm trong `Assets/_Project/`.

```
Assets/_Project/
├── Art/            Icons, Logos, Fonts, Sprites, Animations, Materials
├── Audio/          Music, SFX
├── Prefabs/        UI, Skeleton (overlay xương), Common
├── Scenes/         Boot, Main, Practice
├── ScriptableObjects/  Songs, Themes
├── Plugins/Android/    cầu nối MediaPipe NDK
├── StreamingAssets/Songs/  video mẫu H.264 720p@30
├── Scripts/
│   ├── Core/        bootstrap, event bus, service locator, enum dùng chung
│   ├── Data/        model dữ liệu (pose, bài nhảy, điểm, profile), lưu cục bộ
│   ├── AI/          pose estimation, góc khớp, filter, DTW, chấm điểm
│   ├── CameraInput/ camera
│   ├── Network/     gọi backend FastAPI
│   ├── UI/          screen manager, theme, đổi chế độ
│   └── Features/    Onboarding, SongSelect, Practice, Result, Elderly, Reels, Classroom, Settings
└── Tests/          EditMode, PlayMode
```

## Ánh xạ plan → thư mục

| Plan | Thư mục |
|---|---|
| FLOW-01 Onboarding | `Scripts/Features/Onboarding` |
| FLOW-02 Chọn bài | `Scripts/Features/SongSelect` |
| FLOW-03 Nhảy + chấm live | `Scripts/Features/Practice`, `AI`, `CameraInput` |
| FLOW-04 Tổng kết | `Scripts/Features/Result` |
| FLOW-05/06 Elderly | `Scripts/Features/Elderly`, `UI` |
| FLOW-07 Reels | `Scripts/Features/Reels` |
| FLOW-08 Classroom | `Scripts/Features/Classroom`, `Network` |
| FLOW-09 Đổi chế độ | `Scripts/UI`, `Scripts/Core` |
| TECH-01/06 MediaPipe + bridge | `Scripts/AI`, `Plugins/Android` |
| TECH-07 Lưu cục bộ | `Scripts/Data` |
| TECH-09 Backend | `Scripts/Network` |

## Gợi ý chia assembly (khi bắt đầu code)

`Core` ← `Data` ← `AI`; `CameraInput`, `Network`, `UI` phụ thuộc `Core`/`Data`; `Features` dùng tất cả. Module nền không tham chiếu `Features`.

## Quy ước

- Namespace theo thư mục: `DanceUp.Core`, `DanceUp.AI`, `DanceUp.Features.Practice`...
- Git: GitFlow (`main`, `develop`, `feature/*`); video/audio/model đã cấu hình Git LFS trong `.gitattributes`.
