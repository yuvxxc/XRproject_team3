using System;
using System.Collections.Generic;
using System.Linq;
using TwentyOz.VivenSDK.Scripts.Core.Lua;
using Twoz.Viven.Interactions;
using UnityEditor;
using UnityEngine;

namespace ArenaX.Editor
{
    /// <summary>
    /// Arena X 씬 자동 설정 에디터 도구
    /// 매니저 오브젝트 생성, VivenLuaBehaviour 설정, Injection 연결을 자동으로 처리
    /// 선택한 좌석만 Sittable로 설정 가능
    /// </summary>
    public class ArenaXSceneSetup : EditorWindow
    {
        // Lua 스크립트 경로
        private const string SCRIPTS_PATH = "Assets/ArenaX/Scripts";
        private const string MANAGER_SCRIPT_PATH = SCRIPTS_PATH + "/Manager/ArenaXManager.lua";
        private const string SEAT_UI_MANAGER_SCRIPT_PATH = SCRIPTS_PATH + "/UI/SeatUIManager.lua";
        private const string SEAT_SELECTION_UI_SCRIPT_PATH = SCRIPTS_PATH + "/UI/SeatSelectionUI.lua";
        private const string AUDIENCE_MANAGER_SCRIPT_PATH = SCRIPTS_PATH + "/Avatar/AudienceManager.lua";
        private const string SEAT_CONTROLLER_SCRIPT_PATH = SCRIPTS_PATH + "/Seat/SeatController.lua";

        // 프리팹 경로
        private const string SEAT_PREFAB_PATH = "Assets/ArenaX/Prefabs/Seats/Seat.prefab";
        private const string AUDIENCE_PREFAB_PATH = "Assets/ArenaX/Prefabs/Avatars/VirtualAudience.prefab";

        // 씬 검증 결과
        private bool hasArenaXManager;
        private bool hasSeatUIManager;
        private bool hasAudienceManager;
        private bool hasSeatSelectionUI;
        private int totalSeatCount;
        private int configuredSeatCount;

        // 설정된 좌석 목록
        private List<GameObject> configuredSeats = new List<GameObject>();

        private Vector2 scrollPosition;
        private Vector2 seatListScrollPosition;

        [MenuItem("ArenaX/씬 설정 도구", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<ArenaXSceneSetup>("ArenaX 씬 설정");
            window.minSize = new Vector2(400, 500);
            window.ValidateScene();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSceneStatus();
            EditorGUILayout.Space(10);

            DrawSetupActions();
            EditorGUILayout.Space(10);

            DrawSeatConfiguration();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Arena X 씬 설정 도구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "이 도구는 Arena X 프로젝트의 씬 구성을 자동으로 설정합니다.\n" +
                "• 매니저 오브젝트 생성 및 Lua 스크립트 연결\n" +
                "• VivenLuaBehaviour Injection 자동 설정\n" +
                "• 좌석 컴포넌트 구성 (VObject, VivenSittable, SeatController)",
                MessageType.Info);
        }

        private void DrawSceneStatus()
        {
            EditorGUILayout.LabelField("현재 씬 상태", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            DrawStatusItem("ArenaXManager", hasArenaXManager);
            DrawStatusItem("SeatUIManager", hasSeatUIManager);
            DrawStatusItem("AudienceManager", hasAudienceManager);
            DrawStatusItem("SeatSelectionUI", hasSeatSelectionUI);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"전체 좌석 수: {totalSeatCount}");
            EditorGUILayout.LabelField($"Sittable 설정됨: {configuredSeatCount}개");

            EditorGUI.indentLevel--;

            if (GUILayout.Button("씬 상태 새로고침"))
            {
                ValidateScene();
            }
        }

