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
    public static partial class EnvironmentProduction
    {
        [Serializable] public class Processing { public string id, sourcePrefab; public string[] modifications, materials; public int[] trianglesByLod; public bool staticExterior; }
        [Serializable] public class ReadyAsset
        {
            public string id, prefab, sourcePrefab, usage;
            public string[] modifications, materialsUsed;
            public int[] trianglesByLod;
            public int collisionTriangles;
            public float width, height, depth;
            public bool passed;
        }
        [Serializable] public class ReadyReport { public List<ReadyAsset> assets = new List<ReadyAsset>(); }
        private static readonly Dictionary<string, Material> SurfaceCache = new Dictionary<string, Material>();
        private static readonly string[] SurfaceNames = { "Rock", "Masonry", "Earth", "Wood", "Thatch", "RoofTile", "Bark", "OriginalSupplies" };

        [MenuItem("Tools/Three Body Traveller/Assets/Import Repaired Environment")]
        public static ReadyReport ImportAvailable()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            foreach (var folder in new[] { Root + "/Meshes", Root + "/Materials", Root + "/Prefabs", Docs + "/Previews" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh(); ConfigureReadyPhotoMaps(); SurfaceCache.Clear();
            var sources = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(Work + "/sources.json"));
            var report = File.Exists(Docs + "/ready-validation.json") ? JsonUtility.FromJson<ReadyReport>(File.ReadAllText(Docs + "/ready-validation.json")) : new ReadyReport();
            foreach (var source in sources.assets)
            {
                string folder = Work + "/Output/" + source.id;
                if (!File.Exists(folder + "/processing.json")) continue;
                var data = JsonUtility.FromJson<Processing>(File.ReadAllText(folder + "/processing.json"));
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = NewObject(source.id, scene); root.isStatic = true;
                    var lods = new LOD[3]; var counts = new int[3]; var used = new HashSet<string>();
                    Mesh collision = null;
                    for (int level = 0; level < 3; level++)
                    {
                        var materials = new List<Material>();
                        var mesh = ReadWorkingMesh(folder + "/LOD" + level + ".meshbin", source.id, level, materials, used);
                        var child = NewObject("LOD " + level, scene); child.transform.SetParent(root.transform, false); child.isStatic = true;
                        child.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials.ToArray();
                        renderer.shadowCastingMode = level < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                        lods[level] = new LOD(level == 0 ? .42f : level == 1 ? .12f : .006f, new Renderer[] { renderer });
                        counts[level] = mesh.triangles.Length / 3;
                        if (level == (source.height > 20 ? 2 : 1)) collision = mesh;
                    }
                    var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                    var solid = NewObject("Static collision", scene); solid.transform.SetParent(root.transform, false); solid.isStatic = true;
                    var collider = solid.AddComponent<MeshCollider>(); collider.sharedMesh = collision; collider.convex = false;
                    collider.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.UseFastMidphase;
                    var bounds = lods[0].renderers[0].bounds;
                    string path = Root + "/Prefabs/" + source.id + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    var check = new ReadyAsset { id = source.id, prefab = path, sourcePrefab = source.sourcePrefab,
                        modifications = source.id == "CaravanSupplies" ? new[] { "Original atlas-sensitive geometry and validated UV-preserving LODs retained; leather tint and roughness adjusted; small fasteners retained." } : data.modifications, trianglesByLod = counts, collisionTriangles = collision.triangles.Length / 3,
                        materialsUsed = used.OrderBy(s => s).ToArray(), width = bounds.size.x, height = bounds.size.y, depth = bounds.size.z,
                        usage = data.staticExterior ? "Static exterior shell; door is intentionally closed." : source.id == "PyramidPortal" || source.id == "RammedEarthGatehouse" ? "Static architecture with a through aperture." : "Static environment dressing.",
                        passed = counts.All(n => n > 0) && counts[0] >= counts[1] && counts[1] >= counts[2] && collision != null
                            && lods.SelectMany(l => l.renderers).All(r => r.sharedMaterials.All(m => m != null && m.shader.isSupported && m.mainTexture != null))
                            && Mathf.Abs(bounds.min.y) < .02f };
                    report.assets.RemoveAll(a => a.id == source.id); report.assets.Add(check);
                    report.assets = report.assets.OrderBy(a => sources.assets.FindIndex(s => s.id == a.id)).ToList();
                    File.WriteAllText(Docs + "/ready-validation.json", JsonUtility.ToJson(report, true));
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            AssetDatabase.SaveAssets(); return report;
        }

        private static Mesh ReadWorkingMesh(string path, string id, int level, List<Material> materials, HashSet<string> used)
        {
            Mesh mesh;
            if (id == "CaravanSupplies")
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(HunyuanEnvironmentLibrary.Root + "/Prefabs/CaravanSupplies.prefab");
                var renderer = source.GetComponent<LODGroup>().GetLODs()[level].renderers[0];
                mesh = Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                mesh.name = id + "_Ready_LOD" + level;
                mesh.vertices = mesh.vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
                mesh.normals = mesh.normals.Select(n => renderer.transform.TransformDirection(n).normalized).ToArray();
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                used.Add("OriginalSupplies"); materials.Add(Surface("OriginalSupplies",id));
                return SaveReadyMesh(mesh);
            }
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != "TBM1") throw new InvalidDataException(path);
                int count = reader.ReadInt32(), indexCount = reader.ReadInt32(), surfaceCount = reader.ReadInt32();
                var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
                for (int i = 0; i < count; i++) { float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle(); vertices[i] = new Vector3(x, z, -y); }
                for (int i = 0; i < count; i++) { float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle(); normals[i] = new Vector3(x, z, -y).normalized; }
                for (int i = 0; i < count; i++) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                var indices = new int[indexCount]; for (int i = 0; i < indices.Length; i++) indices[i] = reader.ReadInt32();
                var surfaces = new List<int>[surfaceCount]; for (int i = 0; i < surfaceCount; i++) surfaces[i] = new List<int>();
                for (int i = 0; i < indexCount / 3; i++) { int surface = reader.ReadInt32(); if (surface < 0 || surface >= surfaceCount) throw new InvalidDataException("Invalid material index."); surfaces[surface].Add(indices[i * 3]); surfaces[surface].Add(indices[i * 3 + 1]); surfaces[surface].Add(indices[i * 3 + 2]); }
                mesh = new Mesh { name = id + "_Ready_LOD" + level, indexFormat = IndexFormat.UInt32, vertices = vertices, normals = normals, uv = uv };
                var active = Enumerable.Range(0, surfaceCount).Where(i => surfaces[i].Count > 0).ToArray(); mesh.subMeshCount = active.Length;
                for (int i = 0; i < active.Length; i++) { mesh.SetTriangles(surfaces[active[i]], i); string category = SurfaceNames[active[i]]; used.Add(category); materials.Add(Surface(category, id)); }
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                if (id != "CaravanSupplies")
                {
                    var tangents = mesh.tangents;
                    if (tangents.Length != count) tangents = new Vector4[count];
                    for (int i = 0; i < count; i++) if (new Vector3(tangents[i].x,tangents[i].y,tangents[i].z).sqrMagnitude < .01f) { Vector3 t = Vector3.Cross(Mathf.Abs(normals[i].y) > .95f ? Vector3.right : Vector3.up, normals[i]).normalized; tangents[i] = new Vector4(t.x, t.y, t.z, 1); }
                    mesh.tangents = tangents;
                }
            }
            return SaveReadyMesh(mesh);
        }

        private static Mesh SaveReadyMesh(Mesh mesh)
        {
            string output = Root + "/Meshes/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(output);
            if (existing == null) AssetDatabase.CreateAsset(mesh, output);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            return mesh;
        }

        private static Material Surface(string category, string id)
        {
            if (SurfaceCache.TryGetValue(category, out var cached)) return cached;
            string path = Root + "/Materials/" + category + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(category == "OriginalSupplies" ? "Standard" : "Traveller/Environment Surface")); AssetDatabase.CreateAsset(material, path); }
            if (category == "OriginalSupplies")
            {
                var original = AssetDatabase.LoadAssetAtPath<Material>(HunyuanEnvironmentLibrary.Root + "/CaravanSupplies/CaravanSupplies.mat");
                material.shader = original.shader; material.CopyPropertiesFromMaterial(original);
                material.color = new Color(.8f, .74f, .65f); material.SetFloat("_GlossMapScale", .45f);
            }
            else
            {
                string source = category == "Rock" ? "Assets/ThirdParty/EnvironmentReady/PolyHaven/rock_face" : category == "Bark" ? "Assets/ThirdParty/EnvironmentReady/PolyHaven/pine_bark" : category == "Thatch" ? "Assets/ThirdParty/EnvironmentReady/PolyHaven/thatch_roof_angled" : "Assets/ThirdParty/PolyHaven/" + (category == "Masonry" ? "sandstone_blocks_05" : category == "Earth" ? "clay_plaster" : category == "Wood" ? "wood_planks" : "grey_roof_tiles");
                string prefix = Path.GetFileName(source);
                material.shader = Shader.Find("Traveller/Environment Surface"); material.enableInstancing = true;
                material.SetTexture("_MainTex", PhotoMap(source, prefix, "diff"));
                material.SetTexture("_BumpMap", PhotoMap(source, prefix, "nor_gl"));
                material.SetTexture("_SurfaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + category + "_Surface.png"));
                material.SetFloat("_DetailMetres", category == "Rock" ? 7f : category == "Bark" ? .65f : category == "Wood" ? 1.6f : 2.2f);
                material.SetFloat("_UseUV", category == "Thatch" ? 1 : 0);
                material.mainTextureScale = category == "Thatch" ? Vector2.one * 3 : Vector2.one;
                material.SetFloat("_NormalStrength", category == "Rock" ? 1.1f : .8f);
                material.SetFloat("_RoughnessBias", .06f);
                material.color = category == "Rock" ? new Color(.92f, .82f, .69f) : category == "Earth" ? new Color(1.10f, .94f, .76f) : category == "Wood" ? new Color(.67f, .58f, .46f) : category == "Bark" ? new Color(.68f, .62f, .53f) : category == "Thatch" ? new Color(1.16f,1.02f,.78f) : Color.white;
                if (material.mainTexture == null || material.GetTexture("_BumpMap") == null || material.GetTexture("_SurfaceMap") == null) throw new FileNotFoundException("Missing photographic PBR maps for " + category);
            }
            EditorUtility.SetDirty(material); SurfaceCache[category] = material; return material;
        }

        private static Texture2D PhotoMap(string folder, string prefix, string suffix)
        {
            string path = Directory.GetFiles(folder, prefix + "_" + suffix + "_*", SearchOption.AllDirectories).FirstOrDefault(p => !p.EndsWith(".meta"));
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ConfigureReadyPhotoMaps()
        {
            foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (!importer.sRGBTexture && importer.mipmapEnabled && importer.maxTextureSize == 2048 && importer.textureCompression == TextureImporterCompression.CompressedHQ && importer.wrapMode == TextureWrapMode.Repeat) continue;
                importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.maxTextureSize=2048;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;
                importer.SaveAndReimport();
            }
            const string folder = "Assets/ThirdParty/EnvironmentReady";
            if (!Directory.Exists(folder)) return;
            foreach (string path in Directory.GetFiles(folder, "*.jpg", SearchOption.AllDirectories))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool normal = path.Contains("_nor_gl_"); bool srgb = path.Contains("_diff_");
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == 2048 && importer.wrapMode == TextureWrapMode.Repeat) continue;
                importer.textureType = type; importer.sRGBTexture = srgb; importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        public static string[] CaptureReadyLibrary()
        {
            var report = JsonUtility.FromJson<ReadyReport>(File.ReadAllText(Docs + "/ready-validation.json"));
            var images = new List<string>();
            for (int start = 0; start < report.assets.Count; start += 4)
            {
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var entries = report.assets.Skip(start).Take(4).ToArray();
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entries[i].prefab), scene);
                        instance.transform.rotation = Quaternion.Euler(0,-24,0);
                        var group = instance.GetComponent<LODGroup>(); group.ForceLOD(0);
                        var renderer = group.GetLODs()[0].renderers[0]; var bounds = renderer.bounds;
                        instance.transform.localScale *= 2.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                        bounds = renderer.bounds;
                        instance.transform.position += new Vector3(((entries.Length - 1) * .5f - i) * 3.4f - bounds.center.x, -bounds.min.y, -bounds.center.z);
                    }
                    foreach (var pose in new[] { new Vector4(45, 210, 0, 1.35f), new Vector4(20, 50, 0, .8f), new Vector4(10, 140, 0, .45f) })
                    {
                        var light = NewObject("Studio light", scene).AddComponent<Light>(); light.type = LightType.Directional;
                        light.transform.rotation = Quaternion.Euler(pose.x, pose.y, pose.z); light.intensity = pose.w;
                    }
                    var camera = NewObject("Studio camera", scene).AddComponent<Camera>(); camera.scene = scene;
                    camera.orthographic = true; camera.orthographicSize = 4.1f; camera.nearClipPlane = .05f; camera.farClipPlane = 50;
                    camera.transform.position = new Vector3(0, 3.1f, 16); camera.transform.LookAt(new Vector3(0, 1.3f, 0));
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .06f, .07f);
                    string path = Docs + "/Previews/Ready_" + (start / 4 + 1).ToString("00") + ".jpg";
                    TravellerSceneChecks.Capture(camera, path); images.Add(path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            return images.ToArray();
        }

        private static GameObject NewObject(string name, Scene scene) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
    }
}
