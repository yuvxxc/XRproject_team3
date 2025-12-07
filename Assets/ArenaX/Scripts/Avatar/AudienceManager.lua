--[[
    AudienceManager.lua
    가상 관객 관리자
    플레이어 주변에만 관객을 배치하여 성능 최적화

    기능:
    - 플레이어 앞 2-3열에만 관객 배치
    - Object Pooling으로 성능 최적화
    - 관객 표시 토글
    - 최대 인원 제한 (10-20명)
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
---@details ArenaXManager가 있는 오브젝트
ArenaXManagerObject = checkInject(ArenaXManagerObject)

---@type GameObject
---@details 관객 프리팹 (앉은 자세)
AudiencePrefab = checkInject(AudiencePrefab)

---@type Transform
---@details 관객 오브젝트 부모 (풀링용)
AudienceContainer = NullableInject(AudienceContainer)

---@type int
---@details 최대 표시 관객 수
MaxVisibleAudience = 15

---@type int
---@details 플레이어 앞 몇 열까지 관객 배치
FrontRowsToFill = 2
--endregion

-- 모듈 임포트
local util = require 'xlua.util'

-- 컴포넌트 참조
local arenaXManager = nil

-- 관객 오브젝트 풀
---@type GameObject[]
local audiencePool = {}

-- 활성화된 관객들
---@type table<string, GameObject>
local activeAudience = {}

-- 관객 표시 상태
local isVisible = false

-- 현재 플레이어 좌석 정보
local playerSeatRow = nil
local playerSeatNumber = nil

--region 생명주기 함수

function awake()
    Debug.Log("[AudienceManager] Awake")
end

function start()
    Debug.Log("[AudienceManager] Start")

    -- ArenaXManager 참조 가져오기
    if ArenaXManagerObject ~= nil then
        arenaXManager = ArenaXManagerObject:GetLuaComponent("ArenaXManager")
    end

    -- 오브젝트 풀 초기화
    InitializePool()
end

function onEnable()
    Debug.Log("[AudienceManager] OnEnable")
end

function onDisable()
    Debug.Log("[AudienceManager] OnDisable")

    -- 모든 관객 제거
    ClearAudience()
end

--endregion

--region 오브젝트 풀

--- 오브젝트 풀 초기화
function InitializePool()
    Debug.Log("[AudienceManager] InitializePool - Size: " .. MaxVisibleAudience)

    if AudiencePrefab == nil then
        Debug.LogError("[AudienceManager] AudiencePrefab is nil")
        return
    end

    local parent = AudienceContainer
    if parent == nil then
        parent = self.transform
    end

    for i = 1, MaxVisibleAudience do
        local audience = GameObject.Instantiate(AudiencePrefab, parent)
        audience.name = "PooledAudience_" .. i
        audience:SetActive(false)
        table.insert(audiencePool, audience)
    end

    Debug.Log("[AudienceManager] Pool initialized with " .. #audiencePool .. " objects")
end

--- 풀에서 관객 오브젝트 가져오기
---@return GameObject | nil
function GetFromPool()
    for i, audience in ipairs(audiencePool) do
        if not audience.activeSelf then
            return audience
        end
    end

    Debug.LogWarning("[AudienceManager] Pool exhausted")
    return nil
end

--- 관객 오브젝트를 풀로 반환
---@param audience GameObject
function ReturnToPool(audience)
    if audience ~= nil then
        audience:SetActive(false)
    end
end

--endregion

--region 관객 배치

--- 플레이어 주변에 관객 배치
---@param playerSeatId string 플레이어가 앉은 좌석 ID
function SpawnAudienceNearPlayer(playerSeatId)
    Debug.Log("[AudienceManager] SpawnAudienceNearPlayer: " .. tostring(playerSeatId))

    if not isVisible then
        Debug.Log("[AudienceManager] Audience display is off")
        return
    end

    if arenaXManager == nil then
        Debug.LogError("[AudienceManager] ArenaXManager is nil")
        return
    end

    -- 기존 관객 제거
    ClearActiveAudience()

    -- 플레이어 좌석 정보 가져오기
    local playerSeat = arenaXManager.GetSeatInfo(playerSeatId)
    if playerSeat == nil then
        Debug.LogWarning("[AudienceManager] Player seat not found: " .. playerSeatId)
        return
    end

    playerSeatRow = playerSeat.row
    playerSeatNumber = playerSeat.number

    -- 앞 열 좌석들 찾기
    local frontSeats = GetFrontRowSeats(playerSeat)

    -- 관객 배치
    local spawnedCount = 0
    for _, seatData in ipairs(frontSeats) do
        if spawnedCount >= MaxVisibleAudience then
            break
        end

        -- 플레이어 좌석은 제외
        if seatData.seatId ~= playerSeatId then
            SpawnAudienceAtSeat(seatData)
            spawnedCount = spawnedCount + 1
        end
    end

    Debug.Log("[AudienceManager] Spawned " .. spawnedCount .. " audience members")
end

--- 앞 열 좌석들 가져오기
---@param playerSeat SeatData
---@return SeatData[]
function GetFrontRowSeats(playerSeat)
    local frontSeats = {}

    if arenaXManager == nil then return frontSeats end

    local allSeats = arenaXManager.GetAllSeats()
    local playerRowIndex = GetRowIndex(playerSeat.row)

    for seatId, seatData in pairs(allSeats) do
        local seatRowIndex = GetRowIndex(seatData.row)

        -- 플레이어보다 앞 열이고, FrontRowsToFill 범위 내인 경우
        if seatRowIndex < playerRowIndex and
           playerRowIndex - seatRowIndex <= FrontRowsToFill then

            -- 플레이어 좌석 근처 범위 (좌우 3칸 이내)
            local numberDiff = math.abs(seatData.number - playerSeat.number)
            if numberDiff <= 3 then
                table.insert(frontSeats, seatData)
            end
        end
    end

    return frontSeats
end

--- 열 문자를 인덱스로 변환 (A=1, B=2, ...)
---@param row string
---@return int
function GetRowIndex(row)
    if row == nil or #row == 0 then return 0 end
    return string.byte(row:upper()) - string.byte('A') + 1
end

--- 특정 좌석에 관객 배치
---@param seatData SeatData
function SpawnAudienceAtSeat(seatData)
    local audience = GetFromPool()
    if audience == nil then
        Debug.LogWarning("[AudienceManager] Could not get audience from pool")
        return
    end

    -- 위치 및 회전 설정
    audience.transform.position = seatData.position
    audience.transform.rotation = seatData.rotation

    -- 활성화
    audience:SetActive(true)

    -- 활성 목록에 추가
    activeAudience[seatData.seatId] = audience
end

--endregion

--region 관객 제거

--- 모든 활성 관객 제거 (풀로 반환)
function ClearActiveAudience()
    Debug.Log("[AudienceManager] ClearActiveAudience")

    for seatId, audience in pairs(activeAudience) do
        ReturnToPool(audience)
    end

    activeAudience = {}
end

--- 모든 관객 제거 (외부 호출용)
function ClearAudience()
    Debug.Log("[AudienceManager] ClearAudience")
    ClearActiveAudience()
end

--endregion

--region 토글 기능

--- 관객 표시 토글
---@param show boolean
function ToggleAudience(show)
    Debug.Log("[AudienceManager] ToggleAudience: " .. tostring(show))

    isVisible = show

    if show then
        -- 현재 플레이어 좌석이 있으면 관객 배치
        if arenaXManager ~= nil then
            local playerState = arenaXManager.GetPlayerState()
            if playerState.isSeated and playerState.currentSeatId ~= nil then
                SpawnAudienceNearPlayer(playerState.currentSeatId)
            end
        end
    else
        -- 관객 숨기기
        ClearActiveAudience()
    end
end

--- 관객 표시 상태 조회
---@return boolean
function IsVisible()
    return isVisible
end

--endregion

--region 유틸리티

--- 활성 관객 수 조회
---@return int
function GetActiveAudienceCount()
    local count = 0
    for _ in pairs(activeAudience) do
        count = count + 1
    end
    return count
end

--- 관객 외형 랜덤화 (선택사항)
---@param audience GameObject
function RandomizeAppearance(audience)
    -- TODO: 다양한 외형 적용
    -- - 머리 색상
    -- - 의상 색상
    -- - 체형 등
end

--endregion
