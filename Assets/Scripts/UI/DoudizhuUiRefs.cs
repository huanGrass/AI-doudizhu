using System;
using System.Collections.Generic;
using Doudizhu.Game;
using UnityEngine;

namespace Doudizhu.UI
{
    [Serializable]
    public struct RankSpriteEntry
    {
        public string Key;
        public Sprite Sprite;
    }

    public sealed class DoudizhuUiRefs : MonoBehaviour
    {
        public GameObject CardFacePrefab;
        public GameObject CardBackPrefab;
        public GameObject ActionButtonPrefab;

        public Sprite Background;
        public Sprite CardBack;
        public Sprite Joker;
        public Sprite SmallJoker;
        public Sprite BigJoker;
        public Sprite SuitSpade;
        public Sprite SuitHeart;
        public Sprite SuitClub;
        public Sprite SuitDiamond;

        public List<RankSpriteEntry> RankSprites = new List<RankSpriteEntry>();

        private readonly Dictionary<string, Sprite> _rankMap = new Dictionary<string, Sprite>();

        private void Awake()
        {
            EnsureRankMap();
        }

        private void EnsureRankMap()
        {
            if (_rankMap.Count == RankSprites.Count && _rankMap.Count > 0)
            {
                return;
            }

            _rankMap.Clear();
            foreach (RankSpriteEntry entry in RankSprites)
            {
                if (!string.IsNullOrEmpty(entry.Key) && entry.Sprite != null)
                {
                    _rankMap[entry.Key] = entry.Sprite;
                }
            }
        }

        public Sprite GetRankSprite(CardRank rank)
        {
            EnsureRankMap();
            string key = rank switch
            {
                CardRank.Ace => "A",
                CardRank.Two => "2",
                CardRank.Three => "3",
                CardRank.Four => "4",
                CardRank.Five => "5",
                CardRank.Six => "6",
                CardRank.Seven => "7",
                CardRank.Eight => "8",
                CardRank.Nine => "9",
                CardRank.Ten => "10",
                CardRank.Jack => "J",
                CardRank.Queen => "Q",
                CardRank.King => "K",
                _ => string.Empty
            };

            return key.Length > 0 && _rankMap.TryGetValue(key, out Sprite sprite) ? sprite : null;
        }

        public Sprite GetSuitSprite(CardSuit suit)
        {
            return suit switch
            {
                CardSuit.Spade => SuitSpade,
                CardSuit.Heart => SuitHeart,
                CardSuit.Club => SuitClub,
                CardSuit.Diamond => SuitDiamond,
                _ => null
            };
        }
    }
}
