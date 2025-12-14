# ArenaX 변경 이력

## [2024-12-15] UI 토글 버그 수정

### 버그 수정
- **UI 토글 동작 안함 문제 해결** (`SeatUIManager.lua`)
  - `MinimapCanvasName`을 "MinimapCanvas" → "DefaultCanvas"로 변경
  - `GameObject.Find()` 대신 `transform:Find()`로 Canvas 자식에서 검색
  - 이벤트 리스너 등록 타이밍 수정: `onEnable()` → `FindRequiredObjects()` 이후 `RegisterButtonListeners()` 호출
  - Unity 생명주기 순서 문제 해결 (onEnable이 start보다 먼저 호출되는 문제)

---

## [2024-12-15] 관객 시스템 및 UI 개선

### 새로운 기능
- **Object Pooling 기반 관객 시스템** (`AudienceManager.lua`)
  - 5개 타입 × 10개 인스턴스 = 총 50명 관객 풀
  - MeshRenderer/Collider 토글 방식 (SetActive 대신)
  - 좌석 앞좌석(FrontSeat) Transform 기반 관객 배치

- **UI 토글 버튼 시스템** (`SeatUIManager.lua`)
  - `UIToggleButton`: 항상 보이는 UI 열기/닫기 버튼
  - `UIContainerPanel`: 토글되는 메인 UI 컨테이너
  - VR 환경에서 키보드 Input 미지원으로 버튼 방식 채택
  - 토글 버튼 텍스트 자동 업데이트 ("UI 열기" ↔ "UI 닫기")

- **에디터 도구 추가**
  - `AudienceSetup.cs`: 관객 풀 자동 설정, DefaultCanvas에 토글 버튼 생성
  - `SeatFrontSeatsSetup.cs`: 좌석별 앞좌석 Transform 자동 설정
  - `UIToggleSetup.cs`: DefaultCanvas UI 토글 시스템 자동 설정
    - 메뉴: `ArenaX > Setup UI Toggle System`
    - MainUI 컨테이너 생성 및 기존 UI 요소 이동
    - UIToggleButton 버튼 자동 생성

- **관객 캐릭터 프리팹** (`Char1~5.prefab`)
  - 5가지 타입의 앉은 관객 캐릭터

### 버그 수정
- **VIVEN SDK Lua 호환성 수정**
  - `Debug.LogError` / `Debug.LogWarning` → `Debug.Log` 변경
  - VIVEN SDK에서 지원하지 않는 함수 사용으로 인한 LuaException 해결
  - 영향 파일: `AudienceManager.lua`, `SeatUIManager.lua`, `SeatController.lua`, `SeatSelectionUI.lua`, `ArenaXManager.lua`

- **스크립트 초기화 순서 문제 해결** (`SeatUIManager.lua`)
  - 근본 원인: SeatUIManager(자식)가 ArenaXManager(부모)보다 먼저 start() 실행
  - 해결: 코루틴 + WaitForEndOfFrame() 대기 후 참조 획득
  - `GetLuaComponentInParent()` 폴백 추가

- **관객 토글 로직 수정** (`ArenaXManager.lua`)
  - 착석 여부와 관계없이 `AudienceManager.ToggleAudience()` 호출하도록 변경
  - 디버그 로그 추가로 상태 추적 용이

### 기술적 변경사항
- **ArenaX.Editor.asmdef**: TMPro 어셈블리 참조 추가
  - GUID: `6055be8ebefd69e48b49212b09b47b2f`

- **SeatController.lua**: FrontSeat1~20 Transform 주입 지원
  - 최대 20개 앞좌석 Transform 설정 가능
  - ArenaXManager에 자동 등록

### 파일 구조
```
Assets/ArenaX/
├── Scripts/
│   ├── Avatar/
│   │   └── AudienceManager.lua      # 관객 풀링 시스템
│   ├── Editor/
│   │   ├── ArenaX.Editor.asmdef     # TMPro 참조 추가
│   │   ├── AudienceSetup.cs         # 관객 설정 에디터 도구
│   │   ├── SeatFrontSeatsSetup.cs   # 앞좌석 설정 에디터 도구
│   │   └── UIToggleSetup.cs         # UI 토글 시스템 설정 도구
│   ├── Manager/
│   │   └── ArenaXManager.lua        # 토글 로직 수정
│   ├── Seat/
│   │   └── SeatController.lua       # FrontSeat Transform 지원
│   └── UI/
│       ├── SeatSelectionUI.lua      # LogError 수정
│       └── SeatUIManager.lua        # UI 버튼 토글, 초기화 순서 수정
└── model/
    └── Characters/
        └── Char1~5.prefab           # 관객 캐릭터 프리팹
```

### 사용 방법

#### 관객 시스템 설정
1. Unity 메뉴: `ArenaX > Setup Audience System`
2. 자동으로 AudiencePool 계층구조 생성
3. 각 좌석의 FrontSeat Transform에 앞좌석 위치/회전 설정

#### UI 토글 설정 (DefaultCanvas)
1. Unity 메뉴: `ArenaX > Setup UI Toggle System` 실행
2. DefaultCanvas를 선택하고 "UI 토글 시스템 설정" 버튼 클릭
3. 자동으로 MainUI 컨테이너와 UIToggleButton 생성
4. 기존 UI 요소들(Minimap, AudienceToggleButton 등)은 MainUI로 이동됨
5. **UIToggleButton 클릭**: UI 열기/닫기 토글

#### UI 구조 예시
```
DefaultCanvas (World Space Canvas)
├── UIToggleButton      # 항상 보임 - UI 열기/닫기 버튼 (오른쪽 상단)
└── MainUI              # 토글됨 - 메인 UI 컨테이너
    ├── Minimap
    ├── AudienceToggleButton
    └── ... 기타 UI 요소
```
