using System.Collections.Generic;
using Doudizhu.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Doudizhu.EditorTools
{
    public static class DoudizhuUiBootstrap
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string UiRootName = "UIRoot";
        private const string PrefabRoot = "Assets/Prefabs";
        private const string UiPrefabRoot = "Assets/Prefabs/UI";
        private const string ArtRoot = "Assets/Art";

        private const float CanvasWidth = 1280f;
        private const float CanvasHeight = 720f;

        [MenuItem("Tools/Doudizhu/Bootstrap UI")]
        public static void Bootstrap()
        {
            EditorSceneManager.OpenScene(ScenePath);
            RemoveExistingUi();

            EnsureFolder(PrefabRoot);
            EnsureFolder(UiPrefabRoot);

            string[] artFiles =
            {
                "背景.png",
                "牌底.png",
                "joker.png",
                "小王图案.png",
                "大王图案.png",
                "方块.png",
                "梅花.png",
                "红桃.png",
                "黑桃.png",
                "a.png",
                "2.png",
                "3.png",
                "4.png",
                "5.png",
                "6.png",
                "7.png",
                "8.png",
                "9.png",
                "10.png",
                "j.png",
                "q.png",
                "k.png"
            };

            foreach (string file in artFiles)
            {
                EnsureSprite($"{ArtRoot}/{file}");
            }

            Sprite background = LoadSprite("背景.png");
            Sprite cardBack = LoadSprite("牌底.png");
            Sprite joker = LoadSprite("joker.png");
            Sprite smallKing = LoadSprite("小王图案.png");
            Sprite bigKing = LoadSprite("大王图案.png");
            Sprite suitDiamond = LoadSprite("方块.png");
            Sprite suitClub = LoadSprite("梅花.png");
            Sprite suitHeart = LoadSprite("红桃.png");
            Sprite suitSpade = LoadSprite("黑桃.png");

            Dictionary<string, Sprite> rankSprites = new Dictionary<string, Sprite>
            {
                { "A", LoadSprite("a.png") },
                { "2", LoadSprite("2.png") },
                { "3", LoadSprite("3.png") },
                { "4", LoadSprite("4.png") },
                { "5", LoadSprite("5.png") },
                { "6", LoadSprite("6.png") },
                { "7", LoadSprite("7.png") },
                { "8", LoadSprite("8.png") },
                { "9", LoadSprite("9.png") },
                { "10", LoadSprite("10.png") },
                { "J", LoadSprite("j.png") },
                { "Q", LoadSprite("q.png") },
                { "K", LoadSprite("k.png") }
            };

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject cardFacePrefab = CreateCardFacePrefab();
            GameObject cardBackPrefab = CreateCardBackPrefab(cardBack);
            GameObject playerPanelPrefab = CreatePlayerPanelPrefab(font);
            GameObject actionButtonPrefab = CreateActionButtonPrefab(font);

            GameObject uiRoot = CreateCanvasRoot();
            CreateBackground(uiRoot.transform, background);
            CreateTopBar(uiRoot.transform, font, joker);
            CreateTableArea(uiRoot.transform, font);
            CreateBottomCards(uiRoot.transform, cardBackPrefab);
            CreatePlayerPanels(uiRoot.transform, playerPanelPrefab, font, smallKing, bigKing, joker);
            CreateHandArea(uiRoot.transform, font, cardFacePrefab, rankSprites, suitSpade, suitHeart, suitClub, suitDiamond, joker, smallKing, bigKing);
            CreateActionBar(uiRoot.transform, actionButtonPrefab, font);
            AttachUiRefs(uiRoot, cardFacePrefab, cardBackPrefab, actionButtonPrefab, background, cardBack, joker, smallKing, bigKing, suitSpade, suitHeart, suitClub, suitDiamond, rankSprites);
            uiRoot.AddComponent<DoudizhuUiController>();
            EnsureBatchScreenshotProvider();
            EnsureEventSystem();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static void RemoveExistingUi()
        {
            GameObject existing = GameObject.Find(UiRootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
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

        private static void EnsureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.spritePixelsPerUnit != 100)
            {
                importer.spritePixelsPerUnit = 100;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Sprite LoadSprite(string filename)
        {
            string path = $"{ArtRoot}/{filename}";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject CreateCanvasRoot()
        {
            GameObject root = new GameObject(UiRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return root;
        }

        private static void CreateBackground(Transform parent, Sprite sprite)
        {
            GameObject bg = CreateImage("Background", parent, sprite, Color.white);
            RectTransform rect = bg.GetComponent<RectTransform>();
            SetRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static void CreateTopBar(Transform parent, Font font, Sprite joker)
        {
            GameObject bar = CreatePanel("TopBar", parent, new Color(0f, 0f, 0f, 0.35f));
            RectTransform rect = bar.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 52f), new Vector2(0f, 0f));

            Text title = CreateText("Title", bar.transform, "欢乐斗地主 2.0.1", font, 18, TextAnchor.MiddleLeft, Color.white);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            SetRect(titleRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(260f, 40f), new Vector2(18f, 0f));

            Text status = CreateText("Status", bar.transform, "房间  |  金币桌", font, 16, TextAnchor.MiddleRight, new Color(0.9f, 0.95f, 1f, 1f));
            RectTransform statusRect = status.GetComponent<RectTransform>();
            SetRect(statusRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(220f, 40f), new Vector2(-18f, 0f));

            GameObject icon = CreateImage("JokerIcon", bar.transform, joker, Color.white);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            SetRect(iconRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(28f, 28f), new Vector2(-250f, 0f));
        }

        private static void CreateTableArea(Transform parent, Font font)
        {
            GameObject table = CreatePanel("TableArea", parent, new Color(0.96f, 0.86f, 0.68f, 0.9f));
            RectTransform rect = table.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(720f, 320f), new Vector2(0f, 30f));

            Text tip = CreateText("CenterTip", table.transform, "等待出牌", font, 20, TextAnchor.MiddleCenter, new Color(0.35f, 0.25f, 0.1f, 1f));
            RectTransform tipRect = tip.GetComponent<RectTransform>();
            SetRect(tipRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(200f, 40f), new Vector2(0f, -18f));
        }

        private static void CreateBottomCards(Transform parent, GameObject cardBackPrefab)
        {
            GameObject group = new GameObject("BottomCards", typeof(RectTransform));
            group.transform.SetParent(parent, false);
            RectTransform rect = group.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(240f, 80f), new Vector2(0f, -72f));

            float[] offsets = { -40f, 0f, 40f };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject card = InstantiatePrefab(cardBackPrefab, group.transform);
                card.name = $"BottomCard_{i + 1}";
                RectTransform cardRect = card.GetComponent<RectTransform>();
                SetRect(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56f, 76f), new Vector2(offsets[i], 0f));
            }
        }

        private static void CreatePlayerPanels(Transform parent, GameObject prefab, Font font, Sprite smallKing, Sprite bigKing, Sprite joker)
        {
            GameObject left = InstantiatePrefab(prefab, parent);
            left.name = "PlayerPanel_Left";
            RectTransform leftRect = left.GetComponent<RectTransform>();
            SetRect(leftRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, 140f), new Vector2(20f, 90f));
            ApplyPlayerPanel(left.transform, "一缕阳光", "18.4万", "地主", smallKing);

            GameObject right = InstantiatePrefab(prefab, parent);
            right.name = "PlayerPanel_Right";
            RectTransform rightRect = right.GetComponent<RectTransform>();
            SetRect(rightRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(260f, 140f), new Vector2(-20f, 90f));
            ApplyPlayerPanel(right.transform, "江大海盗", "18.4万", "农民", bigKing);

            GameObject bottom = InstantiatePrefab(prefab, parent);
            bottom.name = "PlayerPanel_Bottom";
            RectTransform bottomRect = bottom.GetComponent<RectTransform>();
            SetRect(bottomRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(320f, 140f), new Vector2(24f, 130f));
            ApplyPlayerPanel(bottom.transform, "Madlee", "20.7万", "自己", joker);
        }

        private static void ApplyPlayerPanel(Transform panel, string name, string coin, string role, Sprite avatarSprite)
        {
            Text[] texts = panel.GetComponentsInChildren<Text>();
            foreach (Text text in texts)
            {
                if (text.name == "NameText")
                {
                    text.text = name;
                }
                else if (text.name == "CoinText")
                {
                    text.text = coin;
                }
                else if (text.name == "RoleText")
                {
                    text.text = role;
                }
            }

            Image avatar = panel.Find("Avatar").GetComponent<Image>();
            avatar.sprite = avatarSprite;
            avatar.preserveAspect = true;
        }

        private static void CreateHandArea(
            Transform parent,
            Font font,
            GameObject cardFacePrefab,
            Dictionary<string, Sprite> rankSprites,
            Sprite suitSpade,
            Sprite suitHeart,
            Sprite suitClub,
            Sprite suitDiamond,
            Sprite joker,
            Sprite smallKing,
            Sprite bigKing)
        {
            GameObject hand = new GameObject("HandArea", typeof(RectTransform));
            hand.transform.SetParent(parent, false);
            RectTransform rect = hand.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(960f, 160f), new Vector2(0f, 120f));

            Text label = CreateText("HandLabel", hand.transform, "你的手牌", font, 18, TextAnchor.UpperLeft, Color.white);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            SetRect(labelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, 28f), new Vector2(0f, -6f));

            // Hand cards are created by the runtime controller.
        }

        private static void CreateActionBar(Transform parent, GameObject buttonPrefab, Font font)
        {
            GameObject bar = new GameObject("ActionBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            RectTransform rect = bar.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(360f, 70f), new Vector2(0f, 40f));

            string[] labels = { "出牌", "不出", "提示" };
            Color[] colors =
            {
                new Color(0.18f, 0.45f, 0.8f, 1f),
                new Color(0.85f, 0.55f, 0.2f, 1f),
                new Color(0.18f, 0.45f, 0.8f, 1f)
            };
            float[] xOffsets = { -120f, 0f, 120f };
            for (int i = 0; i < labels.Length; i++)
            {
                GameObject button = InstantiatePrefab(buttonPrefab, bar.transform);
                button.name = $"ActionButton_{labels[i]}";
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                SetRect(buttonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 36f), new Vector2(xOffsets[i], 0f));
                Text text = button.GetComponentInChildren<Text>();
                text.text = labels[i];
                text.font = font;
                Image image = button.GetComponent<Image>();
                image.color = colors[i];
            }
        }

        private static void AttachUiRefs(
            GameObject root,
            GameObject cardFacePrefab,
            GameObject cardBackPrefab,
            GameObject actionButtonPrefab,
            Sprite background,
            Sprite cardBack,
            Sprite joker,
            Sprite smallKing,
            Sprite bigKing,
            Sprite suitSpade,
            Sprite suitHeart,
            Sprite suitClub,
            Sprite suitDiamond,
            Dictionary<string, Sprite> rankSprites)
        {
            DoudizhuUiRefs refs = root.AddComponent<DoudizhuUiRefs>();
            refs.CardFacePrefab = cardFacePrefab;
            refs.CardBackPrefab = cardBackPrefab;
            refs.ActionButtonPrefab = actionButtonPrefab;
            refs.Background = background;
            refs.CardBack = cardBack;
            refs.Joker = joker;
            refs.SmallJoker = smallKing;
            refs.BigJoker = bigKing;
            refs.SuitSpade = suitSpade;
            refs.SuitHeart = suitHeart;
            refs.SuitClub = suitClub;
            refs.SuitDiamond = suitDiamond;

            refs.RankSprites.Clear();
            foreach (KeyValuePair<string, Sprite> entry in rankSprites)
            {
                refs.RankSprites.Add(new RankSpriteEntry { Key = entry.Key, Sprite = entry.Value });
            }
        }

        private static GameObject CreatePlayerPanelPrefab(Font font)
        {
            string prefabPath = $"{UiPrefabRoot}/PlayerPanel.prefab";
            GameObject root = new GameObject("PlayerPanel", typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 140f);

            Image image = root.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.35f);

            GameObject avatar = CreateImage("Avatar", root.transform, null, Color.white);
            RectTransform avatarRect = avatar.GetComponent<RectTransform>();
            SetRect(avatarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 72f), new Vector2(12f, 0f));

            Text name = CreateText("NameText", root.transform, "玩家", font, 18, TextAnchor.UpperLeft, Color.white);
            RectTransform nameRect = name.GetComponent<RectTransform>();
            SetRect(nameRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-90f, 24f), new Vector2(96f, -10f));

            Text coin = CreateText("CoinText", root.transform, "0", font, 16, TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.6f, 1f));
            RectTransform coinRect = coin.GetComponent<RectTransform>();
            SetRect(coinRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-90f, 24f), new Vector2(96f, -38f));

            Text role = CreateText("RoleText", root.transform, "农民", font, 16, TextAnchor.UpperLeft, new Color(0.9f, 0.8f, 0.6f, 1f));
            RectTransform roleRect = role.GetComponent<RectTransform>();
            SetRect(roleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-90f, 24f), new Vector2(96f, -64f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateActionButtonPrefab(Font font)
        {
            string prefabPath = $"{UiPrefabRoot}/ActionButton.prefab";
            GameObject root = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(90f, 36f);

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.18f, 0.45f, 0.8f, 1f);

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(root.transform, false);
            Text label = labelObj.GetComponent<Text>();
            label.font = font;
            label.text = "按钮";
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            SetRect(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCardFacePrefab()
        {
            string prefabPath = $"{UiPrefabRoot}/CardFace.prefab";
            GameObject root = new GameObject("CardFace", typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(64f, 92f);

            Image image = root.GetComponent<Image>();
            image.color = Color.white;

            GameObject rank = CreateImage("Rank", root.transform, null, Color.white);
            RectTransform rankRect = rank.GetComponent<RectTransform>();
            SetRect(rankRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, 22f), new Vector2(6f, -6f));

            GameObject suit = CreateImage("Suit", root.transform, null, Color.white);
            RectTransform suitRect = suit.GetComponent<RectTransform>();
            SetRect(suitRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, 16f), new Vector2(8f, -30f));

            GameObject center = CreateImage("Center", root.transform, null, Color.white);
            RectTransform centerRect = center.GetComponent<RectTransform>();
            SetRect(centerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(36f, 36f), new Vector2(0f, 0f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCardBackPrefab(Sprite sprite)
        {
            string prefabPath = $"{UiPrefabRoot}/CardBack.prefab";
            GameObject root = new GameObject("CardBack", typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(56f, 76f);

            Image image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ApplyCardVisual(Transform card, CardData data)
        {
            Image rank = card.Find("Rank").GetComponent<Image>();
            Image suit = card.Find("Suit").GetComponent<Image>();
            Image center = card.Find("Center").GetComponent<Image>();

            rank.sprite = data.Rank;
            rank.preserveAspect = true;
            suit.sprite = data.Suit;
            suit.preserveAspect = true;
            center.sprite = data.Center;
            center.preserveAspect = true;

            suit.enabled = data.Suit != null;
            center.enabled = data.Center != null;
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

        private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return obj;
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
            EventSystem existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing == null)
            {
                GameObject system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                system.transform.SetParent(null, false);
                return;
            }

            StandaloneInputModule legacy = existing.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
            }

            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private readonly struct CardData
        {
            public readonly Sprite Rank;
            public readonly Sprite Suit;
            public readonly Sprite Center;

            public CardData(Sprite rank, Sprite suit, Sprite center)
            {
                Rank = rank;
                Suit = suit;
                Center = center;
            }
        }
    }
}
