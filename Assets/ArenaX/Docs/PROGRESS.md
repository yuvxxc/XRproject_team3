# Arena X - 개발 진행 체크리스트

## 프로젝트 상태

**시작일**: 2025-12-07
**현재 단계**: Phase 3 - UI 시스템 (진행 중)
**최종 업데이트**: 2025-12-15

---

## Phase 1: 기초 구조 설정 (Foundation)

### 1.1 프로젝트 구조 생성
- [x] 폴더 구조 생성 (ArenaX/)
- [x] 아키텍처 문서 작성 (ARCHITECTURE.md)
- [x] 진행 체크리스트 작성 (PROGRESS.md)
- [x] 타입 정의 파일 생성 (def.lua)

### 1.2 핵심 매니저 스크립트
- [x] ArenaXManager.lua 생성
  - [x] 좌석 데이터 구조 정의
  - [x] 이벤트 시스템 구현
  - [x] Manager 참조 관리
- [x] SeatController.lua 생성
  - [x] VivenSittable 연동
  - [x] 착석/이탈 이벤트 처리
- [x] SeatUIManager.lua 생성
  - [x] 미니맵 버튼 시스템
  - [x] 좌석 선택 이벤트
  - [x] 관객 토글 버튼
- [x] AudienceManager.lua 생성
  - [x] Object Pool 구현
  - [x] 플레이어 주변 관객 배치
  - [x] 토글 기능

### 1.3 테스트 씬 구성
- [x] XRproject.unity 씬 사용 (기존 경기장 씬)
- [x] 기본 공연장 환경 배치 (기존 경기장 사용)
- [x] 테스트용 좌석 3개 배치

### 1.4 에디터 도구 (추가)
- [x] ArenaXSceneSetup.cs 에디터 도구 생성
  - [x] 매니저 오브젝트 자동 생성
  - [x] VivenLuaBehaviour Injection 자동 설정
  - [x] 좌석 컴포넌트 자동 구성 (VObject, VivenSittable, SeatController)
  - [x] SitPoint/SitDetector 자동 생성

---

## Phase 2: 좌석 시스템 (Seating System)

### 2.1 좌석 프리팹 제작
- [ ] Seat_Normal.prefab 생성
  - [ ] VObject 컴포넌트
  - [ ] VivenSittable 컴포넌트
  - [ ] Collider 설정
  - [ ] SeatController.lua 연결
- [ ] 좌석 모델 적용 (있는 경우)

### 2.2 좌석 배치
- [ ] 좌석 배치 규칙 정의
- [ ] A열 좌석 배치 (10석)
- [ ] B열 좌석 배치 (10석)
- [ ] C열 좌석 배치 (10석)
- [ ] 좌석 ID 시스템 구현

### 2.3 착석 기능 테스트
- [ ] 직접 걸어가서 착석 테스트
- [ ] 착석 시 카메라 위치 확인
- [ ] 앉은 상태에서 주변 둘러보기 테스트

---

## Phase 3: UI 시스템 (UI System)

### 3.1 미니맵 UI 제작
- [x] MinimapCanvas 생성
  - [x] World Space Canvas 설정
  - [x] VivenCanvasSetting 적용
- [x] 좌석 배치도 이미지 적용 (경기장 사진)
- [x] SeatButton 동적 생성

### 3.2 SeatUIManager 구현
- [x] SeatUIManager.lua 생성
  - [x] 미니맵 좌석 버튼 동적 생성
  - [x] 좌석 클릭 이벤트 처리
  - [x] 현재 좌석 강조 표시
- [x] 정보 패널 UI
  - [x] 좌석 번호 표시 (1F A-1 형식)
  - [x] 구역 정보 표시

### 3.3 SeatSelectionUI 구현 (추가)
- [x] SeatSelectionUI.lua 생성
  - [x] 드롭다운 필터 (Block, 층)
  - [x] 좌석 버튼 그리드 동적 생성
  - [x] 좌석 선택 및 정보 표시
  - [x] Select 버튼으로 텔레포트

