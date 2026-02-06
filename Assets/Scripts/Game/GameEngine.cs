using System;
using System.Collections.Generic;

namespace Doudizhu.Game
{
    public sealed class GameEngine
    {
        private readonly List<PlayerState> _players = new List<PlayerState>(3);
        private readonly IGameStrategy _strategy;
        private readonly Random _rng;
        private readonly int _maxRedeal;

        private GamePhase _phase;
        private PlayAction? _lastPlay;
        private int _lastPlayer;
        private int _currentPlayer;
        private int _passCount;

        private int _bidHighScore;
        private int _bidHighPlayer;
        private int _bidsMade;
        private BidStage _bidStage;
        private int _callPlayer;
        private int _callCount;
        private int _robCount;
        private int _redealCount;
        private List<Card> _bottomCards = new List<Card>(3);

        public GameEngine(IGameStrategy strategy, int seed, int maxRedeal = 3)
        {
            _strategy = strategy;
            _rng = new Random(seed);
            _maxRedeal = maxRedeal;
            Setup();
        }

        public IReadOnlyList<PlayerState> Players => _players;
        public int CurrentPlayer => _currentPlayer;
        public GamePhase Phase => _phase;
        public int LandlordIndex => _bidHighPlayer;
        public PlayAction? LastPlay => _lastPlay;
        public IReadOnlyList<Card> BottomCards => _bottomCards;
        public BidStage BidStage => _bidStage;

        private void Setup()
        {
            _players.Clear();
            for (int i = 0; i < 3; i++)
            {
                _players.Add(new PlayerState($"Player{i + 1}"));
            }

            Redeal();
        }

        private void Redeal()
        {
            foreach (PlayerState player in _players)
            {
                player.Hand.Clear();
                player.Role = PlayerRole.Unknown;
            }

            Deck deck = new Deck();
            DealResult deal = deck.Deal(_rng);

            _players[0].Hand.AddRange(deal.Player0);
            _players[1].Hand.AddRange(deal.Player1);
            _players[2].Hand.AddRange(deal.Player2);
            _bottomCards = deal.Bottom;
            SortHands();

            _phase = GamePhase.Bidding;
            _currentPlayer = 0;
            _bidsMade = 0;
            _bidHighScore = 0;
            _bidHighPlayer = -1;
            _bidStage = BidStage.Call;
            _callPlayer = -1;
            _callCount = 0;
            _robCount = 0;
            _lastPlay = null;
            _lastPlayer = -1;
            _passCount = 0;
        }

        public StepResult Step()
        {
            if (_phase == GamePhase.Finished)
            {
                return new StepResult(GamePhase.Finished, StepKind.Finish, _currentPlayer, 0, PlayAction.Pass(), _bidHighPlayer);
            }

            if (_phase == GamePhase.Bidding)
            {
                PlayerState player = _players[_currentPlayer];
                int currentHigh = _bidStage == BidStage.Call ? (_callPlayer >= 0 ? 1 : 0) : (_robCount > 0 ? 1 : 0);
                int bid = _strategy.ChooseBid(player, currentHigh);
                bid = Math.Clamp(bid, 0, 1);

                StepResult result = new StepResult(GamePhase.Bidding, StepKind.Bid, _currentPlayer, bid, PlayAction.Pass(), -1);
                if (_bidStage == BidStage.Call)
                {
                    _bidsMade++;
                    if (bid > 0 && _callPlayer < 0)
                    {
                        _callPlayer = _currentPlayer;
                        _bidHighPlayer = _currentPlayer;
                    }
                    if (bid > 0)
                    {
                        _callCount++;
                    }

                    if (_bidsMade >= 3)
                    {
                        if (_callPlayer < 0)
                        {
                            _redealCount++;
                            if (_redealCount > _maxRedeal)
                            {
                                _bidHighPlayer = 0;
                                EnterPlaying();
                                return new StepResult(GamePhase.Playing, StepKind.Redeal, _bidHighPlayer, 1, PlayAction.Pass(), -1);
                            }

                            Redeal();
                            return new StepResult(GamePhase.Bidding, StepKind.Redeal, -1, 0, PlayAction.Pass(), -1);
                        }

                        // Only one player called landlord: lock immediately, no rob stage.
                        if (_callCount == 1)
                        {
                            _bidHighPlayer = _callPlayer;
                            EnterPlaying();
                            return new StepResult(GamePhase.Playing, StepKind.Bid, _currentPlayer, bid, PlayAction.Pass(), -1);
                        }

                        _bidStage = BidStage.Rob;
                        _bidsMade = 0;
                        _robCount = 0;
                        _currentPlayer = (_callPlayer + 1) % _players.Count;
                        return result;
                    }

                    _currentPlayer = (_currentPlayer + 1) % _players.Count;
                    return result;
                }

                _bidsMade++;
                if (bid > 0)
                {
                    _bidHighPlayer = _currentPlayer;
                    _robCount++;
                }

                if (_bidsMade >= 2)
                {
                    EnterPlaying();
                    return new StepResult(GamePhase.Playing, StepKind.Bid, result.PlayerIndex, result.BidScore, PlayAction.Pass(), -1);
                }

                _currentPlayer = (_currentPlayer + 1) % _players.Count;
                return result;
            }

            return StepPlay();
        }

