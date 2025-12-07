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
   ArenaXManagerObject: (씬의 ArenaXManager 연결)
   SeatRow: "A"
   SeatNumber: 1
   SeatType: "일반"
   SeatSection: "1층"
   SitDetectionDelay: 0.5
   PlayerTag: "Player"
   PlayerLayerName: "" (비워두거나 레이어 이름)
   ```

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

## 5. 미니맵 UI 제작

### 5.1 Canvas 생성

1. **Canvas 생성**: `UI > Canvas`
   - 이름: `MinimapCanvas`

2. **Canvas 설정**:
   ```
   Render Mode: World Space
   Width: 800
   Height: 600
   Scale: 0.001, 0.001, 0.001 (작게 조절)
   ```

3. **컴포넌트 추가**:
   - `VivenCanvasSetting` (VR UI용)

### 5.2 좌석 버튼 컨테이너

1. **MinimapCanvas 하위에 Panel 생성**: 이름: `SeatButtonContainer`

2. **컴포넌트 추가**:
   - `Grid Layout Group`
   ```
   Cell Size: 50, 50
   Spacing: 5, 5
   Constraint: Fixed Column Count
   Constraint Count: 5 (한 열의 좌석 수)
   ```

### 5.3 좌석 버튼 프리팹

1. **Button 생성**: `UI > Button - TextMeshPro`
   - 이름: `SeatButton`

2. **Button 설정**:
   - Size: 50 x 50
   - 텍스트: 비워두기 (스크립트에서 설정)

3. **프리팹으로 저장**: `Assets/ArenaX/Prefabs/UI/SeatButton.prefab`

4. **씬에서 삭제** (프리팹만 사용)

### 5.4 추가 UI 요소 (선택사항)

**정보 패널**:
- 현재 좌석 정보 표시 텍스트

**관객 토글 버튼**:
- "관객 보기/숨기기" 버튼

### 5.5 SeatUIManager에 연결

SeatUIManager의 Injection에서:
```
MinimapCanvas: MinimapCanvas 오브젝트
SeatButtonContainer: SeatButtonContainer 오브젝트
SeatButtonPrefab: SeatButton.prefab
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
