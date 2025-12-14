using System.Collections.Generic;
using TMPro;
using TwentyOz.VivenSDK.Scripts.Core.Lua;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaX.Editor
{
    /// <summary>
    /// Arena X 관객 풀 자동 설정 에디터 도구
    /// 5개 타입 × 10개 인스턴스 = 총 50명의 관객 풀 구성
    /// Object Pooling 방식으로 MeshRenderer/Collider 토글
    /// </summary>
    public class AudienceSetup : EditorWindow
    {
        // 설정
        private const int POOL_TYPE_COUNT = 5;
        private const int INSTANCES_PER_TYPE = 10;
        private const string AUDIENCE_MANAGER_SCRIPT_PATH = "Assets/ArenaX/Scripts/Avatar/AudienceManager.lua";

        // 프리팹 슬롯
        private GameObject[] audiencePrefabs = new GameObject[POOL_TYPE_COUNT];
        private string[] prefabLabels = { "Type1 (남성1)", "Type2 (남성2)", "Type3 (여성1)", "Type4 (여성2)", "Type5 (기타)" };

        // 씬 참조
        private GameObject audiencePoolParent;
        private GameObject audienceManagerObject;
        private GameObject seatUIManagerObject;
        private GameObject audienceToggleButton;
        private GameObject defaultCanvas;
        private GameObject minimapObject;

        // 상태
        private bool hasAudiencePool;
        private bool hasToggleButton;
        private int totalPooledCount;
        private Vector2 scrollPosition;

        // 배치 설정
        private Vector3 hidePosition = new Vector3(0, -9999, 0);
        private float spacing = 1.0f;

        [MenuItem("ArenaX/관객 풀 설정", false, 101)]
        public static void ShowWindow()
        {
            var window = GetWindow<AudienceSetup>("관객 풀 설정");
            window.minSize = new Vector2(450, 550);
            window.ValidateScene();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSceneStatus();
            EditorGUILayout.Space(10);

            DrawPrefabSlots();
            EditorGUILayout.Space(10);

            DrawSetupActions();
            EditorGUILayout.Space(10);

            DrawToggleButtonSection();
            EditorGUILayout.Space(10);

            DrawPoolInfo();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("관객 풀 설정 도구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "관객 Object Pool을 자동으로 생성합니다.\n\n" +
                "• 5개 타입 × 10개 인스턴스 = 총 50명\n" +
                "• MeshRenderer/Collider 토글 방식 (SetActive 대신)\n" +
                "• 숨김 위치: (0, -9999, 0)\n\n" +
                "사용법:\n" +
                "1. 앉은 자세 관객 프리팹 5종 등록\n" +
                "2. '관객 풀 생성' 클릭\n" +
                "3. AudienceManager에 자동 연결됨",
                MessageType.Info);
        }

        private void DrawSceneStatus()
        {
            EditorGUILayout.LabelField("현재 상태", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            // AudiencePool 상태
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AudiencePool", GUILayout.Width(150));
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = hasAudiencePool ? Color.green : Color.red;
            EditorGUILayout.LabelField(hasAudiencePool ? $"✓ 존재함 ({totalPooledCount}개)" : "✗ 없음", style);
            EditorGUILayout.EndHorizontal();

            // AudienceManager 상태
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("AudienceManager", GUILayout.Width(150));
            style.normal.textColor = audienceManagerObject != null ? Color.green : Color.red;
            EditorGUILayout.LabelField(audienceManagerObject != null ? "✓ 존재함" : "✗ 없음", style);
            EditorGUILayout.EndHorizontal();

            // SeatUIManager 상태
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("SeatUIManager", GUILayout.Width(150));
            style.normal.textColor = seatUIManagerObject != null ? Color.green : Color.red;
            EditorGUILayout.LabelField(seatUIManagerObject != null ? "✓ 존재함" : "✗ 없음", style);
            EditorGUILayout.EndHorizontal();

            // DefaultCanvas 상태
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("DefaultCanvas", GUILayout.Width(150));
            style.normal.textColor = defaultCanvas != null ? Color.green : Color.red;
            EditorGUILayout.LabelField(defaultCanvas != null ? "✓ 존재함" : "✗ 없음", style);
            EditorGUILayout.EndHorizontal();

            // 토글 버튼 상태
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("관객 토글 버튼", GUILayout.Width(150));
            style.normal.textColor = hasToggleButton ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(hasToggleButton ? "✓ 존재함" : "✗ 없음", style);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;

            if (GUILayout.Button("상태 새로고침"))
            {
                ValidateScene();
            }
        }

        private void DrawPrefabSlots()
        {
            EditorGUILayout.LabelField("관객 프리팹 설정", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "앉은 자세의 관객 프리팹 5종을 등록하세요.\n" +
                "각 프리팹은 MeshRenderer 또는 SkinnedMeshRenderer가 필요합니다.",
                MessageType.None);

            EditorGUILayout.Space(5);

            for (int i = 0; i < POOL_TYPE_COUNT; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(prefabLabels[i], GUILayout.Width(100));
                audiencePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                    audiencePrefabs[i], typeof(GameObject), false);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            // 프리팹 카운트
            int validPrefabs = 0;
            foreach (var prefab in audiencePrefabs)
            {
                if (prefab != null) validPrefabs++;
            }
            EditorGUILayout.LabelField($"등록된 프리팹: {validPrefabs}/{POOL_TYPE_COUNT}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupActions()
        {
            EditorGUILayout.LabelField("설정 작업", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 프리팹 유효성 검사
            int validPrefabs = 0;
            foreach (var prefab in audiencePrefabs)
            {
                if (prefab != null) validPrefabs++;
            }

            // 관객 풀 생성 버튼
            GUI.backgroundColor = validPrefabs > 0 ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
            GUI.enabled = validPrefabs > 0;
            if (GUILayout.Button($"🎭 관객 풀 생성\n({validPrefabs}개 타입 × {INSTANCES_PER_TYPE}개 = {validPrefabs * INSTANCES_PER_TYPE}명)", GUILayout.Height(50)))
            {
                CreateAudiencePool();
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            // AudienceManager 연결 버튼
            GUI.backgroundColor = hasAudiencePool && audienceManagerObject != null ? new Color(0.6f, 0.8f, 1f) : Color.gray;
            GUI.enabled = hasAudiencePool && audienceManagerObject != null;
            if (GUILayout.Button("🔗 AudienceManager에 풀 연결", GUILayout.Height(30)))
            {
                ConnectPoolToManager();
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // 정리 버튼
            EditorGUILayout.LabelField("정리 도구", EditorStyles.miniBoldLabel);

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("🗑️ 관객 풀 삭제", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("확인",
                    "AudiencePool 오브젝트와 모든 자식을 삭제합니다.\n계속하시겠습니까?",
                    "삭제", "취소"))
                {
                    DeleteAudiencePool();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolInfo()
        {
            if (!hasAudiencePool || audiencePoolParent == null) return;

            EditorGUILayout.LabelField("풀 구성 정보", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < POOL_TYPE_COUNT; i++)
            {
                var poolName = $"Type{i + 1}Pool";
                var poolTransform = audiencePoolParent.transform.Find(poolName);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(poolName, GUILayout.Width(100));

                if (poolTransform != null)
                {
                    int childCount = poolTransform.childCount;
                    var countStyle = new GUIStyle(EditorStyles.label);
                    countStyle.normal.textColor = childCount >= INSTANCES_PER_TYPE ? Color.green : Color.yellow;
                    EditorGUILayout.LabelField($"{childCount}개", countStyle, GUILayout.Width(50));

                    if (GUILayout.Button("선택", GUILayout.Width(50)))
                    {
                        Selection.activeGameObject = poolTransform.gameObject;
                        EditorGUIUtility.PingObject(poolTransform.gameObject);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("없음", GUILayout.Width(50));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #region Validation

        private void ValidateScene()
        {
            audiencePoolParent = GameObject.Find("AudiencePool");
            audienceManagerObject = GameObject.Find("AudienceManager");
            seatUIManagerObject = GameObject.Find("SeatUIManager");
            defaultCanvas = GameObject.Find("DefaultCanvas");
            minimapObject = GameObject.Find("Minimap");
            audienceToggleButton = GameObject.Find("AudienceToggleButton");

            hasAudiencePool = audiencePoolParent != null;
            hasToggleButton = audienceToggleButton != null;

            totalPooledCount = 0;
            if (hasAudiencePool)
            {
                for (int i = 0; i < POOL_TYPE_COUNT; i++)
                {
                    var poolTransform = audiencePoolParent.transform.Find($"Type{i + 1}Pool");
                    if (poolTransform != null)
                    {
                        totalPooledCount += poolTransform.childCount;
                    }
                }
            }

            Repaint();
        }

        #endregion

        #region Pool Creation

        private void CreateAudiencePool()
        {
            Undo.SetCurrentGroupName("관객 풀 생성");
            var undoGroup = Undo.GetCurrentGroup();

            // 기존 풀 삭제 (있다면)
            if (audiencePoolParent != null)
            {
                Undo.DestroyObjectImmediate(audiencePoolParent);
            }

            // AudiencePool 부모 생성
            audiencePoolParent = new GameObject("AudiencePool");
            Undo.RegisterCreatedObjectUndo(audiencePoolParent, "Create AudiencePool");
            audiencePoolParent.transform.position = Vector3.zero;

            int totalCreated = 0;

            // 각 타입별 풀 생성
            for (int typeIndex = 0; typeIndex < POOL_TYPE_COUNT; typeIndex++)
            {
                var prefab = audiencePrefabs[typeIndex];
                if (prefab == null) continue;

                // TypeN Pool 생성
                var poolName = $"Type{typeIndex + 1}Pool";
                var poolGo = new GameObject(poolName);
                Undo.RegisterCreatedObjectUndo(poolGo, $"Create {poolName}");
                poolGo.transform.SetParent(audiencePoolParent.transform);
                poolGo.transform.localPosition = Vector3.zero;

                // 인스턴스 생성
                for (int i = 0; i < INSTANCES_PER_TYPE; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, $"Create Audience Instance");

                    instance.name = $"Audience_{typeIndex + 1}_{i + 1}";
                    instance.transform.SetParent(poolGo.transform);

                    // 숨김 위치로 이동
                    instance.transform.position = hidePosition;
                    instance.transform.rotation = Quaternion.identity;

                    // MeshRenderer 비활성화
                    DisableRenderers(instance);

                    // Collider 비활성화
                    DisableColliders(instance);

                    totalCreated++;
                }

                Debug.Log($"[AudienceSetup] {poolName} 생성 완료: {INSTANCES_PER_TYPE}개");
            }

            Undo.CollapseUndoOperations(undoGroup);

            ValidateScene();

            // AudienceManager 자동 연결
            if (audienceManagerObject != null)
            {
                ConnectPoolToManager();
            }

            EditorUtility.DisplayDialog("완료",
                $"관객 풀 생성 완료!\n\n" +
                $"• 총 {totalCreated}개 인스턴스 생성\n" +
                $"• 위치: AudiencePool\n" +
                $"• 초기 상태: 숨김 (MeshRenderer/Collider 비활성화)",
                "확인");
        }

        private void DisableRenderers(GameObject go)
        {
            // MeshRenderer
            var meshRenderers = go.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                mr.enabled = false;
            }

            // SkinnedMeshRenderer
            var skinnedRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedRenderers)
            {
                smr.enabled = false;
            }
        }

        private void DisableColliders(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        #endregion

        #region Manager Connection

        private void ConnectPoolToManager()
        {
            if (audienceManagerObject == null || audiencePoolParent == null)
            {
                Debug.LogWarning("[AudienceSetup] AudienceManager 또는 AudiencePool이 없습니다.");
                return;
            }

            var luaBehaviour = audienceManagerObject.GetComponent<VivenLuaBehaviour>();
            if (luaBehaviour == null)
            {
                Debug.LogWarning("[AudienceSetup] AudienceManager에 VivenLuaBehaviour가 없습니다.");
                return;
            }

            Undo.RecordObject(luaBehaviour, "Connect Audience Pool");

            var serializedObject = new SerializedObject(luaBehaviour);
            var injectionProperty = serializedObject.FindProperty("injection");
            var gameObjectValues = injectionProperty.FindPropertyRelative("gameObjectValues");

            // 각 타입 풀 연결
            var poolInjections = new Dictionary<string, GameObject>();

            for (int i = 0; i < POOL_TYPE_COUNT; i++)
            {
                var poolName = $"Type{i + 1}Pool";
                var poolTransform = audiencePoolParent.transform.Find(poolName);
                if (poolTransform != null)
                {
                    poolInjections[poolName] = poolTransform.gameObject;
                }
            }

            // ArenaXManager 연결
            var arenaXManager = GameObject.Find("ArenaXManager");
            if (arenaXManager != null)
            {
                poolInjections["ArenaXManagerObject"] = arenaXManager;
            }

            SetGameObjectInjections(gameObjectValues, poolInjections);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(luaBehaviour);

            Debug.Log($"[AudienceSetup] AudienceManager에 {poolInjections.Count}개 풀 연결 완료");
            EditorUtility.DisplayDialog("완료", "AudienceManager에 풀이 연결되었습니다.", "확인");
        }

        private void SetGameObjectInjections(SerializedProperty gameObjectValues, Dictionary<string, GameObject> injections)
        {
            var existingNames = new HashSet<string>();

            // 기존 항목 업데이트
            for (int i = 0; i < gameObjectValues.arraySize; i++)
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

        #endregion

        #region Cleanup

        private void DeleteAudiencePool()
        {
            if (audiencePoolParent != null)
            {
                Undo.DestroyObjectImmediate(audiencePoolParent);
                Debug.Log("[AudienceSetup] AudiencePool 삭제됨");
            }

            ValidateScene();
        }

        #endregion

        #region Toggle Button UI

        private void DrawToggleButtonSection()
        {
            EditorGUILayout.LabelField("관객 토글 버튼 UI", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "관객 표시/숨기기 토글 버튼을 생성합니다.\n" +
                "DefaultCanvas의 Minimap 아래에 배치됩니다.",
                MessageType.None);

            EditorGUILayout.Space(5);

            // 토글 버튼 생성 버튼 (DefaultCanvas 필요)
            bool canCreateButton = !hasToggleButton && defaultCanvas != null;
            GUI.backgroundColor = canCreateButton ? new Color(0.6f, 0.8f, 1f) : Color.gray;
            GUI.enabled = canCreateButton;
            if (GUILayout.Button("🔘 관객 토글 버튼 생성", GUILayout.Height(35)))
            {
                CreateToggleButton();
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            // 연결 버튼
            bool canConnect = hasToggleButton && seatUIManagerObject != null;
            GUI.backgroundColor = canConnect ? new Color(0.6f, 0.8f, 1f) : Color.gray;
            GUI.enabled = canConnect;
            if (GUILayout.Button("🔗 SeatUIManager에 연결", GUILayout.Height(25)))
            {
                ConnectToggleButtonToUIManager();
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            // 삭제 버튼
            GUI.backgroundColor = hasToggleButton ? new Color(1f, 0.6f, 0.6f) : Color.gray;
            GUI.enabled = hasToggleButton;
            if (GUILayout.Button("🗑️ 토글 버튼 삭제", GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("확인", "관객 토글 버튼을 삭제합니다.", "삭제", "취소"))
                {
                    DeleteToggleButton();
                }
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void CreateToggleButton()
        {
            Undo.SetCurrentGroupName("관객 토글 버튼 생성");
            var undoGroup = Undo.GetCurrentGroup();

            // 부모 Canvas 찾기 - DefaultCanvas 우선
            Canvas parentCanvas = null;
            if (defaultCanvas != null)
            {
                parentCanvas = defaultCanvas.GetComponent<Canvas>();
            }
            else if (seatUIManagerObject != null)
            {
                parentCanvas = seatUIManagerObject.GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null)
            {
                EditorUtility.DisplayDialog("오류",
                    "DefaultCanvas를 찾을 수 없습니다.\n" +
                    "씬에 DefaultCanvas가 있는지 확인하세요.",
                    "확인");
                return;
            }

            // 버튼 생성
            var buttonGo = new GameObject("AudienceToggleButton");
            Undo.RegisterCreatedObjectUndo(buttonGo, "Create Toggle Button");
            buttonGo.transform.SetParent(parentCanvas.transform, false);

            // RectTransform 설정 - Minimap 아래에 배치
            // Minimap: anchor(0,1), anchoredPosition(241.99, -360), size(300, 300)
            var rectTransform = buttonGo.AddComponent<RectTransform>();

            // Minimap 아래에 배치
            if (minimapObject != null)
            {
                var minimapRect = minimapObject.GetComponent<RectTransform>();
                if (minimapRect != null)
                {
                    // Minimap과 같은 앵커 사용 (왼쪽 상단)
                    rectTransform.anchorMin = minimapRect.anchorMin;
                    rectTransform.anchorMax = minimapRect.anchorMax;
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);

                    // Minimap 하단 아래로 배치
                    // Minimap 하단 = anchoredPosition.y - sizeDelta.y/2
                    float minimapBottom = minimapRect.anchoredPosition.y - minimapRect.sizeDelta.y / 2;
                    rectTransform.anchoredPosition = new Vector2(minimapRect.anchoredPosition.x, minimapBottom - 30);
                }
                else
                {
                    // 기본값: 왼쪽 상단 앵커
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(0, 1);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchoredPosition = new Vector2(242, -540);
                }
            }
            else
            {
                // Minimap이 없으면 왼쪽 하단에 배치
                rectTransform.anchorMin = new Vector2(0, 0);
                rectTransform.anchorMax = new Vector2(0, 0);
                rectTransform.pivot = new Vector2(0, 0);
                rectTransform.anchoredPosition = new Vector2(20, 20);
            }

            rectTransform.sizeDelta = new Vector2(150, 40);

            // Image 컴포넌트
            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Button 컴포넌트
            var button = buttonGo.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            button.colors = colors;

            // 텍스트 자식 오브젝트 생성
            var textGo = new GameObject("Text");
            Undo.RegisterCreatedObjectUndo(textGo, "Create Button Text");
            textGo.transform.SetParent(buttonGo.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // TextMeshProUGUI 컴포넌트
            var uiText = textGo.AddComponent<TextMeshProUGUI>();
            uiText.text = "관객 보기";
            uiText.fontSize = 18;
            uiText.alignment = TextAlignmentOptions.Center;
            uiText.color = Color.white;

            Undo.CollapseUndoOperations(undoGroup);

            audienceToggleButton = buttonGo;
            hasToggleButton = true;

            // 자동으로 SeatUIManager에 연결
            if (seatUIManagerObject != null)
            {
                ConnectToggleButtonToUIManager();
            }

            ValidateScene();

            EditorUtility.DisplayDialog("완료",
                "관객 토글 버튼이 생성되었습니다.\n\n" +
                "위치나 크기는 필요에 따라 수동으로 조정하세요.",
                "확인");
        }

        private void ConnectToggleButtonToUIManager()
        {
            if (seatUIManagerObject == null || audienceToggleButton == null)
            {
                Debug.LogWarning("[AudienceSetup] SeatUIManager 또는 토글 버튼이 없습니다.");
                return;
            }

            var luaBehaviour = seatUIManagerObject.GetComponent<VivenLuaBehaviour>();
            if (luaBehaviour == null)
            {
                Debug.LogWarning("[AudienceSetup] SeatUIManager에 VivenLuaBehaviour가 없습니다.");
                return;
            }

            Undo.RecordObject(luaBehaviour, "Connect Toggle Button");

            var serializedObject = new SerializedObject(luaBehaviour);
            var injectionProperty = serializedObject.FindProperty("injection");
            var gameObjectValues = injectionProperty.FindPropertyRelative("gameObjectValues");

            // AudienceToggleButton 연결
            var injections = new Dictionary<string, GameObject>
            {
                { "AudienceToggleButton", audienceToggleButton }
            };

            SetGameObjectInjections(gameObjectValues, injections);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(luaBehaviour);

            Debug.Log("[AudienceSetup] 토글 버튼이 SeatUIManager에 연결되었습니다.");
            EditorUtility.DisplayDialog("완료", "토글 버튼이 SeatUIManager에 연결되었습니다.", "확인");
        }

        private void DeleteToggleButton()
        {
            if (audienceToggleButton != null)
            {
                Undo.DestroyObjectImmediate(audienceToggleButton);
                Debug.Log("[AudienceSetup] 토글 버튼 삭제됨");
            }

            ValidateScene();
        }

        #endregion
    }
}
