using System.Collections.Generic;

namespace Doudizhu.Game
{
    public enum PlayType
    {
        Pass,
        Single
    }

    public readonly struct PlayAction
    {
        public PlayType Type { get; }
        public List<Card> Cards { get; }

        public PlayAction(PlayType type, List<Card> cards)
        {
            Type = type;
            Cards = cards;
        }

        public static PlayAction Pass()
        {
            return new PlayAction(PlayType.Pass, new List<Card>());
        }

        public static PlayAction Single(Card card)
        {
            return new PlayAction(PlayType.Single, new List<Card> { card });
        }
    }
}
