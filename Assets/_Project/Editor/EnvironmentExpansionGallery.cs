using System;
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
    public static partial class EnvironmentExpansionLibrary
    {
        public const string GalleryScene = "Assets/_Project/Scenes/Environment/Environment_ExpansionYard.unity";
        public const string PreviewBuild = "Builds/EnvironmentExpansionYard.app";
        [Serializable] private class BuildResult { public string result, path; public int errors, warnings; public double seconds; }

        [MenuItem("Tools/Three Body Traveller/Assets/Build Expansion Inspection Yard")]
        public static string BuildYard()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            var original = SceneManager.GetActiveScene();
            if (original.isDirty) throw new InvalidOperationException("The active scene has unsaved edits.");
            string returnPath = original.path;
            var report = JsonUtility.FromJson<EnvironmentProduction.ReadyReport>(File.ReadAllText(Docs + "/ready-validation.json"));
            if (report.assets.Any(a => !a.passed)) throw new InvalidDataException("Fix failed assets first.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var yard = new GameObject("Supplementary environment inspection yard");
                var preview = yard.AddComponent<ThreeBodyProblem.EnvironmentAssetPreview>();
                int count = report.assets.Count, columns = Mathf.CeilToInt(Mathf.Sqrt(count));
                preview.stations = new Transform[count]; preview.assetIds = new string[count];
                preview.viewpoints = new Vector3[count]; preview.viewTargets = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    var entry = report.assets[i];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab));
                    instance.transform.SetParent(yard.transform, false);
                    instance.transform.position = new Vector3((i % columns - columns / 2) * 160, 0, (i / columns - columns / 2) * 160);
                    preview.stations[i] = instance.transform; preview.assetIds[i] = entry.id;
                    if (entry.id == "ShangTimberGate") { preview.gate = instance.transform; preview.gateWallProbeOffset = 1.68f; }
                    if (entry.id == "MedievalStoneArch") { preview.portal = instance.transform; preview.portalWallProbeOffset = 1.4f; }
                    if (entry.id == "PalaceStoneStairs")
                    {
                        var mesh = instance.GetComponent<LODGroup>().GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                        float top = mesh.bounds.max.y - mesh.bounds.size.y * .15f;
                        if (mesh.vertices.Where(v => v.y > top).Average(v => v.z) > 0) instance.transform.rotation = Quaternion.Euler(0, 180, 0);
                        preview.stairs = instance.transform;
                    }
                }
                preview.shelters = preview.stations.Where(t => t.name == "ShadeOverhangV2" || t.name == "GobiShadeCanopyV3").ToArray();
                preview.palace = preview.stations.FirstOrDefault(t => t.name == "PyramidEntryPassage");
                var bounds = preview.stations.Select(t => t.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds).ToArray();
                for (int i = 0; i < count; i++)
                {
                    var b = bounds[i]; preview.viewTargets[i] = b.center;
                    float radius = Mathf.Max(1.4f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * 1.6f);
                    bool clear = false;
                    foreach (float angle in new[] { 25f, -25f, 0f, 65f, -65f, 90f, -90f, 135f, 180f })
                    {
                        var eye = b.center + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, b.size.y * .20f, Mathf.Cos(angle * Mathf.Deg2Rad) * radius);
                        eye.y = Mathf.Max(1.65f, eye.y);
                        bool blocked = false;
                        for (int j = 0; j < count && !blocked; j++)
                        {
                            if (j == i) continue; var other = bounds[j]; other.Expand(.4f); float hit;
                            if (other.Contains(eye) || other.IntersectRay(new Ray(eye, (b.center - eye).normalized), out hit) && hit < Vector3.Distance(eye, b.center)) blocked = true;
                        }
                        if (blocked) continue; preview.viewpoints[i] = eye; clear = true; break;
                    }
                    if (!clear) throw new InvalidOperationException("No clear view for " + preview.assetIds[i]);
                }
                if (preview.palace != null)
                {
                    preview.stations = preview.stations.Concat(new[] { preview.palace }).ToArray();
                    preview.assetIds = preview.assetIds.Concat(new[] { "PyramidPalace_Interior" }).ToArray();
                    preview.viewpoints = preview.viewpoints.Concat(new[] { preview.palace.position + new Vector3(0, 1.9f, 6) }).ToArray();
                    preview.viewTargets = preview.viewTargets.Concat(new[] { preview.palace.position + new Vector3(0, 1.9f, -6) }).ToArray();
                }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Walkable inspection terrain"; floor.isStatic = true;
                var floorMesh = Object.Instantiate(floor.GetComponent<MeshFilter>().sharedMesh); floorMesh.name = "ExpansionYardGround";
                floorMesh.vertices = floorMesh.vertices.Select(v => v * 160).ToArray(); floorMesh.RecalculateBounds();
                string groundPath = Root + "/Meshes/ExpansionYardGround.asset";
                var savedGround = AssetDatabase.LoadAssetAtPath<Mesh>(groundPath);
                if (savedGround == null) { AssetDatabase.CreateAsset(floorMesh, groundPath); savedGround = floorMesh; }
                else { EditorUtility.CopySerialized(floorMesh, savedGround); EditorUtility.SetDirty(savedGround); Object.DestroyImmediate(floorMesh); }
                floor.GetComponent<MeshFilter>().sharedMesh = savedGround; floor.GetComponent<MeshCollider>().sharedMesh = savedGround;
                floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/YardGround.mat");
                var sun = new GameObject("Inspection daylight").AddComponent<Light>(); sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(45, -35, 0); sun.intensity = 1.15f; sun.color = new Color(1, .96f, .90f); sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.48f, .52f, .60f);
                RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/YardSky.mat");
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 350; RenderSettings.fogEndDistance = 1400; RenderSettings.fogColor = new Color(.62f, .68f, .73f);
                var visitor = new GameObject("First person visitor", typeof(CharacterController));
                var controller = visitor.GetComponent<CharacterController>(); controller.height = 1.8f; controller.radius = .3f;
                controller.center = Vector3.up * .9f; controller.skinWidth = .03f; controller.stepOffset = .35f;
                var cameraObject = new GameObject("Inspection camera", typeof(Camera), typeof(AudioListener)); cameraObject.transform.SetParent(visitor.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.tag = "MainCamera"; camera.fieldOfView = 60; camera.nearClipPlane = .08f; camera.farClipPlane = 2000; camera.allowHDR = true;
                var player = visitor.AddComponent<ThreeBodyProblem.TravellerPlayer>(); player.viewCamera = camera; player.eyeHeight = 1.65f; player.walkSpeed = 4; preview.player = player;
                preview.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf");
                var gateBounds = preview.gate.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds;
                player.Teleport(new Vector3(gateBounds.center.x, 1.7f, gateBounds.max.z + 7), gateBounds.center);
                Directory.CreateDirectory(Path.GetDirectoryName(GalleryScene)); EditorSceneManager.SaveScene(scene, GalleryScene); AssetDatabase.SaveAssets();
                TravellerSceneChecks.Capture(camera, Docs + "/Previews/InspectionYard_Entry.jpg"); return GalleryScene;
            }
            finally { if (!string.IsNullOrEmpty(returnPath)) EditorSceneManager.OpenScene(returnPath); }
        }

        public static string BuildPreview()
        {
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { GalleryScene }, locationPathName = PreviewBuild, target = BuildTarget.StandaloneOSX, options = BuildOptions.Development });
            File.WriteAllText(Docs + "/preview-build.json", JsonUtility.ToJson(new BuildResult { result = result.summary.result.ToString(), path = PreviewBuild, errors = result.summary.totalErrors, warnings = result.summary.totalWarnings, seconds = result.summary.totalTime.TotalSeconds }, true));
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Expansion preview build failed.");
            return Path.GetFullPath(PreviewBuild);
        }
    }
}
