# Arena X - 시스템 아키텍처 설계서

## 프로젝트 개요

**Arena X**는 VR 환경에서 공연장 좌석 위치를 미리 체험할 수 있는 메타버스 애플리케이션입니다.
사용자는 미니맵 UI를 통해 좌석을 선택하고, 해당 위치에서 무대가 어떻게 보이는지 확인할 수 있습니다.

---

## 핵심 기능

| 기능 | 설명 | 우선순위 |
|------|------|----------|
| **좌석 선택 UI** | 미니맵에서 좌석 클릭 시 해당 좌석으로 텔레포트 | P0 |
| **좌석 착석** | VivenSittable을 통한 앉기 기능 | P0 |
| **가상 관객 토글** | 내 앞좌석 관객 On/Off로 시야 확인 | P1 |
| **자유 이동** | 공연장 내 자유롭게 돌아다니기 | P1 |
| **UI 동기화** | 착석 시 UI 자동 갱신 | P2 |

---

## 시스템 구조도

```
┌─────────────────────────────────────────────────────────────────┐
│                        Arena X System                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │   ArenaX     │    │   SeatUI     │    │  Audience    │       │
│  │   Manager    │◄──►│   Manager    │◄──►│  Manager     │       │
│  │   (Core)     │    │   (UI)       │    │  (NPC)       │       │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘       │
│         │                   │                   │                │
│         ▼                   ▼                   ▼                │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │    Seat      │    │   Minimap    │    │   Virtual    │       │
│  │  Controller  │    │   Canvas     │    │   Audience   │       │
│  │ (Per Seat)   │    │   (World)    │    │   Pool       │       │
│  └──────────────┘    └──────────────┘    └──────────────┘       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 컴포넌트 설계

### 1. ArenaXManager.lua (핵심 매니저)

**역할**: 전체 시스템 조율, 좌석 데이터 관리, 이벤트 허브

```lua
-- 컴포넌트 구조
ArenaXManager
├── 좌석 데이터 (seats table)
├── 현재 플레이어 좌석 정보
├── 이벤트 콜백 (좌석 선택, 착석, 이탈)
└── Manager 참조들 (SeatUIManager, AudienceManager)
```

**주요 기능**:
- `SelectSeat(seatId)` - 좌석 선택 및 텔레포트
- `OnPlayerSit(seatId)` - 착석 이벤트 처리
- `OnPlayerStand()` - 이탈 이벤트 처리
- `GetSeatInfo(seatId)` - 좌석 정보 조회

---

### 2. SeatController.lua (개별 좌석)

**역할**: 각 좌석의 상호작용 처리

**착석 감지 방식**:
> VivenSittable에는 착석/이탈 이벤트가 없습니다.
> 따라서 **Trigger Collider**를 사용하여 플레이어 착석을 감지합니다.

**오브젝트 구조**:
```
Seat (GameObject)
├── VObject (네트워크 동기화)
├── VivenSittable (앉기 기능)
│   └── sitPoint → SitDetector 참조
│
└── SitDetector (자식 GameObject)
    ├── Box Collider (Is Trigger ✓)
    └── VivenLuaBehaviour + SeatController.lua
```

**필수 컴포넌트**:
- VObject (네트워크 동기화) - 부모에 부착
- VivenSittable (앉기 기능) - 부모에 부착
- Collider (Is Trigger) - SitDetector에 부착
- SeatController.lua - SitDetector에 부착

```lua
-- Injection 설정
SeatController
├── ArenaXManagerObject (필수)
├── SeatRow ("A", "B", "C"...)
├── SeatNumber (1, 2, 3...)
├── SeatType ("일반", "VIP", "장애인석")
├── SeatSection ("1층", "2층", "VIP석")
├── SitDetectionDelay (0.5초, 착석 판정 딜레이)
├── PlayerTag ("Player")
└── PlayerLayerName ("" 또는 레이어 이름)
```

**착석 감지 흐름**:
```
[플레이어가 Collider 진입]
    ↓
onTriggerEnter() → isPlayerInTrigger = true
    ↓
[0.5초 대기 (SitDetectionDelay)]
    ↓
update()에서 타이머 체크
    ↓
