using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Doudizhu.UI
{
    public sealed class OnlineRoomStageController : MonoBehaviour
    {
        private const string ServerBaseUrl = "http://127.0.0.1:5014";
        private const float RefreshInterval = 1f;
        private const int StartPlayerCount = 3;

        private bool _started;

        private void Start()
        {
            ApplyRoomWaitingStage();
            StartCoroutine(PollPlayers());
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
            SetNodeActive("TableArea/BidBar", false);
            SetNodeActive("TableArea/RestartButton", false);
            SetNodeActive("BottomCards", false);
            SetNodeActive("HandArea", false);

            RefreshSeats();
        }

        private IEnumerator PollPlayers()
        {
            while (!_started)
            {
                string url = $"{ServerBaseUrl}/api/tables";
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 4;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        TableListResponseDto response = JsonUtility.FromJson<TableListResponseDto>(request.downloadHandler.text);
                        if (response != null && response.tables != null)
                        {
                            for (int i = 0; i < response.tables.Length; i++)
                            {
                                TableInfoDto table = response.tables[i];
                                if (table.tableId == OnlineRoomSession.TableId)
                                {
                                    OnlineRoomSession.ReplacePlayers(table.players);
                                    RefreshSeats();
                                    TryStartMatch(table.players);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!_started)
                {
                    yield return new WaitForSeconds(RefreshInterval);
                }
            }
        }

        private void TryStartMatch(string[] players)
        {
            int count = players == null ? 0 : players.Length;
            if (count < StartPlayerCount)
            {
                return;
            }

            DoudizhuUiController gameController = GetComponent<DoudizhuUiController>();
            if (gameController == null)
            {
                return;
            }

            _started = true;
            SetText("TopBar/Status", $"联机房间 | 桌子 {OnlineRoomSession.TableId} | 开始对局");
            SetNodeActive("BottomCards", true);
            SetNodeActive("HandArea", true);
            gameController.enabled = true;
            enabled = false;
        }

        private void RefreshSeats()
        {
            List<string> players = new List<string>(OnlineRoomSession.Players);
            string localName = string.IsNullOrWhiteSpace(OnlineRoomSession.LocalPlayerName) ? "我" : OnlineRoomSession.LocalPlayerName;

            SetPanel("PlayerPanel_Bottom", localName, true);

            List<string> others = new List<string>();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != localName)
                {
                    others.Add(players[i]);
                }
            }

            SetPanel("PlayerPanel_Left", others.Count > 0 ? others[0] : "空位", others.Count > 0);
            SetPanel("PlayerPanel_Right", others.Count > 1 ? others[1] : "空位", others.Count > 1);
        }

        private void SetPanel(string panelPath, string playerName, bool occupied)
        {
            SetText($"{panelPath}/NameText", playerName);
            SetText($"{panelPath}/CoinText", occupied ? "在线" : "--");
            SetText($"{panelPath}/RoleText", occupied ? "准备" : "空");
            SetText($"{panelPath}/CardCountText", string.Empty);
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

        [System.Serializable]
        private sealed class TableListResponseDto
        {
            public TableInfoDto[] tables;
        }

        [System.Serializable]
        private sealed class TableInfoDto
        {
            public int tableId;
            public string[] players;
        }
    }
}
