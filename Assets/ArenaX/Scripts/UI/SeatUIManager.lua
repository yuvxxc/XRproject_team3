--[[
    SeatUIManager.lua
    미니맵 UI 및 좌석 선택 인터페이스 관리

    기능:
    - 미니맵에 좌석 배치도 표시
    - 좌석 버튼 클릭으로 해당 좌석 선택
    - 현재 착석한 좌석 강조 표시
    - 관객 표시 토글 버튼
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
ArenaXManagerName = "ArenaXManager"

---@type GameObject
---@details 미니맵 Canvas (비워두면 자동으로 "MinimapCanvas" 찾음)
MinimapCanvas = NullableInject(MinimapCanvas)

---@type string
---@details 미니맵 Canvas 오브젝트 이름 (자동 찾기용)
MinimapCanvasName = "MinimapCanvas"

---@type GameObject
---@details 좌석 버튼 컨테이너 (비워두면 자동으로 "SeatButtonContainer" 찾음)
SeatButtonContainer = NullableInject(SeatButtonContainer)

---@type string
---@details 좌석 버튼 컨테이너 이름 (자동 찾기용)
SeatButtonContainerName = "SeatButtonContainer"

---@type GameObject
---@details 좌석 버튼 프리팹 (필수 - 프리팹은 자동으로 찾을 수 없음)
SeatButtonPrefab = NullableInject(SeatButtonPrefab)

---@type GameObject
---@details 정보 패널 (좌석 정보 표시)
InfoPanel = NullableInject(InfoPanel)

---@type GameObject
---@details 현재 좌석 정보 텍스트
CurrentSeatText = NullableInject(CurrentSeatText)

---@type GameObject
---@details 관객 토글 버튼
AudienceToggleButton = NullableInject(AudienceToggleButton)

---@type Transform
---@details 플레이어 카메라 (UI가 따라다닐 대상, 비워두면 Camera.main 사용)
PlayerCamera = NullableInject(PlayerCamera)

---@type string
---@details UI 팔로우 모드: "follow" (시선 따라감), "wrist" (손목 부착), "toggle" (호출형), "fixed" (고정)
UIFollowMode = "toggle"

---@type float
---@details UI가 플레이어 앞에 위치할 거리
UIDistance = 1.5

---@type float
---@details UI 팔로우 속도 (부드러운 이동)
UIFollowSpeed = 5.0

---@type float
---@details UI가 나타날 때 플레이어 앞 높이 오프셋
UIHeightOffset = -0.3
--endregion

-- 모듈 임포트
local util = require 'xlua.util'

-- 컴포넌트 참조
local arenaXManager = nil

-- 좌석 버튼 맵 (seatId -> Button GameObject)
---@type table<string, GameObject>
local seatButtons = {}

-- 현재 선택/강조된 좌석 ID
local highlightedSeatId = nil

-- UI 상태
local isUIVisible = false
local targetPosition = nil
local targetRotation = nil

-- 디버그용 타이머
local debugTimer = 0
local DEBUG_INTERVAL = 2.0  -- 2초 간격으로 로그 출력

-- UI 색상 설정
local UI_COLORS = {
    normal = Color(0.3, 0.3, 0.3, 1),      -- 회색 (기본)
    selected = Color(0.2, 0.6, 1, 1),       -- 파란색 (선택됨)
    occupied = Color(0.8, 0.2, 0.2, 1),     -- 빨간색 (착석 중)
    available = Color(0.2, 0.8, 0.2, 1),    -- 초록색 (선택 가능)
    disabled = Color(0.5, 0.5, 0.5, 0.5)    -- 회색 반투명 (비활성)
}

--region 생명주기 함수

function awake()
    Debug.Log("[SeatUIManager] Awake")
end

function start()
    Debug.Log("[SeatUIManager] Start")

    -- 자동 찾기 실행
    FindRequiredObjects()

    -- ArenaXManager 참조 가져오기
    if ArenaXManagerObject ~= nil then
        arenaXManager = ArenaXManagerObject:GetLuaComponent("ArenaXManager")
        if arenaXManager ~= nil then
            Debug.Log("[SeatUIManager] ArenaXManager connected")
        end
    end

    -- 플레이어 카메라 자동 찾기 (주입 안된 경우)
    if PlayerCamera == nil then
        local mainCam = Camera.main
        if mainCam ~= nil then
            PlayerCamera = mainCam.transform
            Debug.Log("[SeatUIManager] PlayerCamera found via Camera.main")
        end
    end

    -- UI 초기화
    InitializeUI()

    -- 토글 모드일 경우 시작 시 숨김
    if UIFollowMode == "toggle" and MinimapCanvas ~= nil then
        SetMinimapVisible(false)
    end
end

--- 필요한 오브젝트들 자동 찾기
function FindRequiredObjects()
    -- ArenaXManager 찾기
    if ArenaXManagerObject == nil then
        ArenaXManagerObject = GameObject.Find(ArenaXManagerName)
        if ArenaXManagerObject ~= nil then
            Debug.Log("[SeatUIManager] ArenaXManager found: " .. ArenaXManagerName)
        else
            Debug.LogWarning("[SeatUIManager] ArenaXManager not found: " .. ArenaXManagerName)
        end
    end

    -- MinimapCanvas 찾기
    if MinimapCanvas == nil then
        MinimapCanvas = GameObject.Find(MinimapCanvasName)
        if MinimapCanvas ~= nil then
            Debug.Log("[SeatUIManager] MinimapCanvas found: " .. MinimapCanvasName)
        else
            Debug.LogWarning("[SeatUIManager] MinimapCanvas not found: " .. MinimapCanvasName)
        end
    end

    -- SeatButtonContainer 찾기
    if SeatButtonContainer == nil then
        SeatButtonContainer = GameObject.Find(SeatButtonContainerName)
        if SeatButtonContainer ~= nil then
            Debug.Log("[SeatUIManager] SeatButtonContainer found: " .. SeatButtonContainerName)
        else
            Debug.LogWarning("[SeatUIManager] SeatButtonContainer not found: " .. SeatButtonContainerName)
        end
    end

    -- SeatButtonPrefab은 프리팹이라 자동으로 찾을 수 없음
    if SeatButtonPrefab == nil then
        Debug.LogWarning("[SeatUIManager] SeatButtonPrefab not assigned - seat buttons will not be generated")
    end
end

--- 매 프레임 UI 위치 업데이트
function update()
    if not isUIVisible then return end
    if MinimapCanvas == nil then return end

    if UIFollowMode == "follow" then
        UpdateFollowUI()
    end
end

--- 시선 따라가는 UI 업데이트
function UpdateFollowUI()
    if PlayerCamera == nil then return end

    -- 목표 위치 계산 (플레이어 앞)
    local camForward = PlayerCamera.forward
    camForward.y = 0  -- 수평으로만
    camForward = camForward.normalized

    local targetPos = PlayerCamera.position + camForward * UIDistance
    targetPos.y = PlayerCamera.position.y + UIHeightOffset

    -- 부드럽게 이동
    MinimapCanvas.transform.position = Vector3.Lerp(
        MinimapCanvas.transform.position,
        targetPos,
        Time.deltaTime * UIFollowSpeed
    )

    -- 플레이어를 바라보도록 회전
    local lookDir = PlayerCamera.position - MinimapCanvas.transform.position
    lookDir.y = 0
    if lookDir.magnitude > 0.01 then
        MinimapCanvas.transform.rotation = Quaternion.LookRotation(-lookDir)
    end

    -- 2초 간격 디버그 로그
    debugTimer = debugTimer + Time.deltaTime
    if debugTimer >= DEBUG_INTERVAL then
        debugTimer = 0
        local uiPos = MinimapCanvas.transform.position
        local playerPos = PlayerCamera.position
        local offset = uiPos - playerPos
        Debug.Log(string.format(
            "[SeatUIManager] DEBUG - Player: (%.2f, %.2f, %.2f) | UI: (%.2f, %.2f, %.2f) | Offset: (%.2f, %.2f, %.2f) | Distance: %.2f",
            playerPos.x, playerPos.y, playerPos.z,
            uiPos.x, uiPos.y, uiPos.z,
            offset.x, offset.y, offset.z,
            offset.magnitude
        ))
    end
end

function onEnable()
    Debug.Log("[SeatUIManager] OnEnable")

    -- 관객 토글 버튼 이벤트 등록
    if AudienceToggleButton ~= nil then
        local button = AudienceToggleButton:GetComponent("Button")
        if button ~= nil then
            button.onClick:AddListener(OnAudienceToggleClicked)
        end
    end
end

function onDisable()
    Debug.Log("[SeatUIManager] OnDisable")

    -- 이벤트 해제
    if AudienceToggleButton ~= nil then
        local button = AudienceToggleButton:GetComponent("Button")
        if button ~= nil then
            button.onClick:RemoveListener(OnAudienceToggleClicked)
        end
    end
end

--endregion

--region UI 초기화

--- UI 초기화
function InitializeUI()
    Debug.Log("[SeatUIManager] InitializeUI")

    -- 기존 버튼 제거
    ClearSeatButtons()

    -- 좌석 버튼 생성은 ArenaXManager에서 좌석 데이터를 받은 후 수행
    -- 또는 수동으로 호출
end

--- 좌석 데이터 기반으로 버튼 생성
---@param seats table<string, SeatData>
function GenerateSeatButtons(seats)
    Debug.Log("[SeatUIManager] GenerateSeatButtons")

    ClearSeatButtons()

    for seatId, seatData in pairs(seats) do
        CreateSeatButton(seatId, seatData)
    end
end

--- 개별 좌석 버튼 생성
---@param seatId string
---@param seatData SeatData
function CreateSeatButton(seatId, seatData)
    if SeatButtonPrefab == nil or SeatButtonContainer == nil then
        Debug.LogError("[SeatUIManager] SeatButtonPrefab or Container is nil")
        return
    end

    -- 버튼 인스턴스 생성
    local buttonObj = GameObject.Instantiate(SeatButtonPrefab, SeatButtonContainer.transform)
    buttonObj.name = "SeatButton_" .. seatId

    -- 버튼 텍스트 설정
    local textComp = buttonObj:GetComponentInChildren(typeof(TMP_Text))
    if textComp ~= nil then
        textComp.text = seatId
    end

    -- 버튼 클릭 이벤트 등록
    local button = buttonObj:GetComponent("Button")
    if button ~= nil then
        -- 클로저로 seatId 캡처
        local capturedSeatId = seatId
        button.onClick:AddListener(function()
            OnSeatButtonClicked(capturedSeatId)
        end)
    end

    -- 버튼 색상 설정
    UpdateButtonColor(buttonObj, seatData)

    -- 맵에 저장
    seatButtons[seatId] = buttonObj
end

--- 모든 좌석 버튼 제거
function ClearSeatButtons()
    for seatId, buttonObj in pairs(seatButtons) do
        if buttonObj ~= nil then
            GameObject.Destroy(buttonObj)
        end
    end
    seatButtons = {}
end

--endregion

--region 이벤트 핸들러

--- 좌석 버튼 클릭 시
---@param seatId string
function OnSeatButtonClicked(seatId)
    Debug.Log("[SeatUIManager] OnSeatButtonClicked: " .. seatId)

    if arenaXManager ~= nil then
        arenaXManager.SelectSeat(seatId)
    end
end

--- 관객 토글 버튼 클릭 시
function OnAudienceToggleClicked()
    Debug.Log("[SeatUIManager] OnAudienceToggleClicked")

    if arenaXManager ~= nil then
        local currentState = arenaXManager.IsAudienceVisible()
        arenaXManager.ToggleAudience(not currentState)

        -- 토글 버튼 텍스트 업데이트
        UpdateAudienceToggleText(not currentState)
    end
end

--endregion

--region UI 업데이트

--- 현재 좌석 강조 표시
---@param seatId string
function HighlightCurrentSeat(seatId)
    Debug.Log("[SeatUIManager] HighlightCurrentSeat: " .. seatId)

    -- 이전 강조 해제
    if highlightedSeatId ~= nil and seatButtons[highlightedSeatId] ~= nil then
        local prevButton = seatButtons[highlightedSeatId]
        SetButtonColor(prevButton, UI_COLORS.normal)
    end

    -- 새 좌석 강조
    if seatButtons[seatId] ~= nil then
        local button = seatButtons[seatId]
        SetButtonColor(button, UI_COLORS.selected)
    end

    highlightedSeatId = seatId

    -- 정보 패널 업데이트
    UpdateInfoPanel(seatId)
end

--- 강조 표시 해제
function ClearHighlight()
    Debug.Log("[SeatUIManager] ClearHighlight")

    if highlightedSeatId ~= nil and seatButtons[highlightedSeatId] ~= nil then
        local button = seatButtons[highlightedSeatId]
        SetButtonColor(button, UI_COLORS.normal)
    end

    highlightedSeatId = nil

    -- 정보 패널 초기화
    if CurrentSeatText ~= nil then
        local text = CurrentSeatText:GetComponent("TMP_Text")
        if text ~= nil then
            text.text = "좌석을 선택하세요"
        end
    end
end

--- 정보 패널 업데이트
---@param seatId string
function UpdateInfoPanel(seatId)
    if InfoPanel == nil then return end

    if arenaXManager == nil then return end

    local seatData = arenaXManager.GetSeatInfo(seatId)
    if seatData == nil then return end

    -- 현재 좌석 텍스트 업데이트
    if CurrentSeatText ~= nil then
        local text = CurrentSeatText:GetComponent("TMP_Text")
        if text ~= nil then
            text.text = string.format("%s구역 %s열 %d번",
                seatData.section, seatData.row, seatData.number)
        end
    end
end

--- 관객 토글 버튼 텍스트 업데이트
---@param isVisible boolean
function UpdateAudienceToggleText(isVisible)
    if AudienceToggleButton == nil then return end

    local textComp = AudienceToggleButton:GetComponentInChildren(typeof(TMP_Text))
    if textComp ~= nil then
        if isVisible then
            textComp.text = "관객 숨기기"
        else
            textComp.text = "관객 보기"
        end
    end
end

--- 버튼 색상 설정
---@param buttonObj GameObject
---@param color Color
function SetButtonColor(buttonObj, color)
    if buttonObj == nil then return end

    local image = buttonObj:GetComponent("Image")
    if image ~= nil then
        image.color = color
    end
end

--- 좌석 데이터 기반 버튼 색상 업데이트
---@param buttonObj GameObject
---@param seatData SeatData
function UpdateButtonColor(buttonObj, seatData)
    local color = UI_COLORS.normal

    if not seatData.isAvailable then
        color = UI_COLORS.disabled
    elseif seatData.isOccupied then
        color = UI_COLORS.occupied
    else
        color = UI_COLORS.available
    end

    SetButtonColor(buttonObj, color)
end

--- 전체 미니맵 업데이트
function UpdateMinimap()
    Debug.Log("[SeatUIManager] UpdateMinimap")

    if arenaXManager == nil then return end

    local seats = arenaXManager.GetAllSeats()
    for seatId, seatData in pairs(seats) do
        local buttonObj = seatButtons[seatId]
        if buttonObj ~= nil then
            -- 현재 강조된 좌석은 유지
            if seatId ~= highlightedSeatId then
                UpdateButtonColor(buttonObj, seatData)
            end
        end
    end
end

--endregion

--region 유틸리티

--- 미니맵 표시/숨기기
---@param show boolean
function SetMinimapVisible(show)
    if MinimapCanvas == nil then return end

    isUIVisible = show

    if show then
        -- UI를 플레이어 앞에 배치
        PositionUIInFrontOfPlayer()
    end

    MinimapCanvas:SetActive(show)

    Debug.Log("[SeatUIManager] SetMinimapVisible: " .. tostring(show))
end

--- 미니맵 토글
function ToggleMinimap()
    SetMinimapVisible(not isUIVisible)
end

--- UI를 플레이어 앞에 배치 (토글 모드용)
function PositionUIInFrontOfPlayer()
    if MinimapCanvas == nil then return end
    if PlayerCamera == nil then return end

    -- 플레이어 앞에 배치
    local camForward = PlayerCamera.forward
    camForward.y = 0
    camForward = camForward.normalized

    local targetPos = PlayerCamera.position + camForward * UIDistance
    targetPos.y = PlayerCamera.position.y + UIHeightOffset

    MinimapCanvas.transform.position = targetPos

    -- 플레이어를 바라보도록 회전
    local lookDir = PlayerCamera.position - targetPos
    lookDir.y = 0
    if lookDir.magnitude > 0.01 then
        MinimapCanvas.transform.rotation = Quaternion.LookRotation(-lookDir)
    end
end

--- UI 표시 상태 확인
---@return boolean
function IsMinimapVisible()
    return isUIVisible
end

--- UI 팔로우 모드 변경
---@param mode string "follow" | "wrist" | "toggle" | "fixed"
function SetFollowMode(mode)
    UIFollowMode = mode
    Debug.Log("[SeatUIManager] SetFollowMode: " .. mode)

    -- 모드 변경 시 UI 재배치
    if isUIVisible then
        PositionUIInFrontOfPlayer()
    end
end

--endregion
