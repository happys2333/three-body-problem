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
    public static class TravellerCrowdChecks
    {
        [Serializable] public class Pair
        {
            public string variant, clip;
            public int poses, vertices;
            public float boneRotation, vertexDisplacement, radius;
            public bool passed;
        }
        [Serializable] public class Report
        {
            public string scope = "Nine supporting-character variants, five runtime motions, five CPU-baked poses each. Finite skin, deformation and gross bounds only; visual inspection is separate.";
            public List<Pair> combinations = new List<Pair>();
        }

        public static Report CheckAndCapture()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before checking crowd assets.");
            var report = new Report(); var actors = new List<GameObject>(); var graphs = new List<PlayableGraph>();
            var scene = EditorSceneManager.NewPreviewScene();
            const string output = "Documentation/CharacterProduction";
            Directory.CreateDirectory(output + "/Previews");
            try
            {
                var variants = JsonUtility.FromJson<TravellerCrowdLibrary.LibraryReport>(File.ReadAllText(output + "/crowd-library-validation.json")).variants;
                foreach (var variant in variants)
                {
                    var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(variant.prefab), scene);
                    actors.Add(actor); var animator = actor.GetComponentInChildren<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                    foreach (var clip in new[] { "MX_Idle", "MX_WeightShift", "MX_Walking", "MX_Shiver", "MX_InjuredHurtingIdle" })
                    {
                        var pair = new Pair { variant = variant.id, clip = clip, passed = true };
                        var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                        var baseline = new Dictionary<SkinnedMeshRenderer, Vector3[]>();
                        var bones = skins.SelectMany(s => s.bones).Distinct().ToArray();
                        Quaternion[] rotation = null;
                        var mesh = new Mesh();
                        try
                        {
                            for (int pose = 0; pose < 5; pose++)
                            {
                                TravellerStoryCharacters.WithSample(animator, clip, pose * .22f, () => {
                                if (pose == 0) rotation = bones.Select(b => b.localRotation).ToArray();
                                else for (int b = 0; b < bones.Length; b++) pair.boneRotation = Mathf.Max(pair.boneRotation, Quaternion.Angle(rotation[b], bones[b].localRotation));
                                foreach (var skin in skins)
                                {
                                    skin.BakeMesh(mesh, false);
                                    var vertices = mesh.vertices.Select(v => skin.transform.TransformPoint(v) - actor.transform.position).ToArray();
                                    if (pose == 0) baseline[skin] = vertices;
                                    for (int v = 0; v < vertices.Length; v++)
                                    {
                                        var p = vertices[v]; pair.passed &= float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
                                        pair.radius = Mathf.Max(pair.radius, p.magnitude);
                                        if (pose != 0) pair.vertexDisplacement = Mathf.Max(pair.vertexDisplacement, Vector3.Distance(p, baseline[skin][v]));
                                    }
                                    pair.vertices += vertices.Length;
                                }
                                pair.poses++;
                                });
                            }
                        }
                        finally { Object.DestroyImmediate(mesh); }
                        pair.passed &= pair.radius > 1 && pair.radius < 4 && pair.boneRotation > .05f && pair.vertexDisplacement > .001f;
                        report.combinations.Add(pair);
                    }
                    var graph = PlayableGraph.Create("Crowd evaluated preview"); graphs.Add(graph); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var idle = actor.GetComponent<TravellerActor>().settings.clips.First(c=>c.name=="MX_WeightShift");
                    var playable = AnimationClipPlayable.Create(graph,idle); AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(playable);
                    graph.Play();graph.Evaluate(0);playable.SetTime(idle.length*.25);graph.Evaluate(0);
                }
                var camera = NewObject("Crowd review camera", scene).AddComponent<Camera>();
                camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f,.07f,.09f);
                camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 40;
                var light = NewObject("Crowd review key", scene).AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(35, 200, 0);
                var fill = NewObject("Crowd review fill", scene).AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .65f; fill.transform.rotation = Quaternion.Euler(15, 125, 0);
                Capture(camera, actors, graphs, 0, 3, 1.9f, output + "/Previews/Unity_Crowd_Guards.jpg");
                Capture(camera, actors, graphs, 3, 6, 3.0f, output + "/Previews/Unity_Crowd_Citizens.jpg");
                File.WriteAllText(output + "/crowd-motion-validation.json", JsonUtility.ToJson(report, true));
                if (report.combinations.Any(p => !p.passed)) throw new InvalidDataException("Crowd motion check failed; inspect crowd-motion-validation.json.");
                return report;
            }
            finally { foreach(var graph in graphs)if(graph.IsValid())graph.Destroy(); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject NewObject(string name, Scene scene) { var result = new GameObject(name); SceneManager.MoveGameObjectToScene(result, scene); return result; }
        private static void Capture(Camera camera, List<GameObject> actors, List<PlayableGraph> graphs, int start, int count, float size, string path)
        {
            var temporary = new List<GameObject>(); var meshes = new List<Mesh>();
            try
            {
                for (int i = 0; i < actors.Count; i++)
                {
                    var actor = actors[i]; actor.SetActive(i >= start && i < start + count); if (!actor.activeSelf) continue;
                    actor.transform.position = new Vector3((i - start - (count - 1) * .5f) * 1.45f, 0, 0);
                    graphs[i].Evaluate(0);
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh = new Mesh(); meshes.Add(mesh); skin.BakeMesh(mesh, false);
                        var baked = NewObject("Evaluated crowd skin", camera.scene); temporary.Add(baked); baked.transform.SetParent(skin.transform, false);
                        baked.AddComponent<MeshFilter>().sharedMesh = mesh; baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
                    }
                }
                camera.transform.position = new Vector3(0, 1.55f, 10); camera.transform.LookAt(new Vector3(0,1.15f,0)); camera.orthographicSize = size;
                TravellerSceneChecks.Capture(camera, path);
            }
            finally
            {
                foreach (var item in temporary) Object.DestroyImmediate(item); foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
                foreach (var actor in actors) foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.enabled = true;
            }
        }
    }
}
