using Doudizhu.Server.Game;

namespace Doudizhu.Server;

public sealed class TableRoomService
{
    private const int MaxPlayersPerTable = 3;

    private readonly object _gate = new();
    private readonly List<TableRoom> _tables = [new(1), new(2), new(3), new(4)];
    private readonly Random _rng = new();

    public IReadOnlyList<TableDto> GetTables()
    {
        lock (_gate)
        {
            return _tables.Select(ToDto).ToArray();
        }
    }

    public TableStateResult GetTableState(int tableId, string? forPlayer)
    {
        lock (_gate)
        {
            TableRoom? room = _tables.FirstOrDefault(t => t.TableId == tableId);
            if (room == null)
            {
                return new TableStateResult(false, null);
            }

            return new TableStateResult(true, ToStateDto(room, forPlayer));
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
                room.Players.Add(new PlayerSlot(playerName));
            }

            if (room.Phase == TablePhase.Playing || room.Phase == TablePhase.Finished)
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
                return new ReadyResult(true, false, false, ToStateDto(room, playerName));
            }

            room.Players[index].IsReady = ready;

            if (room.Phase == TablePhase.WaitingReady && room.Players.Count == MaxPlayersPerTable && room.Players.All(p => p.IsReady))
            {
                StartBidding(room);
            }

