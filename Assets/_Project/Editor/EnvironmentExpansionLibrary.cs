using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class EnvironmentExpansionLibrary
    {
        public const string Root = "Assets/_Project/Art/EnvironmentExpansionReady";
        public const string Work = "ArtSource/Environment/Prepared/2026-10-31";
        public const string Docs = "Documentation/EnvironmentProduction/ExpansionReady";
        private static readonly string[] Surfaces = { "Rock", "Masonry", "Earth", "Wood", "Thatch", "RoofTile", "Bark", "OriginalSupplies" };
        [Serializable] public class Processing
        {
            public string id, sourceId, sourcePrefab, usage;
            public string[] modifications;
            public bool charred, hasInterior;
            public float openingWidth, openingHeight;
        }

        [MenuItem("Tools/Three Body Traveller/Assets/Import Environment Expansion")]
        public static EnvironmentProduction.ReadyReport ImportAvailable()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            foreach (var folder in new[] { Root + "/Meshes", Root + "/Materials", Root + "/Prefabs", Docs }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var report = new EnvironmentProduction.ReadyReport();
            foreach (string path in Directory.GetFiles(Work + "/Output", "processing.json", SearchOption.AllDirectories).OrderBy(p => p))
            {
                var data = JsonUtility.FromJson<Processing>(File.ReadAllText(path));
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject(data.id); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene); root.isStatic = true;
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(data.sourcePrefab);
                    if (source == null) throw new FileNotFoundException(data.sourcePrefab);
                    var sourceMaterial = source.GetComponent<LODGroup>().GetLODs()[0].renderers[0].sharedMaterial;
                    var lods = new LOD[3]; var counts = new int[3]; var used = new HashSet<string>(); var materials = new Dictionary<string, Material>();
                    Mesh collision = null;
                    for (int level = 0; level < 3; level++)
                    {
                        var meshMaterials = new List<Material>();
                        var mesh = ReadMesh(Path.GetDirectoryName(path) + "/LOD" + level + ".meshbin", data, level, sourceMaterial, meshMaterials, used, materials);
                        var child = new GameObject("LOD " + level); child.transform.SetParent(root.transform, false); child.isStatic = true;
                        child.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterials = meshMaterials.ToArray();
                        renderer.shadowCastingMode = level < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        lods[level] = new LOD(level == 0 ? .42f : level == 1 ? .12f : .006f, new Renderer[] { renderer });
                        counts[level] = mesh.triangles.Length / 3;
                        if (level == (data.openingWidth > 0 ? 0 : data.id == "NaturalRidgeV2" ? 2 : 1)) collision = mesh;
                    }
                    var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                    var solid = new GameObject("Collision"); solid.transform.SetParent(root.transform, false); solid.isStatic = true;
                    var collider = solid.AddComponent<MeshCollider>(); collider.sharedMesh = collision; collider.convex = false;
                    collider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.UseFastMidphase;
                    var bounds = lods[0].renderers[0].bounds;
                    if (data.id.EndsWith("Brazier", StringComparison.Ordinal))
                    {
                        var torch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
                        foreach (string childName in new[] { "Living torch flame", "Torch sparks", "Torch light" })
                        {
                            var template = torch.GetComponentsInChildren<Transform>(true).First(t => t.name == childName);
                            var fire = Object.Instantiate(template.gameObject, root.transform); fire.name = childName;
                            fire.transform.localPosition = Vector3.up * bounds.size.y * (childName == "Torch light" ? .78f : 1.04f); fire.transform.localScale = template.localScale * 1.25f;
                            var light = fire.GetComponent<Light>(); if (light != null) { light.range = 6; light.intensity = 1.4f; }
                        }
                    }
                    if (data.id == "BronzeWallTorchBracket")
                    {
                        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
                        var upperCup = lods[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.vertices.Where(v => v.y > bounds.size.y * .78f && v.x < 0).ToArray();
                        var flameBase = new Vector3(upperCup.Average(v => v.x), bounds.size.y + .01f, upperCup.Average(v => v.z));
                        foreach (string childName in new[] { "Living torch flame", "Torch sparks", "Torch light" })
                        {
                            var original = template.GetComponentsInChildren<Transform>(true).First(t => t.name == childName);
                            var fire = Object.Instantiate(original.gameObject, root.transform); fire.name = childName;
                            fire.transform.localPosition = flameBase; fire.transform.localScale = original.localScale * .55f;
                            var flameRenderer = fire.GetComponent<MeshRenderer>();
                            if (flameRenderer != null) fire.transform.localPosition += Vector3.up * (flameBase.y - flameRenderer.bounds.min.y);
                            var light = fire.GetComponent<Light>(); if (light != null) { light.range = 4; light.intensity = .9f; }
                        }
                    }
                    if (data.hasInterior)
                    {
                        foreach (var point in new[] { new Vector3(-5.8f, 2.6f, 0), new Vector3(5.8f, 2.6f, 0), new Vector3(0, 2.6f, 15) })
                        {
                            var lamp = new GameObject("Palace interior light").AddComponent<Light>(); lamp.transform.SetParent(root.transform, false);
                            lamp.transform.localPosition = point; lamp.type = LightType.Point; lamp.range = 13; lamp.intensity = 2.2f;
                            lamp.renderMode = LightRenderMode.ForceVertex;
                            lamp.color = new Color(1, .63f, .28f); lamp.shadows = LightShadows.None;
                        }
                        var chamber = new GameObject("Chamber centre"); chamber.transform.SetParent(root.transform, false); chamber.transform.localPosition = new Vector3(0, .25f, 0);
                        var dais = new GameObject("Royal dais anchor"); dais.transform.SetParent(root.transform, false); dais.transform.localPosition = new Vector3(0, .75f, -5);
                    }
                    if (data.id.Contains("Waterskin") || data.id == "TravellerLeatherFlask" || data.id == "ShangOracleBone")
                    {
                        Object.DestroyImmediate(collider); var box = solid.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size; box.isTrigger = true;
                        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
                    }
                    var grip = new GameObject("Placement anchor"); grip.transform.SetParent(root.transform, false); grip.transform.localPosition = Vector3.up * bounds.size.y * .8f;
                    string prefab = Root + "/Prefabs/" + data.id + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, prefab);
                    bool passed = counts[0] > counts[1] && counts[1] > counts[2] && counts[2] > 0 && Mathf.Abs(bounds.min.y) < .02f;
                    report.assets.Add(new EnvironmentProduction.ReadyAsset { id = data.id, prefab = prefab, sourcePrefab = data.sourcePrefab,
                        usage = data.usage, modifications = data.modifications, trianglesByLod = counts,
                        collisionTriangles = data.id.Contains("Waterskin") || data.id == "TravellerLeatherFlask" || data.id == "ShangOracleBone" ? 0 : collision.triangles.Length / 3, width = bounds.size.x, height = bounds.size.y, depth = bounds.size.z,
                        materialsUsed = used.OrderBy(s => s).ToArray(), passed = passed });
                    File.WriteAllText(Docs + "/ready-validation.json", JsonUtility.ToJson(report, true));
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            AssetDatabase.SaveAssets();
            if (report.assets.Any(a => !a.passed)) throw new InvalidDataException("Expansion import validation failed.");
            return report;
        }

        private static Mesh ReadMesh(string path, Processing data, int level, Material original, List<Material> materials, HashSet<string> used, Dictionary<string, Material> cache)
        {
            Mesh mesh;
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != "TBM1") throw new InvalidDataException(path);
                int n = reader.ReadInt32(), indicesCount = reader.ReadInt32(), slots = reader.ReadInt32();
                var vertices = new Vector3[n]; var normals = new Vector3[n]; var uv = new Vector2[n];
                for (int i = 0; i < n; i++) { float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle(); vertices[i] = new Vector3(x, z, -y); }
                for (int i = 0; i < n; i++) { float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle(); normals[i] = new Vector3(x, z, -y).normalized; }
                for (int i = 0; i < n; i++) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                var indices = new int[indicesCount]; for (int i = 0; i < indicesCount; i++) indices[i] = reader.ReadInt32();
                var groups = new List<int>[slots]; for (int i = 0; i < slots; i++) groups[i] = new List<int>();
                for (int i = 0; i < indicesCount; i += 3)
                {
                    int slot = reader.ReadInt32(); if (slot < 0 || slot >= slots) throw new InvalidDataException(path);
                    if (Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]).sqrMagnitude < 1e-16f) continue;
                    groups[slot].Add(indices[i]); groups[slot].Add(indices[i + 1]); groups[slot].Add(indices[i + 2]);
                }
                var fallbackNormals = new Vector3[n];
                foreach (var group in groups) for (int i = 0; i < group.Count; i += 3)
                {
                    var normal = Vector3.Cross(vertices[group[i + 1]] - vertices[group[i]], vertices[group[i + 2]] - vertices[group[i]]).normalized;
                    for (int v = 0; v < 3; v++) fallbackNormals[group[i + v]] += normal;
                }
                for (int i = 0; i < n; i++) if (normals[i].sqrMagnitude < .01f) normals[i] = fallbackNormals[i].sqrMagnitude > .01f ? fallbackNormals[i].normalized : Vector3.up;
                mesh = new Mesh { name = data.id + "_Ready_LOD" + level, indexFormat = IndexFormat.UInt32, vertices = vertices, normals = normals, uv = uv };
                var active = Enumerable.Range(0, slots).Where(i => groups[i].Count > 0).ToArray(); mesh.subMeshCount = active.Length;
                for (int i = 0; i < active.Length; i++)
                {
                    mesh.SetTriangles(groups[active[i]], i); string category = Surfaces[active[i]]; used.Add(category);
                    if (!cache.TryGetValue(category, out var material))
                    {
                        if (category == "OriginalSupplies" || data.charred)
                        {
                            string materialPath = Root + "/Materials/" + data.id + "_" + category + ".mat";
                            var template = category == "OriginalSupplies" ? original : AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/" + category + ".mat");
                            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            if (material == null) { material = new Material(template); AssetDatabase.CreateAsset(material, materialPath); }
                            else material.CopyPropertiesFromMaterial(template);
                            if (data.charred) material.color = new Color(.14f, .13f, .12f);
                            else if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", .65f);
                            if (data.id == "TravellerLeatherFlask" && category == "OriginalSupplies")
                            {
                                material.mainTextureScale = new Vector2(.12f, .12f); material.mainTextureOffset = new Vector2(.43f, .79f);
                            }
                            material.enableInstancing = true; EditorUtility.SetDirty(material);
                        }
                        else material = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/" + category + ".mat");
                        if (material == null || !material.shader.isSupported || material.mainTexture == null) throw new InvalidDataException("Missing surface: " + category);
                        cache[category] = material;
                    }
                    materials.Add(material);
                }
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                var tangents = mesh.tangents;
                for (int i = 0; i < tangents.Length; i++) if (new Vector3(tangents[i].x, tangents[i].y, tangents[i].z).sqrMagnitude < .01f)
                {
                    var axis = Mathf.Abs(normals[i].y) > .95f ? Vector3.right : Vector3.up;
                    var tangent = Vector3.Cross(axis, normals[i]).normalized; tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, 1);
                }
                mesh.tangents = tangents;
            }
            string output = Root + "/Meshes/" + mesh.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(output);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, output); return mesh; }
            // Mesh native GPU buffers must be updated as well as serialized fields.
            saved.Clear(); saved.indexFormat = mesh.indexFormat;
            saved.vertices = mesh.vertices; saved.normals = mesh.normals; saved.uv = mesh.uv; saved.tangents = mesh.tangents;
            saved.subMeshCount = mesh.subMeshCount;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) saved.SetTriangles(mesh.GetTriangles(sub), sub);
            saved.bounds = mesh.bounds; saved.UploadMeshData(false); EditorUtility.SetDirty(saved);
            Object.DestroyImmediate(mesh); return saved;
        }

        [MenuItem("Tools/Three Body Traveller/Assets/Capture Environment Expansion")]
        public static string[] Capture()
        {
            return HunyuanEnvironmentLibrary.CaptureManifest(Docs + "/ready-validation.json", Docs + "/Previews");
        }
    }
}
