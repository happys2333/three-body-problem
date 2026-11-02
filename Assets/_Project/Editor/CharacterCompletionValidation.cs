using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class CharacterCompletionValidation
    {
        public const string BuildOutput = "Builds/CharacterCompletion20261102/Traveller.app";
        private const string BuildRequest = "ThreeBody.CharacterCompletion.BuildPending";
        [InitializeOnLoadMethod]
        private static void ResumeBuild() { EditorApplication.update -= TryBuild; EditorApplication.update += TryBuild; }
        public static void QueueBuild() { EraGuardProduction.GuardProject(); SessionState.SetBool(BuildRequest, true); ResumeBuild(); }
        private static void TryBuild()
        {
            if (!SessionState.GetBool(BuildRequest, false) || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            SessionState.SetBool(BuildRequest, false); Build();
        }
        private static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || UnityEditor.BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Editor is busy.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene first.");
            Directory.CreateDirectory(Path.GetDirectoryName(BuildOutput));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = TravellerStage.SceneNames.Select(n => TravellerSceneBuilder.SceneFolder + "/" + n + ".unity").ToArray(),
                locationPathName = BuildOutput, target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.CompressWithLz4 | BuildOptions.DetailedBuildReport });
            var s = report.summary;
            File.WriteAllText(CharacterCompletionProduction.Docs + "/Unity/native-build.json", "{\"result\":\"" + s.result + "\",\"errors\":" + s.totalErrors + ",\"warnings\":" + s.totalWarnings + ",\"seconds\":" + s.totalTime.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
        }
        [Serializable] private sealed class Motion
        { public string id, clip; public bool passed = true; public int poses; public float maximumRadius, maximumDisplacement, boneRotation; }
        [Serializable] private sealed class Report
        { public bool passed; public string[] characters; public List<Motion> motions = new List<Motion>(); }

        public static void Run(params string[] characters)
        {
            EraGuardProduction.GuardProject(); if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode.");
            string folder = CharacterCompletionProduction.Docs + "/Previews"; Directory.CreateDirectory(folder);
            if (characters.Length == 0) characters = new[] { "PopeV3", "ShangCourtLady", "MedievalCleric" };
            var report = new Report { characters = characters }; var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var cameraObject = new GameObject("Completion review camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .065f, .08f); camera.nearClipPlane = .05f; camera.farClipPlane = 40;
                foreach (var setting in new[] { new Vector3(35, 205, 1.3f), new Vector3(20, 125, .65f) })
                {
                    var lightObject = new GameObject("Completion review light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
                    var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = setting.z;
                    light.transform.rotation = Quaternion.Euler(setting.x, setting.y, 0);
                }
                foreach (string id in characters)
                {
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(CharacterCompletionProduction.ReviewedCharacter(id), scene);
                    try
                    {
                        var animator = root.GetComponentInChildren<Animator>(); var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                        foreach (var skin in skins) skin.updateWhenOffscreen = true;
                        foreach (string clip in new[] { "MX_Idle", "MX_Walking", "MX_Running", "MX_SitIdle", "MX_TalkCalm", "MX_CarryBoxIdle", "MX_Bow", "MX_ShieldEyes" })
                        {
                            var item = new Motion { id = id, clip = clip }; Vector3[][] initial = null; Quaternion[] rotations = null;
                            for (int pose = 0; pose < 5; pose++)
                            {
                                int sample = pose;
                                TravellerStoryCharacters.WithSample(animator, clip, pose * .22f, () => {
                                    var points = skins.Select(s => BakedPoints(s, root.transform.position)).ToArray();
                                    var bones = skins.SelectMany(s => s.bones).Distinct().ToArray();
                                    if (sample == 0) { initial = points; rotations = bones.Select(b => b.localRotation).ToArray(); }
                                    else for (int i = 0; i < bones.Length; i++) item.boneRotation = Mathf.Max(item.boneRotation, Quaternion.Angle(rotations[i], bones[i].localRotation));
                                    for (int s = 0; s < points.Length; s++) for (int v = 0; v < points[s].Length; v++)
                                    {
                                        var p = points[s][v]; item.passed &= float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
                                        item.maximumRadius = Mathf.Max(item.maximumRadius, p.magnitude);
                                        item.maximumDisplacement = Mathf.Max(item.maximumDisplacement, Vector3.Distance(p, initial[s][v]));
                                    }
                                    item.poses++;
                                });
                            }
                            item.passed &= item.maximumRadius > 1 && item.maximumRadius < 4 && item.maximumDisplacement > .001f && item.boneRotation > .05f;
                            report.motions.Add(item);
                            if (clip == "MX_Idle" || clip == "MX_Walking" || clip == "MX_SitIdle")
                                TravellerStoryCharacters.WithSample(animator, clip, .44f, () => CaptureSkin(camera, root, folder + "/" + id + "_" + clip + ".jpg"));
                        }
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                foreach (string id in new[] { "PapalThrone", "OpenManuscript" })
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterCompletionProduction.Prefab(id)); if (asset == null) continue;
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                    try
                    {
                        var lod = root.GetComponent<LODGroup>(); if (lod != null) lod.ForceLOD(0);
                        var b = CharacterCompletionProduction.Bounds(root); camera.orthographicSize = Mathf.Max(.3f, b.size.magnitude * .6f);
                        camera.transform.position = b.center + new Vector3(b.size.x * 1.2f, b.size.y * .5f + .35f, b.size.magnitude * 2);
                        camera.transform.LookAt(b.center); TravellerSceneChecks.Capture(camera, folder + "/" + id + "_Unity.jpg");
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                report.passed = report.motions.All(m => m.passed);
                File.WriteAllText(CharacterCompletionProduction.Docs + "/Unity/" + (characters.Length == 1 ? characters[0] + "-" : "") + "motion-validation.json", JsonUtility.ToJson(report, true));
                if (!report.passed) throw new InvalidDataException("Completion motion validation failed.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static Vector3[] BakedPoints(SkinnedMeshRenderer skin, Vector3 origin)
        {
            var mesh = new Mesh();
            try { skin.BakeMesh(mesh, true); return mesh.vertices.Select(v => skin.transform.TransformPoint(v) - origin).ToArray(); }
            finally { Object.DestroyImmediate(mesh); }
        }
        private static void CaptureSkin(Camera camera, GameObject root, string path)
        {
            var temporary = new List<GameObject>(); var meshes = new List<Mesh>();
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            try
            {
                foreach (var skin in skins)
                {
                    var mesh = new Mesh(); skin.BakeMesh(mesh, true); meshes.Add(mesh);
                    var go = new GameObject("Baked review skin"); SceneManager.MoveGameObjectToScene(go, camera.scene); temporary.Add(go); go.transform.SetParent(skin.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
                }
                camera.orthographicSize = 1.23f; camera.transform.position = new Vector3(1.1f, 1.35f, 5); camera.transform.LookAt(new Vector3(0, .92f, 0));
                TravellerSceneChecks.Capture(camera, path);
            }
            finally { foreach (var go in temporary) Object.DestroyImmediate(go); foreach (var mesh in meshes) Object.DestroyImmediate(mesh); foreach (var skin in skins) skin.enabled = true; }
        }
    }
}
