using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    // Incremental city dressing: the palace, story actors and revival system are
    // deliberately outside this authoring entry point.
    public static class ShangCivicSceneProduction
    {
        private const string ScenePath = "Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity";
        private const string DistrictName = "10 Civic life and working districts";

        [MenuItem("Tools/Three Body Traveller/Capital/Apply Prepared Civic Districts")]
        public static void Apply()
        {
            EraGuardProduction.GuardProject();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Open the saved Chaoge scene first.");
            string backup = "Logs/civic-backups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup); File.Copy(ScenePath, backup + "/02_ChaogeCourt.unity", true);
            var stage = Object.FindFirstObjectByType<TravellerStage>();
            var world = stage.GetComponentsInChildren<Transform>(true).First(t => t.name == "Shang Capital");
            var old = world.Find(DistrictName); if (old != null) Object.DestroyImmediate(old.gameObject);
            var district = new GameObject(DistrictName).transform; district.SetParent(world, false);
            int buildings = 0, props = 0, wardHouse = 0, wardShed = 0;
            var targets = world.GetComponentsInChildren<Transform>(true).Where(t =>
                t.name == "Market courtyard house" || t.name == "Residential timber-and-earth house" ||
                t.name == "Residential rear shed" || t.name == "Royal granary" || t.name == "Bronze casting workshop").ToArray();
            foreach (var target in targets)
            {
                bool shed = target.name == "Residential rear shed", granary = target.name == "Royal granary", workshop = target.name == "Bronze casting workshop";
                string id = granary ? "ShangGranary" : workshop ? "ShangWorkshop" : shed || buildings % 3 == 1 ? "ShangDwelling" : "ShangCourtyardHall";
                var prefab = Prefab(id); if (prefab == null) continue;
                if (target.name == "Residential timber-and-earth house" || shed)
                {
                    // The old five-column grid intersected the ancestral hall
                    // terrace and its side halls. Keep the ward west of them.
                    int index = shed ? wardShed++ : wardHouse++;
                    target.position = new Vector3(-196 + index % 3 * 18, 0, -30 + index / 3 * 25)
                        + (shed ? new Vector3(5, 0, 13) : Vector3.zero);
                }
                foreach (var child in target.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                foreach (var collider in target.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                var oldLod = target.GetComponent<LODGroup>(); if (oldLod != null) Object.DestroyImmediate(oldLod);
                var building = Instantiate(prefab, target, Vector3.zero, target.name.StartsWith("Residential", StringComparison.Ordinal) ? 180 : 0);
                building.name = "Prepared civic building";
                CloseGables(building, id);
                var b = HighBounds(building);
                float height = granary ? 6.8f : workshop ? 6 : shed ? 3 : target.name.StartsWith("Market", StringComparison.Ordinal) ? 5.5f : 4.8f;
                float width = granary ? 18 : workshop ? 24 : shed ? 9 : 17;
                float depth = granary ? 23 : workshop ? 22 : shed ? 8 : 14;
                building.localScale *= Mathf.Min(height / b.size.y, width / b.size.x, depth / b.size.z);
                buildings++;
            }
            foreach (var target in world.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Grain storage jar" || t.name == "Clay casting furnace").ToArray())
            {
                var prefab = Prefab(target.name == "Grain storage jar" ? "ShangStorageJar" : "ShangCastingFurnace");
                if (prefab == null) continue;
                foreach (var child in target.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                foreach (var component in target.GetComponents<Component>())
                    if (component is Renderer || component is MeshFilter || component is Collider) Object.DestroyImmediate(component);
                target.localScale = Vector3.one; var p = target.position; p.y = 0; target.position = p;
                Instantiate(prefab, target, Vector3.zero, 0); props++;
            }
            if (Prefab("ShangMarketStall") != null)
                foreach (var t in world.GetComponentsInChildren<Transform>(true).Where(t =>
                    t.name == "Market stall post" || t.name == "Market woven awning" ||
                    t.name == "Market goods bench" || t.name == "Earthenware trade jar").ToArray())
                    Object.DestroyImmediate(t.gameObject);
            foreach (float side in new[] { -1f, 1f })
            {
                Place("ShangWatchtower", new Vector3(side * 38, 0, -196), 180, district);
                Place("ShangWatchtower", new Vector3(side * 203, 0, 105), side < 0 ? 90 : -90, district);
                for (int row = 0; row < 3; row++)
                {
                    float z = -164 + row * 40;
                    var stall = Place("ShangMarketStall", new Vector3(side * 25, 0, z), 0, district);
                    if (stall != null)
                    {
                        var b = HighBounds(stall);
                        stall.localScale *= Mathf.Min(5 / b.size.x, 3.2f / b.size.y);
                    }
                    Place("ShangMarketGoods", new Vector3(side * 28.3f, 0, z - 1.3f), row * 80, district, .55f);
                    Place("ShangGrainBasket", new Vector3(side * 28.6f, 0, z + 2.1f), row * 71, district);
                    Place("ShangStorageJar", new Vector3(side * 28.6f, 0, z - 2.1f), 0, district, .65f);
                }
                Place("ShangWoodCart", new Vector3(side * 47, 0, -151), side * 20, district);
            }
            for (int i = 0; i < 5; i++)
            {
                float z = -150 + i * 38;
                Place("ShangWoodCart", new Vector3(156, 0, z - 8), 35 + i * 18, district);
            }
            ReplaceLooseBronzeVessels(world);
            ReplaceBatchedBronzeVessels(world, district);
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(-88 + i % 4 * 28, 0, 149 + i / 4 * 39);
                Place("ShangGrainBasket", p, 23 * i, district);
                if (i % 2 == 0) Place("ShangWoodCart", p + new Vector3(7, 0, -3), 90, district);
            }
            foreach (var p in new[] { new Vector3(-208, 0, 8), new Vector3(-208, 0, 83), new Vector3(67, 0, -165) })
                Place("ShangVillageWell", p, 0, district);
            var bridge = Place("ShangWoodFootbridge", new Vector3(154, 0, 8), 90, district);
            if (bridge != null) PrepareBridge(bridge);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(ShangCivicAssetProduction.Docs + "/scene-integration.json", JsonUtility.ToJson(new Integration {
                scene = ScenePath, buildings = buildings, replacedProps = props,
                addedInstances = district.GetComponentsInChildren<LODGroup>().Length,
                backup = backup, palaceAndRevivalRebuilt = false }, true));
        }

        [Serializable] private class Integration { public string scene, backup; public int buildings, replacedProps, addedInstances; public bool palaceAndRevivalRebuilt; }

        private static GameObject Prefab(string id)
        {
            foreach (string root in new[] { ShangCivicAssetProduction.Root, "Assets/_Project/Art/EnvironmentExpansionReady", "Assets/_Project/Art/EnvironmentReady" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/" + id + ".prefab");
                if (prefab != null) return prefab;
            }
            return null;
        }
        private static Transform Instantiate(GameObject prefab, Transform parent, Vector3 local, float yaw)
        {
            var value = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            value.transform.SetParent(parent, false); value.transform.localPosition = local;
            value.transform.localRotation = Quaternion.Euler(0, yaw, 0); return value.transform;
        }
        private static Transform Place(string id, Vector3 position, float yaw, Transform parent, float scale = 1)
        {
            var prefab = Prefab(id); if (prefab == null) return null;
            var value = Instantiate(prefab, parent, position, yaw); value.localScale *= scale; return value;
        }
        private static Bounds HighBounds(Transform root)
        {
            var renderers = root.GetComponent<LODGroup>().GetLODs()[0].renderers;
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        private static void ReplaceLooseBronzeVessels(Transform world)
        {
            var prefab = Prefab("ShangBronzeDing"); if (prefab == null) return;
            // Palace masonry has combined geometry; replace only independent
            // workshop/temple placeholders, leaving the palace assembly intact.
            foreach (var marker in world.GetComponentsInChildren<Transform>(true).Where(t =>
                t.name == "Bronze ritual vessel" && t.GetComponent<MeshRenderer>() != null).ToArray())
            {
                float scale = marker.GetComponent<MeshFilter>().sharedMesh.bounds.size.y / .9f;
                Vector3 floor = marker.position - Vector3.up * scale * .75f;
                foreach (var part in marker.parent.Cast<Transform>().Where(t =>
                    (t.name == "Ding tripod leg" || t.name == "Ding upright handle") &&
                    Vector3.Distance(t.position, floor) < scale * 2.6f).ToArray()) Object.DestroyImmediate(part.gameObject);
                foreach (var component in marker.GetComponents<Component>())
                    if (component is Renderer || component is MeshFilter || component is Collider) Object.DestroyImmediate(component);
                var vessel = Instantiate(prefab, marker, new Vector3(0, -.75f * scale, 0), 0);
                vessel.localScale *= 2.05f * scale / HighBounds(vessel).size.y;
            }
        }
        private static void CloseGables(Transform building, string id)
        {
            string path = ShangCivicAssetProduction.Root + "/Meshes/" + id + "_ClosedGables.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var group = building.GetComponent<LODGroup>();
            if (mesh == null)
            {
                var points = new List<Vector3>();
                foreach (var renderer in group.GetLODs()[0].renderers)
                {
                    var source = renderer.GetComponent<MeshFilter>().sharedMesh;
                    var materials = renderer.sharedMaterials; var vertices = source.vertices;
                    for (int slot = 0; slot < source.subMeshCount; slot++)
                        if (slot < materials.Length && materials[slot] != null && materials[slot].name.Contains("Thatch"))
                            foreach (int index in source.GetTriangles(slot).Distinct())
                                points.Add(building.InverseTransformPoint(renderer.transform.TransformPoint(vertices[index])));
                }
                if (points.Count < 3) throw new InvalidDataException("Missing thatch roof for gables: " + id);
                var roof = new Bounds(points[0], Vector3.zero); foreach (var p in points) roof.Encapsulate(p);
                var v = new List<Vector3>(); var indices = new List<int>(); var uv = new List<Vector2>();
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = roof.center.x + side * roof.extents.x * .91f;
                    var corners = new[] {
                        new Vector3(x - .055f, roof.min.y, roof.center.z - roof.extents.z * .92f),
                        new Vector3(x - .055f, roof.min.y, roof.center.z + roof.extents.z * .92f),
                        new Vector3(x - .055f, roof.max.y - .13f, roof.center.z),
                        new Vector3(x + .055f, roof.min.y, roof.center.z - roof.extents.z * .92f),
                        new Vector3(x + .055f, roof.min.y, roof.center.z + roof.extents.z * .92f),
                        new Vector3(x + .055f, roof.max.y - .13f, roof.center.z) };
                    var center = corners.Aggregate(Vector3.zero, (sum, p) => sum + p) / 6;
                    int[] triangles = { 0,1,2, 3,5,4, 0,3,4, 0,4,1, 1,4,5, 1,5,2, 2,5,3, 2,3,0 };
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        var a = corners[triangles[t]]; var b = corners[triangles[t + 1]]; var c = corners[triangles[t + 2]];
                        if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3 - center) < 0) { var swap = b; b = c; c = swap; }
                        foreach (var p in new[] { a, b, c }) { indices.Add(v.Count); v.Add(p); uv.Add(new Vector2(p.z / 2, p.y / 2)); }
                    }
                }
                mesh = new Mesh { name = id + " closed earthen gables", vertices = v.ToArray(), uv = uv.ToArray(), triangles = indices.ToArray() };
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path);
            }
            var cap = new GameObject("Closed earthen gables", typeof(MeshFilter), typeof(MeshRenderer));
            cap.transform.SetParent(building, false); cap.isStatic = true;
            cap.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = cap.GetComponent<MeshRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/Earth.mat");
            var lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++) lods[i].renderers = lods[i].renderers.Concat(new Renderer[] { r }).ToArray();
            group.SetLODs(lods); group.RecalculateBounds();
        }
        private static void ReplaceBatchedBronzeVessels(Transform world, Transform district)
        {
            var palace = world.GetComponentsInChildren<Transform>(true).First(t => t.name == "Pyramid masonry and enclosed palace");
            var markers = palace.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Bronze ritual vessel").ToArray();
            var regions = new List<Bounds>();
            foreach (var marker in markers)
            {
                var leg = palace.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Ding tripod leg")
                    .OrderBy(t => Vector3.Distance(t.position, marker.position)).First();
                float scale = leg.localScale.y / .43f;
                Vector3 floor = marker.position - Vector3.up * .75f * scale;
                regions.Add(new Bounds(floor + Vector3.up * scale * 1.04f, new Vector3(2.2f, 2.16f, 2.2f) * scale));
                var replacement = Place("ShangBronzeDing", floor, 0, district);
                replacement.name = "Prepared palace bronze vessel";
                replacement.localScale *= 2.05f * scale / HighBounds(replacement).size.y;
            }
            foreach (var filter in palace.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial.name != "CapitalBronze") continue;
                var source = filter.sharedMesh;
                if (source == null || source.subMeshCount != 1) continue;
                // Derived copies keep all original meshes and every structural
                // material untouched. Retain every triangle outside the props.
                if (source.name.EndsWith("_CivicVessels", StringComparison.Ordinal)) continue;
                var vertices = source.vertices; var triangles = source.triangles; var kept = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    bool remove = regions.Any(b => b.Contains(filter.transform.TransformPoint(vertices[triangles[i]])) &&
                        b.Contains(filter.transform.TransformPoint(vertices[triangles[i + 1]])) &&
                        b.Contains(filter.transform.TransformPoint(vertices[triangles[i + 2]])));
                    if (!remove) { kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]); }
                }
                if (kept.Count == triangles.Length) continue;
                string path = ShangCivicAssetProduction.Root + "/Meshes/" + source.name + "_CivicVessels.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null)
                {
                    saved = Object.Instantiate(source); saved.name = source.name + "_CivicVessels";
                    saved.SetTriangles(kept, 0); AssetDatabase.CreateAsset(saved, path);
                }
                filter.sharedMesh = saved;
                foreach (var collider in palace.GetComponentsInChildren<MeshCollider>(true))
                    if (collider.sharedMesh == source) collider.sharedMesh = saved;
            }
        }
        private static void PrepareBridge(Transform bridge)
        {
            // Intersect the actual triangles: plank corners lie at the sides and
            // need not contain any vertex near the middle of the walking lane.
            var renderer = bridge.GetComponent<LODGroup>().GetLODs()[0].renderers[0];
            float deck;
            var probe = renderer.gameObject.AddComponent<MeshCollider>();
            try
            {
                probe.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Physics.SyncTransforms(); RaycastHit hit;
                if (!probe.Raycast(new Ray(bridge.position + Vector3.up * 10, Vector3.down), out hit, 20))
                    throw new InvalidDataException("Bridge has no central deck.");
                deck = hit.point.y - bridge.position.y;
            }
            finally { Object.DestroyImmediate(probe); }
            bridge.position += Vector3.up * (.18f - deck);
            float width = Mathf.Min(2.4f, HighBounds(bridge).size.x * .75f);
            Box(bridge, "Continuous walkable deck", new Vector3(0, deck - .06f, 0), new Vector3(7.6f, .12f, width), true);
            foreach (float side in new[] { -1f, 1f })
                Box(bridge, "Rail collision", new Vector3(0, deck + .52f, side * width * .5f), new Vector3(7.6f, 1.04f, .12f), false);
        }
        private static void Box(Transform parent, string name, Vector3 local, Vector3 size, bool visible)
        {
            var value = visible ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject(name, typeof(BoxCollider));
            value.name = name; value.transform.SetParent(parent, false); value.transform.localPosition = local;
            if (visible)
            {
                value.transform.localScale = size;
                value.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/Wood.mat");
            }
            else value.GetComponent<BoxCollider>().size = size;
        }
    }
}
