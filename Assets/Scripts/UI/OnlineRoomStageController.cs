using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Doudizhu.Game;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Doudizhu.UI
{
    public sealed class OnlineRoomStageController : MonoBehaviour
    {
        private const float RefreshInterval = 0.8f;
        private const float HandSpacing = 28f;
        private const float HandCardWidth = 64f;
        private const float HandCardHeight = 92f;

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
        private DoudizhuUiRefs _refs;
        private readonly List<GameObject> _handCards = new();
        private readonly List<Card> _currentHandCards = new();
        private readonly HashSet<int> _selectedIndices = new();

        private void Start()
        {
            _refs = GetComponent<DoudizhuUiRefs>();
            _handArea = transform.Find("HandArea");

            ApplyRoomWaitingStage();
            BindBidButtons();
            BindActionButtons();
            BindRestartButton();
            EnsureLeaveButton();
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

                Text noText = _bidNoButton.GetComponentInChildren<Text>();
                if (noText != null)
                {
                    noText.text = "不叫";
                }
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

                Text yesText = _bidYesButton.GetComponentInChildren<Text>();
                if (yesText != null)
                {
                    yesText.text = "叫地主";
                }
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
                _hintButton.gameObject.SetActive(false);
            }
        }

        private IEnumerator PollStateLoop()
        {
            while (true)
            {
                yield return FetchAndApplyState();
                yield return new WaitForSeconds(RefreshInterval);
            }
        }

        private IEnumerator FetchAndApplyState()
        {
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
            string[] players = state.players ?? Array.Empty<string>();
            bool[] readyStates = state.readyStates ?? Array.Empty<bool>();
            bool[] connectedStates = state.connectedStates ?? Array.Empty<bool>();
            int[] handCounts = state.handCounts ?? Array.Empty<int>();
            OnlineRoomSession.ReplacePlayers(players);

            RefreshSeats(players, readyStates, connectedStates, handCounts);
            UpdateHandCards(state.myHand ?? Array.Empty<string>());

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

                if (localInTable && !localReady && !_readyRequesting)
                {
                    StartCoroutine(SendReady());
                }

                return;
            }

            if (state.phase == 1)
            {
                string currentBidder = string.IsNullOrWhiteSpace(state.currentBidder) ? "-" : state.currentBidder;
                SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 叫地主中");
                SetText("TableArea/CenterTip", $"轮到 {currentBidder} 叫地主");

                bool myTurn = string.Equals(state.currentBidder, OnlineRoomSession.LocalPlayerName, StringComparison.Ordinal);
                SetNodeActive("TableArea/BidBar", myTurn);
                SetNodeActive("TableArea/ActionBar", false);
                SetNodeActive("TableArea/RestartButton", false);
                SetNodeActive("HandArea", false);
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

                return;
            }

            string winner = string.IsNullOrWhiteSpace(state.winner) ? "-" : state.winner;
            SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 对局结束");
            SetText("TableArea/CenterTip", $"胜者: {winner}");
            SetNodeActive("TableArea/BidBar", false);
            SetNodeActive("TableArea/ActionBar", false);
            SetNodeActive("TableArea/RestartButton", true);
            SetNodeActive("HandArea", true);
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
            }

            _bidRequesting = false;
        }

        private IEnumerator SendPlay(bool pass)
        {
            _playRequesting = true;

            string[] selected = Array.Empty<string>();
            if (!pass)
            {
                selected = GetSelectedCardCodes();
                if (selected.Length == 0)
                {
                    _playRequesting = false;
                    yield break;
                }
            }

            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/play";
            PlayRequestDto payload = new()
            {
                playerName = OnlineRoomSession.LocalPlayerName,
                pass = pass,
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
            }

            if (!pass)
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

        private static string SerializeCard(Card card)
        {
            string rank = card.Rank switch
            {
                CardRank.JokerSmall => "SJ",
                CardRank.JokerBig => "BJ",
                CardRank.Jack => "J",
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
            public string[] players;
            public bool[] readyStates;
            public bool[] connectedStates;
            public bool[] restartVotes;
            public string currentBidder;
            public string landlord;
            public string currentTurn;
            public int[] handCounts;
            public string[] lastPlayCards;
            public string[] myHand;
            public string winner;
            public string lastActionPlayer;
            public bool lastActionWasPass;
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
    }
}