### 3.4 UI 인터랙션
- [x] VR 포인터로 버튼 클릭 테스트
- [x] UI 위치 조정 (플레이어 앞 표시)
- [ ] 관객 토글 버튼 추가

---

## Phase 4: 텔레포트 시스템 (Teleport)

### 4.1 좌석 선택 텔레포트
- [ ] UI에서 좌석 선택 시 텔레포트 구현
- [ ] 페이드 인/아웃 효과 추가
- [ ] 텔레포트 후 자동 착석

### 4.2 자유 이동
- [ ] 기본 이동 활성화 확인
- [ ] 이동 가능 영역 제한
- [ ] 무대 영역 진입 제한

---

## Phase 5: 관객 시스템 (Audience System)

### 5.1 관객 프리팹 제작
- [ ] VirtualAudience.prefab 생성
- [ ] 앉은 자세 애니메이션 적용
- [ ] 다양한 외형 변형 준비 (선택사항)

### 5.2 AudienceManager 구현
- [ ] AudienceManager.lua 생성
  - [ ] Object Pool 구현
  - [ ] 플레이어 주변 관객 배치 로직
  - [ ] 토글 기능 구현

### 5.3 성능 최적화
- [ ] 최대 관객 수 제한 (10-20명)
- [ ] 플레이어 앞 2-3열만 관객 표시
- [ ] LOD 적용 (선택사항)

---

## Phase 6: 통합 및 테스트 (Integration)

### 6.1 시스템 통합
- [ ] 모든 Manager 연결
- [ ] 이벤트 흐름 테스트
- [ ] 에러 핸들링 추가

### 6.2 VR 테스트
- [ ] Quest에서 테스트
- [ ] 성능 프로파일링
- [ ] 최적화 적용

### 6.3 사용자 경험 개선
- [ ] 피드백 수집
- [ ] UI/UX 개선
- [ ] 버그 수정

---

## Phase 7: 폴리싱 (Polish)

### 7.1 시각적 개선
- [ ] 좌석 모델 개선
- [ ] 조명 설정
- [ ] 무대 효과 추가

### 7.2 오디오
- [ ] 공연장 앰비언트 사운드
- [ ] UI 사운드 효과

### 7.3 추가 기능
- [ ] 좌석 정보 상세 표시
- [ ] 무대와의 거리/각도 표시
- [ ] 스크린샷 기능

---

## 이슈 트래커

### 현재 이슈
| ID | 설명 | 상태 | 우선순위 |
|----|------|------|----------|
| - | - | - | - |

### 해결된 이슈
| ID | 설명 | 해결일 |
|----|------|--------|
| #001 | Injection 값이 런타임에 적용되지 않음 (SeatNumber가 항상 1) | 2025-12-15 |
| | 원인: Lua 스크립트에서 `SeatNumber = 1` 직접 할당이 주입된 값을 덮어씀 | |
| | 해결: `SeatNumber = SeatNumber or 1` 패턴으로 변경 | |

---

## 변경 이력

| 날짜 | 변경 내용 | 담당자 |
|------|----------|--------|
| 2025-12-07 | 프로젝트 구조 및 문서 생성 | Claude |
| 2025-12-07 | 핵심 Lua 스크립트 4개 생성 (Manager, Seat, UI, Avatar) | Claude |
| 2025-12-15 | ArenaXSceneSetup.cs 에디터 도구 생성 | Claude |
| 2025-12-15 | SeatSelectionUI.lua 좌석 선택 UI 생성 | Claude |
| 2025-12-15 | Injection 값 보존 패턴 수정 (`var = var or default`) | Claude |
| 2025-12-15 | 좌석 선택 UI 테스트 완료 (A-1, A-2, A-3 정상 표시) | Claude |

---

## 참고 자료

- [Viven SDK Wiki](https://wiki.viven.app/developer)
- [API Reference](https://sdkdoc.viven.app/api/SDK/TwentyOz.VivenSDK)
- [VivenSittable 가이드](https://wiki.viven.app/developer/contents/sittable)
