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

            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera camera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            cameraObj.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            GameObject bootstrap = new GameObject("RuntimeBootstrap");
            bootstrap.AddComponent<DoudizhuRuntimeUiBuilder>();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}
