using System;
using System.Collections;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Doudizhu.UI
{
    public sealed class OnlineLobbyBridge : MonoBehaviour
    {
        private const string ServerBaseUrl = "http://127.0.0.1:5014";
        private const string LocalPlayerName = "Madlee";

        private bool _onlineButtonHooked;
        private bool _isRefreshing;
        private bool _lobbySeen;

        private DoudizhuRuntimeUiBuilder _builder;
        private MethodInfo _startGameMethod;

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
                    SetTopStatus("联机模式 | 服务端不可用");
                    _isRefreshing = false;
                    yield break;
                }

                TableListResponseDto response = JsonUtility.FromJson<TableListResponseDto>(request.downloadHandler.text);
                if (response == null || response.tables == null)
                {
                    SetTopStatus("联机模式 | 服务端返回无效数据");
                    _isRefreshing = false;
                    yield break;
                }

                SetTopStatus("联机模式 | 已连接服务端");
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

            joinButton.onClick.RemoveAllListeners();
            int capturedTableId = table.tableId;
            joinButton.onClick.AddListener(() => StartCoroutine(JoinTableAndEnterRoom(capturedTableId)));
            joinButton.interactable = table.playerCount < capacity || ContainsPlayer(table, LocalPlayerName);
        }

        private IEnumerator JoinTableAndEnterRoom(int tableId)
        {
            SetTopStatus($"联机模式 | 正在加入桌子 {tableId}...");

            string url = $"{ServerBaseUrl}/api/tables/{tableId}/join";
            JoinTableRequestDto req = new JoinTableRequestDto { playerName = LocalPlayerName };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(req));

            TableInfoDto joinedTable = null;
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 4;

                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string err = string.IsNullOrEmpty(request.error) ? $"HTTP {request.responseCode}" : request.error;
                    SetTopStatus($"联机模式 | 入桌失败: {err}");
                    yield break;
                }

                joinedTable = JsonUtility.FromJson<TableInfoDto>(request.downloadHandler.text);
            }

            if (_builder == null || _startGameMethod == null)
            {
                SetTopStatus("联机模式 | 入桌成功，但界面启动失败");
                yield break;
            }

            string[] players = joinedTable != null && joinedTable.players != null
                ? joinedTable.players
                : Array.Empty<string>();
            OnlineRoomSession.Set(tableId, LocalPlayerName, players);

            GameObject oldRoot = GameObject.Find("UIRoot");
            if (oldRoot == null)
            {
                SetTopStatus("联机模式 | 入桌成功，但大厅节点丢失");
                yield break;
            }

            _startGameMethod.Invoke(_builder, new object[] { oldRoot, $"联机房间 | 桌子 {tableId}" });
            yield return null;

            GameObject newRoot = GameObject.Find("UIRoot");
            if (newRoot != null && newRoot.GetComponent<OnlineRoomStageController>() == null)
            {
                newRoot.AddComponent<OnlineRoomStageController>();
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

            SetTopStatus("联机模式 | 正在连接服务端...");
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

    public static class OnlineRoomSession
    {
        public static int TableId { get; private set; }
        public static string LocalPlayerName { get; private set; }
        public static IReadOnlyList<string> Players => _players;

        private static readonly List<string> _players = new List<string>();

        public static void Set(int tableId, string localPlayerName, IEnumerable<string> players)
        {
            TableId = tableId;
            LocalPlayerName = localPlayerName ?? string.Empty;
            _players.Clear();
            if (players == null)
            {
                return;
            }

            foreach (string p in players)
            {
                if (!string.IsNullOrWhiteSpace(p))
                {
                    _players.Add(p);
                }
            }
        }
    }
}
