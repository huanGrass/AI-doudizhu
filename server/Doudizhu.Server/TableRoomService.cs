namespace Doudizhu.Server;

public sealed class TableRoomService
{
    private const int MaxPlayersPerTable = 3;

    private readonly object _gate = new();
    private readonly List<TableRoom> _tables = [new(1), new(2), new(3), new(4)];

    public IReadOnlyList<TableDto> GetTables()
    {
        lock (_gate)
        {
            return _tables.Select(ToDto).ToArray();
        }
    }

    public TableStateResult GetTableState(int tableId)
    {
        lock (_gate)
        {
            TableRoom? room = _tables.FirstOrDefault(t => t.TableId == tableId);
            if (room == null)
            {
                return new TableStateResult(false, null);
            }

            return new TableStateResult(true, ToStateDto(room));
        }
    }

    public JoinTableResult JoinTable(int tableId, string playerName)
    {
        lock (_gate)
        {
            TableRoom? room = _tables.FirstOrDefault(t => t.TableId == tableId);
            if (room == null)
            {
                return new JoinTableResult(false, false, null);
            }

            int existingIndex = room.Players.FindIndex(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            bool alreadyInTable = existingIndex >= 0;
            if (!alreadyInTable && room.Players.Count >= MaxPlayersPerTable)
            {
                return new JoinTableResult(true, false, ToDto(room));
            }

            if (!alreadyInTable)
            {
                room.Players.Add(new PlayerSlot(playerName, false));
            }

            if (room.Phase != TablePhase.WaitingReady && room.Phase != TablePhase.Bidding)
            {
                ResetRound(room);
            }

            return new JoinTableResult(true, true, ToDto(room));
        }
    }

    public ReadyResult SetReady(int tableId, string playerName, bool ready)
    {
        lock (_gate)
        {
            TableRoom? room = _tables.FirstOrDefault(t => t.TableId == tableId);
            if (room == null)
            {
                return new ReadyResult(false, false, false, null);
            }

            int index = room.Players.FindIndex(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return new ReadyResult(true, false, false, ToStateDto(room));
            }

            room.Players[index].IsReady = ready;

            if (ready && room.Phase == TablePhase.WaitingReady && room.Players.Count == MaxPlayersPerTable && room.Players.All(p => p.IsReady))
            {
                StartBidding(room);
            }

            return new ReadyResult(true, true, true, ToStateDto(room));
        }
    }

    public BidResult SubmitBid(int tableId, string playerName, bool callLandlord)
    {
        lock (_gate)
        {
            TableRoom? room = _tables.FirstOrDefault(t => t.TableId == tableId);
            if (room == null)
            {
                return new BidResult(false, false, false, "table not found", null);
            }

            int playerIndex = room.Players.FindIndex(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            if (playerIndex < 0)
            {
                return new BidResult(true, false, false, "player not in table", ToStateDto(room));
            }

            if (room.Phase != TablePhase.Bidding)
            {
                return new BidResult(true, true, false, "table is not in bidding phase", ToStateDto(room));
            }

            if (room.CurrentBidderIndex != playerIndex)
            {
                return new BidResult(true, true, false, "not your turn", ToStateDto(room));
            }

            if (room.BidSlots[playerIndex].HasBid)
            {
                return new BidResult(true, true, false, "player has already bid", ToStateDto(room));
            }

            room.BidSlots[playerIndex] = new BidSlot(true, callLandlord);
            room.BidHistory.Add(new BidAction(playerIndex, room.Players[playerIndex].Name, callLandlord));
            if (callLandlord)
            {
                room.LandlordIndex = playerIndex;
            }

            room.BidsTaken++;

            if (room.BidsTaken >= MaxPlayersPerTable)
            {
                if (room.LandlordIndex >= 0)
                {
                    room.Phase = TablePhase.Playing;
                }
                else
                {
                    ResetRound(room);
                }
            }
            else
            {
                room.CurrentBidderIndex = FindNextBidder(room, playerIndex);
            }

            return new BidResult(true, true, true, null, ToStateDto(room));
        }
    }

    private static void StartBidding(TableRoom room)
    {
        room.Phase = TablePhase.Bidding;
        room.BidSlots = [new BidSlot(false, false), new BidSlot(false, false), new BidSlot(false, false)];
        room.BidsTaken = 0;
        room.LandlordIndex = -1;
        room.BidHistory.Clear();
        room.CurrentBidderIndex = 0;
    }

    private static int FindNextBidder(TableRoom room, int current)
    {
        for (int i = 1; i <= MaxPlayersPerTable; i++)
        {
            int idx = (current + i) % MaxPlayersPerTable;
            if (!room.BidSlots[idx].HasBid)
            {
                return idx;
            }
        }

        return current;
    }

    private static void ResetRound(TableRoom room)
    {
        room.Phase = TablePhase.WaitingReady;
        room.CurrentBidderIndex = -1;
        room.LandlordIndex = -1;
        room.BidsTaken = 0;
        room.BidHistory.Clear();
        room.BidSlots = [new BidSlot(false, false), new BidSlot(false, false), new BidSlot(false, false)];
        for (int i = 0; i < room.Players.Count; i++)
        {
            room.Players[i].IsReady = false;
        }
    }

    private static TableDto ToDto(TableRoom room)
    {
        return new TableDto(room.TableId, room.Players.Select(p => p.Name).ToArray(), room.Players.Count, MaxPlayersPerTable);
    }

    private static TableStateDto ToStateDto(TableRoom room)
    {
        string[] players = room.Players.Select(p => p.Name).ToArray();
        bool[] readyStates = room.Players.Select(p => p.IsReady).ToArray();
        bool?[] bidChoices = room.BidSlots.Select(slot => slot.HasBid ? (bool?)slot.CallLandlord : null).ToArray();
        string? currentBidder = room.CurrentBidderIndex >= 0 && room.CurrentBidderIndex < room.Players.Count
            ? room.Players[room.CurrentBidderIndex].Name
            : null;
        string? landlord = room.LandlordIndex >= 0 && room.LandlordIndex < room.Players.Count
            ? room.Players[room.LandlordIndex].Name
            : null;

        BidActionDto[] history = room.BidHistory
            .Select(h => new BidActionDto(h.PlayerIndex, h.PlayerName, h.CallLandlord))
            .ToArray();

        return new TableStateDto(
            room.TableId,
            MaxPlayersPerTable,
            room.Phase,
            players,
            readyStates,
            currentBidder,
            landlord,
            bidChoices,
            history);
    }

    private sealed class TableRoom
    {
        public TableRoom(int tableId)
        {
            TableId = tableId;
            Players = [];
            BidSlots = [new BidSlot(false, false), new BidSlot(false, false), new BidSlot(false, false)];
            BidHistory = [];
        }

        public int TableId { get; }

        public List<PlayerSlot> Players { get; }

        public TablePhase Phase { get; set; } = TablePhase.WaitingReady;

        public int CurrentBidderIndex { get; set; } = -1;

        public int LandlordIndex { get; set; } = -1;

        public int BidsTaken { get; set; }

        public BidSlot[] BidSlots { get; set; }

        public List<BidAction> BidHistory { get; }
    }

    private sealed record PlayerSlot(string Name, bool IsReady)
    {
        public string Name { get; } = Name;
        public bool IsReady { get; set; } = IsReady;
    }

    private readonly record struct BidSlot(bool HasBid, bool CallLandlord);

    private readonly record struct BidAction(int PlayerIndex, string PlayerName, bool CallLandlord);
}

public enum TablePhase
{
    WaitingReady = 0,
    Bidding = 1,
    Playing = 2
}

public sealed record TableStateDto(
    int TableId,
    int Capacity,
    TablePhase Phase,
    string[] Players,
    bool[] ReadyStates,
    string? CurrentBidder,
    string? Landlord,
    bool?[] BidChoices,
    BidActionDto[] BidHistory);

public sealed record BidActionDto(int PlayerIndex, string PlayerName, bool CallLandlord);

public sealed record TableStateResult(bool Exists, TableStateDto? State);

public sealed record ReadyRequest(string PlayerName, bool Ready);

public sealed record ReadyResult(bool Exists, bool PlayerInTable, bool Success, TableStateDto? State);

public sealed record BidRequest(string PlayerName, bool CallLandlord);

public sealed record BidResult(bool Exists, bool PlayerInTable, bool Success, string? Error, TableStateDto? State);

public sealed record JoinTableRequest(string PlayerName);

public sealed record JoinTableResult(bool Exists, bool Success, TableDto? Table);

public sealed record TableDto(int TableId, string[] Players, int PlayerCount, int Capacity);

public sealed record TableListResponse(IReadOnlyList<TableDto> Tables);

public sealed record ErrorResponse(string Error);

