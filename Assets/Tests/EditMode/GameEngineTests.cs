using System.Collections.Generic;
using NUnit.Framework;

namespace Doudizhu.Game.Tests
{
    public class GameEngineTests
    {
        [Test]
        public void DeckBuilds54UniqueCards()
        {
            Deck deck = new Deck();
            HashSet<string> seen = new HashSet<string>();

            foreach (Card card in deck.Cards)
            {
                Assert.IsTrue(seen.Add(card.ToString()), $"Duplicate card: {card}");
            }

            Assert.AreEqual(54, deck.Cards.Count);
        }

        [Test]
        public void DealGives17EachAnd3Bottom()
        {
            Deck deck = new Deck();
            DealResult deal = deck.Deal(new System.Random(1));

            Assert.AreEqual(17, deal.Player0.Count);
            Assert.AreEqual(17, deal.Player1.Count);
            Assert.AreEqual(17, deal.Player2.Count);
            Assert.AreEqual(3, deal.Bottom.Count);
        }

        [Test]
        public void AutoGameFinishesWithWinner()
        {
            GameEngine engine = new GameEngine(new AutoSingleStrategy(), 42);
            int winner;
            PlayAction action;
            int safety = 0;

            while (!engine.Step(out winner, out action))
            {
                safety++;
                if (safety > 500)
                {
                    Assert.Fail("Game did not finish within 500 steps.");
                }
            }

            Assert.IsTrue(winner >= 0 && winner < 3);
        }
    }
}
