using System.Collections.Generic;

namespace Doudizhu.Game
{
    public readonly struct PlayPattern
    {
        public PlayType Type { get; }
        public int MainRank { get; }
        public int CardsCount { get; }

        public PlayPattern(PlayType type, int mainRank, int cardsCount)
        {
            Type = type;
            MainRank = mainRank;
            CardsCount = cardsCount;
        }
    }

    public static class PlayRules
    {
        private const int MaxStraightRank = 14;

        public static bool TryEvaluate(IReadOnlyList<Card> cards, out PlayPattern pattern)
        {
            pattern = default;
            if (cards == null || cards.Count == 0)
            {
                return false;
            }

            Dictionary<int, int> counts = BuildRankCounts(cards);
            int total = cards.Count;

            if (IsRocket(counts))
            {
                pattern = new PlayPattern(PlayType.Rocket, Card.RankValue(CardRank.JokerBig), total);
                return true;
            }

            if (counts.Count == 1)
            {
                int rank = FirstKey(counts);
                int count = counts[rank];
                if (count == 1)
                {
                    pattern = new PlayPattern(PlayType.Single, rank, total);
                    return true;
                }

                if (count == 2)
                {
                    pattern = new PlayPattern(PlayType.Pair, rank, total);
                    return true;
                }

                if (count == 3)
                {
                    pattern = new PlayPattern(PlayType.Triple, rank, total);
                    return true;
                }

                if (count == 4)
                {
                    pattern = new PlayPattern(PlayType.Bomb, rank, total);
                    return true;
                }
            }

            if (total == 4 && HasCounts(counts, 3, 1))
            {
                int tripleRank = FindRankWithCount(counts, 3);
                pattern = new PlayPattern(PlayType.TripleWithSingle, tripleRank, total);
                return true;
            }

            if (total == 5 && HasCounts(counts, 3, 2))
            {
                int tripleRank = FindRankWithCount(counts, 3);
                pattern = new PlayPattern(PlayType.TripleWithPair, tripleRank, total);
                return true;
            }

            if (total == 6 && HasCounts(counts, 4, 1, 1))
            {
                int fourRank = FindRankWithCount(counts, 4);
                pattern = new PlayPattern(PlayType.FourWithTwoSingles, fourRank, total);
                return true;
            }

            if (total == 8 && HasCounts(counts, 4, 2, 2))
            {
                int fourRank = FindRankWithCount(counts, 4);
                pattern = new PlayPattern(PlayType.FourWithTwoPairs, fourRank, total);
                return true;
            }

            if (IsStraight(counts, total))
            {
                int maxRank = MaxKey(counts);
                pattern = new PlayPattern(PlayType.Straight, maxRank, total);
                return true;
            }

            if (IsStraightPairs(counts, total))
            {
                int maxRank = MaxKey(counts);
                pattern = new PlayPattern(PlayType.StraightPairs, maxRank, total);
                return true;
            }

            if (IsAirplane(counts, total))
            {
                int maxRank = MaxKey(counts);
                pattern = new PlayPattern(PlayType.Airplane, maxRank, total);
                return true;
            }

            if (IsAirplaneWithSingles(counts, total, out int airplaneMain))
            {
                pattern = new PlayPattern(PlayType.AirplaneWithSingles, airplaneMain, total);
                return true;
            }

            if (IsAirplaneWithPairs(counts, total, out airplaneMain))
            {
                pattern = new PlayPattern(PlayType.AirplaneWithPairs, airplaneMain, total);
                return true;
            }

            return false;
        }

        public static bool CanBeat(PlayPattern current, PlayPattern last)
        {
            if (last.Type == PlayType.Pass)
            {
                return true;
            }

            if (current.Type == PlayType.Rocket)
            {
                return true;
            }

            if (last.Type == PlayType.Rocket)
            {
                return false;
            }

            if (current.Type == PlayType.Bomb && last.Type != PlayType.Bomb)
            {
                return true;
            }

            if (current.Type != last.Type)
            {
                return false;
            }

            if (current.CardsCount != last.CardsCount)
            {
                return false;
            }

            return current.MainRank > last.MainRank;
        }

