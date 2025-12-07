--[[
    SeatController.lua
    개별 좌석 컨트롤러
    각 좌석 오브젝트에 부착되어 착석/이탈 이벤트를 처리

    필수 컴포넌트:
    - VObject (네트워크 동기화)
    - VivenSittable (앉기 기능)
    - Collider (상호작용 영역) - Is Trigger 체크

    착석 감지 방식:
    VivenSittable에는 이벤트가 없으므로, sitPoint의 Trigger Collider를 사용하여
    플레이어 착석/이탈을 감지합니다.

    구조:
    Seat (VObject + VivenSittable)
    └── SitPoint (Transform) - VivenSittable.sitPoint로 연결
        └── SitDetector (Box Collider, Is Trigger) - 이 스크립트 부착
]]

--region Injection list
local _INJECTED_ORDER = 0
local function checkInject(OBJECT)
    _INJECTED_ORDER = _INJECTED_ORDER + 1
    assert(OBJECT, _INJECTED_ORDER .. "th object is missing")
    return OBJECT
end
local function NullableInject(OBJECT)
    _INJECTED_ORDER = _INJECTED_ORDER + 1
    if OBJECT == nil then
        Debug.Log(_INJECTED_ORDER .. "th object is missing")
    end
    return OBJECT
end

---@type GameObject
---@details ArenaXManager가 있는 오브젝트 (비워두면 자동으로 "ArenaXManager" 이름으로 찾음)
ArenaXManagerObject = NullableInject(ArenaXManagerObject)

---@type string
---@details ArenaXManager 오브젝트 이름 (자동 찾기용)
ArenaXManagerName = "ArenaXManager"

---@type string
---@details 좌석 열 번호 (A, B, C...)
SeatRow = "A"

---@type int
---@details 좌석 번호 (1, 2, 3...)
SeatNumber = 1

---@type string
---@details 좌석 타입 ("일반", "VIP", "장애인석")
SeatType = "일반"

---@type string
---@details 좌석 구역 (1층, 2층, VIP석)
SeatSection = "1층"

---@type float
---@details 착석 판정 딜레이 (초) - 너무 빠른 판정 방지
SitDetectionDelay = 0.5

---@type string
---@details 플레이어 감지용 태그 (플레이어 오브젝트에 설정된 태그)
PlayerTag = "Player"

---@type string
---@details 플레이어 감지용 레이어 이름 (태그 대신 사용 가능)
PlayerLayerName = ""
--endregion

-- 컴포넌트 참조
local vivenSittable = nil
local arenaXManager = nil
local seatCollider = nil

-- 좌석 ID (자동 생성)
local seatId = ""

-- 착석 상태
local isOccupied = false
local isPlayerInTrigger = false
local sitTimer = 0
local standTimer = 0

-- 현재 착석한 플레이어
local seatedPlayer = nil

--region 생명주기 함수

function awake()
    -- 좌석 ID 생성
    seatId = SeatRow .. "-" .. tostring(SeatNumber)

    -- VivenSittable 컴포넌트 가져오기 (부모에서 찾기)
    vivenSittable = self:GetComponent("VivenSittable")
    if vivenSittable == nil then
        vivenSittable = self:GetComponentInParent(typeof(CS.Twoz.Viven.Interactions.VivenSittable))
    end

    -- Collider 가져오기
    seatCollider = self:GetComponent(typeof(CS.UnityEngine.Collider))
end

function start()
    -- ArenaXManager 참조 가져오기
    FindArenaXManager()

    -- ArenaXManager에 좌석 등록
    if arenaXManager ~= nil then
        local seatData = {
            seatId = seatId,
            row = SeatRow,
            number = SeatNumber,
            position = self.transform.position,
            rotation = self.transform.rotation,
            seatType = SeatType,
            section = SeatSection,
            isAvailable = true,
            isOccupied = false
        }
        arenaXManager.RegisterSeat(seatId, seatData)
    end
end

--- ArenaXManager 찾기 (Injection 또는 GameObject.Find)
function FindArenaXManager()
    -- 이미 찾았으면 스킵
    if arenaXManager ~= nil then
        return
    end

    -- 1. Injection으로 연결된 경우
    if ArenaXManagerObject ~= nil then
        arenaXManager = ArenaXManagerObject:GetLuaComponent("ArenaXManager")
        if arenaXManager ~= nil then
            return
        end
    end

    -- 2. GameObject.Find로 찾기
    local managerObj = GameObject.Find(ArenaXManagerName)
    if managerObj ~= nil then
        arenaXManager = managerObj:GetLuaComponent("ArenaXManager")
        if arenaXManager ~= nil then
            return
        end
    end

    -- 3. 대체 이름으로 시도
    local altNames = { "ArenaXManager", "GameManager", "Manager" }
    for _, name in ipairs(altNames) do
        local obj = GameObject.Find(name)
        if obj ~= nil then
            local mgr = obj:GetLuaComponent("ArenaXManager")
            if mgr ~= nil then
                arenaXManager = mgr
                return
            end
        end
    end
