using System.Collections.Generic;
using System;
using Doudizhu.Audio;
using Doudizhu.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Doudizhu.UI
{
    public sealed class DoudizhuUiController : MonoBehaviour
    {
        [SerializeField] private DoudizhuUiRefs refs;

        private const int LocalPlayerIndex = 0;
        private const float HandSpacing = 28f;
        private const float HandCardWidth = 64f;
        private const float HandCardHeight = 92f;
        private const float TableSpacing = 24f;
        private const float TableCardWidth = 56f;
        private const float TableCardHeight = 80f;

        private GameEngine _engine;
        private UiInputStrategy _strategy;

        private Transform _handArea;
        private readonly List<GameObject> _handCards = new List<GameObject>();
        private Transform _tableArea;
        private Transform _bottomArea;
        private readonly List<GameObject> _bottomBacks = new List<GameObject>();
        private readonly List<GameObject> _bottomFaces = new List<GameObject>();
        private readonly Dictionary<int, RectTransform> _playAreas = new Dictionary<int, RectTransform>();
        private readonly Dictionary<int, List<GameObject>> _playCards = new Dictionary<int, List<GameObject>>();
        private readonly Dictionary<int, List<Card>> _lastPlays = new Dictionary<int, List<Card>>();
        private readonly Dictionary<int, Text> _passLabels = new Dictionary<int, Text>();
        private readonly HashSet<int> _selectedIndices = new HashSet<int>();

        private Text _centerTip;
        private Text _statusText;
        private PlayerPanelView _leftPanel;
        private PlayerPanelView _rightPanel;
        private PlayerPanelView _bottomPanel;

        private GameObject _actionBar;
        private GameObject _bidBar;
        private readonly List<Button> _bidButtons = new List<Button>();
        private readonly List<Text> _bidButtonLabels = new List<Text>();

        private Button _playButton;
        private Button _passButton;
        private Button _hintButton;
        private Button _restartButton;
        private float _nextTurnTime;

        private const float AiPlayDelay = 1f;

        private void Awake()
        {
            if (refs == null)
            {
                refs = GetComponent<DoudizhuUiRefs>();
            }

            CacheUi();
            EnsurePlayAreas();
            BuildBidBar();
            WireActionButtons();

            _strategy = new UiInputStrategy(LocalPlayerIndex, new AutoGameStrategy());
            _engine = CreateEngine();

            RefreshAll();
        }

        private void Update()
        {
            if (_engine == null || _engine.Phase == GamePhase.Finished)
            {
                return;
            }

            if (_engine.Phase == GamePhase.Bidding)
            {
                if (_engine.CurrentPlayer == LocalPlayerIndex && !_strategy.HasPendingBid)
                {
                    return;
                }
            }
            else
            {
                if (_engine.CurrentPlayer == LocalPlayerIndex && !_strategy.HasPendingPlay)
                {
                    return;
                }
            }

            if (_engine.Phase == GamePhase.Playing && Time.time < _nextTurnTime)
            {
                return;
            }

            StepAndRefresh();
        }

        private void CacheUi()
        {
            Transform root = transform;
            _handArea = root.Find("HandArea");
            _tableArea = root.Find("TableArea");
            _bottomArea = root.Find("BottomCards");
            _actionBar = root.Find("ActionBar")?.gameObject;
            if (_actionBar == null && _tableArea != null)
            {
                _actionBar = _tableArea.Find("ActionBar")?.gameObject;
            }
            _centerTip = root.Find("TableArea/CenterTip")?.GetComponent<Text>();
            _statusText = root.Find("TopBar/Status")?.GetComponent<Text>();

            _leftPanel = new PlayerPanelView(root.Find("PlayerPanel_Left"));
            _rightPanel = new PlayerPanelView(root.Find("PlayerPanel_Right"));
            _bottomPanel = new PlayerPanelView(root.Find("PlayerPanel_Bottom"));

            if (_actionBar != null)
            {
                _playButton = _actionBar.transform.Find("ActionButton_出牌")?.GetComponent<Button>();
                _passButton = _actionBar.transform.Find("ActionButton_不出")?.GetComponent<Button>();
                _hintButton = _actionBar.transform.Find("ActionButton_提示")?.GetComponent<Button>();
            }

            if (_actionBar != null && _tableArea != null && _actionBar.transform.parent != _tableArea)
            {
                RectTransform rect = _actionBar.GetComponent<RectTransform>();
                rect.SetParent(_tableArea, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(360f, 70f);
                rect.anchoredPosition = new Vector2(0f, -120f);
            }

            _restartButton = root.Find("TableArea/RestartButton")?.GetComponent<Button>();

            CacheBottomCards();
        }

        private void BuildBidBar()
        {
            if (refs == null || refs.ActionButtonPrefab == null)
            {
                return;
            }

            _bidBar = new GameObject("BidBar", typeof(RectTransform));
            RectTransform rect = _bidBar.GetComponent<RectTransform>();
            rect.SetParent(_tableArea != null ? _tableArea : transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 60f);
            rect.anchoredPosition = new Vector2(0f, -70f);

            _bidButtons.Clear();
            _bidButtonLabels.Clear();
            float[] xOffsets = { -60f, 60f };
            for (int i = 0; i < 2; i++)
            {
                GameObject buttonObj = Instantiate(refs.ActionButtonPrefab, _bidBar.transform);
                buttonObj.name = $"BidButton_{i}";
                buttonObj.SetActive(true);
                RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(90f, 36f);
                btnRect.anchoredPosition = new Vector2(xOffsets[i], 0f);

                Text text = buttonObj.GetComponentInChildren<Text>();
                if (text != null)
                {
                    _bidButtonLabels.Add(text);
                }

                Button button = buttonObj.GetComponent<Button>();
                int bidValue = i == 0 ? 0 : 1;
                button.onClick.AddListener(() => OnBidClicked(bidValue));
                _bidButtons.Add(button);
            }
        }

        private void WireActionButtons()
        {
            _playButton?.onClick.AddListener(OnPlayClicked);
            _passButton?.onClick.AddListener(OnPassClicked);
            _hintButton?.onClick.AddListener(OnHintClicked);
            _restartButton?.onClick.AddListener(OnRestartClicked);
        }

        private void StepAndRefresh()
        {
            BidStage bidStageBefore = _engine.BidStage;
            PlayAction? lastPlayBefore = _engine.LastPlay;
            StepResult result = _engine.Step();
            if (result.Kind == StepKind.Bid && result.BidScore > 0)
            {
                string bidLabel = _engine.BidStage == BidStage.Rob ? "抢地主" : "叫地主";
                _statusText.text = $"{bidLabel}  |  玩家 {result.PlayerIndex + 1}";
            }

            RecordPlay(result);
            DoudizhuAudioManager.Instance?.PlayStep(result, bidStageBefore, lastPlayBefore);
            RefreshAll();

            if (_engine.Phase == GamePhase.Playing)
            {
                _nextTurnTime = Time.time + AiPlayDelay;
            }
        }

        private void RefreshAll()
        {
            UpdatePhaseUi();
            UpdateBidBarVisuals();
            UpdatePlayerPanels();
            UpdateHand();
            UpdateTableCards();
            UpdateBottomCards();
        }

        private void UpdatePhaseUi()
        {
            if (_centerTip == null)
            {
                return;
            }

            if (_engine.Phase == GamePhase.Bidding)
            {
                _centerTip.text = _engine.BidStage == BidStage.Rob ? "抢地主阶段" : "叫地主阶段";
                ClearAllTablePlays();
                SetActionBarActive(false);
                SetBidBarActive(_engine.CurrentPlayer == LocalPlayerIndex);
                SetRestartButtonActive(false);
            }
            else if (_engine.Phase == GamePhase.Playing)
            {
                _centerTip.text = "等待出牌";
                SetActionBarActive(_engine.CurrentPlayer == LocalPlayerIndex);
                SetBidBarActive(false);
                SetRestartButtonActive(false);
            }
            else
            {
                _centerTip.text = "本局结束";
                SetActionBarActive(false);
                SetBidBarActive(false);
                SetRestartButtonActive(true);
            }
        }

        private void SetActionBarActive(bool active)
        {
            if (_actionBar != null)
            {
                _actionBar.SetActive(active);
            }
        }

        private void SetBidBarActive(bool active)
        {
            if (_bidBar != null)
            {
                _bidBar.SetActive(active);
            }
        }

        private void SetRestartButtonActive(bool active)
        {
            if (_restartButton != null)
            {
                _restartButton.gameObject.SetActive(active);
            }
        }

        private void UpdateBidBarVisuals()
        {
            if (_bidButtonLabels.Count < 2 || _engine == null)
            {
                return;
            }

            if (_engine.BidStage == BidStage.Rob)
            {
                _bidButtonLabels[0].text = "不抢";
                _bidButtonLabels[1].text = "抢地主";
            }
            else
            {
                _bidButtonLabels[0].text = "不叫";
                _bidButtonLabels[1].text = "叫地主";
            }
        }

        private void UpdatePlayerPanels()
        {
            if (_engine == null)
            {
                return;
            }

            _bottomPanel.Apply(_engine.Players[LocalPlayerIndex], "Madlee", "20.7万");
            _leftPanel.Apply(_engine.Players[1], "一缕阳光", "18.4万");
            _rightPanel.Apply(_engine.Players[2], "江大海盗", "18.4万");
        }

        private void UpdateHand()
        {
            if (_handArea == null || refs == null)
            {
                return;
            }

            if (_engine.Phase == GamePhase.Playing && _engine.CurrentPlayer != LocalPlayerIndex)
            {
                _selectedIndices.Clear();
            }

            List<Card> hand = _engine.Players[LocalPlayerIndex].Hand;
            if (_selectedIndices.Count > 0)
            {
                List<int> invalid = new List<int>();
                foreach (int index in _selectedIndices)
                {
                    if (index >= hand.Count)
                    {
                        invalid.Add(index);
                    }
                }

                for (int i = 0; i < invalid.Count; i++)
                {
                    _selectedIndices.Remove(invalid[i]);
                }
            }

            EnsureHandSlots(hand.Count);

            float startX = -(hand.Count - 1) * HandSpacing * 0.5f;
            for (int i = 0; i < _handCards.Count; i++)
            {
                GameObject cardObj = _handCards[i];
                if (i < hand.Count)
                {
                    cardObj.SetActive(true);
                    RectTransform rect = cardObj.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(HandCardWidth, HandCardHeight);
                    float y = 10f + (_selectedIndices.Contains(i) ? 20f : 0f);
                    rect.anchoredPosition = new Vector2(startX + i * HandSpacing, y);
                    ApplyCardVisual(cardObj.transform, hand[i]);

                    Button button = cardObj.GetComponent<Button>();
                    if (button == null)
                    {
                        button = cardObj.AddComponent<Button>();
                    }
                    button.onClick.RemoveAllListeners();
                    int index = i;
                    button.onClick.AddListener(() => ToggleSelection(index));
                }
                else
                {
                    cardObj.SetActive(false);
                }
            }
        }

        private void EnsurePlayAreas()
        {
            if (_tableArea == null || _playAreas.Count > 0)
            {
                return;
            }

            _playAreas[LocalPlayerIndex] = CreatePlayArea("PlayArea_Bottom", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 100f), new Vector2(0f, -60f));
            _playAreas[1] = CreatePlayArea("PlayArea_Left", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 100f), new Vector2(40f, 40f));
            _playAreas[2] = CreatePlayArea("PlayArea_Right", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(200f, 100f), new Vector2(-40f, 40f));

            foreach (KeyValuePair<int, RectTransform> entry in _playAreas)
            {
                _passLabels[entry.Key] = CreatePassLabel(entry.Value);
            }
        }

        private RectTransform CreatePlayArea(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPos)
        {
            GameObject area = new GameObject(name, typeof(RectTransform));
            RectTransform rect = area.GetComponent<RectTransform>();
            rect.SetParent(_tableArea, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            return rect;
        }

        private void RecordPlay(StepResult result)
        {
            if (result.Kind == StepKind.Play || result.Kind == StepKind.Pass)
            {
                ClearPlayerTable(result.PlayerIndex);
            }

            if (result.Kind == StepKind.Play && result.Play.Cards.Count > 0)
            {
                _lastPlays[result.PlayerIndex] = new List<Card>(result.Play.Cards);
                SetPassLabelActive(result.PlayerIndex, false);
            }
            else if (result.Kind == StepKind.Pass)
            {
                SetPassLabelActive(result.PlayerIndex, true);
            }
        }

        private void ClearAllTablePlays()
        {
            _lastPlays.Clear();
            foreach (KeyValuePair<int, List<GameObject>> entry in _playCards)
            {
                SetPlayCardsActive(entry.Value, false);
            }

            foreach (int key in _passLabels.Keys)
            {
                SetPassLabelActive(key, false);
            }
        }

        private void ClearPlayerTable(int playerIndex)
        {
            _lastPlays.Remove(playerIndex);
            if (_playCards.TryGetValue(playerIndex, out List<GameObject> slots))
            {
                SetPlayCardsActive(slots, false);
            }

            SetPassLabelActive(playerIndex, false);
        }

        private void UpdateTableCards()
        {
            if (_tableArea == null || refs == null)
            {
                return;
            }

            foreach (KeyValuePair<int, RectTransform> area in _playAreas)
            {
                int playerIndex = area.Key;
                if (_lastPlays.TryGetValue(playerIndex, out List<Card> cards) && cards.Count > 0)
                {
                    EnsurePlaySlots(playerIndex, cards.Count);
                    List<GameObject> slots = _playCards[playerIndex];
                    float startX = -(cards.Count - 1) * TableSpacing * 0.5f;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        GameObject cardObj = slots[i];
                        if (i < cards.Count)
                        {
                            cardObj.SetActive(true);
                            RectTransform rect = cardObj.GetComponent<RectTransform>();
                            rect.sizeDelta = new Vector2(TableCardWidth, TableCardHeight);
                            rect.anchoredPosition = new Vector2(startX + i * TableSpacing, 0f);
                            ApplyCardVisual(cardObj.transform, cards[i]);
                        }
                        else
                        {
                            cardObj.SetActive(false);
                        }
                    }
                }
                else
                {
                    if (_playCards.TryGetValue(playerIndex, out List<GameObject> slots))
                    {
                        SetPlayCardsActive(slots, false);
                    }
                }
            }
        }

        private void EnsurePlaySlots(int playerIndex, int count)
        {
            if (refs.CardFacePrefab == null || !_playAreas.TryGetValue(playerIndex, out RectTransform area))
            {
                return;
            }

            if (!_playCards.TryGetValue(playerIndex, out List<GameObject> slots))
            {
                slots = new List<GameObject>();
                _playCards[playerIndex] = slots;
            }

            while (slots.Count < count)
            {
                GameObject card = Instantiate(refs.CardFacePrefab, area);
                card.name = $"PlayCard_{playerIndex}_{slots.Count + 1}";
                slots.Add(card);
            }
        }

        private static void SetPlayCardsActive(List<GameObject> cards, bool active)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].SetActive(active);
            }
        }

        private void EnsureHandSlots(int count)
        {
            if (refs.CardFacePrefab == null)
            {
                return;
            }

            while (_handCards.Count < count)
            {
                GameObject card = Instantiate(refs.CardFacePrefab, _handArea);
                card.name = $"HandCard_{_handCards.Count + 1}";
                _handCards.Add(card);
            }
        }

        private void ApplyCardVisual(Transform card, Card data)
        {
            Image rank = card.Find("Rank")?.GetComponent<Image>();
            Image suit = card.Find("Suit")?.GetComponent<Image>();
            Image center = card.Find("Center")?.GetComponent<Image>();

            if (data.Rank == CardRank.JokerSmall || data.Rank == CardRank.JokerBig)
            {
                if (rank != null) rank.sprite = refs.Joker;
                if (center != null) center.sprite = data.Rank == CardRank.JokerSmall ? refs.SmallJoker : refs.BigJoker;
                if (suit != null) suit.sprite = null;
            }
            else
            {
                if (rank != null) rank.sprite = refs.GetRankSprite(data.Rank);
                if (suit != null) suit.sprite = refs.GetSuitSprite(data.Suit);
                if (center != null) center.sprite = refs.GetSuitSprite(data.Suit);
            }

            if (rank != null) rank.preserveAspect = true;
            if (suit != null) suit.preserveAspect = true;
            if (center != null) center.preserveAspect = true;
            if (suit != null) suit.enabled = suit.sprite != null;
            if (center != null) center.enabled = center.sprite != null;

            if (rank != null)
            {
                RectTransform rect = rank.GetComponent<RectTransform>();
                if (data.Rank == CardRank.JokerSmall || data.Rank == CardRank.JokerBig)
                {
                    rect.sizeDelta = new Vector2(20f, 52f);
                    rect.anchoredPosition = new Vector2(8f, -6f);
                }
                else
                {
                    float width = data.Rank == CardRank.Ten ? 32f : 22f;
                    rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
                    rect.anchoredPosition = new Vector2(6f, -6f);
                }

                rank.color = GetRankColor(data);
            }

            if (suit != null)
            {
                suit.color = Color.white;
                RectTransform suitRect = suit.GetComponent<RectTransform>();
                suitRect.anchoredPosition = new Vector2(10f, -34f);
            }

            if (center != null)
            {
                RectTransform centerRect = center.GetComponent<RectTransform>();
                if (data.Rank == CardRank.JokerSmall || data.Rank == CardRank.JokerBig)
                {
                    centerRect.sizeDelta = new Vector2(48f, 48f);
                }
                else
                {
                    centerRect.sizeDelta = new Vector2(36f, 36f);
                }
            }
        }

        private void ToggleSelection(int index)
        {
            if (_engine.Phase != GamePhase.Playing || _engine.CurrentPlayer != LocalPlayerIndex)
            {
                return;
            }

            if (_selectedIndices.Contains(index))
            {
                _selectedIndices.Remove(index);
            }
            else
            {
                _selectedIndices.Add(index);
            }

            UpdateHand();
        }

        private void OnBidClicked(int bid)
        {
            if (_engine.Phase != GamePhase.Bidding)
            {
                return;
            }

            if (_engine.CurrentPlayer != LocalPlayerIndex)
            {
                return;
            }

            _strategy.SetBid(bid);
            StepAndRefresh();
        }

        private void OnPlayClicked()
        {
            if (_engine.Phase != GamePhase.Playing || _engine.CurrentPlayer != LocalPlayerIndex)
            {
                return;
            }

            List<Card> hand = _engine.Players[LocalPlayerIndex].Hand;
            PlayAction action;
            List<Card> selectedCards = GetSelectedCards(hand);
            if (selectedCards.Count > 0)
            {
                action = PlayAction.FromCards(selectedCards);
            }
            else
            {
                return;
            }

            _selectedIndices.Clear();
            _strategy.SetPlay(action);
            StepAndRefresh();
        }

        private void OnPassClicked()
        {
            if (_engine.Phase != GamePhase.Playing || _engine.CurrentPlayer != LocalPlayerIndex)
            {
                return;
            }

            _selectedIndices.Clear();
            _strategy.SetPlay(PlayAction.Pass());
            StepAndRefresh();
        }

        private void OnHintClicked()
        {
            if (_engine.Phase != GamePhase.Playing || _engine.CurrentPlayer != LocalPlayerIndex)
            {
                return;
            }

            PlayAction action = _strategy.BuildAutoPlay(_engine.Players[LocalPlayerIndex], _engine.LastPlay);
            if (action.Type != PlayType.Pass && action.Cards.Count > 0)
            {
                ApplySelectionFromCards(_engine.Players[LocalPlayerIndex].Hand, action.Cards);
            }
        }

        private void OnRestartClicked()
        {
            StartNewGame();
        }

        private List<Card> GetSelectedCards(List<Card> hand)
        {
            List<int> indices = new List<int>(_selectedIndices);
            indices.Sort();
            List<Card> selected = new List<Card>();
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index >= 0 && index < hand.Count)
                {
                    selected.Add(hand[index]);
                }
            }

            return selected;
        }

        private void ApplySelectionFromCards(List<Card> hand, List<Card> cards)
        {
            _selectedIndices.Clear();
            bool[] used = new bool[hand.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                Card target = cards[i];
                for (int j = 0; j < hand.Count; j++)
                {
                    if (used[j])
                    {
                        continue;
                    }

                    if (hand[j].Equals(target))
                    {
                        used[j] = true;
                        _selectedIndices.Add(j);
                        break;
                    }
                }
            }

            UpdateHand();
        }

        private GameEngine CreateEngine()
        {
            int seed = unchecked((int)System.DateTime.UtcNow.Ticks);
            return new GameEngine(_strategy, seed);
        }

        private void StartNewGame()
        {
            _strategy = new UiInputStrategy(LocalPlayerIndex, new AutoGameStrategy());
            _engine = CreateEngine();
            _selectedIndices.Clear();
            _nextTurnTime = Time.time;
            ClearAllTablePlays();
            RefreshAll();
        }

        private void UpdateBottomCards()
        {
            if (_bottomArea == null || refs == null || _engine == null)
            {
                return;
            }

            EnsureBottomFaceSlots(3);
            bool showFaces = _engine.Phase != GamePhase.Bidding;
            for (int i = 0; i < _bottomBacks.Count; i++)
            {
                _bottomBacks[i].SetActive(!showFaces);
            }

            for (int i = 0; i < _bottomFaces.Count; i++)
            {
                GameObject face = _bottomFaces[i];
                if (showFaces && i < _engine.BottomCards.Count)
                {
                    face.SetActive(true);
                    ApplyCardVisual(face.transform, _engine.BottomCards[i]);
                }
                else
                {
                    face.SetActive(false);
                }
            }
        }

        private void CacheBottomCards()
        {
            _bottomBacks.Clear();
            if (_bottomArea == null)
            {
                return;
            }

            for (int i = 0; i < _bottomArea.childCount; i++)
            {
                Transform child = _bottomArea.GetChild(i);
                _bottomBacks.Add(child.gameObject);
            }
        }

        private void EnsureBottomFaceSlots(int count)
        {
            if (refs.CardFacePrefab == null || _bottomArea == null)
            {
                return;
            }

            while (_bottomFaces.Count < count)
            {
                GameObject card = Instantiate(refs.CardFacePrefab, _bottomArea);
                card.name = $"BottomFace_{_bottomFaces.Count + 1}";
                RectTransform rect = card.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(56f, 76f);
                float offset = (_bottomFaces.Count - 1) * 40f;
                rect.anchoredPosition = new Vector2(offset, 0f);
                _bottomFaces.Add(card);
            }
        }

        private Text CreatePassLabel(RectTransform parent)
        {
            GameObject obj = new GameObject("PassText", typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.text = "不出";
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.35f, 0.25f, 0.1f, 1f);
            text.font = _centerTip != null ? _centerTip.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 40f);
            rect.anchoredPosition = Vector2.zero;
            obj.SetActive(false);
            return text;
        }

        private void SetPassLabelActive(int playerIndex, bool active)
        {
            if (_passLabels.TryGetValue(playerIndex, out Text label))
            {
                label.gameObject.SetActive(active);
            }
        }

        private Color GetRankColor(Card data)
        {
            if (data.Rank == CardRank.JokerSmall)
            {
                return Color.black;
            }

            if (data.Suit == CardSuit.Spade || data.Suit == CardSuit.Club)
            {
                return Color.black;
            }

            return Color.white;
        }

        private sealed class PlayerPanelView
        {
            private readonly Transform _root;
            private readonly Text _name;
            private readonly Text _coin;
            private readonly Text _role;

            public PlayerPanelView(Transform root)
            {
                _root = root;
                _name = root?.Find("NameText")?.GetComponent<Text>();
                _coin = root?.Find("CoinText")?.GetComponent<Text>();
                _role = root?.Find("RoleText")?.GetComponent<Text>();
            }

            public void Apply(PlayerState player, string displayName, string coin)
            {
                if (_root == null)
                {
                    return;
                }

                if (_name != null) _name.text = displayName;
                if (_coin != null) _coin.text = coin;
                if (_role != null) _role.text = player.Role == PlayerRole.Landlord ? "地主" : "农民";
            }
        }
    }
}