        private void DrawStatusItem(string name, bool exists)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, GUILayout.Width(150));

            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = exists ? Color.green : Color.red;
            EditorGUILayout.LabelField(exists ? "✓ 존재함" : "✗ 없음", style);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSetupActions()
        {
            EditorGUILayout.LabelField("매니저/UI 설정", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 전체 설정 (매니저 + UI만)
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("🚀 매니저/UI 자동 설정", GUILayout.Height(35)))
            {
                SetupAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            // 개별 설정
            EditorGUILayout.LabelField("개별 설정:", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("매니저 생성"))
            {
                SetupManagers();
                ValidateScene();
            }
            if (GUILayout.Button("UI 설정"))
            {
                SetupUI();
                ValidateScene();
            }
            if (GUILayout.Button("Injection 연결"))
            {
                SetupInjections();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSeatConfiguration()
        {
            EditorGUILayout.LabelField("좌석 설정 (선택 기반)", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 경고: 모든 좌석을 설정하지 않음
            EditorGUILayout.HelpBox(
                "⚠️ 선택한 좌석만 Sittable로 설정됩니다.\n" +
                "Hierarchy에서 원하는 좌석 오브젝트를 선택한 후 버튼을 클릭하세요.\n" +
                "(권장: 6개 이하)",
                MessageType.Warning);

            EditorGUILayout.Space(5);

            // 현재 선택된 오브젝트 표시
            var selectedObjects = Selection.gameObjects;
            var seatCandidates = selectedObjects.Where(go => IsSeatObject(go)).ToList();

            EditorGUILayout.LabelField($"현재 선택: {seatCandidates.Count}개 좌석 오브젝트", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();

            // 선택한 좌석 설정
            GUI.backgroundColor = seatCandidates.Count > 0 ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
            GUI.enabled = seatCandidates.Count > 0;
            if (GUILayout.Button($"🪑 선택한 {seatCandidates.Count}개 좌석 설정", GUILayout.Height(30)))
            {
                ConfigureSelectedSeats(seatCandidates);
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 업데이트 섹션
            EditorGUILayout.LabelField("업데이트 도구", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("🔄 모든 좌석 업데이트\n(SitPoint/Collider 위치 갱신)", GUILayout.Height(40)))
            {
                UpdateAllSeats();
            }

            GUI.enabled = seatCandidates.Count > 0;
            if (GUILayout.Button($"🔄 선택한 {seatCandidates.Count}개만 업데이트", GUILayout.Height(40)))
            {
                UpdateSelectedSeats(seatCandidates);
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 정리 섹션
            EditorGUILayout.LabelField("정리 도구", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("🧹 모든 좌석 정리\n(Viven 컴포넌트 제거)", GUILayout.Height(40)))
            {
                if (EditorUtility.DisplayDialog("확인",
                    "모든 좌석에서 VObject, VivenSittable, VivenLuaBehaviour,\nSitPoint, SitDetector를 제거합니다.\n\n계속하시겠습니까?",
                    "제거", "취소"))
                {
                    CleanAllSeats();
                }
            }

            if (GUILayout.Button("🗑️ 선택한 좌석만 정리", GUILayout.Height(40)))
            {
                CleanSelectedSeats(seatCandidates);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 현재 설정된 좌석 목록
            DrawConfiguredSeatsList();
        }

        private void DrawConfiguredSeatsList()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"현재 설정된 Sittable 좌석 ({configuredSeats.Count}개)", EditorStyles.boldLabel);

            if (configuredSeats.Count == 0)
            {
                EditorGUILayout.HelpBox("설정된 좌석이 없습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            seatListScrollPosition = EditorGUILayout.BeginScrollView(seatListScrollPosition, GUILayout.MaxHeight(150));

            for (int i = configuredSeats.Count - 1; i >= 0; i--)
            {
                var seat = configuredSeats[i];
                if (seat == null)
                {
                    configuredSeats.RemoveAt(i);
                    continue;
                }

                EditorGUILayout.BeginHorizontal();

                // 좌석 선택 버튼
                if (GUILayout.Button(seat.name, EditorStyles.linkLabel, GUILayout.Width(200)))
                {
                    Selection.activeGameObject = seat;
                    EditorGUIUtility.PingObject(seat);
                }

                // 제거 버튼
                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button("제거", GUILayout.Width(50)))
                {
                    CleanSeat(seat);
                    ValidateScene();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        #region Scene Validation

        private void ValidateScene()
        {
            hasArenaXManager = GameObject.Find("ArenaXManager") != null;
            hasSeatUIManager = GameObject.Find("SeatUIManager") != null;
            hasAudienceManager = GameObject.Find("AudienceManager") != null;
            hasSeatSelectionUI = GameObject.Find("SeatSelectionUI") != null;

            // 좌석 카운트 및 설정된 좌석 목록 갱신
            var allObjects = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            totalSeatCount = 0;
            configuredSeatCount = 0;
            configuredSeats.Clear();

            foreach (var obj in allObjects)
            {
                if (IsSeatObject(obj.gameObject))
                {
                    totalSeatCount++;
                    if (obj.GetComponent<VivenSittable>() != null)
                    {
                        configuredSeatCount++;
                        configuredSeats.Add(obj.gameObject);
                    }
                }
            }

            Repaint();
        }

        private bool IsSeatObject(GameObject go)
        {
            var name = go.name.ToLower();
            return name.Contains("seat") ||
                   (name.StartsWith("component#") && go.GetComponent<MeshRenderer>() != null);
        }

        #endregion

        #region Setup Methods

        private void SetupAll()
        {
            Undo.SetCurrentGroupName("Arena X 전체 설정");
            var undoGroup = Undo.GetCurrentGroup();

            try
            {
                SetupManagers();
                SetupUI();
                SetupInjections();
                // 좌석은 자동으로 설정하지 않음 - 선택 기반으로 변경됨

                ValidateScene();

                EditorUtility.DisplayDialog("설정 완료",
                    "Arena X 매니저/UI 설정이 완료되었습니다!\n\n" +
                    $"• 매니저: {(hasArenaXManager ? "✓" : "✗")} ArenaXManager\n" +
                    $"• UI: {(hasSeatUIManager ? "✓" : "✗")} SeatUIManager\n" +
                    $"• 관객: {(hasAudienceManager ? "✓" : "✗")} AudienceManager\n\n" +
                    "⚠️ 좌석 설정은 아래 '좌석 설정' 섹션에서\n" +
                    "원하는 좌석만 선택하여 진행하세요.",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ArenaX Setup] 설정 중 오류 발생: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("오류", $"설정 중 오류가 발생했습니다:\n{e.Message}", "확인");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private void SetupManagers()
        {
            // ArenaXManager 생성
            var arenaXManager = FindOrCreateManager("ArenaXManager", MANAGER_SCRIPT_PATH);

            // AudienceManager 생성
            var audienceManager = FindOrCreateManager("AudienceManager", AUDIENCE_MANAGER_SCRIPT_PATH);

            Debug.Log("[ArenaX Setup] 매니저 오브젝트 생성 완료");
            ValidateScene();
        }

        private void SetupUI()
        {
            // SeatUIManager 생성
            var seatUIManager = FindOrCreateManager("SeatUIManager", SEAT_UI_MANAGER_SCRIPT_PATH);

            // SeatSelectionUI 생성 (Canvas 포함)
            SetupSeatSelectionUI();

            Debug.Log("[ArenaX Setup] UI 설정 완료");
            ValidateScene();
        }

        private GameObject FindOrCreateManager(string name, string scriptPath)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                Debug.Log($"[ArenaX Setup] {name} 이미 존재함");

                // VivenLuaBehaviour 확인 및 추가
                var behaviour = existing.GetComponent<VivenLuaBehaviour>();
                if (behaviour == null)
                {
                    behaviour = Undo.AddComponent<VivenLuaBehaviour>(existing);
                }

                // 스크립트 연결
                AssignVivenScript(behaviour, scriptPath);

                return existing;
            }

            // 새 오브젝트 생성
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            // VivenLuaBehaviour 추가
            var luaBehaviour = Undo.AddComponent<VivenLuaBehaviour>(go);

            // 스크립트 연결
            AssignVivenScript(luaBehaviour, scriptPath);

            Debug.Log($"[ArenaX Setup] {name} 생성됨");
            return go;
        }

        private void SetupSeatSelectionUI()
        {
            var existing = GameObject.Find("SeatSelectionUI");
            if (existing != null)
            {
                Debug.Log("[ArenaX Setup] SeatSelectionUI 이미 존재함");
                return;
            }

            // Canvas 생성
            var canvasGo = new GameObject("SeatSelectionUI");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create SeatSelectionUI");

            var canvas = Undo.AddComponent<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasScaler = Undo.AddComponent<UnityEngine.UI.CanvasScaler>(canvasGo);
            Undo.AddComponent<UnityEngine.UI.GraphicRaycaster>(canvasGo);

            // VivenCanvasSetting 추가 (있다면)
            var vivenCanvasType = Type.GetType("TwentyOz.VivenSDK.Scripts.Core.VivenComponents.UI.VivenCanvasSetting, Assembly-CSharp");
            if (vivenCanvasType != null)
            {
                canvasGo.AddComponent(vivenCanvasType);
            }

            // VivenLuaBehaviour 추가
            var luaBehaviour = Undo.AddComponent<VivenLuaBehaviour>(canvasGo);
            AssignVivenScript(luaBehaviour, SEAT_SELECTION_UI_SCRIPT_PATH);

            // 위치 설정
            canvasGo.transform.position = new Vector3(0, 1.5f, 2f);
            canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            Debug.Log("[ArenaX Setup] SeatSelectionUI 생성됨");
        }

        private void AssignVivenScript(VivenLuaBehaviour behaviour, string scriptPath)
        {
            var vivenScript = AssetDatabase.LoadAssetAtPath<VivenScript>(scriptPath);
            if (vivenScript != null)
            {
                var serializedObject = new SerializedObject(behaviour);
                var luaScriptProperty = serializedObject.FindProperty("luaScript");
                luaScriptProperty.objectReferenceValue = vivenScript;
                serializedObject.ApplyModifiedProperties();

                Debug.Log($"[ArenaX Setup] 스크립트 연결: {scriptPath}");
            }
            else
            {
                Debug.LogWarning($"[ArenaX Setup] 스크립트를 찾을 수 없음: {scriptPath}");
            }
        }

        private void SetupInjections()
        {
            // ArenaXManager의 Injection 설정
            var arenaXManager = GameObject.Find("ArenaXManager");
            var seatUIManager = GameObject.Find("SeatUIManager");
            var audienceManager = GameObject.Find("AudienceManager");

            if (arenaXManager != null)
            {
                var behaviour = arenaXManager.GetComponent<VivenLuaBehaviour>();
                if (behaviour != null)
                {
                    SetupArenaXManagerInjection(behaviour, seatUIManager, audienceManager);
                }
            }

            // SeatUIManager의 Injection 설정
            if (seatUIManager != null)
            {
                var behaviour = seatUIManager.GetComponent<VivenLuaBehaviour>();
                if (behaviour != null)
                {
                    SetupSeatUIManagerInjection(behaviour, arenaXManager);
                }
            }

            // AudienceManager의 Injection 설정
            if (audienceManager != null)
            {
                var behaviour = audienceManager.GetComponent<VivenLuaBehaviour>();
                if (behaviour != null)
                {
                    SetupAudienceManagerInjection(behaviour, arenaXManager);
                }
            }

            Debug.Log("[ArenaX Setup] Injection 연결 완료");
        }

        private void SetupArenaXManagerInjection(VivenLuaBehaviour behaviour, GameObject seatUIManager, GameObject audienceManager)
        {
            var serializedObject = new SerializedObject(behaviour);
            var injectionProperty = serializedObject.FindProperty("injection");

            // gameObjectValues 설정
            var gameObjectValues = injectionProperty.FindPropertyRelative("gameObjectValues");

            // 기존 값 확인 및 업데이트
            var injections = new Dictionary<string, GameObject>
            {
                { "SeatUIManagerObject", seatUIManager },
                { "AudienceManagerObject", audienceManager }
            };

            SetGameObjectInjections(gameObjectValues, injections);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(behaviour);
        }

        private void SetupSeatUIManagerInjection(VivenLuaBehaviour behaviour, GameObject arenaXManager)
        {
            var serializedObject = new SerializedObject(behaviour);
            var injectionProperty = serializedObject.FindProperty("injection");
            var gameObjectValues = injectionProperty.FindPropertyRelative("gameObjectValues");

            var injections = new Dictionary<string, GameObject>
            {
                { "ArenaXManagerObject", arenaXManager }
            };

            SetGameObjectInjections(gameObjectValues, injections);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(behaviour);
        }

        private void SetupAudienceManagerInjection(VivenLuaBehaviour behaviour, GameObject arenaXManager)
        {
            var serializedObject = new SerializedObject(behaviour);
            var injectionProperty = serializedObject.FindProperty("injection");
            var gameObjectValues = injectionProperty.FindPropertyRelative("gameObjectValues");

            // AudiencePrefab 로드
            var audiencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AUDIENCE_PREFAB_PATH);

            var injections = new Dictionary<string, GameObject>
            {
                { "ArenaXManagerObject", arenaXManager },
                { "AudiencePrefab", audiencePrefab }
            };

            SetGameObjectInjections(gameObjectValues, injections);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(behaviour);
        }

        private void SetGameObjectInjections(SerializedProperty gameObjectValues, Dictionary<string, GameObject> injections)
        {
            // 현재 배열 크기 가져오기
            var existingCount = gameObjectValues.arraySize;
            var existingNames = new HashSet<string>();

            // 기존 항목 이름 수집 및 업데이트
            for (int i = 0; i < existingCount; i++)
            {
                var element = gameObjectValues.GetArrayElementAtIndex(i);
                var nameProperty = element.FindPropertyRelative("name");
                var name = nameProperty.stringValue;
                existingNames.Add(name);

                if (injections.ContainsKey(name))
                {
                    var valueProperty = element.FindPropertyRelative("value");
                    valueProperty.objectReferenceValue = injections[name];
                }
            }

            // 새 항목 추가
            foreach (var kvp in injections)
            {
                if (!existingNames.Contains(kvp.Key))
                {
                    gameObjectValues.InsertArrayElementAtIndex(gameObjectValues.arraySize);
                    var newElement = gameObjectValues.GetArrayElementAtIndex(gameObjectValues.arraySize - 1);
                    newElement.FindPropertyRelative("name").stringValue = kvp.Key;
                    newElement.FindPropertyRelative("value").objectReferenceValue = kvp.Value;
                }
            }
        }

        private void SetupSeats()
        {
            // 선택 기반 설정으로 변경됨 - 이 메서드는 더 이상 자동 실행하지 않음
            Debug.Log("[ArenaX Setup] 좌석 설정은 Hierarchy에서 직접 선택 후 진행하세요.");
        }

        /// <summary>
        /// 선택한 좌석만 Sittable로 설정
        /// </summary>
        private void ConfigureSelectedSeats(List<GameObject> seats)
        {
            if (seats.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "선택된 좌석이 없습니다.\nHierarchy에서 좌석 오브젝트를 선택하세요.", "확인");
                return;
            }

            Undo.SetCurrentGroupName("선택한 좌석 설정");
            var undoGroup = Undo.GetCurrentGroup();

            var arenaXManager = GameObject.Find("ArenaXManager");
            int configured = 0;

            for (int i = 0; i < seats.Count; i++)
            {
                ConfigureSeat(seats[i], arenaXManager, i);
                configured++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[ArenaX Setup] {configured}개 좌석 설정 완료");
            ValidateScene();

            EditorUtility.DisplayDialog("완료", $"{configured}개 좌석이 Sittable로 설정되었습니다.\n(A-1 ~ A-{configured})", "확인");
        }

        /// <summary>
        /// 모든 설정된 좌석의 SitPoint/Collider 위치 업데이트
        /// </summary>
        private void UpdateAllSeats()
        {
            if (configuredSeats.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "설정된 좌석이 없습니다.", "확인");
                return;
            }

            Undo.SetCurrentGroupName("모든 좌석 업데이트");
            var undoGroup = Undo.GetCurrentGroup();

            int updated = 0;

            foreach (var seat in configuredSeats)
            {
                if (seat != null && UpdateSeat(seat))
                {
                    updated++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[ArenaX Setup] {updated}개 좌석 업데이트 완료");
            ValidateScene();

            EditorUtility.DisplayDialog("완료", $"{updated}개 좌석이 업데이트되었습니다.", "확인");
        }

        /// <summary>
        /// 선택한 좌석의 SitPoint/Collider 위치 업데이트
        /// </summary>
        private void UpdateSelectedSeats(List<GameObject> seats)
        {
            if (seats.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "선택된 좌석이 없습니다.\nHierarchy에서 좌석 오브젝트를 선택하세요.", "확인");
                return;
            }

            Undo.SetCurrentGroupName("선택한 좌석 업데이트");
            var undoGroup = Undo.GetCurrentGroup();

            int updated = 0;

            foreach (var seat in seats)
            {
                if (UpdateSeat(seat))
                {
                    updated++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[ArenaX Setup] {updated}개 좌석 업데이트 완료");
            ValidateScene();

            EditorUtility.DisplayDialog("완료", $"{updated}개 좌석이 업데이트되었습니다.", "확인");
        }

        /// <summary>
        /// 단일 좌석의 SitPoint/Collider 위치 업데이트
        /// </summary>
        private bool UpdateSeat(GameObject seatObject)
        {
            bool updated = false;

            // BoxCollider center 업데이트
            var boxCollider = seatObject.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                Undo.RecordObject(boxCollider, "Update BoxCollider");
                boxCollider.center = new Vector3(-0.300000012f, -0.209999993f, 0.239999995f);
                updated = true;
            }

            // SitPoint 위치 업데이트
            var sitPoint = seatObject.transform.Find("SitPoint");
            if (sitPoint != null)
            {
                Undo.RecordObject(sitPoint, "Update SitPoint");
                sitPoint.localPosition = new Vector3(-0.256000012f, -0.39199999f, 0.208000004f);
                updated = true;
            }

            if (updated)
            {
                EditorUtility.SetDirty(seatObject);
            }

            return updated;
        }

        /// <summary>
        /// 모든 좌석에서 Viven 컴포넌트 제거
        /// </summary>
        private void CleanAllSeats()
        {
            Undo.SetCurrentGroupName("모든 좌석 정리");
            var undoGroup = Undo.GetCurrentGroup();

            // ToArray()로 복사본 생성 - 순회 중 오브젝트 삭제 시 에러 방지
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var transformList = new List<Transform>(allTransforms);
            int cleaned = 0;

            foreach (var transform in transformList)
            {
                // transform이 이미 삭제되었을 수 있으므로 null 체크
                if (transform == null || transform.gameObject == null) continue;

                if (IsSeatObject(transform.gameObject))
                {
                    if (CleanSeat(transform.gameObject))
                    {
                        cleaned++;
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[ArenaX Setup] {cleaned}개 좌석 정리 완료");
            ValidateScene();

            EditorUtility.DisplayDialog("완료", $"{cleaned}개 좌석에서 Viven 컴포넌트가 제거되었습니다.", "확인");
        }

        /// <summary>
        /// 선택한 좌석에서 Viven 컴포넌트 제거
        /// </summary>
        private void CleanSelectedSeats(List<GameObject> seats)
        {
            if (seats.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "선택된 좌석이 없습니다.", "확인");
                return;
            }

            Undo.SetCurrentGroupName("선택한 좌석 정리");
            var undoGroup = Undo.GetCurrentGroup();

            int cleaned = 0;
            foreach (var seat in seats)
            {
                if (CleanSeat(seat))
                {
                    cleaned++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[ArenaX Setup] {cleaned}개 좌석 정리 완료");
            ValidateScene();
        }

        /// <summary>
        /// 단일 좌석에서 Viven 컴포넌트 제거
        /// </summary>
        private bool CleanSeat(GameObject seatObject)
        {
            bool hadComponents = false;

            // SitDetector 자식 오브젝트 찾아서 삭제
            var sitPoint = seatObject.transform.Find("SitPoint");
            if (sitPoint != null)
            {
                var sitDetector = sitPoint.Find("SitDetector");
                if (sitDetector != null)
                {
                    Undo.DestroyObjectImmediate(sitDetector.gameObject);
                    hadComponents = true;
                }

                // SitPoint도 삭제 (자동 생성된 것이므로)
                Undo.DestroyObjectImmediate(sitPoint.gameObject);
                hadComponents = true;
            }

            // VivenLuaBehaviour 제거 (좌석 자체에 있는 경우)
            var luaBehaviour = seatObject.GetComponent<VivenLuaBehaviour>();
            if (luaBehaviour != null)
            {
                Undo.DestroyObjectImmediate(luaBehaviour);
                hadComponents = true;
            }

            // VivenSittable 제거
            var sittable = seatObject.GetComponent<VivenSittable>();
            if (sittable != null)
            {
                Undo.DestroyObjectImmediate(sittable);
                hadComponents = true;
            }

            // VObject 제거
            var vObject = seatObject.GetComponent<VObject>();
            if (vObject != null)
            {
                Undo.DestroyObjectImmediate(vObject);
                hadComponents = true;
            }

            // Trigger용 BoxCollider 제거 (isTrigger가 true인 것만)
            var colliders = seatObject.GetComponents<BoxCollider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    Undo.DestroyObjectImmediate(col);
                    hadComponents = true;
                }
            }

            if (hadComponents)
            {
                EditorUtility.SetDirty(seatObject);
            }

            return hadComponents;
        }

        private void ConfigureSeat(GameObject seatObject, GameObject arenaXManager, int seatIndex = 0)
        {
            // VObject 추가
            var vObject = seatObject.GetComponent<VObject>();
            if (vObject == null)
            {
                vObject = Undo.AddComponent<VObject>(seatObject);
                vObject.displayName = seatObject.name;
            }

            // Collider 확인/추가 (VivenSittable 전에)
            var collider = seatObject.GetComponent<Collider>();
            if (collider == null)
            {
                var boxCollider = Undo.AddComponent<BoxCollider>(seatObject);
                boxCollider.isTrigger = true;
                boxCollider.center = new Vector3(-0.300000012f, -0.209999993f, 0.239999995f);
                boxCollider.size = new Vector3(0.5f, 0.5f, 0.5f);
                collider = boxCollider;
            }

            // VivenSittable 추가
            var sittable = seatObject.GetComponent<VivenSittable>();
            if (sittable == null)
            {
                sittable = Undo.AddComponent<VivenSittable>(seatObject);
            }

            // SitPoint 확인/생성 (VivenSittable이 있어도 sitPoint가 없을 수 있음)
            var sitPoint = seatObject.transform.Find("SitPoint");
            if (sitPoint == null)
            {
                var sitPointGo = new GameObject("SitPoint");
                Undo.RegisterCreatedObjectUndo(sitPointGo, "Create SitPoint");
                sitPointGo.transform.SetParent(seatObject.transform);

                // sitPoint 로컬 포지션 고정값
                sitPointGo.transform.localPosition = new Vector3(-0.256000012f, -0.39199999f, 0.208000004f);
                sitPointGo.transform.localRotation = Quaternion.identity;
                sitPoint = sitPointGo.transform;
            }

            // VivenSittable의 sitPoint 연결
            var serializedSittable = new SerializedObject(sittable);
            if (serializedSittable.FindProperty("sitPoint").objectReferenceValue == null)
            {
                serializedSittable.FindProperty("sitPoint").objectReferenceValue = sitPoint;
                serializedSittable.ApplyModifiedProperties();
            }

            // SitDetector 생성 (SitPoint 아래에)
            var sitDetector = sitPoint.Find("SitDetector");
            if (sitDetector == null)
            {
                var detectorGo = new GameObject("SitDetector");
                Undo.RegisterCreatedObjectUndo(detectorGo, "Create SitDetector");
                detectorGo.transform.SetParent(sitPoint);
                detectorGo.transform.localPosition = Vector3.zero;
                detectorGo.transform.localRotation = Quaternion.identity;

                // Trigger Collider 추가
                var triggerCollider = detectorGo.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
                triggerCollider.size = new Vector3(0.3f, 0.3f, 0.3f);

                sitDetector = detectorGo.transform;
            }

            // SitDetector에 VivenLuaBehaviour 확인/추가
            var sitDetectorLua = sitDetector.GetComponent<VivenLuaBehaviour>();
            if (sitDetectorLua == null)
            {
                sitDetectorLua = sitDetector.gameObject.AddComponent<VivenLuaBehaviour>();
            }

            // SeatController 스크립트 연결
            AssignVivenScript(sitDetectorLua, SEAT_CONTROLLER_SCRIPT_PATH);

            // SeatController Injection 설정 (인덱스 기반 자동 할당)
            SetupSeatControllerInjection(sitDetectorLua, seatObject, arenaXManager, seatIndex);

            EditorUtility.SetDirty(seatObject);
        }

        private void SetupSeatControllerInjection(VivenLuaBehaviour behaviour, GameObject seatObject, GameObject arenaXManager, int seatIndex = 0)
        {
            var serializedObject = new SerializedObject(behaviour);
            var injectionProperty = serializedObject.FindProperty("injection");

            // GameObject Injection
            var gameObjectValues = injectionProperty.FindPropertyRelative("gameObjectValues");
            var goInjections = new Dictionary<string, GameObject>
            {
                { "ArenaXManagerObject", arenaXManager }
            };
            SetGameObjectInjections(gameObjectValues, goInjections);

            // 인덱스 기반 자동 할당 (A-1, A-2, A-3...)
            // 10개씩 한 행으로 가정: 0-9 → A열, 10-19 → B열...
            int rowIndex = seatIndex / 10;
            int seatNumber = (seatIndex % 10) + 1;
            string seatRow = ((char)('A' + rowIndex)).ToString();
            string seatSection = "1층"; // 기본값

            // String Injection (SeatRow, SeatSection)
            var stringValues = injectionProperty.FindPropertyRelative("stringValue");
            SetStringInjection(stringValues, "SeatRow", seatRow);
            SetStringInjection(stringValues, "SeatType", "일반");
            SetStringInjection(stringValues, "SeatSection", seatSection);

            // Int Injection (SeatNumber)
            var intValues = injectionProperty.FindPropertyRelative("intValue");
            SetIntInjection(intValues, "SeatNumber", seatNumber);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(behaviour);

            Debug.Log($"[ArenaX Setup] 좌석 설정: {seatRow}-{seatNumber} ({seatObject.name})");
        }

        private void SetStringInjection(SerializedProperty stringValues, string name, string value)
        {
            // 기존 항목 찾기
            for (int i = 0; i < stringValues.arraySize; i++)
            {
                var element = stringValues.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("name").stringValue == name)
                {
                    element.FindPropertyRelative("value").stringValue = value;
                    return;
                }
            }

            // 새 항목 추가
            stringValues.InsertArrayElementAtIndex(stringValues.arraySize);
            var newElement = stringValues.GetArrayElementAtIndex(stringValues.arraySize - 1);
            newElement.FindPropertyRelative("name").stringValue = name;
            newElement.FindPropertyRelative("value").stringValue = value;
        }

        private void SetIntInjection(SerializedProperty intValues, string name, int value)
        {
            // 기존 항목 찾기
            for (int i = 0; i < intValues.arraySize; i++)
            {
                var element = intValues.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("name").stringValue == name)
                {
                    element.FindPropertyRelative("value").intValue = value;
                    return;
                }
            }

            // 새 항목 추가
            intValues.InsertArrayElementAtIndex(intValues.arraySize);
            var newElement = intValues.GetArrayElementAtIndex(intValues.arraySize - 1);
            newElement.FindPropertyRelative("name").stringValue = name;
            newElement.FindPropertyRelative("value").intValue = value;
        }

        private (string row, int number, string section) ParseSeatInfo(string seatName)
        {
            // 기본값
            string row = "A";
            int number = 1;
            string section = "1층";

            // "Component#N" 형식 파싱
            if (seatName.StartsWith("Component#"))
            {
                var numStr = seatName.Substring("Component#".Length);
                if (int.TryParse(numStr, out int componentNum))
                {
                    // 10개씩 한 열로 가정
                    int rowIndex = componentNum / 10;
                    row = ((char)('A' + rowIndex)).ToString();
                    number = (componentNum % 10) + 1;
                }
            }
            // "Seat_A_1" 형식 파싱
            else if (seatName.Contains("_"))
            {
                var parts = seatName.Split('_');
                if (parts.Length >= 2)
                {
                    row = parts[1];
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int num))
                    {
                        number = num;
                    }
                }
            }
            // "SeatA1" 형식 파싱
            else if (seatName.StartsWith("Seat"))
            {
                var info = seatName.Substring(4);
                if (info.Length >= 1)
                {
                    row = info[0].ToString();
                    if (info.Length > 1 && int.TryParse(info.Substring(1), out int num))
                    {
                        number = num;
                    }
                }
            }

            return (row, number, section);
        }

        #endregion
    }
}
