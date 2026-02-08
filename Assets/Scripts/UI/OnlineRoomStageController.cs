using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Doudizhu.UI
{
    public sealed class OnlineRoomStageController : MonoBehaviour
    {
        private const float RefreshInterval = 0.8f;

        private bool _readyRequesting;
        private bool _bidRequesting;
        private bool _playRequesting;

        private Button _bidNoButton;
        private Button _bidYesButton;
        private Button _playButton;
        private Button _passButton;
        private Button _hintButton;

        private void Start()
        {
            ApplyRoomWaitingStage();
            BindBidButtons();
            BindActionButtons();
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
            SetNodeActive("HandArea", true);
            SetNodeActive("TableArea/BidBar", false);

            SetText("HandArea/HandLabel", "你的手牌");
            RefreshSeats(Array.Empty<string>(), Array.Empty<bool>(), Array.Empty<int>());
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
            int[] handCounts = state.handCounts ?? Array.Empty<int>();
            OnlineRoomSession.ReplacePlayers(players);

            RefreshSeats(players, readyStates, handCounts);
            UpdateHandLabel(state.myHand ?? Array.Empty<string>());

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
                if (_passButton != null)
                {
                    _passButton.interactable = state.lastPlayCards != null && state.lastPlayCards.Length > 0;
                }

                return;
            }

            string winner = string.IsNullOrWhiteSpace(state.winner) ? "-" : state.winner;
            SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 对局结束");
            SetText("TableArea/CenterTip", $"胜者: {winner}");
            SetNodeActive("TableArea/BidBar", false);
            SetNodeActive("TableArea/ActionBar", false);
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

        private void UpdateHandLabel(string[] myHand)
        {
            if (myHand.Length == 0)
            {
                SetText("HandArea/HandLabel", "你的手牌");
                return;
            }

            SetText("HandArea/HandLabel", $"你的手牌({myHand.Length}): {string.Join(" ", myHand)}");
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

            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/play";
            PlayRequestDto payload = new()
            {
                playerName = OnlineRoomSession.LocalPlayerName,
                pass = pass
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

            _playRequesting = false;
        }

        private void RefreshSeats(string[] players, bool[] readyStates, int[] handCounts)
        {
            string localName = OnlineRoomSession.LocalPlayerName;
            if (string.IsNullOrWhiteSpace(localName))
            {
                localName = "玩家";
            }

            SetPanel("PlayerPanel_Bottom", localName, true, ResolveReadyForPlayer(players, readyStates, localName), ResolveHandCountForPlayer(players, handCounts, localName));

            List<string> others = new List<string>();
            for (int i = 0; i < players.Length; i++)
            {
                if (!string.Equals(players[i], localName, StringComparison.Ordinal))
                {
                    others.Add(players[i]);
                }
            }

            string leftName = others.Count > 0 ? others[0] : "空位";
            string rightName = others.Count > 1 ? others[1] : "空位";

            SetPanel("PlayerPanel_Left", leftName, others.Count > 0, ResolveReadyForPlayer(players, readyStates, leftName), ResolveHandCountForPlayer(players, handCounts, leftName));
            SetPanel("PlayerPanel_Right", rightName, others.Count > 1, ResolveReadyForPlayer(players, readyStates, rightName), ResolveHandCountForPlayer(players, handCounts, rightName));
        }

        private void SetPanel(string panelPath, string playerName, bool occupied, bool ready, int handCount)
        {
            SetText($"{panelPath}/NameText", playerName);
            SetText($"{panelPath}/CoinText", occupied ? "在线" : "--");
            SetText($"{panelPath}/RoleText", occupied ? (ready ? "准备" : "未准备") : "空位");
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
            public string currentBidder;
            public string landlord;
            public string currentTurn;
            public int[] handCounts;
            public string[] lastPlayCards;
            public string lastPlayPlayer;
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
        }
    }
}