end

function update()
    -- 착석 판정 타이머 처리
    if isPlayerInTrigger and not isOccupied then
        sitTimer = sitTimer + Time.deltaTime
        if sitTimer >= SitDetectionDelay then
            -- 딜레이 후 착석 확정
            OnSit()
        end
    end

    -- 이탈 판정 타이머 처리
    if not isPlayerInTrigger and isOccupied then
        standTimer = standTimer + Time.deltaTime
        if standTimer >= SitDetectionDelay then
            -- 딜레이 후 이탈 확정
            OnStand()
        end
    end
end

function onEnable()
    -- 상태 초기화
    isOccupied = false
    isPlayerInTrigger = false
    sitTimer = 0
    standTimer = 0
end

function onDisable()
    -- 착석 중이었다면 이탈 처리
    if isOccupied then
        OnStand()
    end
end

--region Trigger 이벤트 (착석 감지)

--- 플레이어가 좌석 영역에 들어왔을 때
---@param other Collider
function onTriggerEnter(other)
    if not IsPlayer(other.gameObject) then
        return
    end

    isPlayerInTrigger = true
    sitTimer = 0
    standTimer = 0
    seatedPlayer = other.gameObject

    -- 햅틱 피드백 (좌석 진입 시 가벼운 피드백)
    XR.StartControllerVibration(false, 0.1, 0.05)
    XR.StartControllerVibration(true, 0.1, 0.05)
end

--- 플레이어가 좌석 영역에서 나갔을 때
---@param other Collider
function onTriggerExit(other)
    if not IsPlayer(other.gameObject) then
        return
    end

    isPlayerInTrigger = false
    standTimer = 0
    sitTimer = 0
end

--- 플레이어가 좌석 영역에 머무는 동안 (대체 감지 방법)
---@param other Collider
function onTriggerStay(other)
    -- onTriggerEnter가 놓친 경우를 위한 백업
    if not IsPlayer(other.gameObject) then
        return
    end

    if not isPlayerInTrigger then
        isPlayerInTrigger = true
        sitTimer = 0
        seatedPlayer = other.gameObject
    end
end

--endregion

--region 착석/이탈 처리

--- 착석 확정 처리
function OnSit()
    if isOccupied then
        return
    end

    isOccupied = true
    sitTimer = 0

    -- ArenaXManager에 알림
    if arenaXManager ~= nil then
        arenaXManager.OnPlayerSit(seatId)
    end

    -- 햅틱 피드백 (착석 확정)
    XR.StartControllerVibration(false, 0.3, 0.1)
    XR.StartControllerVibration(true, 0.3, 0.1)

    -- 토스트 메시지 (선택사항)
    -- UI.ToastMessage(SeatSection .. " " .. seatId .. " 좌석에 착석했습니다")
end

--- 이탈 확정 처리
function OnStand()
    if not isOccupied then
        return
    end

    isOccupied = false
    standTimer = 0
    seatedPlayer = nil

    -- ArenaXManager에 알림
    if arenaXManager ~= nil then
        arenaXManager.OnPlayerStand()
    end
end

--endregion

--region 플레이어 감지 유틸리티

--- GameObject가 플레이어인지 확인
---@param go GameObject
---@return boolean
function IsPlayer(go)
    if go == nil then
        return false
    end

    -- 태그로 확인
    if PlayerTag ~= "" and go.tag == PlayerTag then
        return true
    end

    -- 레이어로 확인
    if PlayerLayerName ~= "" then
        local playerLayer = LayerMask.NameToLayer(PlayerLayerName)
        if go.layer == playerLayer then
            return true
        end
    end

    -- 이름에 "Player" 포함 여부로 확인 (폴백)
    if string.find(go.name, "Player") or string.find(go.name, "XR Origin") then
        return true
    end

    -- 부모에서 확인
    local parent = go.transform.parent
    if parent ~= nil then
        local parentName = parent.gameObject.name
        if string.find(parentName, "Player") or string.find(parentName, "XR Origin") then
            return true
        end
    end

    return false
end

--endregion

--region 유틸리티

--- 좌석 ID 조회
---@return string
function GetSeatId()
    return seatId
end

--- 좌석 정보 조회
---@return table
function GetSeatInfo()
    return {
        seatId = seatId,
        row = SeatRow,
        number = SeatNumber,
        seatType = SeatType,
        section = SeatSection,
        isOccupied = isOccupied
    }
end

--- 착석 상태 조회
---@return boolean
function IsOccupied()
    return isOccupied
end

--- 좌석 사용 가능 여부 설정
---@param available boolean
function SetAvailable(available)
    if arenaXManager ~= nil then
        local seatData = arenaXManager.GetSeatInfo(seatId)
        if seatData ~= nil then
            seatData.isAvailable = available
        end
    end
end

--endregion
