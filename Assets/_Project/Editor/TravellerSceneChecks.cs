using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class TravellerSceneChecks
    {
        [MenuItem("Tools/Three Body Traveller/Capture All Scene Moments")]
        public static void CaptureAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture authored views in Edit mode.");
            Directory.CreateDirectory("Logs/TravellerQA");
            Directory.CreateDirectory("Documentation/Previews");
            foreach (string sceneName in TravellerStage.SceneNames)
            {
                EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/" + sceneName + ".unity");
                var stage = Object.FindFirstObjectByType<TravellerStage>();
                var originalSky = stage.skyMaterial;
                stage.skyMaterial = new Material(originalSky);
                try
                {
                    for (int i = 0; i < stage.profile.moments.Length; i++)
                    {
                        stage.ApplyMoment(i, true);
                        Capture(stage.player.viewCamera, "Logs/TravellerQA/" + sceneName + "_" + i + ".jpg");
                        if ((stage.profile.sceneIndex == 0 && i == 1) || (stage.profile.sceneIndex == 1 && i == 0) || (stage.profile.sceneIndex == 2 && i == 1))
                            Capture(stage.player.viewCamera, "Documentation/Previews/" + sceneName + ".jpg");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(stage.skyMaterial);
                    stage.skyMaterial = originalSky;
                }
            }
            EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/01_GobiJourney.unity");
            Debug.Log("TRAVELLER_QA: captured all 12 authored moments.");
        }

        public static void Capture(Camera camera, string path)
        {
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1600, 900, 24);
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.aspect = 16f / 9f; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToJPG(90));
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture);
            }
        }
    }
}
