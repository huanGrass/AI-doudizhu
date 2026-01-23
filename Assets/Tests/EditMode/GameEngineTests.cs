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
        public void BiddingAssignsLandlordAndAddsBottomCards()
        {
            GameEngine engine = new GameEngine(new AutoGameStrategy(), 7);
            int safety = 0;

            while (engine.Phase == GamePhase.Bidding)
            {
                engine.Step();
                safety++;
                if (safety > 10)
                {
                    Assert.Fail("Bidding did not finish quickly.");
                }
            }

            int landlord = engine.LandlordIndex;
            Assert.IsTrue(landlord >= 0 && landlord < 3);
            Assert.AreEqual(PlayerRole.Landlord, engine.Players[landlord].Role);
            Assert.AreEqual(20, engine.Players[landlord].Hand.Count);
        }

        [Test]
        public void AutoGameFinishesWithWinner()
        {
            GameEngine engine = new GameEngine(new AutoGameStrategy(), 42);
            int safety = 0;

            while (engine.Phase != GamePhase.Finished)
            {
                engine.Step();
                safety++;
                if (safety > 800)
                {
                    Assert.Fail("Game did not finish within 800 steps.");
                }
            }

            Assert.IsTrue(engine.CurrentPlayer >= 0 && engine.CurrentPlayer < 3);
        }
    }
}
