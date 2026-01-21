using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Doudizhu.EditorTools
{
    public static class DoudizhuUiBootstrap
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string UiRootName = "UIRoot";
        private const string PrefabRoot = "Assets/Prefabs";
        private const string UiPrefabRoot = "Assets/Prefabs/UI";

        [MenuItem("Tools/Doudizhu/Bootstrap UI")]
        public static void Bootstrap()
        {
            EditorSceneManager.OpenScene(ScenePath);

            if (GameObject.Find(UiRootName) != null)
            {
                Debug.Log("UIRoot already exists. Skipping bootstrap.");
                return;
            }

            EnsureFolder(PrefabRoot);
            EnsureFolder(UiPrefabRoot);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            GameObject playerPanelPrefab = CreatePlayerPanelPrefab(font);
            GameObject actionButtonPrefab = CreateActionButtonPrefab(font);

            GameObject uiRoot = CreateCanvasRoot();
            CreateBackground(uiRoot.transform);
            CreateTopBar(uiRoot.transform, font);
            CreateTableArea(uiRoot.transform, font);
            CreateHandArea(uiRoot.transform, font);
            CreateActionBar(uiRoot.transform, actionButtonPrefab, font);
            CreatePlayerPanels(uiRoot.transform, playerPanelPrefab, font);
            EnsureBatchScreenshotProvider();
            EnsureEventSystem();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path);
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject CreateCanvasRoot()
        {
            GameObject root = new GameObject(UiRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return root;
        }

        private static void CreateBackground(Transform parent)
        {
            GameObject bg = CreatePanel("Background", parent, new Color(0.06f, 0.1f, 0.14f, 1f));
            RectTransform rect = bg.GetComponent<RectTransform>();
            SetRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static void CreateTopBar(Transform parent, Font font)
        {
            GameObject bar = CreatePanel("TopBar", parent, new Color(0.12f, 0.18f, 0.24f, 0.95f));
            RectTransform rect = bar.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 90f), new Vector2(0f, 0f));

            Text title = CreateText("Title", bar.transform, "DOU DIZHU", font, 28, TextAnchor.MiddleLeft, Color.white);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            SetRect(titleRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(320f, 60f), new Vector2(24f, 0f));

            Text status = CreateText("RoundInfo", bar.transform, "Round 1  |  Mult x1", font, 18, TextAnchor.MiddleRight, new Color(0.85f, 0.9f, 1f, 1f));
            RectTransform statusRect = status.GetComponent<RectTransform>();
            SetRect(statusRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(320f, 50f), new Vector2(-24f, 0f));
        }

        private static void CreateTableArea(Transform parent, Font font)
        {
            GameObject table = CreatePanel("TableArea", parent, new Color(0.1f, 0.16f, 0.12f, 0.9f));
            RectTransform rect = table.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(700f, 360f), new Vector2(0f, 60f));

            Text tip = CreateText("CenterTip", table.transform, "Waiting for play...", font, 20, TextAnchor.MiddleCenter, new Color(0.9f, 0.95f, 0.9f, 1f));
            RectTransform tipRect = tip.GetComponent<RectTransform>();
            SetRect(tipRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300f, 40f), new Vector2(0f, -24f));
        }

        private static void CreateHandArea(Transform parent, Font font)
        {
            GameObject hand = CreatePanel("HandArea", parent, new Color(0.09f, 0.13f, 0.18f, 0.95f));
            RectTransform rect = hand.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(900f, 140f), new Vector2(0f, 90f));

            Text label = CreateText("HandLabel", hand.transform, "Your Hand", font, 18, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 0.95f, 1f));
            RectTransform labelRect = label.GetComponent<RectTransform>();
            SetRect(labelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, 30f), new Vector2(16f, -12f));
        }

        private static void CreateActionBar(Transform parent, GameObject buttonPrefab, Font font)
        {
            GameObject bar = CreatePanel("ActionBar", parent, new Color(0.07f, 0.1f, 0.14f, 0.95f));
            RectTransform rect = bar.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(0f, 0f));

            string[] labels = { "Play", "Pass", "Hint" };
            float[] xOffsets = { -140f, 0f, 140f };
            for (int i = 0; i < labels.Length; i++)
            {
                GameObject button = InstantiatePrefab(buttonPrefab, bar.transform);
                button.name = $"ActionButton_{labels[i]}";
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                SetRect(buttonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 48f), new Vector2(xOffsets[i], 0f));
                Text text = button.GetComponentInChildren<Text>();
                text.text = labels[i];
                text.font = font;
            }
        }

        private static void CreatePlayerPanels(Transform parent, GameObject prefab, Font font)
        {
            GameObject left = InstantiatePrefab(prefab, parent);
            left.name = "PlayerPanel_Left";
            RectTransform leftRect = left.GetComponent<RectTransform>();
            SetRect(leftRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(240f, 140f), new Vector2(24f, 60f));

            GameObject right = InstantiatePrefab(prefab, parent);
            right.name = "PlayerPanel_Right";
            RectTransform rightRect = right.GetComponent<RectTransform>();
            SetRect(rightRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(240f, 140f), new Vector2(-24f, 60f));

            GameObject bottom = InstantiatePrefab(prefab, parent);
            bottom.name = "PlayerPanel_Bottom";
            RectTransform bottomRect = bottom.GetComponent<RectTransform>();
            SetRect(bottomRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(480f, 140f), new Vector2(0f, 250f));

            ApplyPlayerPanelText(left.transform, "Player A", "Cards: 17", "Role: Farmer");
            ApplyPlayerPanelText(right.transform, "Player B", "Cards: 17", "Role: Farmer");
            ApplyPlayerPanelText(bottom.transform, "You", "Cards: 17", "Role: Unknown");
        }

        private static void ApplyPlayerPanelText(Transform panel, string name, string cards, string role)
        {
            Text[] texts = panel.GetComponentsInChildren<Text>();
            foreach (Text text in texts)
            {
                if (text.name == "NameText")
                {
                    text.text = name;
                }
                else if (text.name == "CardsText")
                {
                    text.text = cards;
                }
                else if (text.name == "RoleText")
                {
                    text.text = role;
                }
            }
        }

        private static GameObject CreatePlayerPanelPrefab(Font font)
        {
            string prefabPath = $"{UiPrefabRoot}/PlayerPanel.prefab";
            if (System.IO.File.Exists(prefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            GameObject root = new GameObject("PlayerPanel", typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 140f);

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.14f, 0.2f, 0.26f, 0.95f);

            Text name = CreateText("NameText", root.transform, "Player", font, 18, TextAnchor.UpperLeft, Color.white);
            RectTransform nameRect = name.GetComponent<RectTransform>();
            SetRect(nameRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(12f, -8f));

            Text cards = CreateText("CardsText", root.transform, "Cards: 17", font, 16, TextAnchor.UpperLeft, new Color(0.8f, 0.9f, 1f, 1f));
            RectTransform cardsRect = cards.GetComponent<RectTransform>();
            SetRect(cardsRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(12f, -40f));

            Text role = CreateText("RoleText", root.transform, "Role: Unknown", font, 16, TextAnchor.UpperLeft, new Color(0.9f, 0.8f, 0.6f, 1f));
            RectTransform roleRect = role.GetComponent<RectTransform>();
            SetRect(roleRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(12f, -68f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateActionButtonPrefab(Font font)
        {
            string prefabPath = $"{UiPrefabRoot}/ActionButton.prefab";
            if (System.IO.File.Exists(prefabPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            GameObject root = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120f, 48f);

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.26f, 0.34f, 1f);

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(root.transform, false);
            Text label = labelObj.GetComponent<Text>();
            label.font = font;
            label.text = "Action";
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            SetRect(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(prefab, parent);
            }

            return instance;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            return panel;
        }

        private static Text CreateText(string name, Transform parent, string text, Font font, int fontSize, TextAnchor anchor, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text label = obj.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.color = color;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPos;
        }

        private static void EnsureBatchScreenshotProvider()
        {
            if (Object.FindAnyObjectByType<BatchTools.BatchScreenshotProvider>() != null)
            {
                return;
            }

            GameObject provider = new GameObject("BatchScreenshotProvider");
            provider.AddComponent<BatchTools.BatchScreenshotProvider>();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject system = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            system.transform.SetParent(null, false);
        }
    }
}
