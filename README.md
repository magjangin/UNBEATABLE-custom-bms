# UNBEATABLE Custom BMS Loader & Converter

리듬 게임 **UNBEATABLE** (by D-CELL GAMES)에서 BMS 포맷(`.bms`, `.bme`, `.bml`)의 커스텀 채보를 편리하게 변환하고 플레이할 수 있도록 지원하는 MelonLoader 모드 및 독립형 CLI 변환 툴입니다.

---

## 🚀 주요 기능

- **자동 BMS 변환 및 주입 (MelonLoader 모드)**
  - 게임 실행 시 `<GameRoot>/hwa/<곡이름>/` 폴더 내에 있는 BMS 채보와 음원 파일을 탐색합니다.
  - 탐색된 파일을 UNBEATABLE 게임의 자체 커스텀 곡 규격(`CustomSongs`)으로 실시간 자동 변환하여 로컬 경로(`Application.persistentDataPath/CustomSongs`)에 저장 및 로드합니다.
- **스마트 Harmony 패치 적용**
  - 스팀 실행 옵션에 별도로 `-customsongs` 인수를 추가하지 않아도 인게임에서 커스텀 곡 메뉴가 항상 활성화되도록 강제 패치합니다.
- **수록곡 및 컨텐츠 자동 해금**
  - 모드 실행 시 모든 기본 수록곡, 디럭스 에디션 곡 및 메뉴 팔레트가 자동으로 잠금 해제되어 편리한 플레이 환경을 제공합니다.
- **BGA 및 커버 이미지 지원**
  - 곡 폴더 내에 `cover.png` 또는 `video.mp4` / `video.webm` 파일이 존재하는 경우, 변환 시 자동으로 함께 복사되어 인게임 플레이 및 곡 선택 화면에서 연동됩니다.
- **독립형 CLI 변환 프로그램 (`BmsToUnbeatable`)**
  - MelonLoader 모드 없이도 명령 프롬프트(CMD)를 통해 개별 BMS 채보를 직접 게임 내 커스텀 곡 폴더로 변환할 수 있습니다.

---

## 🛠️ BMS 채보 작성 및 매핑 사양

BMS 채보 작성 시 아래의 채널 매핑 및 `#WAV` 명명 규칙을 준수하여 노트를 구성해야 정상적으로 변환됩니다.

### 1. 채널 매핑 (Lanes)

| 채널 (Channel) | 방향 (Side) | 높이 (Height) | 게임 내 위치 |
| :--- | :--- | :--- | :--- |
| **`16`** | Right (우측) | Top (상단) | 우측 위쪽 라인 |
| **`11`** | Right (우측) | Mid (중단) | 우측 중간 라인 |
| **`12`** | Right (우측) | Low (하단) | 우측 아래쪽 라인 |
| **`13`** | Left (좌측) | Top (상단) | 좌측 위쪽 라인 |
| **`14`** | Left (좌측) | Mid (중단) | 좌측 중간 라인 |
| **`15`** | Left (좌측) | Low (하단) | 좌측 아래쪽 라인 |
| **`18`** | - | - | 카메라 방향 전환 및 연출 이벤트 (아래 참고) |

> [!NOTE]
> **채널 18 (방향 전환 이벤트)**
> - `#WAVxxx` 파일명이 **`Flip`** 인 경우: 캐릭터의 실제 방향이 전환됩니다. (플레이어 사이드가 실제로 변경됨)
> - `#WAVxxx` 파일명이 **`Nothing`** 인 경우: 카메라가 중앙으로 정렬되지만, 캐릭터의 플레이 방향(사이드)은 전환되지 않습니다.

---

### 2. WAV 파일명을 통한 노트 타입 인코딩

BMS 내의 `#WAVxx` 정의 시 오디오 파일명(확장자 제외)을 아래 정의된 이름으로 지정하여 노트 종류를 나타냅니다.

| 파일명 키워드 | 노트 타입 | 설명 |
| :--- | :--- | :--- |
| **`Default`** | 일반 노트 | 기본 타격 단일 노트 |
| **`Dodge`** | 회피 노트 | 휘슬/회피 기믹 노트 |
| **`Setpiece`** | 피니시 노트 | 세트피스(강한 타격) 노트 |
| **`Hold`** | 홀드 노트 | 롱노트 시작 및 끝 (동일 레인에서 쌍으로 나타나 시작-끝 연결) |
| **`Double`** | 더블 홀드 노트 | 더블 롱노트 |
| **`Spam`** | 연타 노트 | 연타형 노트 (Mid 라인 전용) |
| **`Freestyle`** | 프리스타일 노트 | 프리스타일 노트 (Mid 라인 전용) |
| **`Brawl`** | 브롤 노트 | 브롤 판정 노트 |

---

## 💻 사용 방법

### 1. MelonLoader 모드 사용법

1. 빌드된 `UNBEATABLE custom mode.dll` 파일을 게임 루트의 `Mods` 폴더에 넣습니다.
2. 게임 루트 디렉토리에 **`hwa`** 폴더를 생성합니다.
3. `hwa` 폴더 아래에 곡별로 하위 폴더를 만들고, 다음과 같이 리소스를 배치합니다:
   ```text
   hwa/
     └── 곡폴더이름/
           ├── chart.bms (또는 .bme, .bml)
           ├── music.ogg (또는 .mp3, .wav)
           ├── cover.png (곡 커버 이미지 - 선택사항)
           └── video.mp4 (배경 동영상 BGA - 선택사항)
   ```
4. 게임을 실행하면 실행 로그와 함께 변환 및 로딩이 자동으로 완료됩니다!

---

### 2. CLI 변환기 (`BmsToUnbeatable`) 사용법

명령 프롬프트(CMD) 또는 터미널을 열어 아래 형식으로 커맨드를 실행하여 직접 변환할 수도 있습니다.

```bash
BmsToUnbeatable <chart.bms> <audioFile> <songFolderName> [--out <customsongsRoot>] [--difficulty Hard] [--creator name]
```

- **필수 인수:**
  - `<chart.bms>`: 원본 BMS 파일의 경로.
  - `<audioFile>`: 채보에 사용할 전체 음원 파일의 경로.
  - `<songFolderName>`: 생성될 게임 내 커스텀 곡 폴더 이름.
- **선택 인수:**
  - `--out`: 커스텀 곡 루트 디렉토리를 재지정합니다. (기본값: `%UserProfile%\AppData\LocalLow\D-CELL GAMES\UNBEATABLE\customsongs`)
  - `--difficulty`: 변환 시 난이도 이름을 설정합니다. (`Beginner`, `Easy`, `Normal`, `Hard`, `Expert`)
  - `--creator`: 커스터마이저 이름을 설정합니다. (지정하지 않을 경우 BMS의 Artist가 적용됨)
