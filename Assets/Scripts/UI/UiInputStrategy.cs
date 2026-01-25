using System.Collections.Generic;
using Doudizhu.Game;

namespace Doudizhu.UI
{
    public sealed class UiInputStrategy : IGameStrategy
    {
        private readonly int _localPlayerIndex;
        private readonly IGameStrategy _autoStrategy;
        private int? _pendingBid;
        private PlayAction? _pendingPlay;

        public UiInputStrategy(int localPlayerIndex, IGameStrategy autoStrategy)
        {
            _localPlayerIndex = localPlayerIndex;
            _autoStrategy = autoStrategy;
        }

        public bool HasPendingBid => _pendingBid.HasValue;
        public bool HasPendingPlay => _pendingPlay.HasValue;

        public void SetBid(int bid)
        {
            _pendingBid = bid;
        }

        public void SetPlay(PlayAction action)
        {
            _pendingPlay = action;
        }

        public int ChooseBid(PlayerState player, int currentHigh)
        {
            if (IsLocal(player))
            {
                int bid = _pendingBid ?? 0;
                _pendingBid = null;
                return bid;
            }

            return _autoStrategy.ChooseBid(player, currentHigh);
        }

        public PlayAction ChoosePlay(PlayerState player, PlayAction? lastPlay)
        {
            if (IsLocal(player))
            {
                PlayAction action = _pendingPlay ?? PlayAction.Pass();
                _pendingPlay = null;
                return action;
            }

            return _autoStrategy.ChoosePlay(player, lastPlay);
        }

        public PlayAction BuildAutoPlay(PlayerState player, PlayAction? lastPlay)
        {
            return PlayRules.FindAutoPlay(player.Hand, lastPlay);
        }

        private bool IsLocal(PlayerState player)
        {
            return player.Name == $"Player{_localPlayerIndex + 1}";
        }
    }
}
