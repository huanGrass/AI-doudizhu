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
        private const float RefreshInterval = 1f;

        private bool _readyRequesting;
        private bool _bidRequesting;

        private Button _bidNoButton;
        private Button _bidYesButton;

        private void Start()
        {
            ApplyRoomWaitingStage();
            BindBidButtons();
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

            RefreshSeats(Array.Empty<string>(), Array.Empty<bool>());
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
            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/state";
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
            OnlineRoomSession.ReplacePlayers(players);

            RefreshSeats(players, readyStates);

            int localIndex = FindPlayerIndex(players, OnlineRoomSession.LocalPlayerName);
            bool localInTable = localIndex >= 0;
            bool localReady = localInTable && localIndex < readyStates.Length && readyStates[localIndex];

            if (state.phase == 0)
            {
                int readyCount = CountReady(readyStates);
                SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 准备 {readyCount}/{state.capacity}");
                SetText("TableArea/CenterTip", "等待玩家准备");
                SetNodeActive("TableArea/BidBar", false);

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
                return;
            }

            string landlord = string.IsNullOrWhiteSpace(state.landlord) ? "-" : state.landlord;
            SetText("TopBar/Status", $"联机房间 | 桌子 {state.tableId} | 地主 {landlord}");
            SetText("TableArea/CenterTip", $"地主已确定: {landlord}（联机出牌流程待接入）");
            SetNodeActive("TableArea/BidBar", false);
        }

        private IEnumerator SendReady()
        {
            _readyRequesting = true;

            string url = $"{OnlineLobbyBridge.ServerBaseUrl}/api/tables/{OnlineRoomSession.TableId}/ready";
            ReadyRequestDto payload = new ReadyRequestDto
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
            BidRequestDto payload = new BidRequestDto
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

        private void RefreshSeats(string[] players, bool[] readyStates)
        {
            string localName = OnlineRoomSession.LocalPlayerName;
            if (string.IsNullOrWhiteSpace(localName))
            {
                localName = "玩家";
            }

            SetPanel("PlayerPanel_Bottom", localName, true, ResolveReadyForPlayer(players, readyStates, localName));

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

            SetPanel("PlayerPanel_Left", leftName, others.Count > 0, ResolveReadyForPlayer(players, readyStates, leftName));
            SetPanel("PlayerPanel_Right", rightName, others.Count > 1, ResolveReadyForPlayer(players, readyStates, rightName));
        }

        private void SetPanel(string panelPath, string playerName, bool occupied, bool ready)
        {
            SetText($"{panelPath}/NameText", playerName);
            SetText($"{panelPath}/CoinText", occupied ? "在线" : "--");
            SetText($"{panelPath}/RoleText", occupied ? (ready ? "准备" : "未准备") : "空位");
            SetText($"{panelPath}/CardCountText", string.Empty);
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
    }
}
