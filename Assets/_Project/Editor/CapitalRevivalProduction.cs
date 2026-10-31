using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class CapitalRevivalProduction
    {
        public const string Root = "Assets/_Project/Art/CapitalRevival";
        public const string Docs = "Documentation/CapitalRevival";
        public const string ScenePath = "Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity";

        [MenuItem("Tools/Three Body Traveller/Prepare Capital Rehydration")]
        private static void ApplyMenu() { Apply(); }

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before integrating revival assets.");
            foreach (var path in new[] { Root + "/Meshes", Root + "/Materials", Docs }) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<CapitalRevivalSettings>(Root + "/Revival.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<CapitalRevivalSettings>(); AssetDatabase.CreateAsset(settings, Root + "/Revival.asset"); }
            if (Shader.Find("Traveller/Hydrating Character") == null) throw new InvalidOperationException("Hydration shader has not imported.");
            var original = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("The capital has unsaved changes; preserve them first.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var backup = "Logs/revival-backups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup); File.Copy(ScenePath, backup + "/02_ChaogeCourt.unity");
                var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TravellerStage>(true)).Single();
                var members = stage.GetComponentsInChildren<TravellerCrowdMember>(true).Where(m => m.isReviving).OrderBy(m => m.variation).ToArray();
                if (members.Length != 7) throw new InvalidDataException("Expected seven existing revival participants.");
                var director = stage.GetComponent<CapitalRevivalDirector>() ?? stage.gameObject.AddComponent<CapitalRevivalDirector>();
                director.settings = settings; director.citizens = new RehydratingCitizen[members.Length];
                var rippleMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Water ripples.mat");
                if (rippleMaterial == null) { rippleMaterial = new Material(Shader.Find("Traveller/Revival Ripple")); AssetDatabase.CreateAsset(rippleMaterial, Root + "/Materials/Water ripples.mat"); }
                var rippleMesh = RippleMesh();
                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i]; var actor = member.GetComponent<TravellerActor>();
                    var animation = actor.settings.clips.Single(c => c.name == settings.getUpMotion);
                    if (!animation.humanMotion || animation.isLooping) throw new InvalidDataException("A non-looping humanoid get-up motion is required.");
                    var revival = member.GetComponent<RehydratingCitizen>() ?? member.gameObject.AddComponent<RehydratingCitizen>();
                    if (revival.paperVisual != null) Object.DestroyImmediate(revival.paperVisual);
                    var paper = new GameObject("Paper to body presentation"); paper.transform.SetParent(actor.transform, false);
                    revival.settings = settings; revival.paperVisual = paper; revival.order = i;
                    var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var renderers = new List<Renderer>();
                    var oldMode = actor.animator.cullingMode; actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var oldUpdates = skins.Select(s => s.updateWhenOffscreen).ToArray(); foreach (var skin in skins) skin.updateWhenOffscreen = true;
                    Bounds combined = new Bounds(); bool first = true;
                    try
                    {
                        TravellerStoryCharacters.WithSample(actor.animator, settings.getUpMotion, settings.getUpStartPhase, () => {
                            for (int s = 0; s < skins.Length; s++)
                            {
                                var skin = skins[s]; var baked = new Mesh(); skin.BakeMesh(baked, false);
                                var matrix = actor.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                                var normalMatrix = matrix.inverse.transpose;
                                baked.vertices = baked.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                                baked.normals = baked.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
                                baked.colors = skin.sharedMesh.colors; baked.RecalculateTangents(); baked.RecalculateBounds();
                                if (first) { combined = baked.bounds; first = false; } else combined.Encapsulate(baked.bounds);
                                var mesh = SaveMesh("Citizen_" + i + "_" + s + "_Prone", baked);
                                var visual = new GameObject("Prone body " + s, typeof(MeshFilter), typeof(MeshRenderer)); visual.transform.SetParent(paper.transform, false);
                                visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                                var renderer = visual.GetComponent<MeshRenderer>();
                                renderer.sharedMaterials = skin.sharedMaterials.Select(PaperMaterial).ToArray(); renderers.Add(renderer);
                            }
                        });
                        var keys = new List<Keyframe>();
                        for (int p = 0; p <= 40; p++)
                        {
                            float phase = Mathf.Lerp(settings.getUpStartPhase, 1, p / 40f);
                            TravellerStoryCharacters.WithSample(actor.animator, settings.getUpMotion, phase, () => {
                                float min = TravellerCrowdLibrary.WorldBounds(actor.gameObject).min.y;
                                keys.Add(new Keyframe(phase, (settings.waterSurfaceY - min) / actor.transform.lossyScale.y));
                            });
                        }
                        revival.getUpFloorCorrection = new AnimationCurve(keys.ToArray());
                        for (int k = 0; k < keys.Count; k++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(revival.getUpFloorCorrection, k, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetKeyRightTangentMode(revival.getUpFloorCorrection, k, AnimationUtility.TangentMode.Linear);
                        }
                    }
                    finally
                    {
                        actor.animator.cullingMode = oldMode;
                        for (int s = 0; s < skins.Length; s++) skins[s].updateWhenOffscreen = oldUpdates[s];
                    }
                    revival.bakedBottom = combined.min.y; revival.bakedHeight = combined.size.y; revival.paperRenderers = renderers.ToArray();
                    var rings = new GameObject("Absorption ripples", typeof(MeshFilter), typeof(MeshRenderer)); rings.transform.SetParent(paper.transform, false);
                    rings.transform.localPosition = new Vector3(combined.center.x, combined.min.y + .002f, combined.center.z);
                    rings.transform.localScale = new Vector3(Mathf.Max(.75f, combined.size.x), 1, Mathf.Max(1, combined.size.z));
                    rings.GetComponent<MeshFilter>().sharedMesh = rippleMesh; var ringRenderer = rings.GetComponent<MeshRenderer>();
                    ringRenderer.sharedMaterial = rippleMaterial; ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    revival.ripple = ringRenderer; paper.SetActive(false);
                    director.citizens[i] = revival;
                    if (PrefabUtility.IsPartOfPrefabInstance(revival)) PrefabUtility.RecordPrefabInstancePropertyModifications(revival);
                }
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Docs + "/integration.json", "{\"participants\":7,\"scene\":\"" + ScenePath + "\",\"backup\":\"" + backup + "\"}");
                return ScenePath;
            }
            finally { SceneManager.SetActiveScene(original); if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static Material PaperMaterial(Material source)
        {
            string path = Root + "/Materials/" + source.name.Replace('/', '_') + "_Hydration.mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(source); AssetDatabase.CreateAsset(result, path); }
            else result.CopyPropertiesFromMaterial(source);
            result.shader = Shader.Find("Traveller/Hydrating Character"); EditorUtility.SetDirty(result); return result;
        }

        private static Mesh SaveMesh(string name, Mesh source)
        {
            source.name = name; string path = Root + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(source, path); return source; }
            existing.Clear(); existing.indexFormat = source.indexFormat; existing.vertices = source.vertices; existing.normals = source.normals;
            existing.uv = source.uv; existing.colors = source.colors; existing.tangents = source.tangents; existing.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) existing.SetTriangles(source.GetTriangles(i), i);
            existing.bounds = source.bounds; existing.UploadMeshData(false); EditorUtility.SetDirty(existing); Object.DestroyImmediate(source); return existing;
        }

        private static Mesh RippleMesh()
        {
            var mesh = new Mesh { vertices = new[] { new Vector3(-1,0,-1), new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,0,-1) },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }, triangles = new[] { 0,1,2,0,2,3 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return SaveMesh("Absorption ripples", mesh);
        }

        public static void BuildPreview()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = TravellerStage.SceneNames.Select(n => TravellerSceneBuilder.SceneFolder + "/" + n + ".unity").ToArray(),
                locationPathName = "Builds/CapitalRevivalPreview.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.Development });
            File.WriteAllText(Docs + "/build.json", "{\"result\":\"" + report.summary.result + "\",\"errors\":" + report.summary.totalErrors + ",\"warnings\":" + report.summary.totalWarnings + "}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Revival preview build failed.");
        }
    }
}
