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
            GameEngine engine = new GameEngine(new AutoSingleStrategy(), 20260123);
            int winner;
            PlayAction action;
            int safety = 0;

            while (!engine.Step(out winner, out action))
            {
                safety++;
                if (safety > 500)
                {
                    Debug.LogWarning("Simulation stopped: exceeded safety limit.");
                    return;
                }
            }

            Debug.Log($"Winner is Player{winner + 1}");
        }
    }

    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject obj = new GameObject("GameRunner");
            obj.AddComponent<GameRunner>();
            Object.DontDestroyOnLoad(obj);
        }
    }
}
