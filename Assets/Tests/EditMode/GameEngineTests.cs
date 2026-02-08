using System.Collections.Generic;
using NUnit.Framework;

#if UNITY_INCLUDE_TESTS
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

        [Test]
        public void PlayRulesEvaluateAndCompare()
        {
            List<Card> pair = new List<Card>
            {
                new Card(CardSuit.Spade, CardRank.Four),
                new Card(CardSuit.Heart, CardRank.Four)
            };
            List<Card> higherPair = new List<Card>
            {
                new Card(CardSuit.Spade, CardRank.Six),
                new Card(CardSuit.Heart, CardRank.Six)
            };
            List<Card> straight = new List<Card>
            {
                new Card(CardSuit.Spade, CardRank.Three),
                new Card(CardSuit.Heart, CardRank.Four),
                new Card(CardSuit.Club, CardRank.Five),
                new Card(CardSuit.Diamond, CardRank.Six),
                new Card(CardSuit.Spade, CardRank.Seven)
            };
            List<Card> bomb = new List<Card>
            {
                new Card(CardSuit.Spade, CardRank.Nine),
                new Card(CardSuit.Heart, CardRank.Nine),
                new Card(CardSuit.Club, CardRank.Nine),
                new Card(CardSuit.Diamond, CardRank.Nine)
            };
            List<Card> rocket = new List<Card>
            {
                new Card(CardSuit.Joker, CardRank.JokerSmall),
                new Card(CardSuit.Joker, CardRank.JokerBig)
            };

            Assert.IsTrue(PlayRules.TryEvaluate(pair, out PlayPattern pairPattern));
            Assert.IsTrue(PlayRules.TryEvaluate(higherPair, out PlayPattern higherPairPattern));
            Assert.IsTrue(PlayRules.TryEvaluate(straight, out PlayPattern straightPattern));
            Assert.IsTrue(PlayRules.TryEvaluate(bomb, out PlayPattern bombPattern));
            Assert.IsTrue(PlayRules.TryEvaluate(rocket, out PlayPattern rocketPattern));

            Assert.IsTrue(PlayRules.CanBeat(higherPairPattern, pairPattern));
            Assert.IsFalse(PlayRules.CanBeat(pairPattern, higherPairPattern));
            Assert.IsTrue(PlayRules.CanBeat(bombPattern, straightPattern));
            Assert.IsTrue(PlayRules.CanBeat(rocketPattern, bombPattern));
        }

        [Test]
        public void AutoPlayLeadsWithPairWhenAvailable()
        {
            List<Card> hand = new List<Card>
            {
                new Card(CardSuit.Spade, CardRank.Five),
                new Card(CardSuit.Heart, CardRank.Five),
                new Card(CardSuit.Club, CardRank.Seven)
            };

            PlayAction action = PlayRules.FindAutoPlay(hand, null);
            Assert.AreEqual(PlayType.Pair, action.Type);
            Assert.AreEqual(2, action.Cards.Count);
        }

        [Test]
        public void CallThenRobSelectsLastRobberAsLandlord()
        {
            Queue<int> bids = new Queue<int>(new[] { 1, 0, 0, 1, 0 });
            GameEngine engine = new GameEngine(new ScriptedBidStrategy(bids), 3);

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

            Assert.AreEqual(GamePhase.Playing, engine.Phase);
            Assert.AreEqual(1, engine.LandlordIndex);
            Assert.AreEqual(20, engine.Players[engine.LandlordIndex].Hand.Count);
        }

        private sealed class ScriptedBidStrategy : IGameStrategy
        {
            private readonly Queue<int> _bids;

            public ScriptedBidStrategy(Queue<int> bids)
            {
                _bids = bids;
            }

            public int ChooseBid(PlayerState player, int currentHigh)
            {
                if (_bids.Count == 0)
                {
                    return 0;
                }

                return _bids.Dequeue();
            }

            public PlayAction ChoosePlay(PlayerState player, PlayAction? lastPlay)
            {
                return PlayAction.Pass();
            }
        }
    }
}
#endif
