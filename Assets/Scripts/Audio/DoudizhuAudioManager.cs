using System.Collections.Generic;
using Doudizhu.Game;
using UnityEngine;

namespace Doudizhu.Audio
{
    public sealed class DoudizhuAudioManager : MonoBehaviour
    {
        private const string BgmPath = "Audio/Bgm/bgm";
        private const string VoiceRoot = "Audio/Voice";

        private static readonly Dictionary<CardRank, string> RankKeys = new Dictionary<CardRank, string>
        {
            { CardRank.Ace, "a" },
            { CardRank.Two, "er" },
            { CardRank.Three, "san" },
            { CardRank.Four, "si" },
            { CardRank.Five, "wu" },
            { CardRank.Six, "liu" },
            { CardRank.Seven, "qi" },
            { CardRank.Eight, "ba" },
            { CardRank.Nine, "jiu" },
            { CardRank.Ten, "shi" },
            { CardRank.Jack, "j" },
            { CardRank.Queen, "q" },
            { CardRank.King, "k" },
            { CardRank.JokerSmall, "xiao_wang" },
            { CardRank.JokerBig, "da_wang" }
        };

        private static readonly Dictionary<PlayType, string> TypeKeys = new Dictionary<PlayType, string>
        {
            { PlayType.Straight, "shun_zi" },
            { PlayType.StraightPairs, "lian_dui" },
            { PlayType.Airplane, "fei_ji" },
            { PlayType.AirplaneWithSingles, "fei_ji" },
            { PlayType.AirplaneWithPairs, "fei_ji" },
            { PlayType.TripleWithSingle, "san_dai_yi" },
            { PlayType.TripleWithPair, "san_dai_yi_duir" },
            { PlayType.FourWithTwoSingles, "si_dai_er" },
            { PlayType.FourWithTwoPairs, "si_dai_liang_duir" },
            { PlayType.Bomb, "zha_dan" },
            { PlayType.Rocket, "wang_zha" }
        };

        private AudioSource _bgmSource;
        private AudioSource _voiceSource;
        private readonly Dictionary<int, VoiceSet> _voiceSets = new Dictionary<int, VoiceSet>();

        public static DoudizhuAudioManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupSources();
            LoadVoiceSets();
            PlayBgm();
        }

        public void PlayStep(StepResult result, BidStage bidStageBefore, PlayAction? lastPlayBefore)
        {
            if (!_voiceSets.TryGetValue(GetVoiceIndex(result.PlayerIndex), out VoiceSet set))
            {
                return;
            }

            AudioClip clip = null;
            switch (result.Kind)
            {
                case StepKind.Bid:
                    clip = set.GetAction(result.BidScore > 0
                        ? (bidStageBefore == BidStage.Call ? "jiao_di_zhu" : "qiang_di_zhu")
                        : "bu_yao");
                    break;
                case StepKind.Pass:
                    clip = set.GetAction(lastPlayBefore.HasValue && lastPlayBefore.Value.Type != PlayType.Pass ? "yao_bu_qi" : "guo");
                    break;
                case StepKind.Play:
                case StepKind.Finish:
                    clip = ResolvePlayClip(set, result.Play);
                    break;
                default:
                    break;
            }

            if (clip != null)
            {
                _voiceSource.PlayOneShot(clip);
            }
        }

        private void SetupSources()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = 0.35f;

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.loop = false;
            _voiceSource.playOnAwake = false;
            _voiceSource.volume = 1f;
        }

        private void PlayBgm()
        {
            AudioClip clip = Resources.Load<AudioClip>(BgmPath);
            if (clip == null)
            {
                return;
            }

            _bgmSource.clip = clip;
            _bgmSource.Play();
        }

        private void LoadVoiceSets()
        {
            _voiceSets.Clear();
            for (int i = 1; i <= 3; i++)
            {
                _voiceSets[i] = new VoiceSet(
                    LoadCategory($"{VoiceRoot}/{i}/action"),
                    LoadCategory($"{VoiceRoot}/{i}/single"),
                    LoadCategory($"{VoiceRoot}/{i}/pair"),
                    LoadCategory($"{VoiceRoot}/{i}/type"));
            }
        }

        private static Dictionary<string, AudioClip> LoadCategory(string path)
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(path);
            Dictionary<string, AudioClip> dict = new Dictionary<string, AudioClip>();
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                if (clip != null && !dict.ContainsKey(clip.name))
                {
                    dict[clip.name] = clip;
                }
            }

            return dict;
        }

        private static int GetVoiceIndex(int playerIndex)
        {
            int normalized = playerIndex % 3;
            return normalized + 1;
        }

        private static AudioClip ResolvePlayClip(VoiceSet set, PlayAction action)
        {
            if (action.Type == PlayType.Pass)
            {
                return set.GetAction("guo");
            }

            if (action.Type == PlayType.Single)
            {
                return set.GetSingle(GetRankKey(action.Cards));
            }

            if (action.Type == PlayType.Pair)
            {
                return set.GetPair(GetPairKey(action.Cards));
            }

            if (TypeKeys.TryGetValue(action.Type, out string typeKey))
            {
                AudioClip typeClip = set.GetType(typeKey);
                if (typeClip != null)
                {
                    return typeClip;
                }
            }

            return set.GetSingle(GetRankKey(action.Cards));
        }

        private static string GetRankKey(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return null;
            }

            CardRank rank = cards[0].Rank;
            int maxValue = Card.RankValue(rank);
            for (int i = 1; i < cards.Count; i++)
            {
                int value = Card.RankValue(cards[i].Rank);
                if (value > maxValue)
                {
                    maxValue = value;
                    rank = cards[i].Rank;
                }
            }

            return RankKeys.TryGetValue(rank, out string key) ? key : null;
        }

        private static string GetPairKey(IReadOnlyList<Card> cards)
        {
            string rankKey = GetRankKey(cards);
            return rankKey == null ? null : $"dui_{rankKey}";
        }

        private readonly struct VoiceSet
        {
            private readonly Dictionary<string, AudioClip> _action;
            private readonly Dictionary<string, AudioClip> _single;
            private readonly Dictionary<string, AudioClip> _pair;
            private readonly Dictionary<string, AudioClip> _type;

            public VoiceSet(
                Dictionary<string, AudioClip> action,
                Dictionary<string, AudioClip> single,
                Dictionary<string, AudioClip> pair,
                Dictionary<string, AudioClip> type)
            {
                _action = action;
                _single = single;
                _pair = pair;
                _type = type;
            }

            public AudioClip GetAction(string key)
            {
                return GetClip(_action, key);
            }

            public AudioClip GetSingle(string key)
            {
                return GetClip(_single, key);
            }

            public AudioClip GetPair(string key)
            {
                return GetClip(_pair, key);
            }

            public AudioClip GetType(string key)
            {
                return GetClip(_type, key);
            }

            private static AudioClip GetClip(Dictionary<string, AudioClip> dict, string key)
            {
                if (dict == null || string.IsNullOrEmpty(key))
                {
                    return null;
                }

                return dict.TryGetValue(key, out AudioClip clip) ? clip : null;
            }
        }
    }
}