        private void EnterPlaying()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                _players[i].Role = i == _bidHighPlayer ? PlayerRole.Landlord : PlayerRole.Farmer;
            }

            if (_bidHighPlayer < 0)
            {
                _bidHighPlayer = 0;
            }

            _players[_bidHighPlayer].Hand.AddRange(_bottomCards);
            _players[_bidHighPlayer].Hand.Sort();
            _phase = GamePhase.Playing;
            _currentPlayer = _bidHighPlayer;
            _lastPlay = null;
            _lastPlayer = -1;
            _passCount = 0;
        }

        private StepResult StepPlay()
        {
            int actingPlayer = _currentPlayer;
            PlayerState player = _players[actingPlayer];
            PlayAction action = _strategy.ChoosePlay(player, _lastPlay);
            action = EnsureLegalPlay(player, action, _lastPlay);

            int winnerIndex = -1;
            StepKind kind = action.Type == PlayType.Pass ? StepKind.Pass : StepKind.Play;

            if (action.Type == PlayType.Pass)
            {
                _passCount++;
            }
            else
            {
                _passCount = 0;
                ApplyPlay(player, action);
                _lastPlay = action;
                _lastPlayer = actingPlayer;

                if (player.Hand.Count == 0)
                {
                    _phase = GamePhase.Finished;
                    winnerIndex = actingPlayer;
                    return new StepResult(GamePhase.Finished, StepKind.Finish, actingPlayer, 0, action, winnerIndex);
                }
            }

            int nextPlayer = (_currentPlayer + 1) % _players.Count;
            if (_passCount >= 2 && _lastPlayer >= 0)
            {
                _passCount = 0;
                _lastPlay = null;
                nextPlayer = _lastPlayer;
            }

            _currentPlayer = nextPlayer;
            return new StepResult(_phase, kind, actingPlayer, 0, action, winnerIndex);
        }

        private static PlayAction EnsureLegalPlay(PlayerState player, PlayAction action, PlayAction? lastPlay)
        {
            if (player.Hand.Count == 0)
            {
                return PlayAction.Pass();
            }

            if (action.Type == PlayType.Pass)
            {
                return action;
            }

            if (!PlayRules.CardsExistInHand(player.Hand, action.Cards))
            {
                return PlayRules.FindAutoPlay(player.Hand, lastPlay);
            }

            if (PlayRules.TryEvaluate(action.Cards, out PlayPattern currentPattern))
            {
                if (lastPlay == null || lastPlay.Value.Type == PlayType.Pass)
                {
                    return PlayAction.FromCards(new List<Card>(action.Cards));
                }

                if (PlayRules.TryEvaluate(lastPlay.Value.Cards, out PlayPattern lastPattern)
                    && PlayRules.CanBeat(currentPattern, lastPattern))
                {
                    return PlayAction.FromCards(new List<Card>(action.Cards));
                }
            }

            return PlayRules.FindAutoPlay(player.Hand, lastPlay);
        }

        private static void ApplyPlay(PlayerState player, PlayAction action)
        {
            foreach (Card card in action.Cards)
            {
                player.Hand.Remove(card);
            }
        }

        private void SortHands()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                _players[i].Hand.Sort();
            }
        }
    }

    public enum GamePhase
    {
        Bidding,
        Playing,
        Finished
    }

    public enum BidStage
    {
        Call,
        Rob
    }

    public enum StepKind
    {
        Bid,
        Play,
        Pass,
        Redeal,
        Finish
    }

    public readonly struct StepResult
    {
        public GamePhase Phase { get; }
        public StepKind Kind { get; }
        public int PlayerIndex { get; }
        public int BidScore { get; }
        public PlayAction Play { get; }
        public int WinnerIndex { get; }

        public StepResult(GamePhase phase, StepKind kind, int playerIndex, int bidScore, PlayAction play, int winnerIndex)
        {
            Phase = phase;
            Kind = kind;
            PlayerIndex = playerIndex;
            BidScore = bidScore;
            Play = play;
            WinnerIndex = winnerIndex;
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
            Role = PlayerRole.Unknown;
        }
    }

    public interface IGameStrategy
    {
        int ChooseBid(PlayerState player, int currentHigh);
        PlayAction ChoosePlay(PlayerState player, PlayAction? lastPlay);
    }

    public sealed class AutoGameStrategy : IGameStrategy
    {
        public int ChooseBid(PlayerState player, int currentHigh)
        {
            if (currentHigh >= 1)
            {
                return 0;
            }

            return 1;
        }

        public PlayAction ChoosePlay(PlayerState player, PlayAction? lastPlay)
        {
            return PlayRules.FindAutoPlay(player.Hand, lastPlay);
        }
    }
}

