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
    // Isolated library preparation. Story geometry and Build Settings are not edited.
    public static class HunyuanEnvironmentLibrary
    {
        public const string Root = "Assets/_Project/Art/HunyuanEnvironment";
        private const string Docs = "Documentation/EnvironmentProduction";
        [Serializable] public class Files { public string projectFile; }
        [Serializable] public class Entry { public string id, category, sizeAxis; public float targetSizeMetres, yawDegrees; public Files files; }
        [Serializable] public class Manifest { public List<Entry> assets; }
        [Serializable] public class Check
        {
            public string id, source, prefab;
            public bool passed, albedo, normal, metallicSmoothness;
            public int[] trianglesByLod;
            public float widthMetres, heightMetres, depthMetres;
            public string[] textures;
        }
        [Serializable] public class Report { public List<Check> assets = new List<Check>(); }

        [MenuItem("Tools/Three Body Traveller/Assets/Prepare Hunyuan Environment Library")]
        public static Report PrepareAvailable() => PrepareBatch(0, int.MaxValue);

        public static Report PrepareBatch(int skip, int take)
        {
            return PrepareManifest(Docs + "/hunyuan-environment-2026-10-28.json", Root,
                Docs + "/hunyuan-environment-unity-validation.json", skip, take, false);
        }

        public static Report PrepareManifest(string manifestPath, string libraryRoot, string reportPath, int skip, int take, bool addCollision)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing the environment library.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            Directory.CreateDirectory(libraryRoot + "/Prefabs"); Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            AssetDatabase.Refresh();
            var report = File.Exists(reportPath) ? JsonUtility.FromJson<Report>(File.ReadAllText(reportPath)) : new Report();
            foreach (var entry in manifest.assets.Where(a => a.files != null && File.Exists(a.files.projectFile)).Skip(skip).Take(take))
            {
                var check = Prepare(entry, libraryRoot, addCollision); report.assets.RemoveAll(a => a.id == entry.id); report.assets.Add(check);
                // Persist progress so a domain reload never loses a completed import.
                report.assets = report.assets.OrderBy(a => manifest.assets.FindIndex(e => e.id == a.id)).ToList();
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
            AssetDatabase.SaveAssets();
            if (report.assets.Any(a => !a.passed)) throw new InvalidDataException("Environment library validation failed; inspect its report.");
            return report;
        }

        private static Check Prepare(Entry entry, string libraryRoot, bool addCollision)
        {
            string source = entry.files.projectFile, folder = Path.GetDirectoryName(source), maps = folder + "/Textures";
            var importer = (ModelImporter)AssetImporter.GetAtPath(source);
            importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = false;
            importer.importCameras = false; importer.importLights = false; importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            Directory.CreateDirectory(maps); importer.ExtractTextures(maps); AssetDatabase.Refresh();
            var textures = Directory.GetFiles(maps, "*.png").Where(p => !p.EndsWith("_MetallicSmoothness.png")).ToArray();
            foreach (string path in textures)
            {
                bool normal = Has(path, "normal") || Has(path, "_nor");
                bool data = normal || Has(path, "metal") || Has(path, "rough") || Has(path, "occlusion");
                ConfigureTexture(path, normal, !data, Has(path, "metal") || Has(path, "rough"));
            }
            var originals = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Material>().ToArray();
            var ids = originals.Select(m => new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name))
                .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material))).Distinct().ToArray();
            foreach (var id in ids)
            {
                var original = originals.FirstOrDefault(m => m.name == id.name);
                string albedo = textures.FirstOrDefault(p => original != null && original.mainTexture != null && Path.GetFileNameWithoutExtension(p) == original.mainTexture.name)
                    ?? textures.FirstOrDefault(p => !Has(p, "normal") && !Has(p, "_nor") && !Has(p, "metal") && !Has(p, "rough") && !Has(p, "occlusion") && !Has(p, "emissive") && !Has(p, "specular"));
                if (albedo == null) throw new InvalidDataException("No albedo extracted for " + entry.id);
                string normal = textures.FirstOrDefault(p => Has(p, "normal") || Has(p, "_nor"));
                string metal = textures.FirstOrDefault(p => Has(p, "metal"));
                string rough = textures.FirstOrDefault(p => Has(p, "rough"));
                string materialPath = folder + "/" + entry.id + (ids.Length > 1 ? "_" + id.name.Replace(' ', '_') : "") + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, materialPath); }
                material.color = Color.white; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
                material.SetFloat("_Glossiness", .18f); material.SetFloat("_Metallic", 0); material.enableInstancing = true;
                if (normal != null) { material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); material.SetFloat("_BumpScale", .8f); material.EnableKeyword("_NORMALMAP"); }
                if (metal != null && rough != null)
                {
                    string packed = maps + "/" + entry.id + "_MetallicSmoothness.png";
                    HunyuanExpansionLibrary.PackMaterialChannels(metal, rough, packed);
                    material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packed));
                    material.SetFloat("_GlossMapScale", 1); material.EnableKeyword("_METALLICGLOSSMAP");
                }
                EditorUtility.SetDirty(material); importer.AddRemap(id, material);
            }
            importer.SaveAndReimport();
            foreach (string path in textures) if (Has(path, "metal") || Has(path, "rough")) ConfigureTexture(path, false, false, false);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = NewObject(entry.id, scene); var visual = NewObject("Visual", scene); visual.transform.SetParent(root.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), scene); model.transform.SetParent(visual.transform, false);
                visual.transform.localRotation = Quaternion.Euler(0, entry.yawDegrees, 0);
                var high = model.GetComponentsInChildren<MeshRenderer>();
                if (high.Length == 0) throw new InvalidDataException("No static mesh for " + entry.id);
                var bounds = Bounds(high);
                float dimension = entry.sizeAxis == "width" ? bounds.size.x : bounds.size.y;
                if (dimension <= .0001f) throw new InvalidDataException("Empty environment bounds.");
                visual.transform.localScale = Vector3.one * (entry.targetSizeMetres / dimension);
                bounds = Bounds(high); visual.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var lods = new LOD[3]; var triangles = new int[3]; lods[0] = new LOD(.32f, high);
                triangles[0] = high.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
                for (int level = 1; level <= 2; level++)
                {
                    var lower = new List<Renderer>();
                    foreach (var renderer in high)
                    {
                        var sourceMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        var mesh = TravellerSceneBuilder.EnvironmentLod(sourceMesh, "Hunyuan_" + entry.id, level);
                        var child = NewObject("LOD " + level, scene); child.transform.SetParent(renderer.transform, false);
                        child.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var output = child.AddComponent<MeshRenderer>(); output.sharedMaterials = renderer.sharedMaterials;
                        output.shadowCastingMode = level == 1 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        triangles[level] += mesh.triangles.Length / 3; lower.Add(output);
                        if (addCollision && level == 1)
                        {
                            var collider = renderer.gameObject.AddComponent<MeshCollider>();
                            collider.sharedMesh = mesh; collider.convex = false;
                        }
                    }
                    lods[level] = new LOD(level == 1 ? .075f : .009f, lower.ToArray());
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                string prefab = libraryRoot + "/Prefabs/" + entry.id + ".prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, prefab) == null) throw new IOException(prefab);
                bounds = Bounds(high); var materials = high.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                var check = new Check { id = entry.id, source = source, prefab = prefab, trianglesByLod = triangles,
                    widthMetres = bounds.size.x, heightMetres = bounds.size.y, depthMetres = bounds.size.z,
                    albedo = materials.Length > 0 && materials.All(m => m != null && m.shader.isSupported && m.mainTexture != null),
                    normal = materials.Length > 0 && materials.All(m => m != null && m.GetTexture("_BumpMap") != null),
                    metallicSmoothness = materials.Length > 0 && materials.All(m => m != null && m.GetTexture("_MetallicGlossMap") != null), textures = textures };
                check.passed = check.albedo && check.normal && check.metallicSmoothness && triangles.All(t => t > 0)
                    && triangles[1] < triangles[0] && triangles[2] < triangles[1]
                    && Mathf.Abs((entry.sizeAxis == "width" ? bounds.size.x : bounds.size.y) - entry.targetSizeMetres) < .02f;
                return check;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static string[] CaptureLibrary()
        {
            return CaptureManifest(Docs + "/hunyuan-environment-unity-validation.json", Docs + "/Previews");
        }

        public static string[] CaptureManifest(string reportPath, string previewFolder)
        {
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(reportPath));
            Directory.CreateDirectory(previewFolder);
            var images = new List<string>();
            for (int start = 0; start < report.assets.Count; start += 4)
            {
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var entries = report.assets.Skip(start).Take(4).ToArray();
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entries[i].prefab), scene);
                        // Shelf photographs normalize mesh sizes; world-space point light ranges
                        // cannot be normalized in the same way. Use the review lights here.
                        foreach (var embeddedLight in root.GetComponentsInChildren<Light>()) embeddedLight.enabled = false;
                        var group = root.GetComponent<LODGroup>(); group.ForceLOD(0);
                        var high = group.GetLODs()[0].renderers; var bounds = Bounds(high);
                        root.transform.localScale *= 2.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                        bounds = Bounds(high); root.transform.position += new Vector3((i - (entries.Length - 1) * .5f) * 3.4f - bounds.center.x, -bounds.min.y, -bounds.center.z);
                    }
                    foreach (var value in new[] { new Vector4(35, 205, 0, 1.15f), new Vector4(15, 120, 0, .65f) })
                    {
                        var light = NewObject("Review light", scene).AddComponent<Light>(); light.type = LightType.Directional;
                        light.transform.rotation = Quaternion.Euler(value.x, value.y, value.z); light.intensity = value.w;
                    }
                    var camera = NewObject("Review camera", scene).AddComponent<Camera>(); camera.scene = scene;
                    camera.orthographic = true; camera.orthographicSize = 4.1f; camera.nearClipPlane = .05f; camera.farClipPlane = 50;
                    camera.transform.position = new Vector3(0, 3.4f, 16); camera.transform.LookAt(new Vector3(0, 1.3f, 0));
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .065f, .08f);
                    string path = previewFolder + "/Environment_Unity_" + (start / 4 + 1).ToString("00") + ".jpg";
                    TravellerSceneChecks.Capture(camera, path); images.Add(path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            return images.ToArray();
        }

        private static Bounds Bounds(IEnumerable<Renderer> renderers)
        {
            bool first = true; var result = new Bounds();
            foreach (var renderer in renderers) { if (first) { result = renderer.bounds; first = false; } else result.Encapsulate(renderer.bounds); }
            return result;
        }
        private static GameObject NewObject(string name, Scene scene) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        private static bool Has(string path, string part) => Path.GetFileName(path).IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        private static void ConfigureTexture(string path, bool normal, bool srgb, bool readable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            var desired = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType == desired && importer.sRGBTexture == srgb && importer.isReadable == readable && importer.maxTextureSize == 2048) return;
            importer.textureType = desired; importer.sRGBTexture = srgb; importer.isReadable = readable;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        }
    }
}
