using System.Collections.Generic;
using Doudizhu.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Doudizhu.UI
{
    public sealed class DoudizhuRuntimeUiBuilder : MonoBehaviour
    {
        private const float CanvasWidth = 1280f;
        private const float CanvasHeight = 720f;
        private const string ResourceRoot = "Art";

        private readonly Dictionary<string, Sprite> _rankSprites = new Dictionary<string, Sprite>();

        private void Awake()
        {
            if (Object.FindAnyObjectByType<DoudizhuUiController>() != null)
            {
                return;
            }

            EnsureMainCamera();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            Sprite background = LoadSprite("背景");
            Sprite cardBack = LoadSprite("牌底");
            Sprite joker = LoadSprite("joker");
            Sprite smallKing = LoadSprite("小王图案");
            Sprite bigKing = LoadSprite("大王图案");
            Sprite suitDiamond = LoadSprite("方块");
            Sprite suitClub = LoadSprite("梅花");
            Sprite suitHeart = LoadSprite("红桃");
            Sprite suitSpade = LoadSprite("黑桃");

            _rankSprites.Clear();
            _rankSprites["A"] = LoadSprite("a");
            _rankSprites["2"] = LoadSprite("2");
            _rankSprites["3"] = LoadSprite("3");
            _rankSprites["4"] = LoadSprite("4");
            _rankSprites["5"] = LoadSprite("5");
            _rankSprites["6"] = LoadSprite("6");
            _rankSprites["7"] = LoadSprite("7");
            _rankSprites["8"] = LoadSprite("8");
            _rankSprites["9"] = LoadSprite("9");
            _rankSprites["10"] = LoadSprite("10");
            _rankSprites["J"] = LoadSprite("j");
            _rankSprites["Q"] = LoadSprite("q");
            _rankSprites["K"] = LoadSprite("k");

            GameObject uiRoot = CreateCanvasRoot();
            CreateBackground(uiRoot.transform, background);
            CreateTopBar(uiRoot.transform, font, joker);
            GameObject tableArea = CreateTableArea(uiRoot.transform, font);
            CreateBottomCards(uiRoot.transform, cardBack);
            CreatePlayerPanels(uiRoot.transform, font, smallKing, bigKing, joker);
            CreateHandArea(uiRoot.transform, font);
            GameObject actionTemplate = CreateActionButtonTemplate(font);
            CreateActionBar(tableArea.transform, actionTemplate, font);
            CreateRestartButton(tableArea.transform, actionTemplate, font);

            GameObject cardFaceTemplate = CreateCardFaceTemplate();
            cardFaceTemplate.SetActive(false);
            GameObject cardBackTemplate = CreateCardBackTemplate(cardBack);
            cardBackTemplate.SetActive(false);

            AttachUiRefs(uiRoot, cardFaceTemplate, cardBackTemplate, actionTemplate, background, cardBack, joker, smallKing, bigKing, suitSpade, suitHeart, suitClub, suitDiamond);
            uiRoot.AddComponent<DoudizhuUiController>();
            EnsureEventSystem();
        }

        private static Sprite LoadSprite(string name)
        {
            return Resources.Load<Sprite>($"{ResourceRoot}/{name}");
        }

        private static GameObject CreateCanvasRoot()
        {
            GameObject root = new GameObject("UIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            Image image = bg.GetComponent<Image>();
            image.preserveAspect = false;
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

        private static GameObject CreateTableArea(Transform parent, Font font)
        {
            GameObject table = CreatePanel("TableArea", parent, new Color(0.96f, 0.86f, 0.68f, 0.9f));
            RectTransform rect = table.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(720f, 320f), new Vector2(0f, 30f));

            Text tip = CreateText("CenterTip", table.transform, "等待出牌", font, 20, TextAnchor.MiddleCenter, new Color(0.35f, 0.25f, 0.1f, 1f));
            RectTransform tipRect = tip.GetComponent<RectTransform>();
            SetRect(tipRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(200f, 40f), new Vector2(0f, -18f));

            return table;
        }

        private static void CreateBottomCards(Transform parent, Sprite cardBack)
        {
            GameObject group = new GameObject("BottomCards", typeof(RectTransform));
            group.transform.SetParent(parent, false);
            RectTransform rect = group.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(240f, 80f), new Vector2(0f, -72f));

            float[] offsets = { -40f, 0f, 40f };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject card = CreateImage($"BottomCard_{i + 1}", group.transform, cardBack, Color.white);
                RectTransform cardRect = card.GetComponent<RectTransform>();
                SetRect(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(56f, 76f), new Vector2(offsets[i], 0f));
            }
        }

        private static void CreatePlayerPanels(Transform parent, Font font, Sprite smallKing, Sprite bigKing, Sprite joker)
        {
            CreatePlayerPanel(parent, "PlayerPanel_Left", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, 140f), new Vector2(20f, 90f), "一缕阳光", "18.4万", "地主", smallKing, font);
            CreatePlayerPanel(parent, "PlayerPanel_Right", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(260f, 140f), new Vector2(-20f, 90f), "江大海盗", "18.4万", "农民", bigKing, font);
            CreatePlayerPanel(parent, "PlayerPanel_Bottom", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(320f, 140f), new Vector2(24f, 130f), "Madlee", "20.7万", "自己", joker, font);
        }

        private static void CreatePlayerPanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 pos,
            string displayName,
            string coin,
            string role,
            Sprite avatar,
            Font font)
        {
            GameObject panel = CreatePanel(name, parent, new Color(0f, 0f, 0f, 0.35f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            GameObject avatarObj = CreateImage("Avatar", panel.transform, avatar, Color.white);
            RectTransform avatarRect = avatarObj.GetComponent<RectTransform>();
            SetRect(avatarRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 72f), new Vector2(12f, 0f));

            Text nameText = CreateText("NameText", panel.transform, displayName, font, 18, TextAnchor.UpperLeft, Color.white);
            RectTransform nameRect = nameText.GetComponent<RectTransform>();
            SetRect(nameRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-90f, 24f), new Vector2(96f, -10f));

            Text coinText = CreateText("CoinText", panel.transform, coin, font, 16, TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.6f, 1f));
            RectTransform coinRect = coinText.GetComponent<RectTransform>();
            SetRect(coinRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-90f, 24f), new Vector2(96f, -38f));

            Text roleText = CreateText("RoleText", panel.transform, role, font, 16, TextAnchor.UpperLeft, new Color(0.9f, 0.8f, 0.6f, 1f));
            RectTransform roleRect = roleText.GetComponent<RectTransform>();
            SetRect(roleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-90f, 24f), new Vector2(96f, -64f));
        }

        private static void CreateHandArea(Transform parent, Font font)
        {
            GameObject hand = new GameObject("HandArea", typeof(RectTransform));
            hand.transform.SetParent(parent, false);
            RectTransform rect = hand.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(960f, 160f), new Vector2(0f, 120f));

            Text label = CreateText("HandLabel", hand.transform, "你的手牌", font, 18, TextAnchor.UpperLeft, Color.white);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            SetRect(labelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, 28f), new Vector2(0f, -6f));
        }

        private static void CreateActionBar(Transform parent, GameObject buttonPrefab, Font font)
        {
            GameObject bar = new GameObject("ActionBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            RectTransform rect = bar.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 70f), new Vector2(0f, -120f));

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
                GameObject button = Object.Instantiate(buttonPrefab, bar.transform);
                button.name = $"ActionButton_{labels[i]}";
                button.SetActive(true);
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                SetRect(buttonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(90f, 36f), new Vector2(xOffsets[i], 0f));
                Text text = button.GetComponentInChildren<Text>();
                text.text = labels[i];
                text.font = font;
                Image image = button.GetComponent<Image>();
                image.color = colors[i];
            }
        }

        private static void CreateRestartButton(Transform parent, GameObject buttonPrefab, Font font)
        {
            GameObject button = Object.Instantiate(buttonPrefab, parent);
            button.name = "RestartButton";
            button.SetActive(false);
            RectTransform rect = button.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 40f), new Vector2(0f, -40f));
            Text text = button.GetComponentInChildren<Text>();
            text.text = "再来一局";
            text.font = font;
        }

        private static GameObject CreateActionButtonTemplate(Font font)
        {
            GameObject root = new GameObject("ActionButtonTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
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

            root.SetActive(false);
            return root;
        }

        private static GameObject CreateCardFaceTemplate()
        {
            GameObject root = new GameObject("CardFaceTemplate", typeof(RectTransform), typeof(Image));
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

            return root;
        }

        private static GameObject CreateCardBackTemplate(Sprite sprite)
        {
            GameObject root = new GameObject("CardBackTemplate", typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(56f, 76f);

            Image image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            return root;
        }

        private void AttachUiRefs(
            GameObject root,
            GameObject cardFaceTemplate,
            GameObject cardBackTemplate,
            GameObject actionButtonTemplate,
            Sprite background,
            Sprite cardBack,
            Sprite joker,
            Sprite smallKing,
            Sprite bigKing,
            Sprite suitSpade,
            Sprite suitHeart,
            Sprite suitClub,
            Sprite suitDiamond)
        {
            DoudizhuUiRefs refs = root.AddComponent<DoudizhuUiRefs>();
            refs.CardFacePrefab = cardFaceTemplate;
            refs.CardBackPrefab = cardBackTemplate;
            refs.ActionButtonPrefab = actionButtonTemplate;
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
            foreach (KeyValuePair<string, Sprite> entry in _rankSprites)
            {
                refs.RankSprites.Add(new RankSpriteEntry { Key = entry.Key, Sprite = entry.Value });
            }
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

        private static void EnsureEventSystem()
        {
            EventSystem existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing == null)
            {
                GameObject system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                system.transform.SetParent(null, false);
                return;
            }

            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static void EnsureMainCamera()
        {
            if (Object.FindAnyObjectByType<Camera>() != null)
            {
                return;
            }

            GameObject cameraObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObj.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }
    }
}
