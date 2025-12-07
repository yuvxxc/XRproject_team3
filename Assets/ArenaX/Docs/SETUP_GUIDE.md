# Arena X - Unity 설정 가이드

이 문서는 Arena X 프로젝트의 Lua 스크립트를 Unity에서 설정하는 방법을 안내합니다.

---

## 목차

1. [프로젝트 구조 개요](#1-프로젝트-구조-개요)
2. [씬 설정](#2-씬-설정)
3. [매니저 오브젝트 설정](#3-매니저-오브젝트-설정)
4. [좌석 프리팹 제작](#4-좌석-프리팹-제작)
5. [미니맵 UI 제작](#5-미니맵-ui-제작)
6. [관객 프리팹 제작](#6-관객-프리팹-제작)
7. [테스트 및 확인](#7-테스트-및-확인)

---

## 1. 프로젝트 구조 개요

### 생성된 파일 목록

```
Assets/ArenaX/
├── Docs/
│   ├── ARCHITECTURE.md      # 시스템 설계서
│   ├── PROGRESS.md          # 진행 체크리스트
│   └── SETUP_GUIDE.md       # 이 문서
│
├── Scripts/
│   ├── Manager/
│   │   └── ArenaXManager.lua    # 핵심 매니저
│   ├── Seat/
│   │   └── SeatController.lua   # 좌석 컨트롤러
│   ├── UI/
│   │   └── SeatUIManager.lua    # UI 매니저
│   ├── Avatar/
│   │   └── AudienceManager.lua  # 관객 매니저
│   └── Utils/
│       └── def.lua              # 타입 정의
│
├── Prefabs/                 # [제작 필요]
│   ├── Seats/
│   ├── UI/
│   └── Avatars/
│
└── Scenes/                  # [제작 필요]
```

### 스크립트 역할

| 스크립트 | 역할 |
|----------|------|
| **ArenaXManager** | 전체 시스템 조율, 좌석 데이터 관리, 이벤트 허브 |
| **SeatController** | 개별 좌석의 착석/이탈 처리 |
| **SeatUIManager** | 미니맵 UI, 좌석 선택, 관객 토글 버튼 |
| **AudienceManager** | 가상 관객 생성/제거 (Object Pooling) |

---

## 2. 씬 설정

### 2.1 새 씬 생성

1. `File > New Scene` 또는 기존 씬 사용
2. 씬 저장: `Assets/ArenaX/Scenes/ArenaX_Main.unity`

### 2.2 기본 환경 확인

씬에 다음이 있는지 확인:
- ✅ XR Origin (VR 플레이어)
- ✅ Main Camera
- ✅ 공연장 환경 (무대, 바닥 등)

---

## 3. 매니저 오브젝트 설정

### 3.1 ArenaXManager 설정

1. **빈 GameObject 생성**: `Create Empty` → 이름: `ArenaXManager`

2. **컴포넌트 추가**:
   - `VObject` (네트워크 동기화)
   - `VivenLuaBehaviour`

3. **VivenLuaBehaviour 설정**:
   - Script: `Assets/ArenaX/Scripts/Manager/ArenaXManager.lua` 드래그

4. **Injection 설정** (Inspector):
   ```
   SeatUIManagerObject: (나중에 연결)
   AudienceManagerObject: (나중에 연결)
   UseFadeEffect: ✓ (체크)
   ```

### 3.2 SeatUIManager 설정

1. **빈 GameObject 생성**: 이름: `SeatUIManager`

2. **컴포넌트 추가**:
   - `VivenLuaBehaviour`

3. **VivenLuaBehaviour 설정**:
   - Script: `Assets/ArenaX/Scripts/UI/SeatUIManager.lua`

4. **Injection 설정**:
   ```
   ArenaXManagerObject: ArenaXManager 오브젝트 연결
   MinimapCanvas: (나중에 UI 만든 후 연결)
   SeatButtonContainer: (나중에 연결)
   SeatButtonPrefab: (나중에 연결)
   InfoPanel: (선택사항)
   CurrentSeatText: (선택사항)
   AudienceToggleButton: (선택사항)
   PlayerCamera: (자동 감지, 비워도 됨)
   UIFollowMode: "toggle"
   UIDistance: 1.5
   UIFollowSpeed: 5.0
   UIHeightOffset: -0.3
   ```

### 3.3 AudienceManager 설정

1. **빈 GameObject 생성**: 이름: `AudienceManager`

2. **컴포넌트 추가**:
   - `VivenLuaBehaviour`

3. **VivenLuaBehaviour 설정**:
   - Script: `Assets/ArenaX/Scripts/Avatar/AudienceManager.lua`

4. **Injection 설정**:
   ```
   ArenaXManagerObject: ArenaXManager 오브젝트 연결
   AudiencePrefab: (나중에 관객 프리팹 연결)
   AudienceContainer: (선택사항, 정리용 빈 오브젝트)
   MaxVisibleAudience: 15
   FrontRowsToFill: 2
   ```

### 3.4 매니저 간 연결

모든 매니저 생성 후, ArenaXManager의 Injection에서:
- `SeatUIManagerObject` → SeatUIManager 오브젝트
- `AudienceManagerObject` → AudienceManager 오브젝트

---

## 4. 좌석 프리팹 제작

> **중요**: VivenSittable에는 착석/이탈 이벤트가 없습니다.
> Trigger Collider를 사용하여 플레이어 착석을 감지합니다.

### 4.1 좌석 프리팹 구조

```
Seat_Normal (부모)
├── VObject
├── VivenSittable (sitPoint → SitDetector)
├── 의자 모델 (MeshRenderer)
│
└── SitDetector (자식)
    ├── Box Collider (Is Trigger ✓)
    └── VivenLuaBehaviour + SeatController.lua
```

### 4.2 부모 오브젝트 설정 (Seat_Normal)

1. **빈 GameObject 생성**: 이름: `Seat_Normal`

2. **의자 모델 추가** (Cube로 대체 가능)

3. **컴포넌트 추가**:
   - `VObject` (필수)
   - `VivenSittable` (필수)

4. **VObject 설정**:
   ```
   Content Type: Prepared
   Object Sync Type: Continuous
   ```

### 4.3 SitDetector 설정 (자식 오브젝트)

1. **자식 GameObject 생성**: 이름: `SitDetector`
   - 위치: 좌석 위 (플레이어가 앉을 위치)

2. **컴포넌트 추가**:
   - `Box Collider`
   - `VivenLuaBehaviour`

3. **Box Collider 설정**:
   ```
   Is Trigger: ✓ (필수!)
   Size: (0.5, 0.5, 0.5) 또는 적절한 크기
   ```

4. **VivenLuaBehaviour 설정**:
   - Script: `Assets/ArenaX/Scripts/Seat/SeatController.lua`

5. **Injection 설정**:
   ```
   ArenaXManagerObject: (비워두면 자동으로 찾음)
   ArenaXManagerName: "ArenaXManager" (자동 찾기용 이름)
   SeatRow: "A"
   SeatNumber: 1
   SeatType: "일반"
   SeatSection: "1층"
   SitDetectionDelay: 0.5
   PlayerTag: "Player"
   PlayerLayerName: "" (비워두거나 레이어 이름)
   ```

> **자동 연결**: `ArenaXManagerObject`를 비워두면 씬에서
> "ArenaXManager" 이름의 오브젝트를 `GameObject.Find`로 자동 찾습니다.
> 좌석이 많을 때 일일이 연결할 필요 없음!

### 4.4 VivenSittable 연결

1. **부모의 VivenSittable** 선택
2. **sitPoint** 필드에 `SitDetector` 오브젝트 연결

### 4.5 프리팹 저장

- `Assets/ArenaX/Prefabs/Seats/Seat_Normal.prefab`으로 저장

### 4.6 좌석 배치

```
              [무대]
                ↑
   A열: [1][2][3][4][5]  (앞줄)
   B열: [1][2][3][4][5]
   C열: [1][2][3][4][5]  (뒷줄)
```

1. 프리팹을 씬에 배치
2. 각 좌석의 `SeatRow`와 `SeatNumber` 설정
3. 모든 좌석이 무대를 바라보도록 회전

### 4.7 좌석 ID 규칙

좌석 ID는 자동으로 `{SeatRow}-{SeatNumber}` 형식으로 생성됩니다.
- 예: A열 1번 → `"A-1"`
- 예: B열 5번 → `"B-5"`

---

## 5. 좌석 선택 UI 제작

이미지 참고: 왼쪽에 미니맵, 중앙에 드롭다운 필터, 오른쪽에 좌석 버튼 그리드

### 5.1 UI Canvas 구조

```
SeatSelectionCanvas (World Space)
├── Background (Panel - 반투명 배경)
│
├── LeftPanel (미니맵 영역)
│   └── MinimapImage (RawImage - 공연장 배치도)
│
├── CenterPanel (필터 영역)
│   ├── Title (TMP_Text - "좌석 선택")
│   ├── BlockDropdown (TMP_Dropdown - Block 선택)
│   ├── DistrictDropdown (TMP_Dropdown - 층/구역 선택)
│   ├── SelectedSeatText (TMP_Text - 선택된 좌석 정보)
│   └── SelectButton (Button - 선택 확정)
│
└── RightPanel (좌석 버튼 영역)
    └── SeatButtonGrid (Grid Layout Group)
        └── (동적 생성되는 좌석 버튼들)
```

### 5.2 Canvas 설정

1. **Canvas 생성**: `UI > Canvas`
   - 이름: `SeatSelectionCanvas`

2. **Canvas 설정**:
   ```
   Render Mode: World Space
   Width: 1200
   Height: 800
   Scale: 0.001, 0.001, 0.001
   ```

3. **컴포넌트 추가**:
   - `VivenCanvasSetting` (VR UI용)

### 5.3 드롭다운 설정

1. **Block 드롭다운**: `UI > Dropdown - TextMeshPro`
   - 이름: `BlockDropdown`
   - Options: 스크립트에서 자동 설정됨 (All, A, B, C, D)

2. **District 드롭다운**: `UI > Dropdown - TextMeshPro`
   - 이름: `DistrictDropdown`
   - Options: 스크립트에서 자동 설정됨 (All, 1층, 2층, VIP)

### 5.4 좌석 버튼 그리드

1. **Panel 생성**: 이름: `SeatButtonGrid`

2. **Grid Layout Group 추가**:
   ```
   Cell Size: 80, 60
   Spacing: 10, 10
   Start Corner: Upper Left
   Start Axis: Horizontal
   Child Alignment: Upper Left
   Constraint: Fixed Column Count
   Constraint Count: 5
   ```

3. **Content Size Fitter 추가** (선택사항):
   ```
   Vertical Fit: Preferred Size
   ```

### 5.5 좌석 버튼 프리팹

1. **Button 생성**: `UI > Button - TextMeshPro`
   - 이름: `SeatButton`
   - Size: 80 x 60

2. **버튼 텍스트**:
   - Font Size: 14
   - Alignment: Center

3. **프리팹 저장**: `Assets/ArenaX/Prefabs/UI/SeatButton.prefab`

### 5.6 SeatSelectionUI 스크립트 연결

1. **빈 GameObject 생성**: 이름: `SeatSelectionUI`

2. **VivenLuaBehaviour 추가**:
   - Script: `Assets/ArenaX/Scripts/UI/SeatSelectionUI.lua`

3. **Injection 설정**:
   ```
   ArenaXManagerObject: (비워두면 자동 찾기)
   BlockDropdown: BlockDropdown 오브젝트
   DistrictDropdown: DistrictDropdown 오브젝트
   SelectButton: SelectButton 오브젝트
   SeatButtonGrid: SeatButtonGrid 오브젝트
   SeatButtonPrefab: SeatButton.prefab
   SelectedSeatText: (선택된 좌석 표시 텍스트)
   UICanvas: SeatSelectionCanvas
   UIDistance: 1.5
   UIHeightOffset: -0.2
   ```

### 5.7 UI 호출 방법

```lua
-- 다른 스크립트에서 UI 토글
local seatSelectionUI = GameObject.Find("SeatSelectionUI"):GetLuaComponent("SeatSelectionUI")
seatSelectionUI.ToggleUI()
```

---

## 6. 관객 프리팹 제작

### 6.1 관객 프리팹 생성

1. **아바타 모델 준비** (앉은 자세)
   - 또는 Capsule로 임시 대체

2. **GameObject 생성**: 이름: `VirtualAudience`

3. **앉은 자세 설정**:
   - Animator에 앉은 자세 적용
   - 또는 정적 모델 사용

4. **프리팹으로 저장**: `Assets/ArenaX/Prefabs/Avatars/VirtualAudience.prefab`

### 6.2 AudienceManager에 연결

AudienceManager의 Injection에서:
```
AudiencePrefab: VirtualAudience.prefab
```

---

## 7. 테스트 및 확인

### 7.1 체크리스트

**매니저 연결 확인**:
- [ ] ArenaXManager → SeatUIManager, AudienceManager 연결됨
- [ ] SeatUIManager → ArenaXManager 연결됨
- [ ] AudienceManager → ArenaXManager 연결됨

**좌석 확인**:
- [ ] 모든 좌석에 VObject, VivenSittable, Collider 있음
- [ ] 모든 좌석에 SeatController.lua 연결됨
- [ ] 모든 좌석의 SeatRow, SeatNumber 올바름

**UI 확인**:
- [ ] MinimapCanvas가 World Space로 설정됨
- [ ] SeatButtonPrefab이 연결됨

**관객 확인**:
- [ ] AudiencePrefab이 연결됨

### 7.2 플레이 모드 테스트

1. **Unity 플레이 모드 시작**

2. **좌석 착석 테스트**:
   - 좌석에 접근하여 앉기
   - 콘솔에 `[SeatController] OnSit` 로그 확인

3. **UI 토글 테스트**:
   - 코드로 `seatUIManager.ToggleMinimap()` 호출
   - UI가 플레이어 앞에 나타나는지 확인

4. **관객 토글 테스트**:
   - 좌석에 앉은 상태에서 관객 토글
   - 앞 열에 관객이 나타나는지 확인

### 7.3 VR 테스트

1. Quest Link 또는 빌드 후 테스트
2. UI가 적절한 거리에 있는지 확인
3. 버튼 클릭이 잘 되는지 확인

---

## 트러블슈팅

### 문제: 좌석에 앉을 수 없음
- VivenSittable의 `sitPoint`가 설정되었는지 확인
- Collider가 있고 Is Trigger가 체크되었는지 확인

### 문제: UI가 보이지 않음
- Canvas가 World Space인지 확인
- Scale이 너무 작거나 크지 않은지 확인 (0.001 권장)
- `SetMinimapVisible(true)` 호출 확인

### 문제: 관객이 나타나지 않음
- AudiencePrefab이 연결되었는지 확인
- `ToggleAudience(true)` 호출 확인
- 좌석에 앉은 상태인지 확인

### 문제: 콘솔에 "object is missing" 에러
- Injection 필드가 모두 연결되었는지 확인
- `checkInject`로 된 필드는 필수 연결

---

## 다음 단계

1. ✅ Phase 1 완료 (스크립트 작성)
2. ⬜ Phase 2: Unity에서 좌석 시스템 구축
3. ⬜ Phase 3: UI 시스템 구축
4. ⬜ Phase 4: 텔레포트 기능 구현
5. ⬜ Phase 5: 관객 시스템 구축
6. ⬜ Phase 6: 통합 테스트

자세한 진행 상황은 `PROGRESS.md`를 참고하세요.
