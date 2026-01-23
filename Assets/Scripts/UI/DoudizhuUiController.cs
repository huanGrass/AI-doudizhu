using System.Collections.Generic;
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

        private GameEngine _engine;
        private UiInputStrategy _strategy;

        private Transform _handArea;
        private readonly List<GameObject> _handCards = new List<GameObject>();

        private Text _centerTip;
        private Text _statusText;
        private PlayerPanelView _leftPanel;
        private PlayerPanelView _rightPanel;
        private PlayerPanelView _bottomPanel;

        private GameObject _actionBar;
        private GameObject _bidBar;

        private Button _playButton;
        private Button _passButton;
        private Button _hintButton;

        private void Awake()
        {
            if (refs == null)
            {
                refs = GetComponent<DoudizhuUiRefs>();
            }

            CacheUi();
            BuildBidBar();
            WireActionButtons();

            _strategy = new UiInputStrategy(LocalPlayerIndex, new AutoGameStrategy());
            _engine = new GameEngine(_strategy, 20260123);

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

            StepAndRefresh();
        }

        private void CacheUi()
        {
            Transform root = transform;
            _handArea = root.Find("HandArea");
            _actionBar = root.Find("ActionBar")?.gameObject;
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
        }

        private void BuildBidBar()
        {
            if (refs == null || refs.ActionButtonPrefab == null)
            {
                return;
            }

            _bidBar = new GameObject("BidBar", typeof(RectTransform));
            RectTransform rect = _bidBar.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(460f, 70f);
            rect.anchoredPosition = new Vector2(0f, 40f);

            string[] labels = { "不叫", "叫1", "叫2", "叫3" };
            int[] bids = { 0, 1, 2, 3 };
            float startX = -(labels.Length - 1) * 70f * 0.5f;

            for (int i = 0; i < labels.Length; i++)
            {
                GameObject buttonObj = Instantiate(refs.ActionButtonPrefab, _bidBar.transform);
                buttonObj.name = $"BidButton_{labels[i]}";
                RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(90f, 36f);
                btnRect.anchoredPosition = new Vector2(startX + i * 70f, 0f);

                Text text = buttonObj.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = labels[i];
                }

                Button button = buttonObj.GetComponent<Button>();
                int bidValue = bids[i];
                button.onClick.AddListener(() => OnBidClicked(bidValue));
            }
        }

        private void WireActionButtons()
        {
            _playButton?.onClick.AddListener(OnPlayClicked);
            _passButton?.onClick.AddListener(OnPassClicked);
            _hintButton?.onClick.AddListener(OnHintClicked);
        }

        private void StepAndRefresh()
        {
            StepResult result = _engine.Step();
            if (result.Kind == StepKind.Bid && result.BidScore > 0)
            {
                _statusText.text = $"叫分：{result.BidScore}  |  玩家 {result.PlayerIndex + 1}";
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            UpdatePhaseUi();
            UpdatePlayerPanels();
            UpdateHand();
        }

        private void UpdatePhaseUi()
        {
            if (_centerTip == null)
            {
                return;
            }

            if (_engine.Phase == GamePhase.Bidding)
            {
                _centerTip.text = "叫分阶段";
                SetActionBarActive(false);
                SetBidBarActive(true);
            }
            else if (_engine.Phase == GamePhase.Playing)
            {
                _centerTip.text = "等待出牌";
                SetActionBarActive(true);
                SetBidBarActive(false);
            }
            else
            {
                _centerTip.text = "本局结束";
                SetActionBarActive(false);
                SetBidBarActive(false);
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

            List<Card> hand = _engine.Players[LocalPlayerIndex].Hand;
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
                    rect.anchoredPosition = new Vector2(startX + i * HandSpacing, 10f);
                    ApplyCardVisual(cardObj.transform, hand[i]);
                }
                else
                {
                    cardObj.SetActive(false);
                }
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
                if (center != null) center.sprite = data.Rank == CardRank.JokerBig ? refs.BigJoker : refs.SmallJoker;
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

            PlayAction action = _strategy.BuildAutoPlay(_engine.Players[LocalPlayerIndex], _engine.LastPlay);
            _strategy.SetPlay(action);
            StepAndRefresh();
        }

        private void OnPassClicked()
        {
            if (_engine.Phase != GamePhase.Playing || _engine.CurrentPlayer != LocalPlayerIndex)
            {
                return;
            }

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
            _strategy.SetPlay(action);
            StepAndRefresh();
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
