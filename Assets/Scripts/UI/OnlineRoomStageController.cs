using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Doudizhu.Audio;
using Doudizhu.Game;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Doudizhu.UI
{
    public sealed class OnlineRoomStageController : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private const float HandSpacing = 28f;
        private const float HandCardWidth = 64f;
        private const float HandCardHeight = 92f;
        private const float TableSpacing = 24f;
        private const float TableCardWidth = 56f;
        private const float TableCardHeight = 80f;

        private bool _readyRequesting;
        private bool _bidRequesting;
        private bool _playRequesting;
        private bool _restartRequesting;
        private bool _leaveRequesting;

        private Button _bidNoButton;
        private Button _bidYesButton;
        private Button _playButton;
        private Button _passButton;
        private Button _hintButton;
        private Button _restartButton;
        private Button _leaveButton;

        private Transform _handArea;
        private Transform _tableArea;
        private Transform _bottomArea;
        private DoudizhuUiRefs _refs;
        private readonly List<GameObject> _handCards = new();
        private readonly List<Card> _currentHandCards = new();
        private readonly HashSet<int> _selectedIndices = new();
        private readonly List<GameObject> _bottomBacks = new();
        private readonly List<GameObject> _bottomFaces = new();
        private readonly Dictionary<int, RectTransform> _playAreas = new();
        private readonly Dictionary<int, List<GameObject>> _playCards = new();
        private readonly Dictionary<int, List<Card>> _lastPlays = new();
        private readonly Dictionary<int, Text> _passLabels = new();
        private readonly Dictionary<int, Text> _bidLabels = new();
        private readonly Dictionary<string, int> _seatIndexByPlayer = new(StringComparer.Ordinal);

        private int _lastBidHistoryCount;
        private string _lastActionKey = string.Empty;
        private int _lastPhase = -1;
        private int _stateEpoch;
        private TableStateDto _latestState;

        private void Start()
        {
            _refs = GetComponent<DoudizhuUiRefs>();
            _handArea = transform.Find("HandArea");
            _tableArea = transform.Find("TableArea");
            _bottomArea = transform.Find("BottomCards");

            ApplyRoomWaitingStage();
            BindBidButtons();
            BindActionButtons();
            BindRestartButton();
            EnsureLeaveButton();
            EnsurePlayAreas();
            CacheBottomCards();
            StartCoroutine(PollStateLoop());
        }

        private void ApplyRoomWaitingStage()
        {
            DoudizhuUiController gameController = GetComponent<DoudizhuUiController>();
            if (gameController != null)
            {
                gameController.enabled = false;
            }

            SetText("TopBar/Status", $"联机房间 | 桌子 {OnlineRoomSession.TableId}");
            SetText("TableArea/CenterTip", "等待玩家准备");

            SetNodeActive("ActionBar", false);
            SetNodeActive("TableArea/ActionBar", false);
            SetNodeActive("TableArea/RestartButton", false);
            SetNodeActive("BottomCards", false);
            SetNodeActive("HandArea", false);
            SetNodeActive("TableArea/BidBar", false);

            SetText("HandArea/HandLabel", "你的手牌");
            RefreshSeats(Array.Empty<string>(), Array.Empty<bool>(), Array.Empty<bool>(), Array.Empty<int>());
        }

        private void BindRestartButton()
        {
            _restartButton = transform.Find("TableArea/RestartButton")?.GetComponent<Button>();
            if (_restartButton == null)
            {
                return;
            }

            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() =>
            {
                if (!_restartRequesting)
                {
                    StartCoroutine(SendRestart());
                }
            });

            Text text = _restartButton.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = "再来一局";
            }
        }

        private void EnsureLeaveButton()
        {
            Transform topBar = transform.Find("TopBar");
            if (topBar == null)
            {
                return;
            }

            GameObject go = new GameObject("LeaveRoomButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(topBar, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(90f, 32f);
            rect.anchoredPosition = new Vector2(-320f, 0f);

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.72f, 0.23f, 0.2f, 1f);
            _leaveButton = go.GetComponent<Button>();
            _leaveButton.onClick.RemoveAllListeners();
            _leaveButton.onClick.AddListener(() =>
            {
                if (!_leaveRequesting)
                {
                    StartCoroutine(SendLeave());
                }
            });

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(go.transform, false);
            RectTransform lr = labelObj.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
            Text label = labelObj.GetComponent<Text>();
            label.text = "离开";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 16;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void BindBidButtons()
        {
            Transform bidBar = transform.Find("TableArea/BidBar");
            if (bidBar == null)
            {
                return;
            }

            _bidNoButton = bidBar.Find("BidButton_0")?.GetComponent<Button>();
            _bidYesButton = bidBar.Find("BidButton_1")?.GetComponent<Button>();

            if (_bidNoButton != null)
            {
                _bidNoButton.onClick.RemoveAllListeners();
                _bidNoButton.onClick.AddListener(() =>
                {
                    if (!_bidRequesting)
                    {
                        StartCoroutine(SendBid(false));
                    }
                });

            }

            if (_bidYesButton != null)
            {
                _bidYesButton.onClick.RemoveAllListeners();
                _bidYesButton.onClick.AddListener(() =>
                {
                    if (!_bidRequesting)
                    {
                        StartCoroutine(SendBid(true));
                    }
                });

            }
        }

        private void BindActionButtons()
        {
            Transform actionBar = transform.Find("TableArea/ActionBar") ?? transform.Find("ActionBar");
            if (actionBar == null)
            {
                return;
            }

            _playButton = actionBar.Find("ActionButton_出牌")?.GetComponent<Button>();
            _passButton = actionBar.Find("ActionButton_不出")?.GetComponent<Button>();
            _hintButton = actionBar.Find("ActionButton_提示")?.GetComponent<Button>();

            if (_playButton != null)
            {
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(() =>
                {
                    if (!_playRequesting)
                    {
                        StartCoroutine(SendPlay(false));
                    }
                });
            }

            if (_passButton != null)
            {
                _passButton.onClick.RemoveAllListeners();
                _passButton.onClick.AddListener(() =>
                {
                    if (!_playRequesting)
                    {
                        StartCoroutine(SendPlay(true));
                    }
                });
            }

            if (_hintButton != null)
            {
                _hintButton.gameObject.SetActive(true);
                _hintButton.onClick.RemoveAllListeners();
                _hintButton.onClick.AddListener(OnHintClicked);
            }
        }

        private IEnumerator PollStateLoop()
        {
            while (true)
            {
                if (_readyRequesting || _bidRequesting || _playRequesting || _restartRequesting || _leaveRequesting)
                {
                    yield return new WaitForSecondsRealtime(RefreshInterval);
                    continue;
                }

                yield return FetchAndApplyState();
                yield return new WaitForSecondsRealtime(RefreshInterval);
            }
        }

        private IEnumerator FetchAndApplyState()
        {
            int fetchEpoch = _stateEpoch;
            string player = UnityWebRequest.EscapeURL(OnlineRoomSession.LocalPlayerName ?? string.Empty);
            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/state?playerName={player}";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 4;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    SetText("TopBar/Status", $"联机房间 | 桌子 {OnlineRoomSession.TableId} | 同步失败");
                    yield break;
                }

                if (_readyRequesting || _bidRequesting || _playRequesting || _restartRequesting || _leaveRequesting)
                {
                    yield break;
                }

                if (fetchEpoch != _stateEpoch)
                {
                    yield break;
                }

                TableStateDto state = JsonUtility.FromJson<TableStateDto>(request.downloadHandler.text);
                if (state == null)
                {
                    yield break;
                }

                ApplyState(state);
            }
        }

        private void ApplyState(TableStateDto state)
        {
            _latestState = state;
            string[] players = state.players ?? Array.Empty<string>();
            bool[] readyStates = state.readyStates ?? Array.Empty<bool>();
            bool[] connectedStates = state.connectedStates ?? Array.Empty<bool>();
            int[] handCounts = state.handCounts ?? Array.Empty<int>();
            OnlineRoomSession.ReplacePlayers(players);
            RebuildSeatMap(players);

            RefreshSeats(players, readyStates, connectedStates, handCounts);
            UpdateHandCards(state.myHand ?? Array.Empty<string>());
            ProcessBidHistory(state, players);
            ProcessLastAction(state, players);
            UpdateTableCards();
            UpdateBottomCards(state.phase >= 2, state.bottomCards ?? Array.Empty<string>());

            if (_lastPhase != state.phase)
            {
                if (state.phase <= 1)
                {
                    ClearAllTablePlays();
                }

                if (state.phase == 0)
                {
                    _lastBidHistoryCount = 0;
                    ClearAllBidLabels();
                }

                _lastPhase = state.phase;
            }

            int localIndex = FindPlayerIndex(players, OnlineRoomSession.LocalPlayerName);
            bool localInTable = localIndex >= 0;
            bool localReady = localInTable && localIndex < readyStates.Length && readyStates[localIndex];

            if (state.phase == 0)
            {
                int readyCount = CountReady(readyStates);
                SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 准备 {readyCount}/{state.capacity}");
                SetText("TableArea/CenterTip", "等待玩家准备");
                SetNodeActive("TableArea/BidBar", false);
                SetNodeActive("TableArea/ActionBar", false);
                SetNodeActive("TableArea/RestartButton", false);
                SetNodeActive("HandArea", false);
                SetNodeActive("BottomCards", false);

                if (localInTable && !localReady && !_readyRequesting)
                {
                    StartCoroutine(SendReady());
                }

                return;
            }

            if (state.phase == 1)
            {
                bool isRobStage = state.bidStage == (int)TableBidStage.Rob;
                string currentBidder = string.IsNullOrWhiteSpace(state.currentBidder) ? "-" : state.currentBidder;
                string stageText = isRobStage ? "抢地主中" : "叫地主中";
                string actionText = isRobStage ? "抢地主" : "叫地主";
                SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | {stageText}");
                SetText("TableArea/CenterTip", $"轮到 {currentBidder} {actionText}");

                bool myTurn = string.Equals(state.currentBidder, OnlineRoomSession.LocalPlayerName, StringComparison.Ordinal);
                UpdateBidButtonLabels(isRobStage);
                SetNodeActive("TableArea/BidBar", myTurn);
                SetNodeActive("TableArea/ActionBar", false);
                SetNodeActive("TableArea/RestartButton", false);
                SetNodeActive("HandArea", true);
                SetNodeActive("BottomCards", true);
                ShowBottomBacksOnly();
                return;
            }

            if (state.phase == 2)
            {
                string currentTurn = string.IsNullOrWhiteSpace(state.currentTurn) ? "-" : state.currentTurn;
                string landlord = string.IsNullOrWhiteSpace(state.landlord) ? "-" : state.landlord;
                string lastPlay = BuildLastPlayText(state);

                SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 地主 {landlord} | 轮到 {currentTurn}");
                SetText("TableArea/CenterTip", lastPlay);

                bool myTurn = string.Equals(state.currentTurn, OnlineRoomSession.LocalPlayerName, StringComparison.Ordinal);
                SetNodeActive("TableArea/BidBar", false);
                SetNodeActive("TableArea/ActionBar", myTurn);
                SetNodeActive("TableArea/RestartButton", false);
                SetNodeActive("HandArea", true);
                SetNodeActive("BottomCards", true);
                ClearAllBidLabels();

                if (!myTurn)
                {
                    _selectedIndices.Clear();
                    RefreshHandVisual();
                }

                if (_playButton != null)
                {
                    _playButton.interactable = myTurn && _selectedIndices.Count > 0;
                }

                if (_passButton != null)
                {
                    _passButton.interactable = myTurn && state.lastPlayCards != null && state.lastPlayCards.Length > 0;
                }

                if (_hintButton != null)
                {
                    _hintButton.interactable = myTurn;
                }

                return;
            }

            string winner = string.IsNullOrWhiteSpace(state.winner) ? "-" : state.winner;
            SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 对局结束");
            SetText("TableArea/CenterTip", $"胜者: {winner}");
            SetNodeActive("TableArea/BidBar", false);
            SetNodeActive("TableArea/ActionBar", false);
            SetNodeActive("TableArea/RestartButton", true);
            SetNodeActive("HandArea", true);
            SetNodeActive("BottomCards", true);
            ClearAllBidLabels();
            if (_restartButton != null && localInTable)
            {
                bool[] votes = state.restartVotes ?? Array.Empty<bool>();
                bool localVoted = localIndex >= 0 && localIndex < votes.Length && votes[localIndex];
                _restartButton.interactable = !localVoted;
            }
            _selectedIndices.Clear();
            RefreshHandVisual();
        }

        private static string BuildLastPlayText(TableStateDto state)
        {
            string player = string.IsNullOrWhiteSpace(state.lastActionPlayer) ? "-" : state.lastActionPlayer;
            if (state.lastActionWasPass)
            {
                return $"上一手: {player} 不出";
            }

            if (state.lastPlayCards != null && state.lastPlayCards.Length > 0)
            {
                return $"上一手: {player} 出 {string.Join(" ", state.lastPlayCards)}";
            }

            return "等待出牌";
        }

        private void ProcessBidHistory(TableStateDto state, string[] players)
        {
            BidActionDto[] history = state.bidHistory ?? Array.Empty<BidActionDto>();
            if (_lastBidHistoryCount > history.Length)
            {
                _lastBidHistoryCount = 0;
                ClearAllBidLabels();
            }

            for (int i = _lastBidHistoryCount; i < history.Length; i++)
            {
                BidActionDto bid = history[i];
                int serverIdx = ResolveServerIndex(players, bid.playerName, bid.playerIndex);
                int seatIdx = ResolveSeatIndex(players, bid.playerName, bid.playerIndex);
                if (seatIdx < 0 || serverIdx < 0)
                {
                    continue;
                }

                bool isRobStage = bid.bidStage == (int)TableBidStage.Rob;
                SetBidLabel(seatIdx, bid.callLandlord ? (isRobStage ? "抢地主" : "叫地主") : (isRobStage ? "不抢" : "不叫"), true);
                StepResult step = new StepResult(GamePhase.Bidding, StepKind.Bid, serverIdx, bid.callLandlord ? 1 : 0, PlayAction.Pass(), -1);
                DoudizhuAudioManager.Instance?.PlayStep(step, isRobStage ? BidStage.Rob : BidStage.Call, null);
            }

            _lastBidHistoryCount = history.Length;
        }

        private void UpdateBidButtonLabels(bool isRobStage)
        {
            if (_bidNoButton != null)
            {
                Text noText = _bidNoButton.GetComponentInChildren<Text>();
                if (noText != null)
                {
                    noText.text = isRobStage ? "不抢" : "不叫";
                }
            }

            if (_bidYesButton != null)
            {
                Text yesText = _bidYesButton.GetComponentInChildren<Text>();
                if (yesText != null)
                {
                    yesText.text = isRobStage ? "抢地主" : "叫地主";
                }
            }
        }

        private void ProcessLastAction(TableStateDto state, string[] players)
        {
            string[] cards = state.lastPlayCards ?? Array.Empty<string>();
            string actionKey = $"{state.phase}|{state.lastActionPlayer}|{state.lastActionWasPass}|{string.Join(",", cards)}";
            if (string.Equals(actionKey, _lastActionKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastActionKey = actionKey;
            if (string.IsNullOrWhiteSpace(state.lastActionPlayer))
            {
                return;
            }

            int serverIdx = ResolveServerIndex(players, state.lastActionPlayer, -1);
            int seatIdx = ResolveSeatIndex(players, state.lastActionPlayer, -1);
            if (seatIdx < 0 || serverIdx < 0)
            {
                return;
            }

            if (state.lastActionWasPass)
            {
                SetPassLabelActive(seatIdx, true);
                StepResult step = new StepResult(GamePhase.Playing, StepKind.Pass, serverIdx, 0, PlayAction.Pass(), -1);
                PlayAction? lastPlay = cards.Length > 0 ? PlayAction.FromCards(ParseCards(cards)) : PlayAction.Pass();
                DoudizhuAudioManager.Instance?.PlayStep(step, BidStage.Call, lastPlay);
                if (cards.Length == 0)
                {
                    ClearAllTablePlays();
                }

                return;
            }

            List<Card> parsed = ParseCards(cards);
            if (parsed.Count == 0)
            {
                return;
            }

            ClearAllTablePlays();
            _lastPlays[seatIdx] = parsed;
            SetPassLabelActive(seatIdx, false);
            StepResult playStep = new StepResult(GamePhase.Playing, StepKind.Play, serverIdx, 0, PlayAction.FromCards(new List<Card>(parsed)), -1);
            DoudizhuAudioManager.Instance?.PlayStep(playStep, BidStage.Call, null);
        }

        private void OnHintClicked()
        {
            Transform actionBar = transform.Find("TableArea/ActionBar") ?? transform.Find("ActionBar");
            if (actionBar == null || !actionBar.gameObject.activeInHierarchy)
            {
                return;
            }

            PlayAction? lastPlay = GetCurrentLastPlay();

            PlayAction hint = PlayRules.FindAutoPlay(new List<Card>(_currentHandCards), lastPlay);
            if (hint.Type == PlayType.Pass || hint.Cards.Count == 0)
            {
                if (lastPlay.HasValue && lastPlay.Value.Type != PlayType.Pass && !_playRequesting)
                {
                    StartCoroutine(SendPlay(true));
                }
                else
                {
                    SetText("TableArea/CenterTip", "没有牌能打过上家");
                }

                return;
            }

            ApplySelectionFromCards(_currentHandCards, hint.Cards);
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
                    if (used[j] || !hand[j].Equals(target))
                    {
                        continue;
                    }

                    used[j] = true;
                    _selectedIndices.Add(j);
                    break;
                }
            }

            if (_playButton != null)
            {
                _playButton.interactable = _selectedIndices.Count > 0;
            }

            RefreshHandVisual();
        }

        private void UpdateHandCards(string[] handCodes)
        {
            _currentHandCards.Clear();
            for (int i = 0; i < handCodes.Length; i++)
            {
                if (TryParseCard(handCodes[i], out Card card))
                {
                    _currentHandCards.Add(card);
                }
            }

            List<int> invalid = new();
            foreach (int idx in _selectedIndices)
            {
                if (idx < 0 || idx >= _currentHandCards.Count)
                {
                    invalid.Add(idx);
                }
            }

            for (int i = 0; i < invalid.Count; i++)
            {
                _selectedIndices.Remove(invalid[i]);
            }

            RefreshHandVisual();
        }

        private void RefreshHandVisual()
        {
            if (_handArea == null || _refs == null)
            {
                SetText("HandArea/HandLabel", $"你的手牌({_currentHandCards.Count})");
                return;
            }

            EnsureHandSlots(_currentHandCards.Count);
            float startX = -(_currentHandCards.Count - 1) * HandSpacing * 0.5f;

            for (int i = 0; i < _handCards.Count; i++)
            {
                GameObject cardObj = _handCards[i];
                if (i < _currentHandCards.Count)
                {
                    cardObj.SetActive(true);
                    RectTransform rect = cardObj.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(HandCardWidth, HandCardHeight);
                    float y = 10f + (_selectedIndices.Contains(i) ? 20f : 0f);
                    rect.anchoredPosition = new Vector2(startX + i * HandSpacing, y);
                    ApplyCardVisual(cardObj.transform, _currentHandCards[i]);

                    Button button = cardObj.GetComponent<Button>();
                    if (button == null)
                    {
                        button = cardObj.AddComponent<Button>();
                    }

                    button.onClick.RemoveAllListeners();
                    int captured = i;
                    button.onClick.AddListener(() => ToggleSelection(captured));
                }
                else
                {
                    cardObj.SetActive(false);
                }
            }

            SetText("HandArea/HandLabel", $"你的手牌({_currentHandCards.Count})");
        }

        private void EnsureHandSlots(int count)
        {
            if (_refs.CardFacePrefab == null)
            {
                return;
            }

            while (_handCards.Count < count)
            {
                GameObject card = Instantiate(_refs.CardFacePrefab, _handArea);
                card.name = $"OnlineHandCard_{_handCards.Count + 1}";
                _handCards.Add(card);
            }
        }

        private void EnsurePlayAreas()
        {
            if (_tableArea == null || _playAreas.Count > 0)
            {
                return;
            }

            _playAreas[0] = CreatePlayArea("PlayArea_Bottom", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260f, 100f), new Vector2(0f, -60f));
            _playAreas[1] = CreatePlayArea("PlayArea_Left", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(200f, 100f), new Vector2(40f, 40f));
            _playAreas[2] = CreatePlayArea("PlayArea_Right", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(200f, 100f), new Vector2(-40f, 40f));

            foreach (KeyValuePair<int, RectTransform> area in _playAreas)
            {
                _passLabels[area.Key] = CreateStateLabel(area.Value, "PassText", "不出", 22, new Color(0.35f, 0.25f, 0.1f, 1f), new Vector2(120f, 40f));
                _bidLabels[area.Key] = CreateStateLabel(area.Value, "BidText", "叫地主", 24, new Color(1f, 0.9f, 0.2f, 1f), new Vector2(160f, 44f));
                _passLabels[area.Key].gameObject.SetActive(false);
                _bidLabels[area.Key].gameObject.SetActive(false);
            }
        }

        private RectTransform CreatePlayArea(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            GameObject area = new GameObject(name, typeof(RectTransform));
            RectTransform rect = area.GetComponent<RectTransform>();
            rect.SetParent(_tableArea, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private static Text CreateStateLabel(RectTransform parent, string name, string content, int size, Color color, Vector2 rectSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = rectSize;
            rect.anchoredPosition = Vector2.zero;
            return text;
        }

        private void UpdateTableCards()
        {
            if (_refs == null || _refs.CardFacePrefab == null)
            {
                return;
            }

            foreach (KeyValuePair<int, RectTransform> area in _playAreas)
            {
                int player = area.Key;
                if (_lastPlays.TryGetValue(player, out List<Card> cards) && cards.Count > 0)
                {
                    EnsurePlaySlots(player, cards.Count);
                    List<GameObject> slots = _playCards[player];
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
                else if (_playCards.TryGetValue(player, out List<GameObject> hidden))
                {
                    for (int i = 0; i < hidden.Count; i++)
                    {
                        hidden[i].SetActive(false);
                    }
                }
            }
        }

        private void EnsurePlaySlots(int playerIndex, int count)
        {
            if (!_playAreas.TryGetValue(playerIndex, out RectTransform area))
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
                GameObject card = Instantiate(_refs.CardFacePrefab, area);
                card.name = $"PlayCard_{playerIndex}_{slots.Count + 1}";
                slots.Add(card);
            }
        }

        private void ClearAllTablePlays()
        {
            _lastPlays.Clear();
            foreach (List<GameObject> cards in _playCards.Values)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    cards[i].SetActive(false);
                }
            }

            SetPassLabelActive(0, false);
            SetPassLabelActive(1, false);
            SetPassLabelActive(2, false);
        }

        private void ClearPlayerTable(int playerIndex)
        {
            _lastPlays.Remove(playerIndex);
            if (_playCards.TryGetValue(playerIndex, out List<GameObject> slots))
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i].SetActive(false);
                }
            }

            SetPassLabelActive(playerIndex, false);
        }

        private void SetPassLabelActive(int playerIndex, bool active)
        {
            if (_passLabels.TryGetValue(playerIndex, out Text label))
            {
                label.gameObject.SetActive(active);
            }
        }

        private void SetBidLabel(int playerIndex, string content, bool active)
        {
            if (_bidLabels.TryGetValue(playerIndex, out Text label))
            {
                label.text = content;
                label.gameObject.SetActive(active);
            }
        }

        private void ClearAllBidLabels()
        {
            foreach (Text label in _bidLabels.Values)
            {
                label.gameObject.SetActive(false);
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
                _bottomBacks.Add(_bottomArea.GetChild(i).gameObject);
            }
        }

        private void EnsureBottomFaceSlots(int count)
        {
            if (_refs == null || _refs.CardFacePrefab == null || _bottomArea == null)
            {
                return;
            }

            while (_bottomFaces.Count < count)
            {
                GameObject card = Instantiate(_refs.CardFacePrefab, _bottomArea);
                card.name = $"BottomFace_{_bottomFaces.Count + 1}";
                RectTransform rect = card.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(56f, 76f);
                rect.anchoredPosition = new Vector2((_bottomFaces.Count - 1) * 40f, 0f);
                _bottomFaces.Add(card);
            }
        }

        private void ShowBottomBacksOnly()
        {
            for (int i = 0; i < _bottomBacks.Count; i++)
            {
                _bottomBacks[i].SetActive(true);
            }

            for (int i = 0; i < _bottomFaces.Count; i++)
            {
                _bottomFaces[i].SetActive(false);
            }
        }

        private void UpdateBottomCards(bool showFaces, string[] bottomCodes)
        {
            if (_bottomArea == null)
            {
                return;
            }

            EnsureBottomFaceSlots(3);
            if (!showFaces)
            {
                ShowBottomBacksOnly();
                return;
            }

            for (int i = 0; i < _bottomBacks.Count; i++)
            {
                _bottomBacks[i].SetActive(false);
            }

            List<Card> cards = ParseCards(bottomCodes);
            for (int i = 0; i < _bottomFaces.Count; i++)
            {
                if (i < cards.Count)
                {
                    _bottomFaces[i].SetActive(true);
                    ApplyCardVisual(_bottomFaces[i].transform, cards[i]);
                }
                else
                {
                    _bottomFaces[i].SetActive(false);
                }
            }
        }

        private void ToggleSelection(int index)
        {
            Transform actionBar = transform.Find("TableArea/ActionBar") ?? transform.Find("ActionBar");
            if (actionBar == null || !actionBar.gameObject.activeInHierarchy)
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

            if (_playButton != null)
            {
                _playButton.interactable = _selectedIndices.Count > 0;
            }

            RefreshHandVisual();
        }

        private IEnumerator SendReady()
        {
            _readyRequesting = true;
            _stateEpoch++;

            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/ready";
            ReadyRequestDto payload = new()
            {
                playerName = OnlineRoomSession.LocalPlayerName,
                ready = true
            };

            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 4;
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
            }

            _readyRequesting = false;
        }

        private IEnumerator SendBid(bool callLandlord)
        {
            _bidRequesting = true;
            _stateEpoch++;

            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/bid";
            BidRequestDto payload = new()
            {
                playerName = OnlineRoomSession.LocalPlayerName,
                callLandlord = callLandlord
            };

            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 4;
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string err = GetRequestError(request);
                    SetText("TopBar/Status", $"联机房间 | 叫抢地主失败: {err}");
                }
                else
                {
                    TableStateDto state = JsonUtility.FromJson<TableStateDto>(request.downloadHandler.text);
                    if (state != null)
                    {
                        ApplyState(state);
                    }
                }
            }

            _bidRequesting = false;
        }

        private IEnumerator SendPlay(bool pass)
        {
            _playRequesting = true;
            _stateEpoch++;

            bool sendPass = pass;
            string[] selected = Array.Empty<string>();
            if (!sendPass)
            {
                if (!TryBuildPlayableRequest(out sendPass, out selected))
                {
                    _playRequesting = false;
                    yield break;
                }
            }

            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/play";
            PlayRequestDto payload = new()
            {
                playerName = OnlineRoomSession.LocalPlayerName,
                pass = sendPass,
                cards = selected
            };

            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 4;
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string err = GetRequestError(request);
                    SetText("TopBar/Status", $"联机房间 | 出牌失败: {err}");
                    _playRequesting = false;
                    yield break;
                }

                TableStateDto state = JsonUtility.FromJson<TableStateDto>(request.downloadHandler.text);
                if (state != null)
                {
                    ApplyState(state);
                }
            }

            if (!sendPass)
            {
                _selectedIndices.Clear();
                if (_playButton != null)
                {
                    _playButton.interactable = false;
                }
            }

            _playRequesting = false;
        }

        private IEnumerator SendRestart()
        {
            _restartRequesting = true;
            _stateEpoch++;
            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/restart";
            NameOnlyRequest payload = new() { playerName = OnlineRoomSession.LocalPlayerName };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 4;
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
            }

            _restartRequesting = false;
        }

        private IEnumerator SendLeave()
        {
            _leaveRequesting = true;
            _stateEpoch++;
            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/leave";
            NameOnlyRequest payload = new() { playerName = OnlineRoomSession.LocalPlayerName };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 4;
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }
                else
                {
                    string err = string.IsNullOrWhiteSpace(request.error) ? $"HTTP {request.responseCode}" : request.error;
                    SetText("TopBar/Status", $"联机房间 | 离开失败: {err}");
                }
            }

            _leaveRequesting = false;
        }

        private string[] GetSelectedCardCodes()
        {
            List<int> indices = new(_selectedIndices);
            indices.Sort();
            List<string> result = new(indices.Count);
            for (int i = 0; i < indices.Count; i++)
            {
                int idx = indices[i];
                if (idx >= 0 && idx < _currentHandCards.Count)
                {
                    result.Add(SerializeCard(_currentHandCards[idx]));
                }
            }

            return result.ToArray();
        }

        private bool TryBuildPlayableRequest(out bool pass, out string[] cards)
        {
            pass = false;
            cards = Array.Empty<string>();

            string[] selectedCodes = GetSelectedCardCodes();
            if (selectedCodes.Length == 0)
            {
                return false;
            }

            List<Card> selectedCards = ParseCards(selectedCodes);
            if (selectedCards.Count != selectedCodes.Length)
            {
                return false;
            }

            PlayAction? lastPlay = GetCurrentLastPlay();
            bool validSelection = PlayRules.TryEvaluate(selectedCards, out PlayPattern currentPattern);
            if (validSelection && lastPlay.HasValue && lastPlay.Value.Type != PlayType.Pass)
            {
                validSelection = PlayRules.TryEvaluate(lastPlay.Value.Cards, out PlayPattern lastPattern)
                    && PlayRules.CanBeat(currentPattern, lastPattern);
            }

            if (validSelection)
            {
                cards = selectedCodes;
                return true;
            }

            PlayAction auto = PlayRules.FindAutoPlay(new List<Card>(_currentHandCards), lastPlay);
            if (auto.Type == PlayType.Pass || auto.Cards.Count == 0)
            {
                if (lastPlay.HasValue && lastPlay.Value.Type != PlayType.Pass)
                {
                    pass = true;
                    cards = Array.Empty<string>();
                    return true;
                }

                return false;
            }

            cards = new string[auto.Cards.Count];
            for (int i = 0; i < auto.Cards.Count; i++)
            {
                cards[i] = SerializeCard(auto.Cards[i]);
            }

            return true;
        }

        private PlayAction? GetCurrentLastPlay()
        {
            if (_latestState?.lastPlayCards == null || _latestState.lastPlayCards.Length == 0)
            {
                return null;
            }

            List<Card> cards = ParseCards(_latestState.lastPlayCards);
            if (cards.Count == 0)
            {
                return null;
            }

            return PlayAction.FromCards(cards);
        }

        private bool HasPlayableResponse()
        {
            PlayAction? lastPlay = GetCurrentLastPlay();
            if (!lastPlay.HasValue || lastPlay.Value.Type == PlayType.Pass)
            {
                return true;
            }

            PlayAction action = PlayRules.FindAutoPlay(new List<Card>(_currentHandCards), lastPlay);
            return action.Type != PlayType.Pass && action.Cards.Count > 0;
        }

        private static string GetRequestError(UnityWebRequest request)
        {
            if (request == null)
            {
                return "未知错误";
            }

            string text = request.downloadHandler?.text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                ErrorResponseDto dto = JsonUtility.FromJson<ErrorResponseDto>(text);
                if (dto != null && !string.IsNullOrWhiteSpace(dto.error))
                {
                    return dto.error;
                }
            }

            return string.IsNullOrWhiteSpace(request.error) ? $"HTTP {request.responseCode}" : request.error;
        }

        private static string SerializeCard(Card card)
        {
            string rank = card.Rank switch
            {
                CardRank.JokerSmall => "SJ",
                CardRank.JokerBig => "BJ",
                CardRank.Jack => "11",
                CardRank.Queen => "Q",
                CardRank.King => "K",
                CardRank.Ace => "A",
                CardRank.Two => "2",
                _ => ((int)card.Rank).ToString()
            };

            string suit = card.Suit switch
            {
                CardSuit.Spade => "S",
                CardSuit.Heart => "H",
                CardSuit.Club => "C",
                CardSuit.Diamond => "D",
                _ => string.Empty
            };

            return suit + rank;
        }

        private static List<Card> ParseCards(string[] codes)
        {
            List<Card> cards = new();
            if (codes == null)
            {
                return cards;
            }

            for (int i = 0; i < codes.Length; i++)
            {
                if (TryParseCard(codes[i], out Card card))
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        private static bool TryParseCard(string code, out Card card)
        {
            card = default;
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            string input = code.Trim().ToUpperInvariant();
            if (input == "SJ")
            {
                card = new Card(CardSuit.Joker, CardRank.JokerSmall);
                return true;
            }

            if (input == "BJ")
            {
                card = new Card(CardSuit.Joker, CardRank.JokerBig);
                return true;
            }

            if (input.Length < 2)
            {
                return false;
            }

            CardSuit suit = input[0] switch
            {
                'S' => CardSuit.Spade,
                'H' => CardSuit.Heart,
                'C' => CardSuit.Club,
                'D' => CardSuit.Diamond,
                _ => CardSuit.Joker
            };
            if (suit == CardSuit.Joker)
            {
                return false;
            }

            string rankText = input[1..];
            CardRank rank = rankText switch
            {
                "A" => CardRank.Ace,
                "K" => CardRank.King,
                "Q" => CardRank.Queen,
                "J" => CardRank.Jack,
                "11" => CardRank.Jack,
                "2" => CardRank.Two,
                "10" => CardRank.Ten,
                "9" => CardRank.Nine,
                "8" => CardRank.Eight,
                "7" => CardRank.Seven,
                "6" => CardRank.Six,
                "5" => CardRank.Five,
                "4" => CardRank.Four,
                "3" => CardRank.Three,
                _ => 0
            };

            if (rank == 0)
            {
                return false;
            }

            card = new Card(suit, rank);
            return true;
        }

        private void ApplyCardVisual(Transform card, Card data)
        {
            Image rank = card.Find("Rank")?.GetComponent<Image>();
            Image suit = card.Find("Suit")?.GetComponent<Image>();
            Image center = card.Find("Center")?.GetComponent<Image>();

            if (data.Rank == CardRank.JokerSmall || data.Rank == CardRank.JokerBig)
            {
                if (rank != null) rank.sprite = _refs.Joker;
                if (center != null) center.sprite = data.Rank == CardRank.JokerSmall ? _refs.SmallJoker : _refs.BigJoker;
                if (suit != null) suit.sprite = null;
            }
            else
            {
                if (rank != null) rank.sprite = _refs.GetRankSprite(data.Rank);
                if (suit != null) suit.sprite = _refs.GetSuitSprite(data.Suit);
                if (center != null) center.sprite = _refs.GetSuitSprite(data.Suit);
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
                    rect.sizeDelta = new Vector2(width, 22f);
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
                centerRect.sizeDelta = data.Rank == CardRank.JokerSmall || data.Rank == CardRank.JokerBig
                    ? new Vector2(48f, 48f)
                    : new Vector2(36f, 36f);
            }
        }

        private static Color GetRankColor(Card data)
        {
            if (data.Rank == CardRank.JokerSmall)
            {
                return new Color(0.12f, 0.22f, 0.86f, 1f);
            }

            if (data.Suit == CardSuit.Spade || data.Suit == CardSuit.Club)
            {
                return new Color(0.12f, 0.14f, 0.18f, 1f);
            }

            return new Color(0.8f, 0.16f, 0.18f, 1f);
        }

        private void RefreshSeats(string[] players, bool[] readyStates, bool[] connectedStates, int[] handCounts)
        {
            string localName = OnlineRoomSession.LocalPlayerName;
            if (string.IsNullOrWhiteSpace(localName))
            {
                localName = "玩家";
            }

            SetPanel("PlayerPanel_Bottom", localName, true, ResolveReadyForPlayer(players, readyStates, localName), ResolveConnectedForPlayer(players, connectedStates, localName), ResolveHandCountForPlayer(players, handCounts, localName));

            List<string> others = new();
            for (int i = 0; i < players.Length; i++)
            {
                if (!string.Equals(players[i], localName, StringComparison.Ordinal))
                {
                    others.Add(players[i]);
                }
            }

            string leftName = others.Count > 0 ? others[0] : "空位";
            string rightName = others.Count > 1 ? others[1] : "空位";

            SetPanel("PlayerPanel_Left", leftName, others.Count > 0, ResolveReadyForPlayer(players, readyStates, leftName), ResolveConnectedForPlayer(players, connectedStates, leftName), ResolveHandCountForPlayer(players, handCounts, leftName));
            SetPanel("PlayerPanel_Right", rightName, others.Count > 1, ResolveReadyForPlayer(players, readyStates, rightName), ResolveConnectedForPlayer(players, connectedStates, rightName), ResolveHandCountForPlayer(players, handCounts, rightName));
        }

        private void RebuildSeatMap(string[] players)
        {
            _seatIndexByPlayer.Clear();

            string localName = OnlineRoomSession.LocalPlayerName;
            bool localInPlayers = !string.IsNullOrWhiteSpace(localName) && FindPlayerIndex(players, localName) >= 0;
            if (localInPlayers)
            {
                _seatIndexByPlayer[localName] = 0;

                int seat = 1;
                for (int i = 0; i < players.Length && seat <= 2; i++)
                {
                    string player = players[i];
                    if (string.IsNullOrWhiteSpace(player) || string.Equals(player, localName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!_seatIndexByPlayer.ContainsKey(player))
                    {
                        _seatIndexByPlayer[player] = seat;
                        seat++;
                    }
                }

                return;
            }

            for (int i = 0; i < players.Length && i <= 2; i++)
            {
                string player = players[i];
                if (string.IsNullOrWhiteSpace(player) || _seatIndexByPlayer.ContainsKey(player))
                {
                    continue;
                }

                _seatIndexByPlayer[player] = i;
            }
        }

        private int ResolveSeatIndex(string[] players, string playerName, int fallbackServerIndex)
        {
            if (!string.IsNullOrWhiteSpace(playerName) && _seatIndexByPlayer.TryGetValue(playerName, out int mapped))
            {
                return mapped;
            }

            if (fallbackServerIndex >= 0 && fallbackServerIndex < players.Length)
            {
                string fallbackPlayer = players[fallbackServerIndex];
                if (!string.IsNullOrWhiteSpace(fallbackPlayer) && _seatIndexByPlayer.TryGetValue(fallbackPlayer, out mapped))
                {
                    return mapped;
                }
            }

            if (fallbackServerIndex is >= 0 and <= 2)
            {
                return fallbackServerIndex;
            }

            return -1;
        }

        private static int ResolveServerIndex(string[] players, string playerName, int fallbackServerIndex)
        {
            int byName = FindPlayerIndex(players, playerName);
            if (byName >= 0)
            {
                return byName;
            }

            if (fallbackServerIndex >= 0 && fallbackServerIndex < players.Length)
            {
                return fallbackServerIndex;
            }

            return -1;
        }

        private void SetPanel(string panelPath, string playerName, bool occupied, bool ready, bool connected, int handCount)
        {
            SetText($"{panelPath}/NameText", playerName);
            SetText($"{panelPath}/CoinText", occupied ? (connected ? "在线" : "离线") : "--");
            SetText($"{panelPath}/RoleText", occupied ? (connected ? (ready ? "准备" : "未准备") : "离线托管") : "空位");
            SetText($"{panelPath}/CardCountText", occupied && handCount >= 0 ? $"手牌: {handCount}" : string.Empty);
        }

        private static int FindPlayerIndex(string[] players, string playerName)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (string.Equals(players[i], playerName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool ResolveReadyForPlayer(string[] players, bool[] readyStates, string playerName)
        {
            int idx = FindPlayerIndex(players, playerName);
            return idx >= 0 && idx < readyStates.Length && readyStates[idx];
        }

        private static int ResolveHandCountForPlayer(string[] players, int[] handCounts, string playerName)
        {
            int idx = FindPlayerIndex(players, playerName);
            return idx >= 0 && idx < handCounts.Length ? handCounts[idx] : -1;
        }

        private static bool ResolveConnectedForPlayer(string[] players, bool[] connectedStates, string playerName)
        {
            int idx = FindPlayerIndex(players, playerName);
            return idx >= 0 && idx < connectedStates.Length && connectedStates[idx];
        }

        private static int CountReady(bool[] readyStates)
        {
            int count = 0;
            for (int i = 0; i < readyStates.Length; i++)
            {
                if (readyStates[i])
                {
                    count++;
                }
            }

            return count;
        }

        private void SetText(string path, string text)
        {
            Text target = transform.Find(path)?.GetComponent<Text>();
            if (target != null)
            {
                target.text = text;
            }
        }

        private void SetNodeActive(string path, bool active)
        {
            Transform node = transform.Find(path);
            if (node != null)
            {
                node.gameObject.SetActive(active);
            }
        }

        [Serializable]
        private sealed class TableStateDto
        {
            public int tableId;
            public int capacity;
            public int phase;
            public int bidStage;
            public string[] players;
            public bool[] readyStates;
            public bool[] connectedStates;
            public bool[] restartVotes;
            public string currentBidder;
            public string landlord;
            public BidActionDto[] bidHistory;
            public string currentTurn;
            public int[] handCounts;
            public string[] bottomCards;
            public string[] lastPlayCards;
            public string[] myHand;
            public string winner;
            public string lastActionPlayer;
            public bool lastActionWasPass;
        }

        [Serializable]
        private sealed class BidActionDto
        {
            public int playerIndex;
            public string playerName;
            public bool callLandlord;
            public int bidStage;
        }

        private enum TableBidStage
        {
            Call = 0,
            Rob = 1
        }

        [Serializable]
        private sealed class ReadyRequestDto
        {
            public string playerName;
            public bool ready;
        }

        [Serializable]
        private sealed class BidRequestDto
        {
            public string playerName;
            public bool callLandlord;
        }

        [Serializable]
        private sealed class PlayRequestDto
        {
            public string playerName;
            public bool pass;
            public string[] cards;
        }

        [Serializable]
        private sealed class NameOnlyRequest
        {
            public string playerName;
        }

        [Serializable]
        private sealed class ErrorResponseDto
        {
            public string error;
        }
    }
}