OnSit() → ArenaXManager.OnPlayerSit(seatId)
```

**주요 기능**:
- `onTriggerEnter/Exit/Stay()` - Collider로 플레이어 감지
- `OnSit()` - 착석 확정 (딜레이 후)
- `OnStand()` - 이탈 확정 (딜레이 후)
- `IsPlayer(go)` - 태그/레이어/이름으로 플레이어 판별

---

### 3. SeatUIManager.lua (UI 매니저)

**역할**: 미니맵 UI, 좌석 선택 인터페이스

```lua
-- 컴포넌트 구조
SeatUIManager
├── minimapCanvas (World Space Canvas)
├── seatButtons (좌석 버튼들)
├── currentSeatIndicator (현재 위치 표시)
├── audienceToggle (관객 토글 버튼)
└── infoPanel (좌석 정보 패널)
```

**주요 기능**:
- `UpdateMinimap()` - 미니맵 상태 갱신
- `OnSeatButtonClick(seatId)` - 좌석 버튼 클릭 처리
- `HighlightCurrentSeat(seatId)` - 현재 좌석 강조
- `ToggleAudienceDisplay(show)` - 관객 표시 토글

---

### 4. AudienceManager.lua (관객 매니저)

**역할**: 가상 관객 생성 및 관리 (성능 최적화)

```lua
-- 컴포넌트 구조
AudienceManager
├── audiencePrefabs (관객 프리팹 배열)
├── activeAudience (현재 활성화된 관객들)
├── maxVisibleAudience (최대 표시 수: 10-20명)
└── playerSeatPosition (플레이어 좌석 위치)
```

**주요 기능**:
- `SpawnAudienceNearPlayer(seatId)` - 플레이어 주변에만 관객 생성
- `ClearAudience()` - 관객 제거
- `ToggleAudience(show)` - 관객 표시 토글
- `UpdateAudiencePositions()` - 플레이어 이동 시 관객 위치 갱신

**성능 최적화**:
- 플레이어 앞 2-3열에만 관객 배치
- Object Pooling 사용
- LOD (Level of Detail) 적용

---

## 데이터 구조

### 좌석 데이터 (SeatData)

```lua
---@class SeatData
---@field seatId string        -- "A-1", "B-12" 형식
---@field row string           -- 열 번호 (A, B, C...)
---@field number int           -- 좌석 번호 (1, 2, 3...)
---@field position Vector3     -- 월드 좌표
---@field rotation Quaternion  -- 바라보는 방향
---@field seatType string      -- "일반", "VIP", "장애인석"
---@field section string       -- 구역 (1층, 2층, VIP석)
---@field isAvailable boolean  -- 선택 가능 여부
```

### 좌석 배치 예시

```
              [무대]

   A열: [1][2][3][4][5][6][7][8][9][10]
   B열: [1][2][3][4][5][6][7][8][9][10]
   C열: [1][2][3][4][5][6][7][8][9][10]
          ...
```

---

## 이벤트 흐름

### 1. 좌석 선택 (UI에서)

```
[미니맵 클릭]
    ↓
SeatUIManager.OnSeatButtonClick(seatId)
    ↓
ArenaXManager.SelectSeat(seatId)
    ↓
[플레이어 텔레포트]
    ↓
SeatController.OnSit() (VivenSittable)
    ↓
ArenaXManager.OnPlayerSit(seatId)
    ↓
├── SeatUIManager.HighlightCurrentSeat(seatId)
└── AudienceManager.SpawnAudienceNearPlayer(seatId)
```

### 2. 직접 착석 (걸어가서)

```
[플레이어가 좌석에 접근]
    ↓
[VivenSittable 트리거]
    ↓
SeatController.OnSit()
    ↓
ArenaXManager.OnPlayerSit(seatId)
    ↓
├── SeatUIManager.HighlightCurrentSeat(seatId)
└── AudienceManager.SpawnAudienceNearPlayer(seatId)
```

### 3. 관객 토글

```
[UI 토글 버튼 클릭]
    ↓
SeatUIManager.OnAudienceToggle()
    ↓
ArenaXManager.ToggleAudience(show)
    ↓
AudienceManager.ToggleAudience(show)
    ↓
[관객 표시/숨김]
```

---

## 폴더 구조

```
Assets/ArenaX/
├── Docs/
│   ├── ARCHITECTURE.md      # 이 문서
│   └── PROGRESS.md          # 진행 상황 체크리스트
│
├── Scripts/
│   ├── Manager/
│   │   └── ArenaXManager.lua
│   ├── Seat/
│   │   └── SeatController.lua
│   ├── UI/
│   │   └── SeatUIManager.lua
│   ├── Avatar/
│   │   └── AudienceManager.lua
│   └── Utils/
│       └── def.lua          # 타입 정의
│
├── Prefabs/
│   ├── Seats/
│   │   ├── Seat_Normal.prefab
│   │   └── Seat_VIP.prefab
│   ├── UI/
│   │   ├── MinimapCanvas.prefab
│   │   └── SeatButton.prefab
│   └── Avatars/
│       └── VirtualAudience.prefab
│
└── Scenes/
    └── ArenaX_Main.unity
```

---

## 기술 스택

| 기술 | 용도 |
|------|------|
| **Viven SDK** | VR 메타버스 프레임워크 |
| **VivenSittable** | 좌석 착석 기능 |
| **VObject** | 네트워크 동기화 |
| **VivenLuaBehaviour** | Lua 스크립트 실행 |
| **VivenCanvasSetting** | World Space UI |
| **XR Interaction Toolkit** | VR 상호작용 |

---

## 성능 고려사항

### 관객 최적화

1. **Object Pooling**: 관객 오브젝트 재사용
2. **거리 기반 활성화**: 플레이어 주변만 활성화
3. **LOD System**: 거리에 따른 모델 품질 조절
4. **최대 인원 제한**: 동시에 10-20명만 렌더링

### UI 최적화

1. **World Space Canvas**: VR에서 효율적
2. **Canvas Group**: 가시성 제어
3. **Object Pooling**: 버튼 재사용

---

## 확장 가능성

1. **멀티플레이어**: Room API를 통한 다른 사용자 위치 표시
2. **가격 정보**: 좌석별 가격 표시
3. **시야각 분석**: 무대와의 각도/거리 정보
4. **공연 시뮬레이션**: 실제 공연 영상 재생
5. **예매 연동**: 실제 예매 시스템과 연결
