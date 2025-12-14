--[[
    ArenaXManager.lua
    Arena X 프로젝트의 핵심 매니저
    좌석 데이터 관리, 이벤트 허브, 시스템 조율
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
---@details SeatUIManager가 있는 오브젝트 (비워두면 자동으로 찾음)
SeatUIManagerObject = NullableInject(SeatUIManagerObject)

---@type string
---@details SeatUIManager 오브젝트 이름 (자동 찾기용)
SeatUIManagerName = "SeatUIManager"

---@type GameObject
---@details AudienceManager가 있는 오브젝트 (비워두면 자동으로 찾음)
AudienceManagerObject = NullableInject(AudienceManagerObject)

---@type string
---@details AudienceManager 오브젝트 이름 (자동 찾기용)
AudienceManagerName = "AudienceManager"

---@type boolean
---@details 플레이어가 텔레포트될 때 페이드 효과 사용 여부
UseFadeEffect = true
--endregion

-- 모듈 임포트
local util = require 'xlua.util'

-- 매니저 참조
local seatUIManager = nil
local audienceManager = nil

-- 좌석 데이터 저장소
---@type table<string, SeatData>
local seats = {}

-- 현재 플레이어 상태
---@type PlayerSeatState
local playerState = {
    playerId = "",
    currentSeatId = nil,
    isSeated = false,
    lastSeatId = nil
}

-- 관객 표시 상태
local isAudienceVisible = false

-- 이벤트 리스너들
local eventListeners = {
    OnSeatSelected = {},
    OnPlayerSit = {},
    OnPlayerStand = {},
    OnAudienceToggle = {}
}

--region 생명주기 함수

function awake()
    playerState.playerId = Player.Mine.UserID
end

function start()
    -- 자동 찾기 실행
    FindRequiredObjects()

    -- 다른 매니저 참조 가져오기
    if SeatUIManagerObject ~= nil then
        seatUIManager = SeatUIManagerObject:GetLuaComponent("SeatUIManager")
    end

    if AudienceManagerObject ~= nil then
        audienceManager = AudienceManagerObject:GetLuaComponent("AudienceManager")
    end

    -- 초기화
    InitializeSeats()
end

--- 필요한 오브젝트들 자동 찾기
function FindRequiredObjects()
    -- SeatUIManager 찾기
    if SeatUIManagerObject == nil then
        SeatUIManagerObject = GameObject.Find(SeatUIManagerName)
    end

    -- AudienceManager 찾기
    if AudienceManagerObject == nil then
        AudienceManagerObject = GameObject.Find(AudienceManagerName)
    end
end

function onEnable()
end

function onDisable()
end

--endregion

--region 좌석 관리

--- 좌석 데이터 초기화 (씬에서 좌석들을 찾아서 등록)
function InitializeSeats()
    -- SeatController들이 자동으로 RegisterSeat을 호출하여 등록됨
end

--- 좌석 등록
---@param seatId string
---@param seatData SeatData
function RegisterSeat(seatId, seatData)
    seats[seatId] = seatData

    -- 현재 등록된 좌석 수 계산
    local count = 0
    for _ in pairs(seats) do
        count = count + 1
    end

    Debug.Log("[ArenaXManager] Seat registered: " .. seatId .. " (총 " .. count .. "개)")
end

--- 좌석 정보 조회
---@param seatId string
---@return SeatData | nil
function GetSeatInfo(seatId)
    return seats[seatId]
end

--- 모든 좌석 정보 조회
---@return table<string, SeatData>
function GetAllSeats()
    return seats
end

--endregion

--region 좌석 선택 및 텔레포트

--- UI에서 좌석 선택 시 호출
---@param seatId string
function SelectSeat(seatId)
    local seatData = seats[seatId]
    if seatData == nil then
        Debug.LogWarning("[ArenaXManager] Seat not found: " .. seatId)
        return
    end

    if not seatData.isAvailable then
        Debug.LogWarning("[ArenaXManager] Seat not available: " .. seatId)
        return
    end

    -- 이벤트 발생
    FireEvent("OnSeatSelected", seatId)

    -- 텔레포트 실행
    TeleportToSeat(seatId, seatData)
end

--- 좌석으로 텔레포트
---@param seatId string
---@param seatData SeatData
function TeleportToSeat(seatId, seatData)
    Debug.Log("[ArenaXManager] TeleportToSeat: " .. seatId)

    if UseFadeEffect then
        -- 페이드 아웃 -> 텔레포트 -> 페이드 인
        UI.FadeOut(0.3, function()
            -- 플레이어 위치 이동
            Player.Mine.TeleportPlayer(seatData.position, seatData.rotation)
            Debug.Log("[ArenaXManager] Teleported to: " .. tostring(seatData.position))

            UI.FadeIn(0.3, nil)
        end)
    else
        -- 즉시 텔레포트
        Player.Mine.TeleportPlayer(seatData.position, seatData.rotation)
        Debug.Log("[ArenaXManager] Teleported to: " .. tostring(seatData.position))
    end
end

--endregion

--region 착석/이탈 처리

--- 플레이어가 좌석에 앉았을 때 (SeatController에서 호출)
---@param seatId string
function OnPlayerSit(seatId)
    -- 이전 좌석 상태 업데이트
    if playerState.currentSeatId ~= nil then
        local prevSeat = seats[playerState.currentSeatId]
        if prevSeat ~= nil then
            prevSeat.isOccupied = false
        end
    end

    -- 현재 좌석 상태 업데이트
    playerState.currentSeatId = seatId
    playerState.isSeated = true
    playerState.lastSeatId = seatId

    local currentSeat = seats[seatId]
    if currentSeat ~= nil then
        currentSeat.isOccupied = true
    end

    -- UI 업데이트
    if seatUIManager ~= nil then
        seatUIManager.HighlightCurrentSeat(seatId)
    end

    -- 관객 배치
    if audienceManager ~= nil and isAudienceVisible then
        audienceManager.SpawnAudienceNearPlayer(seatId)
    end

    -- 이벤트 발생
    FireEvent("OnPlayerSit", seatId)
end

--- 플레이어가 좌석에서 일어났을 때 (SeatController에서 호출)
function OnPlayerStand()
    local prevSeatId = playerState.currentSeatId

    -- 좌석 상태 업데이트
    if prevSeatId ~= nil then
        local seat = seats[prevSeatId]
        if seat ~= nil then
            seat.isOccupied = false
        end
    end

    -- 플레이어 상태 업데이트
    playerState.currentSeatId = nil
    playerState.isSeated = false

    -- UI 업데이트
    if seatUIManager ~= nil then
        seatUIManager.ClearHighlight()
    end

    -- 관객 제거
    if audienceManager ~= nil then
        audienceManager.ClearAudience()
    end

    -- 이벤트 발생
    FireEvent("OnPlayerStand", prevSeatId)
end

--endregion

--region 관객 시스템

--- 관객 표시 토글
---@param show boolean
function ToggleAudience(show)
    isAudienceVisible = show

    if audienceManager ~= nil then
        if show and playerState.isSeated then
            audienceManager.SpawnAudienceNearPlayer(playerState.currentSeatId)
        else
            audienceManager.ClearAudience()
        end
    end

    -- 이벤트 발생
    FireEvent("OnAudienceToggle", show)
end

--- 관객 표시 상태 조회
---@return boolean
function IsAudienceVisible()
    return isAudienceVisible
end

--endregion

--region 이벤트 시스템

--- 이벤트 리스너 등록
---@param eventType SeatEventType
---@param listener function
function AddEventListener(eventType, listener)
    if eventListeners[eventType] == nil then
        eventListeners[eventType] = {}
    end
    table.insert(eventListeners[eventType], listener)
end

--- 이벤트 리스너 해제
---@param eventType SeatEventType
---@param listener function
function RemoveEventListener(eventType, listener)
    if eventListeners[eventType] == nil then return end

    for i, l in ipairs(eventListeners[eventType]) do
        if l == listener then
            table.remove(eventListeners[eventType], i)
            return
        end
    end
end

--- 이벤트 발생
---@param eventType SeatEventType
---@param data any
function FireEvent(eventType, data)
    if eventListeners[eventType] == nil then return end

    for _, listener in ipairs(eventListeners[eventType]) do
        listener(data)
    end
end

--endregion

--region 유틸리티

--- 현재 플레이어 상태 조회
---@return PlayerSeatState
function GetPlayerState()
    return playerState
end

--- 특정 좌석이 사용 가능한지 확인
---@param seatId string
---@return boolean
function IsSeatAvailable(seatId)
    local seat = seats[seatId]
    if seat == nil then return false end
    return seat.isAvailable and not seat.isOccupied
end

--endregion
