using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BatchTools
{
    public static class UIBatchRunner
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;

        [MenuItem("Tools/Batch/Capture Screenshot")]
        public static void RunAll()
        {
            string scenePath = ResolveScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("No scene available for batch capture.");
                return;
            }

            string dir = "Screenshots";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.OpenScene(scenePath);
            Canvas.ForceUpdateCanvases();

            BatchScreenshotContext context = new BatchScreenshotContext(dir, CaptureWidth, CaptureHeight);

            List<IBatchScreenshotProvider> providers = FindProviders();
            if (providers.Count == 0)
            {
                Debug.Log("No IBatchScreenshotProvider found. Capturing default series.");
                CaptureDefaultSeries(context);
            }
            else
            {
                foreach (IBatchScreenshotProvider provider in providers)
                {
                    try
                    {
                        provider.CaptureScreenshots(context);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Batch screenshot provider failed ({provider.GetType().Name}): {ex}");
                    }
                }
            }

            EnsureMinimumShots(context, 1);
            AssetDatabase.Refresh();
        }

        private static string ResolveScenePath()
        {
            EditorBuildSettingsScene enabledScene = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            if (enabledScene != null && !string.IsNullOrEmpty(enabledScene.path) && File.Exists(enabledScene.path))
            {
                return enabledScene.path;
            }

            UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
            {
                return activeScene.path;
            }

            return string.Empty;
        }

        private static List<IBatchScreenshotProvider> FindProviders()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            return behaviours.OfType<IBatchScreenshotProvider>().ToList();
        }

        private static void CaptureDefaultSeries(BatchScreenshotContext context)
        {
            context.Capture(string.Empty);
        }

        private static void EnsureMinimumShots(BatchScreenshotContext context, int minCount)
        {
            while (context.Count < minCount)
            {
                context.Capture($"fallback_{context.Count + 1}");
            }
        }

        private static void CaptureSceneToPng(string path, int width, int height)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = UnityEngine.Object.FindObjectOfType<Camera>();
            }

            if (cam == null)
            {
                Debug.LogError("No camera found for capture.");
                return;
            }

            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            RenderMode[] modes = new RenderMode[canvases.Length];
            Camera[] assigned = new Camera[canvases.Length];

            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                assigned[i] = canvases[i].worldCamera;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 10f;
            }

            RenderTexture rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].worldCamera = assigned[i];
            }

            Debug.Log("Screenshot saved: " + path);
        }

        private class BatchScreenshotContext : IBatchScreenshotContext
        {
            private readonly string _directory;
            private readonly int _width;
            private readonly int _height;
            private int _index;

            public int Count => _index;

            public BatchScreenshotContext(string directory, int width, int height)
            {
                _directory = directory;
                _width = width;
                _height = height;
                _index = 0;
            }

            public void Capture(string tag)
            {
                _index++;
                string sanitizedTag = Sanitize(tag);
                string fileName = string.IsNullOrEmpty(sanitizedTag)
                    ? $"ui_shot_{_index}.png"
                    : $"ui_shot_{_index}_{sanitizedTag}.png";
                string outputPath = Path.Combine(_directory, fileName);
                Canvas.ForceUpdateCanvases();
                CaptureSceneToPng(outputPath, _width, _height);
            }

            private static string Sanitize(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                char[] invalid = Path.GetInvalidFileNameChars();
                string safe = new string(value.Where(c => !invalid.Contains(c) && c != ' ').ToArray());
                return safe.Length == 0 ? "shot" : safe.ToLowerInvariant();
            }
        }
    }
}
