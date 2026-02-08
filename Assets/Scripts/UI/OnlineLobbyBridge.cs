using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Doudizhu.UI
{
    public sealed class OnlineLobbyBridge : MonoBehaviour
    {
        private const string ServerBaseUrl = "http://127.0.0.1:5014";
        private const string LocalPlayerName = "Madlee";

        private readonly HashSet<int> _patchedTableIds = new HashSet<int>();

        private bool _onlineButtonHooked;
        private bool _isRefreshing;
        private bool _lobbySeen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<OnlineLobbyBridge>() != null)
            {
                return;
            }

            GameObject obj = new GameObject("OnlineLobbyBridge");
            DontDestroyOnLoad(obj);
            obj.AddComponent<OnlineLobbyBridge>();
        }

        private void Update()
        {
            TryHookOnlineButton();
            AutoRefreshWhenLobbyVisible();
        }

        private void TryHookOnlineButton()
        {
            if (_onlineButtonHooked)
            {
                return;
            }

            GameObject onlineBtnObj = GameObject.Find("UIRoot/ModePanel/OnlineModeButton");
            if (onlineBtnObj == null)
            {
                return;
            }

            Button onlineButton = onlineBtnObj.GetComponent<Button>();
            if (onlineButton == null)
            {
                return;
            }

            onlineButton.onClick.AddListener(OnOnlineClicked);
            _onlineButtonHooked = true;
        }

        private void AutoRefreshWhenLobbyVisible()
        {
            GameObject lobby = GameObject.Find("UIRoot/LobbyPanel");
            bool visible = lobby != null && lobby.activeInHierarchy;
            if (!visible)
            {
                _lobbySeen = false;
                _patchedTableIds.Clear();
                return;
            }

            if (!_lobbySeen && !_isRefreshing)
            {
                _lobbySeen = true;
                SetLobbyToEmptyState();
                StartCoroutine(RefreshTablesAndPatchJoinButtons());
            }
        }

        private void OnOnlineClicked()
        {
            SetLobbyToEmptyState();
            if (!_isRefreshing)
            {
                StartCoroutine(RefreshTablesAndPatchJoinButtons());
            }
        }

        private IEnumerator RefreshTablesAndPatchJoinButtons()
        {
            _isRefreshing = true;
            string url = ServerBaseUrl + "/api/tables";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 4;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    SetTopStatus("联机模式  |  服务端不可用");
                    _isRefreshing = false;
                    yield break;
                }

                TableListResponseDto response = JsonUtility.FromJson<TableListResponseDto>(request.downloadHandler.text);
                if (response == null || response.tables == null)
                {
                    SetTopStatus("联机模式  |  服务端数据无效");
                    _isRefreshing = false;
                    yield break;
                }

                SetTopStatus("联机模式  |  已连接服务端");
                for (int i = 0; i < response.tables.Length; i++)
                {
                    PatchTableUi(response.tables[i]);
                }
            }

            _isRefreshing = false;
        }

        private void PatchTableUi(TableInfoDto table)
        {
            GameObject tableObj = GameObject.Find($"UIRoot/LobbyPanel/Table_{table.tableId}");
            if (tableObj == null)
            {
                return;
            }

            Transform tableRoot = tableObj.transform;
            Text players = tableRoot.Find("Players")?.GetComponent<Text>();
            Text seat = tableRoot.Find("SeatText")?.GetComponent<Text>();
            Button joinButton = tableRoot.Find("JoinButton")?.GetComponent<Button>();

            if (players != null)
            {
                string names = table.players != null && table.players.Length > 0 ? string.Join("、", table.players) : "暂无玩家";
                players.text = $"玩家: {names}";
            }

            int capacity = table.capacity <= 0 ? 3 : table.capacity;
            if (seat != null)
            {
                seat.text = $"人数: {table.playerCount}/{capacity}";
            }

            if (joinButton == null)
            {
                return;
            }

            if (!_patchedTableIds.Contains(table.tableId))
            {
                joinButton.onClick.RemoveAllListeners();
                int capturedTableId = table.tableId;
                joinButton.onClick.AddListener(() => StartCoroutine(JoinTableOnly(capturedTableId)));
                _patchedTableIds.Add(table.tableId);
            }

            bool mySeat = ContainsPlayer(table, LocalPlayerName);
            joinButton.interactable = mySeat || table.playerCount < capacity;
        }

        private IEnumerator JoinTableOnly(int tableId)
        {
            string url = $"{ServerBaseUrl}/api/tables/{tableId}/join";
            JoinTableRequestDto req = new JoinTableRequestDto { playerName = LocalPlayerName };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(req));

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 4;

                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    SetTopStatus("联机模式  |  入桌失败");
                    yield break;
                }
            }

            SetTopStatus($"联机模式  |  已加入桌子 {tableId}");
            if (!_isRefreshing)
            {
                StartCoroutine(RefreshTablesAndPatchJoinButtons());
            }
        }

        private void SetLobbyToEmptyState()
        {
            for (int i = 1; i <= 8; i++)
            {
                GameObject tableObj = GameObject.Find($"UIRoot/LobbyPanel/Table_{i}");
                if (tableObj == null)
                {
                    continue;
                }

                Text players = tableObj.transform.Find("Players")?.GetComponent<Text>();
                Text seat = tableObj.transform.Find("SeatText")?.GetComponent<Text>();
                Button joinButton = tableObj.transform.Find("JoinButton")?.GetComponent<Button>();
                if (players != null)
                {
                    players.text = "玩家: 暂无玩家";
                }

                if (seat != null)
                {
                    seat.text = "人数: 0/3";
                }

                if (joinButton != null)
                {
                    joinButton.onClick.RemoveAllListeners();
                    joinButton.interactable = false;
                }
            }

            SetTopStatus("联机模式  |  正在连接服务端");
        }

        private static void SetTopStatus(string text)
        {
            Text status = GameObject.Find("UIRoot/TopBar/Status")?.GetComponent<Text>();
            if (status != null)
            {
                status.text = text;
            }
        }

        private static bool ContainsPlayer(TableInfoDto table, string playerName)
        {
            if (table.players == null)
            {
                return false;
            }

            for (int i = 0; i < table.players.Length; i++)
            {
                if (string.Equals(table.players[i], playerName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class TableListResponseDto
        {
            public TableInfoDto[] tables;
        }

        [Serializable]
        private sealed class TableInfoDto
        {
            public int tableId;
            public string[] players;
            public int playerCount;
            public int capacity;
        }

        [Serializable]
        private sealed class JoinTableRequestDto
        {
            public string playerName;
        }
    }
}
