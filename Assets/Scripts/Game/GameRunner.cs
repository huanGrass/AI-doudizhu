using Doudizhu.UI;
using System;
using UnityEngine;

namespace Doudizhu.Game
{
    public sealed class GameRunner : MonoBehaviour
    {
        private void Start()
        {
            RunSimulation();
        }

        private static void RunSimulation()
        {
            int seed = unchecked((int)System.DateTime.UtcNow.Ticks);
            GameEngine engine = new GameEngine(new AutoGameStrategy(), seed);
            int safety = 0;

            while (engine.Phase != GamePhase.Finished)
            {
                StepResult result = engine.Step();
                safety++;
                if (safety > 800)
                {
                    Debug.LogWarning("Simulation stopped: exceeded safety limit.");
                    return;
                }

                if (result.Kind == StepKind.Bid && result.BidScore > 0)
                {
                    Debug.Log($"Player{result.PlayerIndex + 1} bid {result.BidScore}");
                }
            }

            Debug.Log($"Winner is Player{engine.CurrentPlayer + 1}");
        }
    }

    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindAnyObjectByType<DoudizhuUiController>() != null)
            {
                return;
            }

            GameObject obj = new GameObject("GameRunner");
            obj.AddComponent<GameRunner>();
            Object.DontDestroyOnLoad(obj);
        }
    }
}
