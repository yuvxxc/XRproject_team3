using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace ArenaX.Editor
{
    /// <summary>
    /// DefaultCanvas에 UI 토글 시스템을 설정하는 에디터 도구
    /// - UIToggleButton: 항상 보이는 토글 버튼
    /// - MainUI: 토글되는 메인 UI 컨테이너
    /// </summary>
    public class UIToggleSetup : EditorWindow
    {
        private GameObject defaultCanvas;

        [MenuItem("ArenaX/Setup UI Toggle System")]
        public static void ShowWindow()
        {
            GetWindow<UIToggleSetup>("UI Toggle Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("UI 토글 시스템 설정", EditorStyles.boldLabel);
            GUILayout.Space(10);

            defaultCanvas = (GameObject)EditorGUILayout.ObjectField(
                "DefaultCanvas",
                defaultCanvas,
                typeof(GameObject),
                true
            );

            GUILayout.Space(10);

            if (defaultCanvas == null)
            {
                // 자동 찾기 버튼
                if (GUILayout.Button("DefaultCanvas 자동 찾기"))
                {
                    defaultCanvas = GameObject.Find("DefaultCanvas");
                    if (defaultCanvas == null)
                    {
                        EditorUtility.DisplayDialog("오류", "DefaultCanvas를 찾을 수 없습니다.", "확인");
                    }
                }
            }

            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(defaultCanvas == null);

            if (GUILayout.Button("UI 토글 시스템 설정", GUILayout.Height(40)))
            {
                SetupUIToggleSystem();
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(20);
            GUILayout.Label("설정 결과:", EditorStyles.boldLabel);
            GUILayout.Label(@"
DefaultCanvas
├── UIToggleButton (항상 보임)
│   └── Text (TMP) - 'UI 열기'
└── MainUI (토글됨)
    ├── Minimap
    ├── AudienceToggleButton
    └── ... 기타 UI 요소
", EditorStyles.helpBox);
        }

        private void SetupUIToggleSystem()
        {
            if (defaultCanvas == null)
            {
                EditorUtility.DisplayDialog("오류", "DefaultCanvas를 선택해주세요.", "확인");
                return;
            }

            Undo.RegisterCompleteObjectUndo(defaultCanvas, "Setup UI Toggle System");

            RectTransform canvasRect = defaultCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                EditorUtility.DisplayDialog("오류", "DefaultCanvas에 RectTransform이 없습니다.", "확인");
                return;
            }

            // 1. MainUI 컨테이너 생성 (없으면)
            Transform mainUITransform = defaultCanvas.transform.Find("MainUI");
            GameObject mainUI;

            if (mainUITransform == null)
            {
                mainUI = new GameObject("MainUI");
                mainUI.transform.SetParent(defaultCanvas.transform, false);

                RectTransform mainUIRect = mainUI.AddComponent<RectTransform>();
                // 전체 영역 채우기
                mainUIRect.anchorMin = Vector2.zero;
                mainUIRect.anchorMax = Vector2.one;
                mainUIRect.offsetMin = Vector2.zero;
                mainUIRect.offsetMax = Vector2.zero;

                Undo.RegisterCreatedObjectUndo(mainUI, "Create MainUI");
                Debug.Log("[UIToggleSetup] MainUI 컨테이너 생성됨");
            }
            else
            {
                mainUI = mainUITransform.gameObject;
                Debug.Log("[UIToggleSetup] 기존 MainUI 사용");
            }

            // 2. 기존 UI 요소들을 MainUI로 이동
            MoveChildrenToMainUI(defaultCanvas.transform, mainUI.transform);

            // 3. UIToggleButton 생성 (없으면)
            Transform toggleButtonTransform = defaultCanvas.transform.Find("UIToggleButton");
            GameObject toggleButton;

            if (toggleButtonTransform == null)
            {
                toggleButton = CreateToggleButton(defaultCanvas.transform);
                Undo.RegisterCreatedObjectUndo(toggleButton, "Create UIToggleButton");
                Debug.Log("[UIToggleSetup] UIToggleButton 생성됨");
            }
            else
            {
                toggleButton = toggleButtonTransform.gameObject;
                Debug.Log("[UIToggleSetup] 기존 UIToggleButton 사용");
            }

            // 4. MainUI를 UIToggleButton 뒤로 이동 (토글 버튼이 항상 위에 보이도록)
            mainUI.transform.SetAsLastSibling();
            toggleButton.transform.SetAsLastSibling();

            // 씬 저장 표시
            EditorUtility.SetDirty(defaultCanvas);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                defaultCanvas.scene
            );

            EditorUtility.DisplayDialog(
                "완료",
                "UI 토글 시스템이 설정되었습니다.\n\n" +
                "SeatUIManager에서:\n" +
                "- UIToggleButton: UIToggleButton\n" +
                "- UIContainerPanel: MainUI\n" +
                "를 연결하세요.",
                "확인"
            );
        }

        private void MoveChildrenToMainUI(Transform canvas, Transform mainUI)
        {
            // MainUI와 UIToggleButton을 제외한 모든 자식을 MainUI로 이동
            var childrenToMove = new System.Collections.Generic.List<Transform>();

            for (int i = 0; i < canvas.childCount; i++)
            {
                Transform child = canvas.GetChild(i);
                if (child.name != "MainUI" && child.name != "UIToggleButton")
                {
                    childrenToMove.Add(child);
                }
            }

            foreach (var child in childrenToMove)
            {
                Undo.SetTransformParent(child, mainUI, "Move to MainUI");
                child.SetParent(mainUI, true);
                Debug.Log($"[UIToggleSetup] {child.name}을(를) MainUI로 이동");
            }
        }

        private GameObject CreateToggleButton(Transform parent)
        {
            // 버튼 생성
            GameObject buttonObj = new GameObject("UIToggleButton");
            buttonObj.transform.SetParent(parent, false);

            // RectTransform 설정 - 오른쪽 상단에 배치
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-20, -20);
            rect.sizeDelta = new Vector2(120, 50);

            // Image 컴포넌트 (버튼 배경)
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Button 컴포넌트
            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            button.colors = colors;

            // 텍스트 생성 (TMP 사용)
            GameObject textObj = new GameObject("Text (TMP)");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = "UI 열기";
            tmpText.fontSize = 24;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;

            return buttonObj;
        }

        [MenuItem("ArenaX/Quick Setup UI Toggle")]
        public static void QuickSetup()
        {
            GameObject defaultCanvas = GameObject.Find("DefaultCanvas");
            if (defaultCanvas == null)
            {
                EditorUtility.DisplayDialog("오류", "DefaultCanvas를 찾을 수 없습니다.", "확인");
                return;
            }

            var window = GetWindow<UIToggleSetup>("UI Toggle Setup");
            window.defaultCanvas = defaultCanvas;
            window.SetupUIToggleSystem();
        }
    }
}