            return new ReadyResult(true, true, true, ToStateDto(room, playerName));
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
                return new BidResult(true, false, false, "player not in table", ToStateDto(room, playerName));
            }

            if (room.Phase != TablePhase.Bidding)
            {
                return new BidResult(true, true, false, "table is not in bidding phase", ToStateDto(room, playerName));
            }

            if (room.CurrentBidderIndex != playerIndex)
            {
                return new BidResult(true, true, false, "not your turn", ToStateDto(room, playerName));
            }

            if (room.BidSlots[playerIndex].HasBid)
            {
                return new BidResult(true, true, false, "player has already bid", ToStateDto(room, playerName));
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
                    EnterPlaying(room);
                }
                else
                {
                    ResetRound(room);
                }
            }
            else
            {
                room.CurrentBidderIndex = (playerIndex + 1) % MaxPlayersPerTable;
            }

            return new BidResult(true, true, true, null, ToStateDto(room, playerName));
        }
    }

    public PlayResult SubmitPlay(int tableId, string playerName, bool pass, string[]? cards)
    {
        lock (_gate)
        {
            TableRoom? room = _tables.FirstOrDefault(t => t.TableId == tableId);
            if (room == null)
            {
                return new PlayResult(false, false, false, "table not found", null);
            }

            int playerIndex = room.Players.FindIndex(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            if (playerIndex < 0)
            {
                return new PlayResult(true, false, false, "player not in table", ToStateDto(room, playerName));
            }

            if (room.Phase != TablePhase.Playing)
            {
                return new PlayResult(true, true, false, "table is not in playing phase", ToStateDto(room, playerName));
            }

            if (room.CurrentTurnIndex != playerIndex)
            {
                return new PlayResult(true, true, false, "not your turn", ToStateDto(room, playerName));
            }

            if (pass)
            {
                if (room.LastPlayCards.Count == 0)
                {
                    return new PlayResult(true, true, false, "cannot pass on lead", ToStateDto(room, playerName));
                }

                room.PassCount++;
                room.LastActionPlayer = playerIndex;
                room.LastActionWasPass = true;

                if (room.PassCount >= 2 && room.LastPlayPlayerIndex >= 0)
                {
                    room.PassCount = 0;
                    room.CurrentTurnIndex = room.LastPlayPlayerIndex;
                    room.LastPlayCards.Clear();
                    room.LastPlayPlayerIndex = -1;
                }
                else
                {
                    room.CurrentTurnIndex = NextPlayer(playerIndex);
                }

                return new PlayResult(true, true, true, null, ToStateDto(room, playerName));
            }

            if (cards == null || cards.Length == 0)
            {
                return new PlayResult(true, true, false, "cards are required", ToStateDto(room, playerName));
            }

            List<Card> chosenCards = new(cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                if (!TryParseCard(cards[i], out Card parsed))
                {
                    return new PlayResult(true, true, false, "invalid card format", ToStateDto(room, playerName));
                }

                chosenCards.Add(parsed);
            }

            List<Card> hand = room.Players[playerIndex].Hand;
            if (!PlayRules.CardsExistInHand(hand, chosenCards))
            {
                return new PlayResult(true, true, false, "cards not in hand", ToStateDto(room, playerName));
            }

            if (!PlayRules.TryEvaluate(chosenCards, out PlayPattern currentPattern))
            {
                return new PlayResult(true, true, false, "invalid play pattern", ToStateDto(room, playerName));
            }

            if (room.LastPlayCards.Count > 0)
            {
                if (!PlayRules.TryEvaluate(room.LastPlayCards, out PlayPattern lastPattern))
                {
                    return new PlayResult(true, true, false, "internal invalid last play", ToStateDto(room, playerName));
                }

                if (!PlayRules.CanBeat(currentPattern, lastPattern))
                {
                    return new PlayResult(true, true, false, "play cannot beat last hand", ToStateDto(room, playerName));
                }
            }

            for (int i = 0; i < chosenCards.Count; i++)
            {
                hand.Remove(chosenCards[i]);
            }

            room.LastPlayCards = chosenCards;
            room.LastPlayPlayerIndex = playerIndex;
            room.LastActionPlayer = playerIndex;
            room.LastActionWasPass = false;
            room.PassCount = 0;

            if (hand.Count == 0)
            {
                room.WinnerIndex = playerIndex;
                room.Phase = TablePhase.Finished;
                return new PlayResult(true, true, true, null, ToStateDto(room, playerName));
            }

            room.CurrentTurnIndex = NextPlayer(playerIndex);
            return new PlayResult(true, true, true, null, ToStateDto(room, playerName));
        }
    }

    private static int NextPlayer(int idx)
    {
        return (idx + 1) % MaxPlayersPerTable;
    }

    private void StartBidding(TableRoom room)
    {
        DealCards(room);

        room.Phase = TablePhase.Bidding;
        room.BidSlots = [new BidSlot(false, false), new BidSlot(false, false), new BidSlot(false, false)];
        room.BidsTaken = 0;
        room.LandlordIndex = -1;
        room.BidHistory.Clear();
        room.CurrentBidderIndex = 0;
        room.CurrentTurnIndex = -1;
        room.WinnerIndex = -1;
        room.LastPlayCards.Clear();
        room.LastPlayPlayerIndex = -1;
        room.PassCount = 0;
    }

    private void DealCards(TableRoom room)
    {
        foreach (PlayerSlot player in room.Players)
        {
            player.Hand.Clear();
        }

        Deck deck = new();
        DealResult deal = deck.Deal(_rng);

        room.Players[0].Hand.AddRange(deal.Player0);
        room.Players[1].Hand.AddRange(deal.Player1);
        room.Players[2].Hand.AddRange(deal.Player2);
        room.BottomCards = new List<Card>(deal.Bottom);

        for (int i = 0; i < room.Players.Count; i++)
        {
            room.Players[i].Hand.Sort();
        }
    }

    private static void EnterPlaying(TableRoom room)
    {
        if (room.LandlordIndex < 0)
        {
            room.LandlordIndex = 0;
        }

        room.Players[room.LandlordIndex].Hand.AddRange(room.BottomCards);
        room.Players[room.LandlordIndex].Hand.Sort();

        room.Phase = TablePhase.Playing;
        room.CurrentTurnIndex = room.LandlordIndex;
        room.LastPlayCards.Clear();
        room.LastPlayPlayerIndex = -1;
        room.PassCount = 0;
        room.WinnerIndex = -1;
    }

    private static void ResetRound(TableRoom room)
    {
        room.Phase = TablePhase.WaitingReady;
        room.CurrentBidderIndex = -1;
        room.CurrentTurnIndex = -1;
        room.LandlordIndex = -1;
        room.WinnerIndex = -1;
        room.BidsTaken = 0;
        room.BidHistory.Clear();
        room.BidSlots = [new BidSlot(false, false), new BidSlot(false, false), new BidSlot(false, false)];
        room.BottomCards.Clear();
        room.LastPlayCards.Clear();
        room.LastPlayPlayerIndex = -1;
        room.PassCount = 0;
        room.LastActionPlayer = -1;
        room.LastActionWasPass = false;

        for (int i = 0; i < room.Players.Count; i++)
        {
            room.Players[i].IsReady = false;
            room.Players[i].Hand.Clear();
        }
    }

    private static TableDto ToDto(TableRoom room)
    {
        return new TableDto(room.TableId, room.Players.Select(p => p.Name).ToArray(), room.Players.Count, MaxPlayersPerTable);
    }

    private static TableStateDto ToStateDto(TableRoom room, string? forPlayer)
    {
        string[] players = room.Players.Select(p => p.Name).ToArray();
        bool[] readyStates = room.Players.Select(p => p.IsReady).ToArray();
        bool?[] bidChoices = room.BidSlots.Select(slot => slot.HasBid ? (bool?)slot.CallLandlord : null).ToArray();
        int[] handCounts = room.Players.Select(p => p.Hand.Count).ToArray();

        string? currentBidder = room.CurrentBidderIndex >= 0 && room.CurrentBidderIndex < room.Players.Count
            ? room.Players[room.CurrentBidderIndex].Name
            : null;
        string? landlord = room.LandlordIndex >= 0 && room.LandlordIndex < room.Players.Count
            ? room.Players[room.LandlordIndex].Name
            : null;
        string? currentTurn = room.CurrentTurnIndex >= 0 && room.CurrentTurnIndex < room.Players.Count
            ? room.Players[room.CurrentTurnIndex].Name
            : null;
        string? lastPlayPlayer = room.LastPlayPlayerIndex >= 0 && room.LastPlayPlayerIndex < room.Players.Count
            ? room.Players[room.LastPlayPlayerIndex].Name
            : null;
        string? winner = room.WinnerIndex >= 0 && room.WinnerIndex < room.Players.Count
            ? room.Players[room.WinnerIndex].Name
            : null;

        BidActionDto[] history = room.BidHistory
            .Select(h => new BidActionDto(h.PlayerIndex, h.PlayerName, h.CallLandlord))
            .ToArray();

        string[] myHand = [];
        if (!string.IsNullOrWhiteSpace(forPlayer))
        {
            PlayerSlot? player = room.Players.FirstOrDefault(p => p.Name.Equals(forPlayer, StringComparison.OrdinalIgnoreCase));
            if (player != null)
            {
                myHand = player.Hand.Select(SerializeCard).ToArray();
            }
        }

        string[] lastPlayCards = room.LastPlayCards.Select(SerializeCard).ToArray();

        return new TableStateDto(
            room.TableId,
            MaxPlayersPerTable,
            room.Phase,
            players,
            readyStates,
            currentBidder,
            landlord,
            bidChoices,
            history,
            currentTurn,
            handCounts,
            lastPlayCards,
            lastPlayPlayer,
            myHand,
            winner,
            room.LastActionPlayer >= 0 && room.LastActionPlayer < room.Players.Count ? room.Players[room.LastActionPlayer].Name : null,
            room.LastActionWasPass);
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

    private static bool TryParseCard(string? value, out Card card)
    {
        card = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string input = value.Trim().ToUpperInvariant();
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

    private sealed class TableRoom
    {
        public TableRoom(int tableId)
        {
            TableId = tableId;
            Players = [];
            BidSlots = [new BidSlot(false, false), new BidSlot(false, false), new BidSlot(false, false)];
            BidHistory = [];
            BottomCards = [];
            LastPlayCards = [];
        }

        public int TableId { get; }

        public List<PlayerSlot> Players { get; }

        public TablePhase Phase { get; set; } = TablePhase.WaitingReady;

        public int CurrentBidderIndex { get; set; } = -1;

        public int CurrentTurnIndex { get; set; } = -1;

        public int LandlordIndex { get; set; } = -1;

        public int WinnerIndex { get; set; } = -1;

        public int BidsTaken { get; set; }

        public BidSlot[] BidSlots { get; set; }

        public List<BidAction> BidHistory { get; }

        public List<Card> BottomCards { get; set; }

        public List<Card> LastPlayCards { get; set; }

        public int LastPlayPlayerIndex { get; set; } = -1;

        public int LastActionPlayer { get; set; } = -1;

        public bool LastActionWasPass { get; set; }

        public int PassCount { get; set; }
    }

    private sealed class PlayerSlot
    {
        public PlayerSlot(string name)
        {
            Name = name;
            Hand = [];
        }

        public string Name { get; }

        public bool IsReady { get; set; }

        public List<Card> Hand { get; }
    }

    private readonly record struct BidSlot(bool HasBid, bool CallLandlord);

    private readonly record struct BidAction(int PlayerIndex, string PlayerName, bool CallLandlord);
}

public enum TablePhase
{
    WaitingReady = 0,
    Bidding = 1,
    Playing = 2,
    Finished = 3
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
    BidActionDto[] BidHistory,
    string? CurrentTurn,
    int[] HandCounts,
    string[] LastPlayCards,
    string? LastPlayPlayer,
    string[] MyHand,
    string? Winner,
    string? LastActionPlayer,
    bool LastActionWasPass);

public sealed record BidActionDto(int PlayerIndex, string PlayerName, bool CallLandlord);

public sealed record TableStateResult(bool Exists, TableStateDto? State);

public sealed record ReadyRequest(string PlayerName, bool Ready);

public sealed record ReadyResult(bool Exists, bool PlayerInTable, bool Success, TableStateDto? State);

public sealed record BidRequest(string PlayerName, bool CallLandlord);

public sealed record BidResult(bool Exists, bool PlayerInTable, bool Success, string? Error, TableStateDto? State);

public sealed record PlayRequest(string PlayerName, bool Pass, string[]? Cards);

public sealed record PlayResult(bool Exists, bool PlayerInTable, bool Success, string? Error, TableStateDto? State);

public sealed record JoinTableRequest(string PlayerName);

public sealed record JoinTableResult(bool Exists, bool Success, TableDto? Table);

public sealed record TableDto(int TableId, string[] Players, int PlayerCount, int Capacity);

public sealed record TableListResponse(IReadOnlyList<TableDto> Tables);

public sealed record ErrorResponse(string Error);
