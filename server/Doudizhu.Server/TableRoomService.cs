namespace Doudizhu.Server;

public sealed class TableRoomService
{
    private const int MaxPlayersPerTable = 3;

    private readonly object _gate = new();
    private readonly List<TableRoom> _tables =
    [
        new TableRoom(1, []),
        new TableRoom(2, []),
        new TableRoom(3, []),
        new TableRoom(4, [])
    ];

    public IReadOnlyList<TableDto> GetTables()
    {
        lock (_gate)
        {
            return _tables.Select(ToDto).ToArray();
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

            bool alreadyInTable = room.Players.Any(p => p.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            if (!alreadyInTable && room.Players.Count >= MaxPlayersPerTable)
            {
                return new JoinTableResult(true, false, ToDto(room));
            }

            if (!alreadyInTable)
            {
                room.Players.Add(playerName);
            }

            return new JoinTableResult(true, true, ToDto(room));
        }
    }

    private static TableDto ToDto(TableRoom room)
    {
        return new TableDto(room.TableId, room.Players.ToArray(), room.Players.Count, MaxPlayersPerTable);
    }

    private sealed class TableRoom
    {
        public TableRoom(int tableId, IEnumerable<string> players)
        {
            TableId = tableId;
            Players = new List<string>(players);
        }

        public int TableId { get; }

        public List<string> Players { get; }
    }
}

public sealed record TableDto(int TableId, string[] Players, int PlayerCount, int Capacity);

public sealed record TableListResponse(IReadOnlyList<TableDto> Tables);

public sealed record JoinTableRequest(string PlayerName);

public sealed record JoinTableResult(bool Exists, bool Success, TableDto? Table);

