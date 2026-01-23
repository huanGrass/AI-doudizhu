using System;
using System.Collections.Generic;

namespace Doudizhu.Game
{
    public sealed class GameEngine
    {
        private readonly List<PlayerState> _players = new List<PlayerState>(3);
        private readonly IPlayerStrategy _strategy;
        private PlayAction? _lastPlay;
        private int _lastPlayer;
        private int _currentPlayer;
        private int _passCount;

        public GameEngine(IPlayerStrategy strategy, int seed)
        {
            _strategy = strategy;
            Setup(seed);
        }

        public IReadOnlyList<PlayerState> Players => _players;
        public int CurrentPlayer => _currentPlayer;
        public PlayAction? LastPlay => _lastPlay;

        private void Setup(int seed)
        {
            _players.Clear();
            for (int i = 0; i < 3; i++)
            {
                _players.Add(new PlayerState($"Player{i + 1}"));
            }

            Random rng = new Random(seed);
            Deck deck = new Deck();
            DealResult deal = deck.Deal(rng);

            _players[0].Hand.AddRange(deal.Player0);
            _players[1].Hand.AddRange(deal.Player1);
            _players[2].Hand.AddRange(deal.Player2);

            // Minimal landlord selection: Player1 becomes landlord and gets bottom cards.
            _players[0].Role = PlayerRole.Landlord;
            _players[0].Hand.AddRange(deal.Bottom);

            _currentPlayer = 0;
            _lastPlayer = -1;
            _passCount = 0;
            _lastPlay = null;
        }

        public bool Step(out int winnerIndex, out PlayAction action)
        {
            winnerIndex = -1;
            PlayerState player = _players[_currentPlayer];
            action = _strategy.ChoosePlay(player, _lastPlay);

            if (action.Type == PlayType.Pass)
            {
                _passCount++;
                if (_passCount >= 2)
                {
                    _lastPlay = null;
                    _passCount = 0;
                }
            }
            else
            {
                _passCount = 0;
                ApplyPlay(player, action);
                _lastPlay = action;
                _lastPlayer = _currentPlayer;

                if (player.Hand.Count == 0)
                {
                    winnerIndex = _currentPlayer;
                    return true;
                }
            }

            _currentPlayer = (_currentPlayer + 1) % _players.Count;
            return false;
        }

        private static void ApplyPlay(PlayerState player, PlayAction action)
        {
            foreach (Card card in action.Cards)
            {
                player.Hand.Remove(card);
            }
        }
    }

    public enum PlayerRole
    {
        Unknown,
        Landlord,
        Farmer
    }

    public sealed class PlayerState
    {
        public string Name { get; }
        public PlayerRole Role { get; set; }
        public List<Card> Hand { get; } = new List<Card>(20);

        public PlayerState(string name)
        {
            Name = name;
            Role = PlayerRole.Farmer;
        }
    }

    public interface IPlayerStrategy
    {
        PlayAction ChoosePlay(PlayerState player, PlayAction? lastPlay);
    }

    public sealed class AutoSingleStrategy : IPlayerStrategy
    {
        public PlayAction ChoosePlay(PlayerState player, PlayAction? lastPlay)
        {
            player.Hand.Sort();

            if (player.Hand.Count == 0)
            {
                return PlayAction.Pass();
            }

            if (lastPlay == null || lastPlay.Value.Type == PlayType.Pass)
            {
                return PlayAction.Single(player.Hand[0]);
            }

            Card target = lastPlay.Value.Cards[0];
            foreach (Card card in player.Hand)
            {
                if (card.CompareTo(target) > 0)
                {
                    return PlayAction.Single(card);
                }
            }

            return PlayAction.Pass();
        }
    }
}
