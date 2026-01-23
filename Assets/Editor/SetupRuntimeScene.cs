using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Doudizhu.UI;

namespace Doudizhu.EditorTools
{
    public static class SetupRuntimeScene
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Doudizhu/Setup Runtime Scene")]
        public static void Setup()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                Object.DestroyImmediate(root);
            }

            GameObject bootstrap = new GameObject("RuntimeBootstrap");
            bootstrap.AddComponent<DoudizhuRuntimeUiBuilder>();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}
