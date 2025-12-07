---@meta
-- Arena X 타입 정의 파일

---@class SeatData
---@field seatId string        좌석 고유 ID (예: "A-1", "B-12")
---@field row string           열 번호 (A, B, C...)
---@field number int           좌석 번호 (1, 2, 3...)
---@field position Vector3     월드 좌표
---@field rotation Quaternion  좌석 방향 (무대를 바라보는)
---@field seatType string      좌석 타입 ("일반", "VIP", "장애인석")
---@field section string       구역 (1층, 2층, VIP석)
---@field isAvailable boolean  선택 가능 여부
---@field isOccupied boolean   착석 여부
SeatData = {}

---@class PlayerSeatState
---@field playerId string      플레이어 ID
---@field currentSeatId string | nil  현재 앉은 좌석 ID (없으면 nil)
---@field isSeated boolean     착석 상태
---@field lastSeatId string | nil  마지막으로 앉았던 좌석
PlayerSeatState = {}

---@class AudienceData
---@field audienceId string    관객 고유 ID
---@field seatId string        앉아있는 좌석 ID
---@field avatarType int       아바타 타입 인덱스
---@field isActive boolean     활성화 상태
AudienceData = {}

---@class SeatUIConfig
---@field buttonSize Vector2   버튼 크기
---@field spacing float        버튼 간격
---@field normalColor Color    기본 색상
---@field selectedColor Color  선택된 좌석 색상
---@field occupiedColor Color  착석 중인 좌석 색상
---@field disabledColor Color  비활성화 색상
SeatUIConfig = {}

---@alias SeatType "일반" | "VIP" | "장애인석"
---@alias SeatSection "1층" | "2층" | "VIP석" | "발코니"

-- 이벤트 타입 정의
---@alias SeatEventType "OnSeatSelected" | "OnPlayerSit" | "OnPlayerStand" | "OnAudienceToggle"
