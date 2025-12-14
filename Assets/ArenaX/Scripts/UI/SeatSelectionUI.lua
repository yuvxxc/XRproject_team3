--[[
    SeatSelectionUI.lua
    좌석 선택 UI 컨트롤러

    구성:
    - 왼쪽: 공연장 미니맵 이미지
    - 중앙: 드롭다운 필터 (Block, District, Number)
    - 오른쪽: 필터된 좌석 버튼 그리드

    UI 구조:
    SeatSelectionCanvas (World Space)
    ├── Background Panel
    ├── LeftPanel (미니맵 이미지)
    ├── CenterPanel (드롭다운 필터)
    │   ├── BlockDropdown
    │   ├── DistrictDropdown
    │   └── SelectButton
    └── RightPanel (좌석 버튼 그리드)
        └── SeatButtonGrid (Grid Layout Group)
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
---@details ArenaXManager 오브젝트 (비워두면 자동으로 찾음)
ArenaXManagerObject = NullableInject(ArenaXManagerObject)

---@type string
---@details ArenaXManager 오브젝트 이름
ArenaXManagerName = "ArenaXManager"

---@type GameObject
---@details Block 드롭다운 (TMP_Dropdown)
BlockDropdown = NullableInject(BlockDropdown)

---@type GameObject
---@details District(열) 드롭다운 (TMP_Dropdown)
DistrictDropdown = NullableInject(DistrictDropdown)

---@type GameObject
---@details 좌석 번호 드롭다운 (TMP_Dropdown) - 선택사항
NumberDropdown = NullableInject(NumberDropdown)

---@type GameObject
---@details 선택 버튼
SelectButton = NullableInject(SelectButton)

---@type GameObject
---@details 좌석 버튼 그리드 컨테이너 (Grid Layout Group)
SeatButtonGrid = NullableInject(SeatButtonGrid)

---@type GameObject
---@details 좌석 버튼 프리팹
SeatButtonPrefab = NullableInject(SeatButtonPrefab)

---@type GameObject
---@details 미니맵 이미지 (선택사항)
MinimapImage = NullableInject(MinimapImage)

---@type GameObject
---@details 선택된 좌석 정보 텍스트
SelectedSeatText = NullableInject(SelectedSeatText)

---@type GameObject
---@details UI Canvas (토글용)
UICanvas = NullableInject(UICanvas)

---@type Transform
---@details 플레이어 카메라
PlayerCamera = NullableInject(PlayerCamera)

---@type float
---@details UI 거리
UIDistance = 1.5

---@type float
---@details UI 높이 오프셋
UIHeightOffset = -0.2

---@type string
---@details UI 팔로우 모드: "follow" (시선 따라감), "toggle" (호출형), "fixed" (고정)
UIFollowMode = "follow"

---@type float
---@details UI 팔로우 속도 (부드러운 이동)
UIFollowSpeed = 5.0
--endregion

-- 모듈 임포트
local util = require 'xlua.util'

-- 컴포넌트 참조
local arenaXManager = nil
local blockDropdownComp = nil
local districtDropdownComp = nil
local numberDropdownComp = nil

-- 좌석 데이터
local allSeats = {}
local filteredSeats = {}
local seatButtons = {}

-- 현재 필터 상태
local currentBlock = "All"
local currentDistrict = "All"
local currentNumber = "All"

-- 선택된 좌석
local selectedSeatId = nil

-- UI 상태
local isUIVisible = false

-- 디버그용 타이머
local debugTimer = 0
local DEBUG_INTERVAL = 2.0

-- Block/Section 정의 (공연장 구조에 맞게 수정)
local BLOCKS = {
    "All",
    "A",  -- A Block (무대 정면)
    "B",  -- B Block (왼쪽)
    "C",  -- C Block (뒤쪽)
    "D",  -- D Block (오른쪽)
}

-- District/Row 정의
local DISTRICTS = {
    "All",
    "1층",
    "2층",
    "VIP",
}

--region 생명주기 함수

function awake()
    Debug.Log("[SeatSelectionUI] Awake")
end

function start()
    Debug.Log("[SeatSelectionUI] Start")

    -- 자동 찾기
    FindRequiredObjects()

    -- ArenaXManager 연결
    if ArenaXManagerObject ~= nil then
        arenaXManager = ArenaXManagerObject:GetLuaComponent("ArenaXManager")
        if arenaXManager ~= nil then
            Debug.Log("[SeatSelectionUI] ArenaXManager connected")
        end
    end

    -- 드롭다운 컴포넌트 가져오기
    InitializeDropdowns()

    -- 이벤트 등록
    RegisterEvents()

    -- 플레이어 카메라 찾기
    if PlayerCamera == nil then
        PlayerCamera = FindPlayerCamera()
    end

    -- 카메라 찾기 결과 로그
    if PlayerCamera == nil then
        Debug.Log("[SeatSelectionUI] PlayerCamera not found - UI follow will not work")
    else
        Debug.Log("[SeatSelectionUI] PlayerCamera set: " .. PlayerCamera.gameObject.name)
    end

    -- UI 모드에 따른 초기 상태 설정
    if UICanvas ~= nil then
        if UIFollowMode == "follow" then
            -- follow 모드: 시작 시 표시, 플레이어 따라다님
            SetUIVisible(true)
        elseif UIFollowMode == "toggle" then
            -- toggle 모드: 시작 시 숨김, M키나 메뉴 버튼으로 토글
            SetUIVisible(false)
        elseif UIFollowMode == "fixed" then
            -- fixed 모드: 시작 시 표시, 고정 위치
            SetUIVisible(true)
        else
            Debug.Log("[SeatSelectionUI] Unknown UIFollowMode: " .. tostring(UIFollowMode))
            SetUIVisible(false)
        end
    end

    Debug.Log("[SeatSelectionUI] Initialized with UIFollowMode: " .. tostring(UIFollowMode))

    -- 초기 좌석 데이터 로드 (지연 호출 - SeatController들이 먼저 등록될 시간을 줌)
    self:StartCoroutine(util.cs_generator(function()
        -- 1초 대기 (모든 SeatController가 start()에서 RegisterSeat을 완료할 시간)
        coroutine.yield(WaitForSeconds(1.0))

        Debug.Log("[SeatSelectionUI] Initial seat data loading...")
        UpdateFilteredSeats()
        UpdateSeatButtons()

        local seatCount = GetTableCount(filteredSeats)
        Debug.Log("[SeatSelectionUI] Loaded " .. seatCount .. " seats")
    end))
end

--- 플레이어 카메라 찾기 (여러 방법 시도)
---@return Transform | nil
function FindPlayerCamera()
    -- 1. Camera.main 시도
    local mainCam = Camera.main
    if mainCam ~= nil then
        Debug.Log("[SeatSelectionUI] PlayerCamera found via Camera.main")
        return mainCam.transform
    end

    -- 2. XR Origin 하위에서 찾기 (VR 환경)
    local xrOriginNames = { "XR Origin", "XR Origin (XR Rig)", "XROrigin", "XR Rig" }
    for _, name in ipairs(xrOriginNames) do
        local xrOrigin = GameObject.Find(name)
        if xrOrigin ~= nil then
            local cam = xrOrigin:GetComponentInChildren(typeof(Camera))
            if cam ~= nil then
                Debug.Log("[SeatSelectionUI] PlayerCamera found under " .. name)
                return cam.transform
            end
        end
    end

    -- 3. "MainCamera" 태그로 찾기
    local taggedCam = GameObject.FindWithTag("MainCamera")
    if taggedCam ~= nil then
        Debug.Log("[SeatSelectionUI] PlayerCamera found via MainCamera tag")
        return taggedCam.transform
    end

    -- 4. 이름으로 찾기
    local cameraNames = { "Main Camera", "Camera", "PlayerCamera", "Head" }
    for _, name in ipairs(cameraNames) do
        local camObj = GameObject.Find(name)
        if camObj ~= nil then
            local cam = camObj:GetComponent(typeof(Camera))
            if cam ~= nil then
                Debug.Log("[SeatSelectionUI] PlayerCamera found by name: " .. name)
                return camObj.transform
            end
        end
    end

    return nil
end

function update()
    -- follow 모드일 때 UI 위치 업데이트
    if isUIVisible and UIFollowMode == "follow" then
        UpdateFollowUI()
    end
end

-- UI 토글은 외부에서 ToggleUI() 호출하거나
-- 별도 버튼/이벤트로 처리

--- follow 모드: UI가 플레이어를 따라다님
function UpdateFollowUI()
    if UICanvas == nil or PlayerCamera == nil then return end

    -- 목표 위치 계산 (플레이어 앞)
    local camForward = PlayerCamera.forward
    camForward.y = 0
    camForward = camForward.normalized

    local targetPos = PlayerCamera.position + camForward * UIDistance
    targetPos.y = PlayerCamera.position.y + UIHeightOffset

    -- 부드럽게 이동
    UICanvas.transform.position = Vector3.Lerp(
        UICanvas.transform.position,
        targetPos,
        Time.deltaTime * UIFollowSpeed
    )

    -- 플레이어를 바라보도록 회전
    local lookDir = PlayerCamera.position - UICanvas.transform.position
    lookDir.y = 0
    if lookDir.magnitude > 0.01 then
        UICanvas.transform.rotation = Quaternion.LookRotation(-lookDir)
    end

    -- 디버그 로그 (2초 간격)
    debugTimer = debugTimer + Time.deltaTime
    if debugTimer >= DEBUG_INTERVAL then
        debugTimer = 0
        local uiPos = UICanvas.transform.position
        local playerPos = PlayerCamera.position
    end
end

function onEnable()
    RegisterEvents()
end

function onDisable()
    UnregisterEvents()
end

--endregion

--region 초기화

function FindRequiredObjects()
    if ArenaXManagerObject == nil then
        ArenaXManagerObject = GameObject.Find(ArenaXManagerName)
        if ArenaXManagerObject ~= nil then
            Debug.Log("[SeatSelectionUI] ArenaXManager found")
        end
    end
end

function InitializeDropdowns()
    -- Block 드롭다운
    if BlockDropdown ~= nil then
        blockDropdownComp = BlockDropdown:GetComponent("TMP_Dropdown")
        if blockDropdownComp ~= nil then
            PopulateDropdown(blockDropdownComp, BLOCKS)
            Debug.Log("[SeatSelectionUI] BlockDropdown initialized")
        end
    end

    -- District 드롭다운
    if DistrictDropdown ~= nil then
        districtDropdownComp = DistrictDropdown:GetComponent("TMP_Dropdown")
        if districtDropdownComp ~= nil then
            PopulateDropdown(districtDropdownComp, DISTRICTS)
            Debug.Log("[SeatSelectionUI] DistrictDropdown initialized")
        end
    end

    -- Number 드롭다운 (선택사항)
    if NumberDropdown ~= nil then
        numberDropdownComp = NumberDropdown:GetComponent("TMP_Dropdown")
    end
end

function PopulateDropdown(dropdown, options)
    if dropdown == nil then return end

    dropdown:ClearOptions()

    local optionList = CS.System.Collections.Generic.List(CS.TMPro.TMP_Dropdown.OptionData)()
    for _, option in ipairs(options) do
        local optionData = CS.TMPro.TMP_Dropdown.OptionData(option)
        optionList:Add(optionData)
    end

    dropdown:AddOptions(optionList)
    dropdown.value = 0
end

function RegisterEvents()
    -- Block 드롭다운 이벤트
    if blockDropdownComp ~= nil then
        blockDropdownComp.onValueChanged:AddListener(OnBlockChanged)
    end

    -- District 드롭다운 이벤트
    if districtDropdownComp ~= nil then
        districtDropdownComp.onValueChanged:AddListener(OnDistrictChanged)
    end

    -- Number 드롭다운 이벤트
    if numberDropdownComp ~= nil then
        numberDropdownComp.onValueChanged:AddListener(OnNumberChanged)
    end

    -- Select 버튼 이벤트
    if SelectButton ~= nil then
        local button = SelectButton:GetComponent("Button")
        if button ~= nil then
            button.onClick:AddListener(OnSelectButtonClick)
        end
    end
end

function UnregisterEvents()
    if blockDropdownComp ~= nil then
        blockDropdownComp.onValueChanged:RemoveListener(OnBlockChanged)
    end

    if districtDropdownComp ~= nil then
        districtDropdownComp.onValueChanged:RemoveListener(OnDistrictChanged)
    end

    if numberDropdownComp ~= nil then
        numberDropdownComp.onValueChanged:RemoveListener(OnNumberChanged)
    end

    if SelectButton ~= nil then
        local button = SelectButton:GetComponent("Button")
        if button ~= nil then
            button.onClick:RemoveListener(OnSelectButtonClick)
        end
    end
end

--endregion

--region 드롭다운 이벤트

function OnBlockChanged(index)
    currentBlock = BLOCKS[index + 1]  -- Lua는 1부터 시작
    Debug.Log("[SeatSelectionUI] Block changed: " .. currentBlock)

    UpdateFilteredSeats()
    UpdateSeatButtons()
end

function OnDistrictChanged(index)
    currentDistrict = DISTRICTS[index + 1]
    Debug.Log("[SeatSelectionUI] District changed: " .. currentDistrict)

    UpdateFilteredSeats()
    UpdateSeatButtons()
end

function OnNumberChanged(index)
    -- Number 드롭다운이 있는 경우
    Debug.Log("[SeatSelectionUI] Number changed: " .. tostring(index))

    UpdateFilteredSeats()
    UpdateSeatButtons()
end

--endregion

--region 필터링

function UpdateFilteredSeats()
    filteredSeats = {}

    -- ArenaXManager에서 모든 좌석 가져오기
    if arenaXManager ~= nil then
        allSeats = arenaXManager.GetAllSeats()
    end

    for seatId, seatData in pairs(allSeats) do
        local passFilter = true

        -- Block 필터
        if currentBlock ~= "All" then
            -- seatData.row의 첫 글자로 Block 판별 (예: A1 -> A Block)
            local seatBlock = string.sub(seatData.row, 1, 1)
            if seatBlock ~= currentBlock then
                passFilter = false
            end
        end

        -- District 필터
        if currentDistrict ~= "All" then
            if seatData.section ~= currentDistrict then
                passFilter = false
            end
        end

        if passFilter then
            filteredSeats[seatId] = seatData
        end
    end

    Debug.Log("[SeatSelectionUI] Filtered seats: " .. GetTableCount(filteredSeats))
end

function GetTableCount(t)
    local count = 0
    for _ in pairs(t) do
        count = count + 1
    end
    return count
end

--endregion

--region 좌석 버튼

function UpdateSeatButtons()
    -- 기존 버튼 제거
    ClearSeatButtons()

    if SeatButtonGrid == nil then
        Debug.LogWarning("[SeatSelectionUI] SeatButtonGrid is nil")
        return
    end

    if SeatButtonPrefab == nil then
        Debug.LogWarning("[SeatSelectionUI] SeatButtonPrefab is nil")
        return
    end

    -- 필터된 좌석으로 버튼 생성
    for seatId, seatData in pairs(filteredSeats) do
        CreateSeatButton(seatId, seatData)
    end

    Debug.Log("[SeatSelectionUI] Created " .. GetTableCount(seatButtons) .. " seat buttons")
end

function CreateSeatButton(seatId, seatData)
    local buttonObj = GameObject.Instantiate(SeatButtonPrefab, SeatButtonGrid.transform)
    buttonObj.name = "SeatBtn_" .. seatId

    -- 버튼 텍스트 설정
    local textComp = buttonObj:GetComponentInChildren(typeof(CS.TMPro.TMP_Text))
    if textComp ~= nil then
        -- 짧은 표시: 열-번호 (예: "A-1")
        textComp.text = seatId
    end

    -- 버튼 클릭 이벤트
    local button = buttonObj:GetComponent("Button")
    if button ~= nil then
        local capturedSeatId = seatId
        button.onClick:AddListener(function()
            OnSeatButtonClick(capturedSeatId)
        end)
    end

    -- 버튼 색상 설정 (사용 가능 여부에 따라)
    UpdateButtonAppearance(buttonObj, seatData)

    seatButtons[seatId] = buttonObj
end

function UpdateButtonAppearance(buttonObj, seatData)
    local image = buttonObj:GetComponent("Image")
    if image == nil then return end

    if not seatData.isAvailable then
        -- 사용 불가
        image.color = Color(0.5, 0.5, 0.5, 0.5)
    elseif seatData.isOccupied then
        -- 이미 착석 중
        image.color = Color(0.8, 0.3, 0.3, 1)
    else
        -- 사용 가능
        image.color = Color(0.3, 0.7, 0.3, 1)
    end
end

function ClearSeatButtons()
    for seatId, buttonObj in pairs(seatButtons) do
        if buttonObj ~= nil then
            GameObject.Destroy(buttonObj)
        end
    end
    seatButtons = {}
end

function OnSeatButtonClick(seatId)
    Debug.Log("[SeatSelectionUI] Seat button clicked: " .. seatId)

    -- 이전 선택 해제
    if selectedSeatId ~= nil and seatButtons[selectedSeatId] ~= nil then
        local prevButton = seatButtons[selectedSeatId]
        local prevImage = prevButton:GetComponent("Image")
        if prevImage ~= nil then
            prevImage.color = Color(0.3, 0.7, 0.3, 1)
        end
    end

    -- 새 선택
    selectedSeatId = seatId

    -- 선택된 버튼 강조
    if seatButtons[seatId] ~= nil then
        local buttonImage = seatButtons[seatId]:GetComponent("Image")
        if buttonImage ~= nil then
            buttonImage.color = Color(0.2, 0.5, 1, 1)  -- 파란색
        end
    end

    -- 선택 정보 표시
    UpdateSelectedSeatInfo(seatId)

    -- 햅틱 피드백
    XR.StartControllerVibration(false, 0.2, 0.05)
    XR.StartControllerVibration(true, 0.2, 0.05)
end

function UpdateSelectedSeatInfo(seatId)
    if SelectedSeatText == nil then return end

    local textComp = SelectedSeatText:GetComponent("TMP_Text")
    if textComp == nil then return end

    local seatData = allSeats[seatId]
    if seatData ~= nil then
        textComp.text = string.format("%s %s열 %d번",
            seatData.section or "",
            seatData.row or "",
            seatData.number or 0)
    else
        textComp.text = seatId
    end
end

--endregion

--region Select 버튼

function OnSelectButtonClick()
    Debug.Log("[SeatSelectionUI] Select button clicked")

    if selectedSeatId == nil then
        Debug.LogWarning("[SeatSelectionUI] No seat selected")
        UI.ToastMessage("좌석을 선택해주세요")
        return
    end

    -- ArenaXManager를 통해 좌석 선택
    if arenaXManager ~= nil then
        arenaXManager.SelectSeat(selectedSeatId)
        Debug.Log("[SeatSelectionUI] Seat selected: " .. selectedSeatId)
    end

    -- toggle 모드일 때만 UI 숨기기
    -- follow 모드는 항상 표시 유지
    if UIFollowMode == "toggle" then
        SetUIVisible(false)
    end

    -- 햅틱 피드백
    XR.StartControllerVibration(false, 0.4, 0.1)
    XR.StartControllerVibration(true, 0.4, 0.1)
end

--endregion

--region UI 표시/숨김

function SetUIVisible(visible)
    isUIVisible = visible

    if UICanvas ~= nil then
        if visible then
            PositionUIInFrontOfPlayer()
        end
        UICanvas:SetActive(visible)
    end

    Debug.Log("[SeatSelectionUI] UI visible: " .. tostring(visible))
end

function ToggleUI()
    SetUIVisible(not isUIVisible)

    -- UI 열릴 때 좌석 데이터 갱신
    if isUIVisible then
        UpdateFilteredSeats()
        UpdateSeatButtons()
    end
end

function PositionUIInFrontOfPlayer()
    if UICanvas == nil or PlayerCamera == nil then return end

    local camForward = PlayerCamera.forward
    camForward.y = 0
    camForward = camForward.normalized

    local targetPos = PlayerCamera.position + camForward * UIDistance
    targetPos.y = PlayerCamera.position.y + UIHeightOffset

    UICanvas.transform.position = targetPos

    -- 플레이어를 바라보도록
    local lookDir = PlayerCamera.position - targetPos
    lookDir.y = 0
    if lookDir.magnitude > 0.01 then
        UICanvas.transform.rotation = Quaternion.LookRotation(-lookDir)
    end

    -- 디버그: 위치 정보 출력
    local playerPos = PlayerCamera.position
    local uiPos = targetPos
    local offset = uiPos - playerPos
    Debug.Log(string.format(
        "[SeatSelectionUI] DEBUG - Player: (%.2f, %.2f, %.2f) | UI: (%.2f, %.2f, %.2f) | Offset: (%.2f, %.2f, %.2f) | Distance: %.2f | Forward: (%.2f, %.2f, %.2f)",
        playerPos.x, playerPos.y, playerPos.z,
        uiPos.x, uiPos.y, uiPos.z,
        offset.x, offset.y, offset.z,
        offset.magnitude,
        camForward.x, camForward.y, camForward.z
    ))
end

function IsUIVisible()
    return isUIVisible
end

--endregion

--region 외부 API

--- 특정 Block의 좌석만 표시
function ShowBlock(blockName)
    if blockDropdownComp ~= nil then
        for i, block in ipairs(BLOCKS) do
            if block == blockName then
                blockDropdownComp.value = i - 1
                break
            end
        end
    end
end

--- 특정 District의 좌석만 표시
function ShowDistrict(districtName)
    if districtDropdownComp ~= nil then
        for i, district in ipairs(DISTRICTS) do
            if district == districtName then
                districtDropdownComp.value = i - 1
                break
            end
        end
    end
end

--- 좌석 데이터 새로고침
function RefreshSeats()
    UpdateFilteredSeats()
    UpdateSeatButtons()
end

--endregion
