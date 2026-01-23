using System;
using System.Collections.Generic;

namespace Doudizhu.Game
{
    public sealed class Deck
    {
        private readonly List<Card> _cards = new List<Card>(54);

        public Deck()
        {
            Build();
        }

        public IReadOnlyList<Card> Cards => _cards;

        private void Build()
        {
            _cards.Clear();
            CardSuit[] suits = { CardSuit.Spade, CardSuit.Heart, CardSuit.Club, CardSuit.Diamond };
            CardRank[] ranks =
            {
                CardRank.Three,
                CardRank.Four,
                CardRank.Five,
                CardRank.Six,
                CardRank.Seven,
                CardRank.Eight,
                CardRank.Nine,
                CardRank.Ten,
                CardRank.Jack,
                CardRank.Queen,
                CardRank.King,
                CardRank.Ace,
                CardRank.Two
            };

            foreach (CardSuit suit in suits)
            {
                foreach (CardRank rank in ranks)
                {
                    _cards.Add(new Card(suit, rank));
                }
            }

            _cards.Add(new Card(CardSuit.Joker, CardRank.JokerSmall));
            _cards.Add(new Card(CardSuit.Joker, CardRank.JokerBig));
        }

        public void Shuffle(Random rng)
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                Card temp = _cards[i];
                _cards[i] = _cards[swap];
                _cards[swap] = temp;
            }
        }

        public DealResult Deal(Random rng)
        {
            Shuffle(rng);

            List<Card> p0 = new List<Card>(17);
            List<Card> p1 = new List<Card>(17);
            List<Card> p2 = new List<Card>(17);
            List<Card> bottom = new List<Card>(3);

            for (int i = 0; i < 51; i++)
            {
                Card card = _cards[i];
                if (i % 3 == 0)
                {
                    p0.Add(card);
                }
                else if (i % 3 == 1)
                {
                    p1.Add(card);
                }
                else
                {
                    p2.Add(card);
                }
            }

            bottom.Add(_cards[51]);
            bottom.Add(_cards[52]);
            bottom.Add(_cards[53]);

            return new DealResult(p0, p1, p2, bottom);
        }
    }

    public readonly struct DealResult
    {
        public readonly List<Card> Player0;
        public readonly List<Card> Player1;
        public readonly List<Card> Player2;
        public readonly List<Card> Bottom;

        public DealResult(List<Card> p0, List<Card> p1, List<Card> p2, List<Card> bottom)
        {
            Player0 = p0;
            Player1 = p1;
            Player2 = p2;
            Bottom = bottom;
        }
    }
}
