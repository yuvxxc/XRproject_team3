using System.Collections.Generic;
using TwentyOz.VivenSDK.Scripts.Core.Lua;
using UnityEditor;
using UnityEngine;

namespace ArenaX.Editor
{
    /// <summary>
    /// 좌석별 앞좌석 Transform 자동 생성 도구
    /// SeatController가 있는 오브젝트에 FrontSeat1~10 Transform을 자동 생성
    /// </summary>
    public class SeatFrontSeatsSetup : EditorWindow
    {
        // 설정
        private const int MAX_FRONT_SEATS = 20;

        // 배치 설정
        private float frontDistance = 1.0f;      // 좌석 앞쪽으로 떨어진 거리
        private float sideSpacing = 0.6f;        // 좌우 간격
        private float heightOffset = 0.0f;       // 높이 오프셋
        private int frontSeatsPerSeat = 5;       // 좌석당 앞좌석 수 (1~10)
        private int rowCount = 1;                // 행 수 (1~2)
        private float rowDistance = 0.8f;        // 행 간 거리

        // 상태
        private Vector2 scrollPosition;
        private List<GameObject> foundSeats = new List<GameObject>();
        private bool showAdvancedSettings = false;

        [MenuItem("ArenaX/앞좌석 Transform 설정", false, 102)]
        public static void ShowWindow()
        {
            var window = GetWindow<SeatFrontSeatsSetup>("앞좌석 설정");
            window.minSize = new Vector2(400, 500);
            window.FindSeatsInScene();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSettings();
            EditorGUILayout.Space(10);

            DrawSeatList();
            EditorGUILayout.Space(10);

            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("앞좌석 Transform 설정 도구", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "SeatController가 있는 좌석들에 앞좌석 Transform을 자동 생성합니다.\n\n" +
                "• 좌석 앞쪽에 빈 Transform 생성\n" +
                "• VivenLuaBehaviour에 자동 Injection 연결\n" +
                "• 관객이 배치될 위치로 사용됨\n\n" +
                "사용법:\n" +
                "1. 배치 설정 조정\n" +
                "2. '앞좌석 Transform 생성' 클릭\n" +
                "3. 필요시 수동으로 위치 미세 조정",
                MessageType.Info);
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("배치 설정", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            frontSeatsPerSeat = EditorGUILayout.IntSlider("좌석당 앞좌석 수", frontSeatsPerSeat, 1, MAX_FRONT_SEATS);
            frontDistance = EditorGUILayout.Slider("앞쪽 거리", frontDistance, 0.5f, 3.0f);
            sideSpacing = EditorGUILayout.Slider("좌우 간격", sideSpacing, 0.3f, 1.5f);

            EditorGUILayout.Space(5);

            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "고급 설정");
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                heightOffset = EditorGUILayout.Slider("높이 오프셋", heightOffset, -1.0f, 1.0f);
                rowCount = EditorGUILayout.IntSlider("행 수", rowCount, 1, 2);
                if (rowCount > 1)
                {
                    rowDistance = EditorGUILayout.Slider("행 간 거리", rowDistance, 0.5f, 2.0f);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // 미리보기 정보
            int totalFrontSeats = frontSeatsPerSeat;
            EditorGUILayout.LabelField($"생성될 앞좌석: 좌석당 {totalFrontSeats}개", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawSeatList()
        {
            EditorGUILayout.LabelField($"발견된 좌석 ({foundSeats.Count}개)", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (foundSeats.Count == 0)
            {
                EditorGUILayout.HelpBox("SeatController가 있는 좌석을 찾을 수 없습니다.", MessageType.Warning);
            }
            else
            {
                // 최대 10개만 표시
                int displayCount = Mathf.Min(foundSeats.Count, 10);
                for (int i = 0; i < displayCount; i++)
                {
                    var seat = foundSeats[i];
                    if (seat == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    // 앞좌석 존재 여부 체크
                    var frontSeatParent = seat.transform.Find("FrontSeats");
                    bool hasFrontSeats = frontSeatParent != null && frontSeatParent.childCount > 0;

                    var style = new GUIStyle(EditorStyles.label);
                    style.normal.textColor = hasFrontSeats ? Color.green : Color.yellow;

                    EditorGUILayout.LabelField(seat.name, style, GUILayout.Width(200));
                    EditorGUILayout.LabelField(hasFrontSeats ? $"✓ {frontSeatParent.childCount}개" : "없음", GUILayout.Width(60));

                    if (GUILayout.Button("선택", GUILayout.Width(50)))
                    {
                        Selection.activeGameObject = seat;
                        EditorGUIUtility.PingObject(seat);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (foundSeats.Count > 10)
                {
                    EditorGUILayout.LabelField($"... 외 {foundSeats.Count - 10}개", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("좌석 다시 검색"))
            {
                FindSeatsInScene();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("작업", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 생성 버튼
            GUI.backgroundColor = foundSeats.Count > 0 ? new Color(0.4f, 0.8f, 0.4f) : Color.gray;
            GUI.enabled = foundSeats.Count > 0;
            if (GUILayout.Button($"🪑 앞좌석 Transform 생성\n({foundSeats.Count}개 좌석 × {frontSeatsPerSeat}개 = {foundSeats.Count * frontSeatsPerSeat}개)", GUILayout.Height(50)))
            {
                CreateFrontSeatsForAllSeats();
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // 삭제 버튼
            EditorGUILayout.LabelField("정리 도구", EditorStyles.miniBoldLabel);

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("🗑️ 모든 앞좌석 Transform 삭제", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("확인",
                    "모든 좌석의 FrontSeats를 삭제합니다.\n계속하시겠습니까?",
                    "삭제", "취소"))
                {
                    DeleteAllFrontSeats();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        #region Find Seats

        private void FindSeatsInScene()
        {
            foundSeats.Clear();

            // VivenLuaBehaviour 컴포넌트가 있는 모든 오브젝트 찾기
            var allLuaBehaviours = FindObjectsOfType<VivenLuaBehaviour>();

            foreach (var luaBehaviour in allLuaBehaviours)
            {
                // SeatController.lua를 사용하는지 확인
                if (luaBehaviour.luaScript != null &&
                    luaBehaviour.luaScript.name.Contains("SeatController"))
                {
                    foundSeats.Add(luaBehaviour.gameObject);
                }
            }

            Debug.Log($"[SeatFrontSeatsSetup] Found {foundSeats.Count} seats with SeatController");
            Repaint();
        }

        #endregion

        #region Create Front Seats

        private void CreateFrontSeatsForAllSeats()
        {
            Undo.SetCurrentGroupName("앞좌석 Transform 생성");
            var undoGroup = Undo.GetCurrentGroup();

            int totalCreated = 0;

            foreach (var seat in foundSeats)
            {
                if (seat == null) continue;
                totalCreated += CreateFrontSeatsForSeat(seat);
            }

            Undo.CollapseUndoOperations(undoGroup);

            FindSeatsInScene(); // 상태 업데이트

            EditorUtility.DisplayDialog("완료",
                $"앞좌석 Transform 생성 완료!\n\n" +
                $"• 총 {totalCreated}개 Transform 생성\n" +
                $"• Injection 자동 연결됨",
                "확인");
        }

        private int CreateFrontSeatsForSeat(GameObject seat)
        {
            // 기존 FrontSeats 삭제
            var existingFrontSeats = seat.transform.Find("FrontSeats");
            if (existingFrontSeats != null)
            {
                Undo.DestroyObjectImmediate(existingFrontSeats.gameObject);
            }

            // FrontSeats 부모 생성
            var frontSeatsParent = new GameObject("FrontSeats");
            Undo.RegisterCreatedObjectUndo(frontSeatsParent, "Create FrontSeats Parent");
            frontSeatsParent.transform.SetParent(seat.transform);
            frontSeatsParent.transform.localPosition = Vector3.zero;
            frontSeatsParent.transform.localRotation = Quaternion.identity;

            // 좌석의 forward 방향 계산 (좌석이 바라보는 방향)
            var seatForward = seat.transform.forward;
            var seatRight = seat.transform.right;
            var seatPosition = seat.transform.position;

            // 앞좌석 Transform 생성
            var createdTransforms = new List<Transform>();

            for (int i = 0; i < frontSeatsPerSeat; i++)
            {
                var frontSeat = new GameObject($"FrontSeat{i + 1}");
                Undo.RegisterCreatedObjectUndo(frontSeat, $"Create FrontSeat{i + 1}");
                frontSeat.transform.SetParent(frontSeatsParent.transform);

                // 위치 계산
                Vector3 position = CalculateFrontSeatPosition(i, seatPosition, seatForward, seatRight);
                frontSeat.transform.position = position;

                // 좌석과 같은 방향 바라보기
                frontSeat.transform.rotation = seat.transform.rotation;

                createdTransforms.Add(frontSeat.transform);
            }

            // VivenLuaBehaviour에 Injection 연결
            ConnectInjections(seat, createdTransforms);

            return frontSeatsPerSeat;
        }

        private Vector3 CalculateFrontSeatPosition(int index, Vector3 seatPosition, Vector3 forward, Vector3 right)
        {
            // 행과 열 계산
            int seatsPerRow = Mathf.CeilToInt((float)frontSeatsPerSeat / rowCount);
            int row = index / seatsPerRow;
            int col = index % seatsPerRow;

            // 중앙 정렬을 위한 오프셋
            float totalWidth = (seatsPerRow - 1) * sideSpacing;
            float startOffset = -totalWidth / 2f;

            // 위치 계산
            float forwardOffset = frontDistance + (row * rowDistance);
            float sideOffset = startOffset + (col * sideSpacing);

            Vector3 position = seatPosition
                + forward * forwardOffset
                + right * sideOffset
                + Vector3.up * heightOffset;

            return position;
        }

        private void ConnectInjections(GameObject seat, List<Transform> frontSeatTransforms)
        {
            var luaBehaviour = seat.GetComponent<VivenLuaBehaviour>();
            if (luaBehaviour == null)
            {
                // 부모에서 찾기
                luaBehaviour = seat.GetComponentInParent<VivenLuaBehaviour>();
            }

            if (luaBehaviour == null)
            {
                Debug.LogWarning($"[SeatFrontSeatsSetup] VivenLuaBehaviour not found on {seat.name}");
                return;
            }

            Undo.RecordObject(luaBehaviour, "Connect Front Seat Injections");

            var serializedObject = new SerializedObject(luaBehaviour);
            var injectionProperty = serializedObject.FindProperty("injection");

            // Transform은 objectValues에 저장 (UnityEngine.Object 타입)
            var objectValues = injectionProperty.FindPropertyRelative("objectValues");

            if (objectValues == null)
            {
                Debug.LogError($"[SeatFrontSeatsSetup] objectValues property not found");
                return;
            }

            // FrontSeat1~10 연결
            var injections = new Dictionary<string, Object>();
            for (int i = 0; i < frontSeatTransforms.Count && i < MAX_FRONT_SEATS; i++)
            {
                injections[$"FrontSeat{i + 1}"] = frontSeatTransforms[i];
            }

            SetObjectInjections(objectValues, injections);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(luaBehaviour);

            Debug.Log($"[SeatFrontSeatsSetup] Connected {injections.Count} front seat transforms to {seat.name}");
        }

        private void SetObjectInjections(SerializedProperty objectValues, Dictionary<string, Object> injections)
        {
            var existingNames = new HashSet<string>();

            // 기존 항목 업데이트
            for (int i = 0; i < objectValues.arraySize; i++)
            {
                var element = objectValues.GetArrayElementAtIndex(i);
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
                    objectValues.InsertArrayElementAtIndex(objectValues.arraySize);
                    var newElement = objectValues.GetArrayElementAtIndex(objectValues.arraySize - 1);
                    newElement.FindPropertyRelative("name").stringValue = kvp.Key;
                    newElement.FindPropertyRelative("value").objectReferenceValue = kvp.Value;
                }
            }
        }

        #endregion

        #region Delete Front Seats

        private void DeleteAllFrontSeats()
        {
            Undo.SetCurrentGroupName("앞좌석 Transform 삭제");
            var undoGroup = Undo.GetCurrentGroup();

            int deletedCount = 0;

            foreach (var seat in foundSeats)
            {
                if (seat == null) continue;

                var frontSeats = seat.transform.Find("FrontSeats");
                if (frontSeats != null)
                {
                    deletedCount++;
                    Undo.DestroyObjectImmediate(frontSeats.gameObject);
                }

                // Injection도 정리 (null로 설정)
                ClearFrontSeatInjections(seat);
            }

            Undo.CollapseUndoOperations(undoGroup);

            FindSeatsInScene();

            Debug.Log($"[SeatFrontSeatsSetup] Deleted front seats from {deletedCount} seats");
        }

        private void ClearFrontSeatInjections(GameObject seat)
        {
            var luaBehaviour = seat.GetComponent<VivenLuaBehaviour>();
            if (luaBehaviour == null)
            {
                luaBehaviour = seat.GetComponentInParent<VivenLuaBehaviour>();
            }

            if (luaBehaviour == null) return;

            Undo.RecordObject(luaBehaviour, "Clear Front Seat Injections");

            var serializedObject = new SerializedObject(luaBehaviour);
            var injectionProperty = serializedObject.FindProperty("injection");
            var objectValues = injectionProperty.FindPropertyRelative("objectValues");

            if (objectValues == null) return;

            // FrontSeat1~20 null로 설정
            for (int i = 0; i < objectValues.arraySize; i++)
            {
                var element = objectValues.GetArrayElementAtIndex(i);
                var nameProperty = element.FindPropertyRelative("name");
                var name = nameProperty.stringValue;

                if (name.StartsWith("FrontSeat"))
                {
                    var valueProperty = element.FindPropertyRelative("value");
                    valueProperty.objectReferenceValue = null;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(luaBehaviour);
        }

        #endregion
    }
}
