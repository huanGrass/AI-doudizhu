using System;

namespace Doudizhu.Server.Game
{
    public enum CardSuit
    {
        Spade,
        Heart,
        Club,
        Diamond,
        Joker
    }

    public enum CardRank
    {
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14,
        Two = 15,
        JokerSmall = 16,
        JokerBig = 17
    }

    [Serializable]
    public readonly struct Card : IComparable<Card>
    {
        public CardSuit Suit { get; }
        public CardRank Rank { get; }

        public Card(CardSuit suit, CardRank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public int CompareTo(Card other)
        {
            return RankValue(Rank).CompareTo(RankValue(other.Rank));
        }

        public static int RankValue(CardRank rank)
        {
            return rank switch
            {
                CardRank.Three => 3,
                CardRank.Four => 4,
                CardRank.Five => 5,
                CardRank.Six => 6,
                CardRank.Seven => 7,
                CardRank.Eight => 8,
                CardRank.Nine => 9,
                CardRank.Ten => 10,
                CardRank.Jack => 11,
                CardRank.Queen => 12,
                CardRank.King => 13,
                CardRank.Ace => 14,
                CardRank.Two => 15,
                CardRank.JokerSmall => 16,
                CardRank.JokerBig => 17,
                _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, null)
            };
        }

        public override string ToString()
        {
            return $"{Rank}-{Suit}";
        }
    }
}

