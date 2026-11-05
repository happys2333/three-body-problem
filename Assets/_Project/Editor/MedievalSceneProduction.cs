using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class MedievalSceneProduction
    {
        private const string Models = "Assets/_Project/Art/Medieval20261106/Prefabs/";
        [Serializable] private sealed class Review
        {
            public int portals, piers, houses, guards;
            public bool doorwayClear, threeSunsVisible, guardsCanExit, passed;
        }

        public static void ApplyAndReview()
        {
            string output = Environment.GetEnvironmentVariable("TRAVELLER_MEDIEVAL_REVIEW_OUTPUT")
                ?? "Builds/MedievalProduction20261106/Scene";
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Traveller/03_MedievalTribunal.unity");
            var stage = Object.FindFirstObjectByType<TravellerStage>();
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var review = new Review();
            var front = all.Single(t => t.name == "Gothic front gable");
            foreach (var old in front.GetComponentsInChildren<Transform>(true).Where(t =>
                         t.name == "Recessed portal mouldings" || t.name == "Open oak portal leaf"))
                old.gameObject.SetActive(false);
            Place(front, "CathedralPortal", front.position + Vector3.back * .4f, Quaternion.identity, new Vector3(.9f, .9f, 1));
            var backing = front.Find("Portal backing masonry");
            if (backing == null)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Portal backing masonry"; backing = block.transform;
                backing.SetParent(front, false);
                block.GetComponent<MeshRenderer>().sharedMaterial = front.Find("Open portal and rose aperture").GetComponent<MeshRenderer>().sharedMaterial;
                block.isStatic = true;
            }
            backing.localPosition = new Vector3(0, 13.425f, 0);
            backing.localScale = new Vector3(9.8f, 4.4f, 2);
            review.portals++;
            foreach (var anchor in all.Where(t => t != null && t.name == "Nave pillar"))
            {
                DisablePrimitive(anchor);
                Place(anchor, "ClusterPier", new Vector3(anchor.position.x, 0, anchor.position.z), Quaternion.identity, Vector3.one);
                review.piers++;
            }
            foreach (var old in all.Where(t => t != null && (t.name == "Column plinth" || t.name == "Capital"))) old.gameObject.SetActive(false);
            var houses = all.Where(t => t != null && t.name == "Medieval town building").OrderByDescending(t => t.position.z).ToArray();
            foreach (int index in new[] { 0, 1, 4, 5 })
            {
                var anchor = houses[index]; DisablePrimitive(anchor);
                Place(anchor, "StoneGableHouse", new Vector3(anchor.position.x, 0, anchor.position.z),
                    Quaternion.Euler(0, anchor.position.x < 0 ? 90 : -90, 0), Vector3.one);
                review.houses++;
            }
            Physics.SyncTransforms();
            BakeNavigation(stage);
            var start = new Vector3(0, 0, -49); var end = new Vector3(0, 0, 22);
            Vector3 delta = end - start;
            review.doorwayClear = Physics.CapsuleCastAll(start + Vector3.up * .42f, start + Vector3.up * 1.5f,
                .28f, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .All(hit => hit.transform.IsChildOf(stage.player.transform));
            var sky = stage.profile.moments[1]; var eye = start + Vector3.up * 1.76f;
            review.threeSunsVisible = new[] { sky.sunA, sky.sunB, sky.sunC }.All(direction =>
                Physics.RaycastAll(eye, direction.normalized, 200, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    .All(hit => hit.transform.IsChildOf(stage.player.transform)));
            var instance = NavMesh.AddNavMeshData(stage.GetComponent<NpcDecisionController>().navigation);
            try
            {
                var guards = stage.GetComponentsInChildren<NpcIdentity>().Where(n => n.persona != null && n.persona.characterId == "church_guard").ToArray();
                review.guards = guards.Length;
                NavMeshHit destination = default;
                review.guardsCanExit = guards.Length > 0 && NavMesh.SamplePosition(start, out destination, 5, NavMesh.AllAreas);
                if (review.guardsCanExit)
                    foreach (var guard in guards)
                    {
                        var path = new NavMeshPath();
                        review.guardsCanExit &= NavMesh.SamplePosition(guard.transform.position, out var origin, 5, NavMesh.AllAreas)
                            && NavMesh.CalculatePath(origin.position, destination.position, NavMesh.AllAreas, path)
                            && path.status == NavMeshPathStatus.PathComplete;
                    }
            }
            finally { NavMesh.RemoveNavMeshData(instance); }
            review.passed = review.portals == 1 && review.piers == 10 && review.houses == 4 &&
                review.doorwayClear && review.threeSunsVisible && review.guardsCanExit;
            File.WriteAllText(output + "/scene-checks.json", JsonUtility.ToJson(review, true));
            if (!review.passed) throw new InvalidDataException("Medieval scene geometry review failed; inspect scene-checks.json.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Capture(stage, output);
        }

        private static void DisablePrimitive(Transform anchor)
        {
            var renderer = anchor.GetComponent<MeshRenderer>(); if (renderer != null) renderer.enabled = false;
            var filter = anchor.GetComponent<MeshFilter>(); if (filter != null) filter.sharedMesh = null;
            foreach (var collider in anchor.GetComponents<Collider>()) collider.enabled = false;
        }

        private static void Place(Transform anchor, string id, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            foreach (var child in anchor.Cast<Transform>().Where(t => t.name == "Generated medieval visual" ||
                         anchor.name == "Medieval town building" && t.GetComponent<LODGroup>() != null).ToArray())
                Object.DestroyImmediate(child.gameObject);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + id + ".prefab");
            if (source == null) throw new FileNotFoundException("Prepare the medieval model first: " + id);
            var value = (GameObject)PrefabUtility.InstantiatePrefab(source, anchor.gameObject.scene);
            value.name = "Generated medieval visual";
            var t = value.transform; t.SetParent(anchor, true); t.SetPositionAndRotation(position, rotation); t.localScale = Vector3.one;
            var inherited = t.lossyScale;
            t.localScale = new Vector3(scale.x / inherited.x, scale.y / inherited.y, scale.z / inherited.z);
        }

        private static void BakeNavigation(TravellerStage stage)
        {
            var cast = stage.GetComponent<TravellerCastDirector>();
            var bounds = new Bounds(cast.pope.transform.position + Vector3.back * 45, new Vector3(220, 70, 280));
            var markups = stage.GetComponentsInChildren<TravellerActor>(true).Select(actor =>
                new NavMeshBuildMarkup { root = actor.transform, ignoreFromBuild = true }).ToList();
            markups.Add(new NavMeshBuildMarkup { root = stage.player.transform, ignoreFromBuild = true });
            var sources = new System.Collections.Generic.List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .34f; settings.agentHeight = 1.8f; settings.agentClimb = .5f; settings.agentSlope = 45;
            settings.overrideVoxelSize = true; settings.voxelSize = .12f;
            var generated = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (generated == null) throw new InvalidDataException("No medieval navigation produced.");
            var controller = stage.GetComponent<NpcDecisionController>();
            EditorUtility.CopySerialized(generated, controller.navigation);
            Object.DestroyImmediate(generated); EditorUtility.SetDirty(controller.navigation);
        }

        private static void Capture(TravellerStage stage, string output)
        {
            var actors = stage.GetComponentsInChildren<TravellerActor>().Select(a => a.gameObject).ToArray();
            foreach (var actor in actors) actor.SetActive(false);
            var root = new GameObject("Architecture review camera");
            SceneManager.MoveGameObjectToScene(root, stage.gameObject.scene);
            var camera = root.AddComponent<Camera>(); camera.scene = stage.gameObject.scene;
            camera.fieldOfView = 65; camera.nearClipPlane = .1f; camera.farClipPlane = 500;
            try
            {
                var views = new[] {
                    ("Portal", new Vector3(0, 2.1f, -45), new Vector3(0, 7, -27)),
                    ("Nave", new Vector3(0, 1.8f, -20), new Vector3(0, 5, 30)),
                    ("Street", new Vector3(0, 2.1f, -76), new Vector3(-35, 5, -84)) };
                foreach (var view in views)
                {
                    camera.transform.position = view.Item2; camera.transform.LookAt(view.Item3);
                    TravellerSceneChecks.Capture(camera, output + "/" + view.Item1 + ".jpg");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var actor in actors) actor.SetActive(true);
            }
        }
    }
}
