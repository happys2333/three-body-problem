using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class GeneratedCharacterChecks
    {
        private const string Output = "Documentation/CharacterProduction";
        [Serializable] public class MotionCheck
        {
            public string role, clip;
            public int sampledPoses, checkedVertices;
            public float largestBoneRotationDegrees, largestVertexRadius, largestVertexDisplacement;
            public float lowestVertexY = float.MaxValue, highestVertexY = float.MinValue;
            public float lastPoseLowestY = float.MaxValue;
            public bool finiteSkin, moving, deforming, passed;
        }
        [Serializable] public class Report
        {
            public string method = "Manual PlayableGraph humanoid retarget; five poses per actor/clip; baked vertex deformation and bone rotation checks; corrected fall ends within 15cm of ground. Contact sheets are separate visual evidence, not a cloth-intersection test.";
            public List<MotionCheck> combinations = new List<MotionCheck>();
        }

        [MenuItem("Tools/Three Body Traveller/Check Generated Character Motions")]
        public static Report CheckAndCapture()
        {
            return CheckClipsAndCapture("", 0, int.MaxValue);
        }

        public static Report CheckClipsAndCapture(string prefix, int skip, int take)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before checking character assets.");
            Directory.CreateDirectory(Output + "/Previews");
            var library = GeneratedCharacterLibrary.LoadClips();
            var clips = library.Where(c => c.name.StartsWith(prefix)).Skip(skip).Take(take).ToArray();
            if (clips.Length == 0) throw new InvalidDataException("No prepared clips match this check batch.");
            var scene = EditorSceneManager.NewPreviewScene();
            var report = new Report();
            var graphs = new List<PlayableGraph>();
            var actors = new List<GameObject>();
            try
            {
                for (int i = 0; i < GeneratedCharacterLibrary.Roles.Length; i++)
                {
                    string role = GeneratedCharacterLibrary.Roles[i];
                    var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                        GeneratedCharacterLibrary.Root + "/Prefabs/" + role + ".prefab"), scene);
                    actor.transform.position = new Vector3((i - 2.5f) * 1.48f, 0, 0);
                    actors.Add(actor);
                    var animator = actor.GetComponentInChildren<Animator>();
                    animator.runtimeAnimatorController = null;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                    foreach (var clip in clips)
                    {
                        var graph = PlayableGraph.Create(role + "_" + clip.name);
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        var playable = AnimationClipPlayable.Create(graph, clip);
                        var output = AnimationPlayableOutput.Create(graph, "Humanoid", animator);
                        output.SetSourcePlayable(playable); graph.Play();
                        var check = new MotionCheck { role = role, clip = clip.name, finiteSkin = true };
                        var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
                        var baseline = new Quaternion[skin.bones.Length];
                        Vector3[] baselineVertices = null;
                        var mesh = new Mesh();
                        try
                        {
                            for (int sample = 0; sample < 5; sample++)
                            {
                                playable.SetTime(clip.length * (sample * 0.22)); graph.Evaluate(0);
                                for (int b = 0; b < baseline.Length; b++)
                                    if (sample == 0) baseline[b] = skin.bones[b].localRotation;
                                    else check.largestBoneRotationDegrees = Mathf.Max(check.largestBoneRotationDegrees,
                                        Quaternion.Angle(baseline[b], skin.bones[b].localRotation));
                                skin.BakeMesh(mesh, false);
                                var vertices = mesh.vertices;
                                if (sample == 0) baselineVertices = new Vector3[vertices.Length];
                                for (int v = 0; v < vertices.Length; v++)
                                {
                                    var position = skin.transform.TransformPoint(vertices[v]) - actor.transform.position;
                                    if (sample == 0) baselineVertices[v] = position;
                                    else check.largestVertexDisplacement = Mathf.Max(check.largestVertexDisplacement,
                                        Vector3.Distance(position, baselineVertices[v]));
                                    bool finite = Finite(position.x) && Finite(position.y) && Finite(position.z);
                                    check.finiteSkin &= finite;
                                    if (finite)
                                    {
                                        check.largestVertexRadius = Mathf.Max(check.largestVertexRadius, position.magnitude);
                                        check.lowestVertexY = Mathf.Min(check.lowestVertexY, position.y);
                                        check.highestVertexY = Mathf.Max(check.highestVertexY, position.y);
                                        if (sample == 4) check.lastPoseLowestY = Mathf.Min(check.lastPoseLowestY, position.y);
                                    }
                                    check.checkedVertices++;
                                }
                                check.sampledPoses++;
                            }
                            check.moving = check.largestBoneRotationDegrees > 0.05f;
                            check.deforming = check.largestVertexDisplacement > 0.001f;
                            check.passed = check.finiteSkin && check.moving && check.deforming && check.largestVertexRadius > 0.5f && check.largestVertexRadius < 6f;
                            if (clip.name == "FallForward") check.passed &= check.lastPoseLowestY > -0.03f && check.lastPoseLowestY < 0.15f;
                            report.combinations.Add(check);
                        }
                        finally { Object.DestroyImmediate(mesh); graph.Destroy(); }
                    }
                    var idle = library.FirstOrDefault(c => c.name == "MX_Idle") ?? library.First(c => c.name == "Idle01");
                    var idleGraph = PlayableGraph.Create(role + "_Preview");
                    graphs.Add(idleGraph); idleGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var idlePlayable = AnimationClipPlayable.Create(idleGraph, idle);
                    AnimationPlayableOutput.Create(idleGraph, "Preview", animator).SetSourcePlayable(idlePlayable);
                    idleGraph.Play(); idleGraph.Evaluate(0); idlePlayable.SetTime(0.6); idleGraph.Evaluate(0);
                }
                var camera = NewObject("Character review camera", scene).AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 1.65f, 9f);
                camera.transform.LookAt(new Vector3(0, 1.02f, 0));
                camera.orthographic = true; camera.orthographicSize = 2.5f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.065f, 0.075f, 0.09f);
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 30f;
                camera.scene = scene;
                var key = NewObject("Character review key", scene).AddComponent<Light>();
                key.type = LightType.Directional; key.intensity = 1.35f;
                key.transform.rotation = Quaternion.Euler(35, 210, 0);
                var fill = NewObject("Character review fill", scene).AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = 0.6f;
                fill.transform.rotation = Quaternion.Euler(15, 130, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.localScale = new Vector3(1.6f, 1, 0.7f);
                var floorMaterial = new Material(Shader.Find("Standard"));
                floorMaterial.color = new Color(0.15f, 0.17f, 0.20f); floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                try
                {
                    CapturePose(camera, actors, Output + "/Previews/Unity_Six_Characters.jpg");
                    foreach (var clip in clips)
                    {
                        for (int i = 0; i < actors.Count; i++)
                        {
                            graphs[i].Destroy();
                            var animator = actors[i].GetComponentInChildren<Animator>();
                            var graph = PlayableGraph.Create("Review_" + clip.name); graphs[i] = graph;
                            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                            var playable = AnimationClipPlayable.Create(graph, clip);
                            AnimationPlayableOutput.Create(graph, "Preview", animator).SetSourcePlayable(playable);
                            graph.Play(); graph.Evaluate(0); playable.SetTime(clip.length * 0.55); graph.Evaluate(0);
                        }
                        CapturePose(camera, actors, Output + "/Previews/Unity_Motion_" + clip.name + ".jpg");
                    }
                }
                finally { Object.DestroyImmediate(floorMaterial); }
                string reportName = prefix == "" && skip == 0 && take == int.MaxValue ? "unity-motion-retarget-validation" :
                    "unity-motion-retarget-" + prefix.TrimEnd('_') + "-" + skip;
                File.WriteAllText(Output + "/" + reportName + ".json", JsonUtility.ToJson(report, true));
                if (report.combinations.Any(c => !c.passed)) throw new InvalidDataException("A motion check failed; inspect the saved report.");
                return report;
            }
            finally
            {
                foreach (var graph in graphs) if (graph.IsValid()) graph.Destroy();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static GameObject NewObject(string name, Scene scene)
        {
            var result = new GameObject(name); SceneManager.MoveGameObjectToScene(result, scene); return result;
        }

        private static void CapturePose(Camera camera, List<GameObject> actors, string path)
        {
            // A synchronous editor capture can reuse GPU skinning from the preceding frame.
            // Render the same evaluated skin through temporary CPU-baked meshes for deterministic evidence.
            var skins = actors.SelectMany(a => a.GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();
            var bakedObjects = new List<GameObject>(); var meshes = new List<Mesh>();
            try
            {
                foreach (var skin in skins)
                {
                    var mesh = new Mesh(); meshes.Add(mesh); skin.BakeMesh(mesh, false);
                    var baked = new GameObject("Evaluated pose"); bakedObjects.Add(baked);
                    SceneManager.MoveGameObjectToScene(baked, camera.scene);
                    baked.transform.SetParent(skin.transform, false);
                    baked.AddComponent<MeshFilter>().sharedMesh = mesh;
                    baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                }
                TravellerSceneChecks.Capture(camera, path);
            }
            finally
            {
                foreach (var baked in bakedObjects) Object.DestroyImmediate(baked);
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
                foreach (var skin in skins) skin.enabled = true;
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
