using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem.Editor
{
    public static class NpcDecisionProduction
    {
        public const string Root = "Assets/_Project/Dialogue/Decisions";
        public const string Docs = "Documentation/Dialogue/DecisionRuntime20261102";
        public const string BuildOutput = "Builds/NpcDecision20261102/Traveller.app";
        private const string PrepareRequest = "Traveller.NpcDecisions.Prepare";
        private const string BuildRequest = "Traveller.NpcDecisions.Build";
        [InitializeOnLoadMethod] private static void Resume() { EditorApplication.update -= Pending; EditorApplication.update += Pending; }
        [MenuItem("Tools/Three Body Traveller/Dialogue/Prepare NPC Decisions")]
        public static void QueuePrepare() { EraGuardProduction.GuardProject(); SessionState.SetBool(PrepareRequest, true); Resume(); }
        public static void QueueBuild() { EraGuardProduction.GuardProject(); SessionState.SetBool(BuildRequest, true); Resume(); }
        private static void Pending()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer || EditorApplication.isPlaying) return;
            if (SessionState.GetBool(PrepareRequest, false))
            {
                SessionState.SetBool(PrepareRequest, false);
                try { Prepare(); } catch (Exception error) { Failure("integration-error.txt", error); }
            }
            if (SessionState.GetBool(BuildRequest, false))
            {
                SessionState.SetBool(BuildRequest, false);
                try { Build(); } catch (Exception error) { Failure("build-error.txt", error); }
            }
        }
        private static void Failure(string name, Exception error)
        { Directory.CreateDirectory(Docs + "/Unity"); File.WriteAllText(Docs + "/Unity/" + name, error.ToString()); Debug.LogException(error); }
        public static void Prepare()
        {
            EraGuardProduction.GuardProject();
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene first.");
            Directory.CreateDirectory(Root); Directory.CreateDirectory(Docs + "/Unity"); AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<NpcDecisionSettings>(Root + "/Runtime.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<NpcDecisionSettings>(); AssetDatabase.CreateAsset(settings, Root + "/Runtime.asset"); }
            var previous = EditorSceneManager.GetSceneManagerSetup(); var records = new List<string>();
            try
            {
                for (int index = 1; index <= 2; index++)
                {
                    string scenePath = "Assets/_Project/Scenes/Traveller/" + TravellerStage.SceneNames[index] + ".unity";
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    var stage = UnityEngine.Object.FindFirstObjectByType<TravellerStage>();
                    var controller = stage.GetComponent<NpcDecisionController>(); if (controller == null) controller = stage.gameObject.AddComponent<NpcDecisionController>();
                    controller.settings = settings;
                    var cast = stage.GetComponent<TravellerCastDirector>();
                    var authority = index == 1 ? cast.king : cast.pope;
                    Vector3 center = authority.transform.position + Vector3.back * 45;
                    var bounds = new Bounds(center, new Vector3(220, 70, 280));
                    var markups = new List<NavMeshBuildMarkup>();
                    foreach (var actor in stage.GetComponentsInChildren<TravellerActor>(true)) markups.Add(new NavMeshBuildMarkup { root = actor.transform, ignoreFromBuild = true });
                    markups.Add(new NavMeshBuildMarkup { root = stage.player.transform, ignoreFromBuild = true });
                    var sources = new List<NavMeshBuildSource>();
                    NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
                    var buildSettings = NavMesh.GetSettingsByIndex(0);
                    buildSettings.agentRadius = .34f; buildSettings.agentHeight = 1.8f; buildSettings.agentClimb = .5f; buildSettings.agentSlope = 45;
                    buildSettings.overrideVoxelSize = true; buildSettings.voxelSize = .12f;
                    var data = NavMeshBuilder.BuildNavMeshData(buildSettings, sources, bounds, Vector3.zero, Quaternion.identity);
                    if (data == null) throw new InvalidOperationException("No navigation produced for " + scenePath);
                    string assetPath = Root + "/" + TravellerStage.SceneNames[index] + "_Navigation.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
                    if (existing == null) { data.name = TravellerStage.SceneNames[index] + "_Navigation"; AssetDatabase.CreateAsset(data, assetPath); existing = data; }
                    else { EditorUtility.CopySerialized(data, existing); UnityEngine.Object.DestroyImmediate(data); }
                    EditorUtility.SetDirty(existing); controller.navigation = existing;
                    var feedback = stage.player.viewCamera.GetComponent<SunDamageFeedback>();
                    if (feedback == null) feedback = stage.player.viewCamera.gameObject.AddComponent<SunDamageFeedback>();
                    feedback.player = stage.player; feedback.combat = controller;
                    feedback.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Shaders/SunDamage.shader");
                    int sampled = 0, reachable = 0;
                    var instance = NavMesh.AddNavMeshData(existing);
                    try
                    {
                        NavMeshHit destination; bool target = NavMesh.SamplePosition(index == 1 ? new Vector3(0, 4.01f, 20) : stage.profile.moments[0].viewPosition - Vector3.up * stage.player.eyeHeight, out destination, 8, NavMesh.AllAreas);
                        foreach (var gear in stage.GetComponentsInChildren<EraGuardEquipment>())
                        {
                            NavMeshHit start;
                            if (!NavMesh.SamplePosition(gear.transform.position, out start, 2, NavMesh.AllAreas)) continue;
                            sampled++; var path = new NavMeshPath();
                            if (target && NavMesh.CalculatePath(start.position, destination.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) reachable++;
                        }
                    }
                    finally { instance.Remove(); }
                    if (sampled == 0 || reachable == 0) throw new InvalidOperationException("Guards cannot reach the audience in " + scenePath);
                    EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    records.Add("{\"scene\":\"" + scenePath + "\",\"sources\":" + sources.Count + ",\"sampledGuards\":" + sampled + ",\"reachableGuards\":" + reachable + "}");
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText(Docs + "/Unity/integration.json", "{\"success\":true,\"scenes\":[" + string.Join(",", records) + "]}");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        private static void Build()
        {
            EraGuardProduction.GuardProject();
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene first.");
            Directory.CreateDirectory(Path.GetDirectoryName(BuildOutput)); Directory.CreateDirectory(Docs + "/Unity");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = TravellerStage.SceneNames.Select(n => "Assets/_Project/Scenes/Traveller/" + n + ".unity").ToArray(),
                locationPathName = BuildOutput, target = BuildTarget.StandaloneOSX, options = BuildOptions.Development | BuildOptions.CompressWithLz4 | BuildOptions.DetailedBuildReport });
            var summary = result.summary;
            File.WriteAllText(Docs + "/Unity/build.json", "{\"result\":\"" + summary.result + "\",\"errors\":" + summary.totalErrors + ",\"warnings\":" + summary.totalWarnings + ",\"seconds\":" + summary.totalTime.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
        }
    }
}