        public static PlayAction FindAutoPlay(List<Card> hand, PlayAction? lastPlay)
        {
            if (hand == null || hand.Count == 0)
            {
                return PlayAction.Pass();
            }

            if (lastPlay == null || lastPlay.Value.Type == PlayType.Pass)
            {
                return FindLeadPlay(hand);
            }

            if (!TryEvaluate(lastPlay.Value.Cards, out PlayPattern lastPattern))
            {
                hand.Sort();
                return PlayAction.FromCards(new List<Card> { hand[0] });
            }

            if (TryFindBetterSameType(hand, lastPattern, out List<Card> cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (lastPattern.Type != PlayType.Bomb && lastPattern.Type != PlayType.Rocket)
            {
                if (TryFindBomb(hand, lastPattern.MainRank, out cards))
                {
                    return PlayAction.FromCards(cards);
                }

                if (TryFindRocket(hand, out cards))
                {
                    return PlayAction.FromCards(cards);
                }
            }

            if (lastPattern.Type == PlayType.Bomb && TryFindBomb(hand, lastPattern.MainRank, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            return PlayAction.Pass();
        }

        private static PlayAction FindLeadPlay(List<Card> hand)
        {
            Dictionary<int, List<Card>> buckets = BuildRankBuckets(hand);
            List<int> ranks = SortedRanks(buckets);

            if (TryFindAirplaneWithPairs(ranks, buckets, 2, 0, out List<Card> cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindAirplaneWithSingles(ranks, buckets, 2, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindAirplane(ranks, buckets, 2, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindStraightPairs(ranks, buckets, 3, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindStraight(ranks, buckets, 5, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindTripleWithPair(ranks, buckets, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindTripleWithSingle(ranks, buckets, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindTriple(ranks, buckets, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindPair(ranks, buckets, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            if (TryFindSingle(ranks, buckets, 0, out cards))
            {
                return PlayAction.FromCards(cards);
            }

            return PlayAction.Pass();
        }

        public static bool CardsExistInHand(IReadOnlyList<Card> hand, IReadOnlyList<Card> cards)
        {
            if (cards == null)
            {
                return false;
            }

            Dictionary<Card, int> counts = new Dictionary<Card, int>();
            for (int i = 0; i < hand.Count; i++)
            {
                Card card = hand[i];
                counts.TryGetValue(card, out int count);
                counts[card] = count + 1;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                if (!counts.TryGetValue(card, out int count) || count == 0)
                {
                    return false;
                }

                counts[card] = count - 1;
            }

            return true;
        }

        private static bool TryFindBetterSameType(List<Card> hand, PlayPattern lastPattern, out List<Card> cards)
        {
            cards = null;
            Dictionary<int, List<Card>> buckets = BuildRankBuckets(hand);
            List<int> ranks = SortedRanks(buckets);

            switch (lastPattern.Type)
            {
                case PlayType.Single:
                    return TryFindSingle(ranks, buckets, lastPattern.MainRank, out cards);
                case PlayType.Pair:
                    return TryFindPair(ranks, buckets, lastPattern.MainRank, out cards);
                case PlayType.Triple:
                    return TryFindTriple(ranks, buckets, lastPattern.MainRank, out cards);
                case PlayType.TripleWithSingle:
                    return TryFindTripleWithSingle(ranks, buckets, lastPattern.MainRank, out cards);
                case PlayType.TripleWithPair:
                    return TryFindTripleWithPair(ranks, buckets, lastPattern.MainRank, out cards);
                case PlayType.FourWithTwoSingles:
                    return TryFindFourWithSingles(ranks, buckets, lastPattern.MainRank, out cards);
                case PlayType.FourWithTwoPairs:
                    return TryFindFourWithPairs(ranks, buckets, lastPattern.MainRank, out cards);
                case PlayType.Straight:
                    return TryFindStraight(ranks, buckets, lastPattern.CardsCount, lastPattern.MainRank, out cards);
                case PlayType.StraightPairs:
                    return TryFindStraightPairs(ranks, buckets, lastPattern.CardsCount / 2, lastPattern.MainRank, out cards);
                case PlayType.Airplane:
                    return TryFindAirplane(ranks, buckets, lastPattern.CardsCount / 3, lastPattern.MainRank, out cards);
                case PlayType.AirplaneWithSingles:
                    return TryFindAirplaneWithSingles(ranks, buckets, lastPattern.CardsCount / 4, lastPattern.MainRank, out cards);
                case PlayType.AirplaneWithPairs:
                    return TryFindAirplaneWithPairs(ranks, buckets, lastPattern.CardsCount / 5, lastPattern.MainRank, out cards);
                default:
                    return false;
            }
        }

        private static bool TryFindRocket(List<Card> hand, out List<Card> cards)
        {
            cards = null;
            Card? small = null;
            Card? big = null;
            for (int i = 0; i < hand.Count; i++)
            {
                Card card = hand[i];
                if (card.Rank == CardRank.JokerSmall)
                {
                    small = card;
                }
                else if (card.Rank == CardRank.JokerBig)
                {
                    big = card;
                }
            }

            if (small.HasValue && big.HasValue)
            {
                cards = new List<Card> { small.Value, big.Value };
                return true;
            }

            return false;
        }

        private static bool TryFindBomb(List<Card> hand, int minRank, out List<Card> cards)
        {
            cards = null;
            Dictionary<int, List<Card>> buckets = BuildRankBuckets(hand);
            List<int> ranks = SortedRanks(buckets);
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (rank <= minRank)
                {
                    continue;
                }

                if (buckets[rank].Count == 4)
                {
                    cards = new List<Card>(buckets[rank]);
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindSingle(List<int> ranks, Dictionary<int, List<Card>> buckets, int minRank, out List<Card> cards)
        {
            cards = null;
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (rank > minRank)
                {
                    cards = new List<Card> { buckets[rank][0] };
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindPair(List<int> ranks, Dictionary<int, List<Card>> buckets, int minRank, out List<Card> cards)
        {
            cards = null;
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (rank > minRank && buckets[rank].Count >= 2)
                {
                    cards = new List<Card> { buckets[rank][0], buckets[rank][1] };
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindTriple(List<int> ranks, Dictionary<int, List<Card>> buckets, int minRank, out List<Card> cards)
        {
            cards = null;
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (rank > minRank && buckets[rank].Count >= 3)
                {
                    cards = new List<Card> { buckets[rank][0], buckets[rank][1], buckets[rank][2] };
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindTripleWithSingle(List<int> ranks, Dictionary<int, List<Card>> buckets, int minRank, out List<Card> cards)
        {
            cards = null;
            if (!TryFindTriple(ranks, buckets, minRank, out List<Card> triple))
            {
                return false;
            }

            int tripleRank = Card.RankValue(triple[0].Rank);
            List<Card> singlePool = CollectSingles(ranks, buckets, new HashSet<int> { tripleRank });
            if (singlePool.Count == 0)
            {
                return false;
            }

            cards = new List<Card>(triple) { singlePool[0] };
            return true;
        }

        private static bool TryFindTripleWithPair(List<int> ranks, Dictionary<int, List<Card>> buckets, int minRank, out List<Card> cards)
        {
            cards = null;
            for (int i = 0; i < ranks.Count; i++)
            {
                int tripleRank = ranks[i];
                if (tripleRank <= minRank || buckets[tripleRank].Count < 3)
                {
                    continue;
                }

                for (int j = 0; j < ranks.Count; j++)
                {
                    int pairRank = ranks[j];
                    if (pairRank == tripleRank || buckets[pairRank].Count < 2)
                    {
                        continue;
                    }

                    cards = new List<Card>
                    {
                        buckets[tripleRank][0],
                        buckets[tripleRank][1],
                        buckets[tripleRank][2],
                        buckets[pairRank][0],
                        buckets[pairRank][1]
                    };
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindFourWithSingles(List<int> ranks, Dictionary<int, List<Card>> buckets, int minRank, out List<Card> cards)
        {
            cards = null;
            for (int i = 0; i < ranks.Count; i++)
            {
                int fourRank = ranks[i];
                if (fourRank <= minRank || buckets[fourRank].Count < 4)
                {
                    continue;
                }

                HashSet<int> excluded = new HashSet<int> { fourRank };
                List<Card> singles = CollectSingles(ranks, buckets, excluded);
                if (singles.Count < 2)
                {
                    continue;
                }

                cards = new List<Card>(buckets[fourRank])
                {
                    singles[0],
                    singles[1]
                };
                return true;
            }

            return false;
        }

        private static bool TryFindFourWithPairs(List<int> ranks, Dictionary<int, List<Card>> buckets, int minRank, out List<Card> cards)
        {
            cards = null;
            for (int i = 0; i < ranks.Count; i++)
            {
                int fourRank = ranks[i];
                if (fourRank <= minRank || buckets[fourRank].Count < 4)
                {
                    continue;
                }

                List<int> pairRanks = new List<int>();
                for (int j = 0; j < ranks.Count; j++)
                {
                    int rank = ranks[j];
                    if (rank == fourRank || buckets[rank].Count < 2)
                    {
                        continue;
                    }

                    pairRanks.Add(rank);
                }

                if (pairRanks.Count < 2)
                {
                    continue;
                }

                cards = new List<Card>(buckets[fourRank])
                {
                    buckets[pairRanks[0]][0],
                    buckets[pairRanks[0]][1],
                    buckets[pairRanks[1]][0],
                    buckets[pairRanks[1]][1]
                };
                return true;
            }

            return false;
        }

        private static bool TryFindStraight(List<int> ranks, Dictionary<int, List<Card>> buckets, int length, int minMaxRank, out List<Card> cards)
        {
            cards = null;
            if (length < 5)
            {
                return false;
            }

            List<int> usable = FilterRanks(ranks, buckets, 1);
            return TryBuildSequence(usable, length, minMaxRank, out List<int> sequence)
                && TryBuildCards(sequence, buckets, 1, out cards);
        }

        private static bool TryFindStraightPairs(List<int> ranks, Dictionary<int, List<Card>> buckets, int length, int minMaxRank, out List<Card> cards)
        {
            cards = null;
            if (length < 3)
            {
                return false;
            }

            List<int> usable = FilterRanks(ranks, buckets, 2);
            if (!TryBuildSequence(usable, length, minMaxRank, out List<int> sequence))
            {
                return false;
            }

            return TryBuildCards(sequence, buckets, 2, out cards);
        }

        private static bool TryFindAirplane(List<int> ranks, Dictionary<int, List<Card>> buckets, int length, int minMaxRank, out List<Card> cards)
        {
            cards = null;
            if (length < 2)
            {
                return false;
            }

            List<int> usable = FilterRanks(ranks, buckets, 3);
            if (!TryBuildSequence(usable, length, minMaxRank, out List<int> sequence))
            {
                return false;
            }

            return TryBuildCards(sequence, buckets, 3, out cards);
        }

        private static bool TryFindAirplaneWithSingles(List<int> ranks, Dictionary<int, List<Card>> buckets, int length, int minMaxRank, out List<Card> cards)
        {
            cards = null;
            if (length < 2)
            {
                return false;
            }

            List<int> usable = FilterRanks(ranks, buckets, 3);
            if (!TryBuildSequence(usable, length, minMaxRank, out List<int> sequence))
            {
                return false;
            }

            HashSet<int> exclude = new HashSet<int>(sequence);
            List<Card> singles = CollectSingles(ranks, buckets, exclude);
            if (singles.Count < length)
            {
                return false;
            }

            if (!TryBuildCards(sequence, buckets, 3, out List<Card> tripleCards))
            {
                return false;
            }

            cards = new List<Card>(tripleCards);
            for (int i = 0; i < length; i++)
            {
                cards.Add(singles[i]);
            }

            return true;
        }

        private static bool TryFindAirplaneWithPairs(List<int> ranks, Dictionary<int, List<Card>> buckets, int length, int minMaxRank, out List<Card> cards)
        {
            cards = null;
            if (length < 2)
            {
                return false;
            }

            List<int> usable = FilterRanks(ranks, buckets, 3);
            if (!TryBuildSequence(usable, length, minMaxRank, out List<int> sequence))
            {
                return false;
            }

            HashSet<int> exclude = new HashSet<int>(sequence);
            List<int> pairRanks = new List<int>();
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (exclude.Contains(rank))
                {
                    continue;
                }

                if (buckets[rank].Count >= 2)
                {
                    pairRanks.Add(rank);
                }
            }

            if (pairRanks.Count < length)
            {
                return false;
            }

            if (!TryBuildCards(sequence, buckets, 3, out List<Card> tripleCards))
            {
                return false;
            }

            cards = new List<Card>(tripleCards);
            for (int i = 0; i < length; i++)
            {
                cards.Add(buckets[pairRanks[i]][0]);
                cards.Add(buckets[pairRanks[i]][1]);
            }

            return true;
        }

        private static Dictionary<int, int> BuildRankCounts(IReadOnlyList<Card> cards)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < cards.Count; i++)
            {
                int rank = Card.RankValue(cards[i].Rank);
                counts.TryGetValue(rank, out int count);
                counts[rank] = count + 1;
            }

            return counts;
        }

        private static Dictionary<int, List<Card>> BuildRankBuckets(IReadOnlyList<Card> cards)
        {
            Dictionary<int, List<Card>> buckets = new Dictionary<int, List<Card>>();
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                int rank = Card.RankValue(card.Rank);
                if (!buckets.TryGetValue(rank, out List<Card> list))
                {
                    list = new List<Card>();
                    buckets[rank] = list;
                }

                list.Add(card);
            }

            return buckets;
        }

        private static List<int> SortedRanks(Dictionary<int, List<Card>> buckets)
        {
            List<int> ranks = new List<int>(buckets.Keys);
            ranks.Sort();
            return ranks;
        }

        private static bool IsRocket(Dictionary<int, int> counts)
        {
            return counts.Count == 2
                && counts.ContainsKey(Card.RankValue(CardRank.JokerSmall))
                && counts.ContainsKey(Card.RankValue(CardRank.JokerBig));
        }

        private static bool IsStraight(Dictionary<int, int> counts, int total)
        {
            if (total < 5)
            {
                return false;
            }

            List<int> ranks = SortedKeys(counts);
            if (!AllCountsEqual(counts, 1))
            {
                return false;
            }

            return IsConsecutive(ranks) && MaxKey(counts) <= MaxStraightRank;
        }

        private static bool IsStraightPairs(Dictionary<int, int> counts, int total)
        {
            if (total < 6 || total % 2 != 0)
            {
                return false;
            }

            if (!AllCountsEqual(counts, 2))
            {
                return false;
            }

            List<int> ranks = SortedKeys(counts);
            return IsConsecutive(ranks) && MaxKey(counts) <= MaxStraightRank;
        }

        private static bool IsAirplane(Dictionary<int, int> counts, int total)
        {
            if (total < 6 || total % 3 != 0)
            {
                return false;
            }

            if (!AllCountsEqual(counts, 3))
            {
                return false;
            }

            List<int> ranks = SortedKeys(counts);
            return IsConsecutive(ranks) && MaxKey(counts) <= MaxStraightRank;
        }

        private static bool IsAirplaneWithSingles(Dictionary<int, int> counts, int total, out int mainRank)
        {
            mainRank = 0;
            if (total < 8 || total % 4 != 0)
            {
                return false;
            }

            List<int> tripleRanks = RanksWithCount(counts, 3);
            int tripleCount = tripleRanks.Count;
            if (tripleCount < 2 || tripleCount * 4 != total)
            {
                return false;
            }

            if (!IsConsecutive(tripleRanks) || MaxKey(counts, tripleRanks) > MaxStraightRank)
            {
                return false;
            }

            foreach (int rank in counts.Keys)
            {
                int count = counts[rank];
                if (count != 1 && count != 3)
                {
                    return false;
                }
            }

            mainRank = MaxKey(counts, tripleRanks);
            return true;
        }

        private static bool IsAirplaneWithPairs(Dictionary<int, int> counts, int total, out int mainRank)
        {
            mainRank = 0;
            if (total < 10 || total % 5 != 0)
            {
                return false;
            }

            List<int> tripleRanks = RanksWithCount(counts, 3);
            int tripleCount = tripleRanks.Count;
            if (tripleCount < 2 || tripleCount * 5 != total)
            {
                return false;
            }

            if (!IsConsecutive(tripleRanks) || MaxKey(counts, tripleRanks) > MaxStraightRank)
            {
                return false;
            }

            foreach (int rank in counts.Keys)
            {
                int count = counts[rank];
                if (count != 2 && count != 3)
                {
                    return false;
                }
            }

            mainRank = MaxKey(counts, tripleRanks);
            return true;
        }

        private static bool HasCounts(Dictionary<int, int> counts, params int[] expected)
        {
            List<int> values = new List<int>(counts.Values);
            values.Sort();
            System.Array.Sort(expected);
            if (values.Count != expected.Length)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AllCountsEqual(Dictionary<int, int> counts, int value)
        {
            foreach (int count in counts.Values)
            {
                if (count != value)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsConsecutive(List<int> ranks)
        {
            if (ranks.Count == 0)
            {
                return false;
            }

            for (int i = 1; i < ranks.Count; i++)
            {
                if (ranks[i] != ranks[i - 1] + 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static int MaxKey(Dictionary<int, int> counts)
        {
            int max = int.MinValue;
            foreach (int rank in counts.Keys)
            {
                if (rank > max)
                {
                    max = rank;
                }
            }

            return max;
        }

        private static int MaxKey(Dictionary<int, int> counts, List<int> subset)
        {
            int max = int.MinValue;
            for (int i = 0; i < subset.Count; i++)
            {
                int rank = subset[i];
                if (counts.ContainsKey(rank) && rank > max)
                {
                    max = rank;
                }
            }

            return max;
        }

        private static int FirstKey(Dictionary<int, int> counts)
        {
            foreach (int rank in counts.Keys)
            {
                return rank;
            }

            return 0;
        }

        private static int FindRankWithCount(Dictionary<int, int> counts, int count)
        {
            foreach (int rank in counts.Keys)
            {
                if (counts[rank] == count)
                {
                    return rank;
                }
            }

            return 0;
        }

        private static List<int> SortedKeys(Dictionary<int, int> counts)
        {
            List<int> ranks = new List<int>(counts.Keys);
            ranks.Sort();
            return ranks;
        }

        private static List<int> RanksWithCount(Dictionary<int, int> counts, int count)
        {
            List<int> ranks = new List<int>();
            foreach (int rank in counts.Keys)
            {
                if (counts[rank] == count)
                {
                    ranks.Add(rank);
                }
            }

            ranks.Sort();
            return ranks;
        }

        private static List<int> FilterRanks(List<int> ranks, Dictionary<int, List<Card>> buckets, int minCount)
        {
            List<int> filtered = new List<int>();
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (rank <= MaxStraightRank && buckets[rank].Count >= minCount)
                {
                    filtered.Add(rank);
                }
            }

            return filtered;
        }

        private static bool TryBuildSequence(List<int> ranks, int length, int minMaxRank, out List<int> sequence)
        {
            sequence = null;
            if (ranks.Count < length)
            {
                return false;
            }

            for (int i = 0; i <= ranks.Count - length; i++)
            {
                int start = ranks[i];
                int end = start + length - 1;
                if (end > MaxStraightRank)
                {
                    break;
                }

                if (end <= minMaxRank)
                {
                    continue;
                }

                bool ok = true;
                for (int r = start; r <= end; r++)
                {
                    if (!ranks.Contains(r))
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                {
                    continue;
                }

                sequence = new List<int>(length);
                for (int r = start; r <= end; r++)
                {
                    sequence.Add(r);
                }

                return true;
            }

            return false;
        }

        private static bool TryBuildCards(List<int> ranks, Dictionary<int, List<Card>> buckets, int countPerRank, out List<Card> cards)
        {
            cards = new List<Card>();
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (!buckets.TryGetValue(rank, out List<Card> list) || list.Count < countPerRank)
                {
                    cards = null;
                    return false;
                }

                for (int j = 0; j < countPerRank; j++)
                {
                    cards.Add(list[j]);
                }
            }

            return true;
        }

        private static List<Card> CollectSingles(List<int> ranks, Dictionary<int, List<Card>> buckets, HashSet<int> exclude)
        {
            List<Card> singles = new List<Card>();
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                if (exclude.Contains(rank))
                {
                    continue;
                }

                List<Card> list = buckets[rank];
                for (int j = 0; j < list.Count; j++)
                {
                    singles.Add(list[j]);
                }
            }

            return singles;
        }
    }
}
