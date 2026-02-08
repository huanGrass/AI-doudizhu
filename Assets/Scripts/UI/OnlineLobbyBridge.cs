using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

        private DoudizhuRuntimeUiBuilder _builder;
        private MethodInfo _startGameMethod;
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
            EnsureBuilderRefs();
            TryHookOnlineButton();
            AutoRefreshWhenLobbyVisible();
        }

        private void EnsureBuilderRefs()
        {
            if (_builder == null)
            {
                _builder = FindAnyObjectByType<DoudizhuRuntimeUiBuilder>();
            }

            if (_startGameMethod == null)
            {
                _startGameMethod = typeof(DoudizhuRuntimeUiBuilder).GetMethod("StartGame", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        private void TryHookOnlineButton()
        {
            if (_onlineButtonHooked || _builder == null || _startGameMethod == null)
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
                StartCoroutine(RefreshTablesAndPatchJoinButtons());
            }
        }

        private void OnOnlineClicked()
        {
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
                    _isRefreshing = false;
                    yield break;
                }

                TableListResponseDto response = JsonUtility.FromJson<TableListResponseDto>(request.downloadHandler.text);
                if (response == null || response.tables == null)
                {
                    _isRefreshing = false;
                    yield break;
                }

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
                joinButton.onClick.AddListener(() => StartCoroutine(JoinAndEnter(capturedTableId)));
                _patchedTableIds.Add(table.tableId);
            }

            bool mySeat = ContainsPlayer(table, LocalPlayerName);
            joinButton.interactable = mySeat || table.playerCount < capacity;
        }

        private IEnumerator JoinAndEnter(int tableId)
        {
            if (_builder == null || _startGameMethod == null)
            {
                yield break;
            }

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
                    yield break;
                }
            }

            GameObject root = GameObject.Find("UIRoot");
            if (root == null)
            {
                yield break;
            }

            _startGameMethod.Invoke(_builder, new object[] { root, $"联机模式  |  桌子 {tableId}" });
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
