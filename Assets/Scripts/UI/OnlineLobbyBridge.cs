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
        private const string DefaultServerBaseUrl = "http://127.0.0.1:5014";
        private const string LocalPlayerName = "Madlee";

        private readonly Dictionary<int, TableInfoDto> _tables = new Dictionary<int, TableInfoDto>();
        private DoudizhuRuntimeUiBuilder _builder;
        private MethodInfo _startGameMethod;
        private Button _onlineButton;
        private bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject obj = new GameObject("OnlineLobbyBridge");
            DontDestroyOnLoad(obj);
            obj.AddComponent<OnlineLobbyBridge>();
        }

        private void Start()
        {
            StartCoroutine(BindWhenReady());
        }

        private IEnumerator BindWhenReady()
        {
            float timeoutAt = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                _builder = FindAnyObjectByType<DoudizhuRuntimeUiBuilder>();
                if (_builder != null)
                {
                    GameObject onlineBtnObj = GameObject.Find("UIRoot/ModePanel/OnlineModeButton");
                    Transform onlineBtnTransform = onlineBtnObj != null ? onlineBtnObj.transform : null;
                    if (onlineBtnTransform != null)
                    {
                        _onlineButton = onlineBtnTransform.GetComponent<Button>();
                        if (_onlineButton != null)
                        {
                            _startGameMethod = typeof(DoudizhuRuntimeUiBuilder).GetMethod("StartGame", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (_startGameMethod != null)
                            {
                                _onlineButton.onClick.AddListener(OnOnlineClicked);
                                _initialized = true;
                                yield break;
                            }
                        }
                    }
                }

                yield return null;
            }
        }

        private void OnOnlineClicked()
        {
            if (!_initialized || _builder == null)
            {
                return;
            }

            StartCoroutine(RefreshTablesAndWireJoin());
        }

        private IEnumerator RefreshTablesAndWireJoin()
        {
            string url = DefaultServerBaseUrl + "/api/tables";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 4;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    yield break;
                }

                TableListResponseDto response = JsonUtility.FromJson<TableListResponseDto>(request.downloadHandler.text);
                if (response == null || response.tables == null)
                {
                    yield break;
                }

                _tables.Clear();
                for (int i = 0; i < response.tables.Length; i++)
                {
                    TableInfoDto table = response.tables[i];
                    _tables[table.tableId] = table;
                    UpdateTableUi(table);
                }
            }
        }

        private void UpdateTableUi(TableInfoDto table)
        {
            GameObject tableObj = GameObject.Find($"UIRoot/LobbyPanel/Table_{table.tableId}");
            Transform tableRoot = tableObj != null ? tableObj.transform : null;
            if (tableRoot == null)
            {
                return;
            }

            Text players = tableRoot.Find("Players")?.GetComponent<Text>();
            Text seat = tableRoot.Find("SeatText")?.GetComponent<Text>();
            Button joinButton = tableRoot.Find("JoinButton")?.GetComponent<Button>();

            string playersText = table.players != null && table.players.Length > 0
                ? string.Join("、", table.players)
                : "暂无玩家";
            if (players != null)
            {
                players.text = $"玩家: {playersText}";
            }

            int count = table.playerCount;
            int capacity = table.capacity <= 0 ? 3 : table.capacity;
            if (seat != null)
            {
                seat.text = $"人数: {count}/{capacity}";
            }

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                int tableId = table.tableId;
                joinButton.onClick.AddListener(() => StartCoroutine(JoinTableAndStart(tableId)));
                joinButton.interactable = count < capacity || ContainsPlayer(table, LocalPlayerName);
            }
        }

        private IEnumerator JoinTableAndStart(int tableId)
        {
            string url = $"{DefaultServerBaseUrl}/api/tables/{tableId}/join";
            JoinTableRequestDto requestPayload = new JoinTableRequestDto { playerName = LocalPlayerName };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestPayload));

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
                if (table.players[i] == playerName)
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
