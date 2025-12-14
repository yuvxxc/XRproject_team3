--[[
    AudienceManager.lua
    가상 관객 관리자 (Object Pooling 방식)

    VIVEN SDK에서 동적 VObject Instantiate가 불가능하므로
    씬에 미리 배치된 오브젝트를 풀에서 가져와 재사용

    구조:
    - 5개 관객 타입 (AudienceType1~5)
    - 각 타입당 10개 인스턴스 = 총 50명
    - MeshRenderer/Collider 토글 방식 (SetActive 대신)

    사용법:
    1. 씬에 AudiencePool 오브젝트 생성
    2. 하위에 Type1Pool ~ Type5Pool 생성
    3. 각 풀에 앉은 관객 오브젝트 10개씩 배치
    4. AudienceSetup.cs 에디터 도구로 자동 설정 가능
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
---@details ArenaXManager가 있는 오브젝트 (비워두면 자동으로 찾음)
ArenaXManagerObject = NullableInject(ArenaXManagerObject)

---@type string
---@details ArenaXManager 오브젝트 이름 (자동 찾기용)
ArenaXManagerName = ArenaXManagerName or "ArenaXManager"

-- 풀 부모 오브젝트 (타입별)
---@type GameObject
---@details Type1 관객 풀 부모 오브젝트
Type1Pool = NullableInject(Type1Pool)

---@type GameObject
---@details Type2 관객 풀 부모 오브젝트
Type2Pool = NullableInject(Type2Pool)

---@type GameObject
---@details Type3 관객 풀 부모 오브젝트
Type3Pool = NullableInject(Type3Pool)

---@type GameObject
---@details Type4 관객 풀 부모 오브젝트
Type4Pool = NullableInject(Type4Pool)

---@type GameObject
---@details Type5 관객 풀 부모 오브젝트
Type5Pool = NullableInject(Type5Pool)

---@type int
---@details 플레이어 앞에 배치할 관객 수 (최대)
MaxAudienceToShow = MaxAudienceToShow or 20
--endregion

--region Variables

local util = require 'xlua.util'

---@type any
---@details ArenaXManager 참조
local arenaXManager = nil

---@type boolean
---@details 관객 표시 상태
local isVisible = false

---@type boolean
---@details 초기화 완료 여부
local isInitialized = false

---@type Vector3
---@details 숨김 위치 (풀에서 비활성화 시 이동할 위치)
local HIDE_POSITION = nil

--endregion

--region Pool Variables

---@type table<string, table>
---@details 타입별 풀 테이블 {available = {인덱스...}, inUse = {인덱스...}}
local pools = {
    Type1 = { available = {}, inUse = {} },
    Type2 = { available = {}, inUse = {} },
    Type3 = { available = {}, inUse = {} },
    Type4 = { available = {}, inUse = {} },
    Type5 = { available = {}, inUse = {} }
}

---@type table<string, table>
---@details 타입별 오브젝트 테이블 (GameObject 배열)
local poolObjects = {
    Type1 = {},
    Type2 = {},
    Type3 = {},
    Type4 = {},
    Type5 = {}
}

---@type table<string, table>
---@details 타입별 MeshRenderer 테이블 (배열의 배열)
local poolMeshRenderers = {
    Type1 = {},
    Type2 = {},
    Type3 = {},
    Type4 = {},
    Type5 = {}
}

---@type table<string, table>
---@details 타입별 Collider 테이블 (배열의 배열)
local poolColliders = {
    Type1 = {},
    Type2 = {},
    Type3 = {},
    Type4 = {},
    Type5 = {}
}

---@type table<string, table>
---@details 타입별 초기 위치/회전 저장 테이블
local poolInitialPose = {
    Type1 = {},
    Type2 = {},
    Type3 = {},
    Type4 = {},
    Type5 = {}
}

---@type table<string, GameObject>
---@details 타입별 풀 부모 매핑
local poolParents = {}

---@type string[]
---@details 타입 이름 배열
local typeNames = { "Type1", "Type2", "Type3", "Type4", "Type5" }

---@type table
---@details 현재 활성화된 관객 정보 {typeName, poolIndex, seatTransform}
local activeAudience = {}

--endregion

--region Unity Lifecycle

function awake()
    -- Pool injection 체크 - 하나도 없으면 스킵 (테스트 모드)
    if not Type1Pool and not Type2Pool and not Type3Pool and not Type4Pool and not Type5Pool then
        Debug.Log("[AudienceManager] No pool parents assigned - skipping initialization")
        isInitialized = false
        return
    end

    -- 숨김 위치 초기화 (아주 먼 곳)
    HIDE_POSITION = Vector3(0, -9999, 0)

    -- 풀 부모 매핑 설정
    poolParents = {
        Type1 = Type1Pool,
        Type2 = Type2Pool,
        Type3 = Type3Pool,
        Type4 = Type4Pool,
        Type5 = Type5Pool
    }

    -- 풀 초기화
    InitializePools()

    isInitialized = true
    Debug.Log("[AudienceManager] Initialized with pooling")
end

function start()
    if not isInitialized then return end

    -- ArenaXManager 참조 가져오기
    FindArenaXManager()

    Debug.Log("[AudienceManager] Start complete")
end

function onEnable()
    if not isInitialized then return end
end

function onDisable()
    if not isInitialized then return end
    -- 모든 관객 풀로 반환
    ReturnAllToPool()
end

--endregion

--region ArenaXManager 연결

--- ArenaXManager 찾기
function FindArenaXManager()
    if arenaXManager ~= nil then return end

    -- 1. Injection으로 연결된 경우
    if ArenaXManagerObject ~= nil then
        arenaXManager = ArenaXManagerObject:GetLuaComponent("ArenaXManager")
        if arenaXManager ~= nil then
            Debug.Log("[AudienceManager] ArenaXManager connected via injection")
            return
        end
    end

    -- 2. GameObject.Find로 찾기
    local managerObj = GameObject.Find(ArenaXManagerName)
    if managerObj ~= nil then
        arenaXManager = managerObj:GetLuaComponent("ArenaXManager")
        if arenaXManager ~= nil then
            Debug.Log("[AudienceManager] ArenaXManager found: " .. ArenaXManagerName)
            return
        end
    end

    Debug.Log("[AudienceManager] ArenaXManager not found")
end

--endregion

--region Pool Management

---@details 자식 오브젝트 수집 유틸리티 함수
---@param parentObj GameObject 부모 오브젝트
---@param objTable table 오브젝트 저장 테이블
function GetChildren(parentObj, objTable)
    for i = 1, #objTable do objTable[i] = nil end

    for i = 0, parentObj.transform.childCount - 1 do
        local child = parentObj.transform:GetChild(i).gameObject
        objTable[#objTable + 1] = child
    end
end

---@details 모든 풀 초기화
function InitializePools()
    for _, typeName in ipairs(typeNames) do
        local poolParent = poolParents[typeName]
        if poolParent then
            InitializePool(typeName, poolParent)
        end
    end

    local totalCount = 0
    for _, typeName in ipairs(typeNames) do
        totalCount = totalCount + #poolObjects[typeName]
    end
    Debug.Log("[AudienceManager] Total pooled objects: " .. totalCount)
end

---@details 단일 타입 풀 초기화
---@param typeName string 타입명
---@param poolParent GameObject 풀 부모 오브젝트
function InitializePool(typeName, poolParent)
    if not poolParent then return end

    -- 기존 테이블 초기화
    poolObjects[typeName] = {}
    poolMeshRenderers[typeName] = {}
    poolColliders[typeName] = {}
    poolInitialPose[typeName] = {}
    pools[typeName].available = {}
    pools[typeName].inUse = {}

    -- GetChildren으로 자식 오브젝트 수집
    GetChildren(poolParent, poolObjects[typeName])

    -- 초기 위치/회전 저장 및 풀에 추가
    for i, obj in ipairs(poolObjects[typeName]) do
        -- 초기 위치 저장
        poolInitialPose[typeName][i] = {
            Pos = obj.transform.position,
            Rot = obj.transform.rotation
        }

        -- MeshRenderer 수집
        local meshRenderers = obj:GetComponentsInChildren(typeof(CS.UnityEngine.MeshRenderer))
        local tempMeshes = {}
        for j = 0, meshRenderers.Length - 1 do
            tempMeshes[#tempMeshes + 1] = meshRenderers[j]
        end
        poolMeshRenderers[typeName][i] = tempMeshes

        -- SkinnedMeshRenderer도 수집 (캐릭터 모델용)
        local skinnedRenderers = obj:GetComponentsInChildren(typeof(CS.UnityEngine.SkinnedMeshRenderer))
        for j = 0, skinnedRenderers.Length - 1 do
            poolMeshRenderers[typeName][i][#poolMeshRenderers[typeName][i] + 1] = skinnedRenderers[j]
        end

        -- Collider 수집
        local colliders = obj:GetComponentsInChildren(typeof(CS.UnityEngine.Collider))
        local tempColliders = {}
        for j = 0, colliders.Length - 1 do
            tempColliders[#tempColliders + 1] = colliders[j]
        end
        poolColliders[typeName][i] = tempColliders

        -- available 풀에 추가
        table.insert(pools[typeName].available, i)

        -- 비활성화 (MeshRenderer/Collider 끄기 + 위치 이동)
        SetPoolObjectVisible(typeName, i, false)
    end

    Debug.Log("[AudienceManager] Pool initialized: " .. typeName .. " (" .. #poolObjects[typeName] .. " objects)")
end

---@details 풀 오브젝트 가시성 설정 (SetActive 대신 사용)
---@param typeName string 타입명
---@param poolIndex number 풀 인덱스
---@param visible boolean 가시성 여부
function SetPoolObjectVisible(typeName, poolIndex, visible)
    local obj = poolObjects[typeName][poolIndex]
    if not obj then return end

    -- MeshRenderer 활성화/비활성화
    local meshRenderers = poolMeshRenderers[typeName][poolIndex]
    if meshRenderers then
        for _, mr in ipairs(meshRenderers) do
            mr.enabled = visible
        end
    end

    -- Collider 활성화/비활성화
    local colliders = poolColliders[typeName][poolIndex]
    if colliders then
        for _, col in ipairs(colliders) do
            col.enabled = visible
        end
    end

    -- 숨김 위치로 이동 (비활성화 시)
    if not visible then
        obj.transform.position = HIDE_POSITION
    end
end

---@details 풀에서 오브젝트 가져오기 (랜덤 타입)
---@return GameObject|nil, string, number 오브젝트, 타입명, 인덱스
function GetFromPoolRandom()
    -- 사용 가능한 타입 찾기
    local availableTypes = {}
    for _, typeName in ipairs(typeNames) do
        if #pools[typeName].available > 0 then
            table.insert(availableTypes, typeName)
        end
    end

    if #availableTypes == 0 then
        Debug.Log("[AudienceManager] All pools exhausted")
        return nil, nil, -1
    end

    -- 랜덤 타입 선택
    local randomIndex = math.random(1, #availableTypes)
    local typeName = availableTypes[randomIndex]

    return GetFromPool(typeName)
end

---@details 특정 타입 풀에서 오브젝트 가져오기
---@param typeName string 타입명
---@return GameObject|nil, string, number 오브젝트, 타입명, 인덱스
function GetFromPool(typeName)
    local pool = pools[typeName]
    if not pool or #pool.available == 0 then
        return nil, typeName, -1
    end

    -- available에서 하나 가져오기
    local index = pool.available[1]
    table.remove(pool.available, 1)
    table.insert(pool.inUse, index)

    local obj = poolObjects[typeName][index]

    return obj, typeName, index
end

---@details 풀로 오브젝트 반환
---@param typeName string 타입명
---@param poolIndex number 풀 내 인덱스
function ReturnToPool(typeName, poolIndex)
    local pool = pools[typeName]
    if not pool then return end

    -- inUse에서 제거
    for i = #pool.inUse, 1, -1 do
        if pool.inUse[i] == poolIndex then
            table.remove(pool.inUse, i)
            break
        end
    end

    -- 이미 available에 있는지 확인
    for _, idx in ipairs(pool.available) do
        if idx == poolIndex then
            return
        end
    end

    -- available에 추가
    table.insert(pool.available, poolIndex)

    -- 비활성화
    SetPoolObjectVisible(typeName, poolIndex, false)
end

---@details 모든 오브젝트를 풀로 반환
function ReturnAllToPool()
    for _, audienceInfo in ipairs(activeAudience) do
        ReturnToPool(audienceInfo.typeName, audienceInfo.poolIndex)
    end
    activeAudience = {}
    Debug.Log("[AudienceManager] All audience returned to pool")
end

--endregion

--region Public Functions (외부 호출용)

---@details 관객 표시 토글 (외부 호출용)
---@param show boolean 표시 여부
function ToggleAudience(show)
    Debug.Log("[AudienceManager] ToggleAudience: " .. tostring(show))

    isVisible = show

    if show then
        -- 현재 플레이어 좌석 정보로 관객 배치
        if arenaXManager ~= nil then
            local playerState = arenaXManager.GetPlayerState()
            if playerState and playerState.isSeated and playerState.currentSeatId ~= nil then
                -- 현재 좌석의 앞좌석 Transform 가져오기
                local frontSeatTransforms = arenaXManager.GetFrontSeatTransforms(playerState.currentSeatId)
                if frontSeatTransforms and #frontSeatTransforms > 0 then
                    SpawnAudienceAtTransforms(frontSeatTransforms)
                else
                    Debug.Log("[AudienceManager] No front seat transforms found")
                end
            else
                Debug.Log("[AudienceManager] Player not seated, skipping audience spawn")
            end
        end
    else
        -- 관객 숨기기
        ReturnAllToPool()
    end
end

---@details 지정된 Transform들에 관객 배치
---@param transforms Transform[] 좌석 Transform 배열
function SpawnAudienceAtTransforms(transforms)
    -- 기존 관객 제거
    ReturnAllToPool()

    local spawnCount = math.min(#transforms, MaxAudienceToShow)

    for i = 1, spawnCount do
        local seatTransform = transforms[i]
        if seatTransform then
            local obj, typeName, poolIndex = GetFromPoolRandom()
            if obj then
                -- 위치/회전 설정
                obj.transform.position = seatTransform.position
                obj.transform.rotation = seatTransform.rotation

                -- 활성화
                SetPoolObjectVisible(typeName, poolIndex, true)

                -- 활성 목록에 추가
                table.insert(activeAudience, {
                    typeName = typeName,
                    poolIndex = poolIndex,
                    transform = seatTransform
                })
            end
        end
    end

    Debug.Log("[AudienceManager] Spawned " .. #activeAudience .. " audience members")
end

---@details 좌석 ID 기반 관객 배치 (ArenaXManager에서 호출)
---@param seatId string 좌석 ID
function SpawnAudienceNearPlayer(seatId)
    if not isVisible then
        Debug.Log("[AudienceManager] Audience display is off")
        return
    end

    if arenaXManager == nil then
        FindArenaXManager()
        if arenaXManager == nil then
            Debug.Log("[AudienceManager] ArenaXManager not found")
            return
        end
    end

    -- 해당 좌석의 앞좌석 Transform 가져오기
    local frontSeatTransforms = arenaXManager.GetFrontSeatTransforms(seatId)
    if frontSeatTransforms and #frontSeatTransforms > 0 then
        SpawnAudienceAtTransforms(frontSeatTransforms)
    else
        Debug.Log("[AudienceManager] No front seat transforms for: " .. tostring(seatId))
    end
end

---@details 관객 전체 제거 (ArenaXManager에서 호출)
function ClearAudience()
    ReturnAllToPool()
end

---@details 관객 표시 상태 조회
---@return boolean
function IsVisible()
    return isVisible
end

---@details 활성 관객 수 조회
---@return int
function GetActiveAudienceCount()
    return #activeAudience
end

---@details 풀 상태 조회 (디버그용)
---@return table
function GetPoolStatus()
    local status = {}
    for _, typeName in ipairs(typeNames) do
        status[typeName] = {
            available = #pools[typeName].available,
            inUse = #pools[typeName].inUse,
            total = #poolObjects[typeName]
        }
    end
    return status
end

--endregion
