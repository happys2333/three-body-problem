using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    /// <summary>Prepare collected architecture for Built-in without changing story scenes.</summary>
    public static class TravellerArchitectureLibrary
    {
        public const string LibraryRoot = "Assets/_Project/Prefabs/Architecture";
        public const string GalleryScene = "Assets/_Project/Scenes/Architecture/Architecture_AssetGallery.unity";
        public const string LocalRoot = "Assets/ThirdParty/LocalOnly/CSStudioChineseBuildings";
        public const string LocalGallery = LocalRoot + "/Prepared/Chinese_Buildings_Gallery.unity";
        private const string MaterialRoot = "Assets/_Project/Materials/Architecture";
        private const string MapRoot = "Assets/_Project/Art/ArchitectureMaps";
        private static readonly Dictionary<string, Material> converted = new Dictionary<string, Material>();
        private static readonly Dictionary<string, string> prefabs = new Dictionary<string, string>();
        private static readonly string[] PhysicalAssets = {
            "chinese_armchair", "chinese_stool", "chinese_screen_panels", "chinese_tea_table",
            "large_castle_door", "modular_fort_01", "clay_roof_tiles", "clay_plaster",
            "cobblestone_pavement", "wood_planks", "stone_wall", "grey_roof_tiles", "red_plaster_weathered"
        };

        [Serializable] private sealed class CheckReport
        {
            public int models;
            public int preparedPrefabs;
            public int localModels;
            public int materialCount;
            public string[] errors;
            public string gallery;
        }

        [MenuItem("Tools/Three Body Traveller/Assets/Prepare Architecture Library")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before preparing assets.");
            var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string returnScene = current.path;
            if (current.isDirty && !string.IsNullOrEmpty(returnScene)) EditorSceneManager.SaveScene(current);
            converted.Clear(); prefabs.Clear();
            foreach (string folder in new[] { LibraryRoot, MaterialRoot, MapRoot, Path.GetDirectoryName(GalleryScene) })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            ConfigureTextures();
            foreach (string asset in PhysicalAssets) PhysicalMaterial(asset, asset, "Assets/ThirdParty/PolyHaven/" + asset, false);
            var roots = new List<string> { "Assets/ThirdParty/Architecture" };
            roots.AddRange(PhysicalAssets.Take(6).Select(asset => "Assets/ThirdParty/PolyHaven/" + asset));
            if (Directory.Exists(LocalRoot)) roots.Add(LocalRoot + "/Models");
            var paths = AssetDatabase.FindAssets("t:Model", roots.ToArray())
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path).ToArray();
            var rows = new StringBuilder("name,source,triangles,width_m,height_m,depth_m,prefab,local_only\n");
            foreach (string path in paths)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                bool local = path.StartsWith(LocalRoot, StringComparison.Ordinal);
                string group = Group(path);
                string destination = local ? LocalRoot + "/Prepared/Prefabs" : LibraryRoot + "/" + group;
                Directory.CreateDirectory(destination);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = Path.GetFileNameWithoutExtension(path);
                    ConvertRenderers(instance, group, path, local);
                    // Source pavilion is almost 40 m wide; prepare an 8 m variant.
                    if (group == "ChinesePavilion") instance.transform.localScale *= 0.2f;
                    // KayKit's RTS houses are below one metre tall in the source.
                    if (group == "KayKitMedieval") instance.transform.localScale *= 4f;
                    var bound = BoundsOf(instance);
                    var triangles = instance.GetComponentsInChildren<MeshFilter>(true)
                        .Where(filter => filter.sharedMesh != null)
                        .Sum(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                            .Sum(submesh => (long)filter.sharedMesh.GetIndexCount(submesh) / 3));
                    string output = destination + "/" + Safe(instance.name) + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(instance, output);
                    prefabs[group + "/" + instance.name] = output;
                    rows.AppendLine(string.Join(",", new[] { Csv(instance.name), Csv(path), triangles.ToString(),
                        bound.size.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        bound.size.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        bound.size.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), Csv(output), local ? "true" : "false" }));
                    if (group == "modular_fort_01") SplitFort(instance);
                }
                finally { Object.DestroyImmediate(instance); }
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/architecture-model-catalog.csv", rows.ToString(), new UTF8Encoding(true));
            BuildGallery(false);
            if (Directory.Exists(LocalRoot)) BuildGallery(true);
            AssetDatabase.SaveAssets();
            ValidateLibrary(paths);
            if (!string.IsNullOrEmpty(returnScene)) EditorSceneManager.OpenScene(returnScene);
            Debug.Log("ARCHITECTURE_LIBRARY: prepared " + paths.Length + " models for Built-in; story scenes and Build Settings preserved.");
        }

        private static void ConfigureTextures()
        {
            var roots = new List<string> { "Assets/ThirdParty/Architecture" };
            roots.AddRange(PhysicalAssets.Select(asset => "Assets/ThirdParty/PolyHaven/" + asset));
            if (Directory.Exists(LocalRoot)) roots.Add(LocalRoot);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", roots.ToArray()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                string lower = Path.GetFileName(path).ToLowerInvariant();
                bool normal = lower.Contains("_nor_gl_") || lower.Contains("_normal");
                bool data = normal || lower.Contains("_rough") || lower.Contains("_metal") || lower.Contains("_ao_") || lower.Contains("_orm") || lower.Contains("_arm");
                var desired = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.maxTextureSize == 1024 && importer.textureType == desired && importer.sRGBTexture == !data) continue;
                importer.maxTextureSize = 1024;
                importer.textureType = desired;
                importer.sRGBTexture = !data;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }

        private static string Group(string path)
        {
            if (path.Contains("/KenneyFantasyTown/")) return "KenneyFantasyTown";
            if (path.Contains("/QuaterniusMedieval/")) return "QuaterniusMedieval";
            if (path.Contains("/KayKitMedieval/")) return "KayKitMedieval";
            if (path.Contains("/VVayToyekPavilion/")) return "ChinesePavilion";
            if (path.StartsWith(LocalRoot, StringComparison.Ordinal)) return "CSStudioLocal";
            return path.Split('/')[3];
        }

        private static void ConvertRenderers(GameObject instance, string group, string source, bool local)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) continue;
                    string key = group + "/" + original.name;
                    Material material;
                    if (!converted.TryGetValue(key, out material))
                    {
                        if (source.Contains("/PolyHaven/"))
                        {
                            string prefix = original.name;
                            if (group == "modular_fort_01" && prefix.EndsWith("_1", StringComparison.Ordinal)) prefix = prefix.Substring(0, prefix.Length - 2);
                            material = PhysicalMaterial(key, prefix, Path.GetDirectoryName(source), local);
                        }
                        else
                        {
                            string folder = local ? LocalRoot + "/Prepared/Materials" : MaterialRoot + "/" + group;
                            Directory.CreateDirectory(folder);
                            string materialPath = folder + "/" + Safe(original.name) + ".mat";
                            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, materialPath); }
                            material.shader = Shader.Find("Standard");
                            material.color = original.HasProperty("_Color") ? original.color : Color.white;
                            material.mainTexture = original.HasProperty("_MainTex") ? original.mainTexture : null;
                            material.SetFloat("_Glossiness", 0.17f);
                            material.enableInstancing = true;
                            EditorUtility.SetDirty(material);
                        }
                        converted[key] = material;
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        private static Material PhysicalMaterial(string key, string prefix, string sourceFolder, bool local)
        {
            Material material;
            if (converted.TryGetValue(key, out material)) return material;
            string folder = local ? LocalRoot + "/Prepared/Materials" : MaterialRoot + "/PolyHaven";
            Directory.CreateDirectory(folder);
            string path = folder + "/" + Safe(key) + ".mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find("Standard"); material.color = Color.white;
            material.mainTexture = Texture(sourceFolder, prefix + "_diff_1k");
            material.SetTexture("_BumpMap", Texture(sourceFolder, prefix + "_nor_gl_1k"));
            material.SetFloat("_BumpScale", 0.65f); material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_OcclusionMap", Texture(sourceFolder, prefix + "_ao_1k"));
            material.SetFloat("_Glossiness", 0.18f);
            var rough = Texture(sourceFolder, prefix + "_rough_1k");
            var metal = Texture(sourceFolder, prefix + "_metal_1k");
            if (rough != null)
            {
                string mapPath = MapRoot + "/" + Safe(prefix) + "_metallic_smoothness.png";
                if (!File.Exists(mapPath))
                {
                    var r = ReadTexture(rough); var m = metal == null ? null : ReadTexture(metal);
                    var rp = r.GetPixels32(); var mp = m == null ? null : m.GetPixels32();
                    var pixels = new Color32[rp.Length];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(mp == null ? (byte)0 : mp[i].r, 0, 0, (byte)(255 - rp[i].r));
                    var packed = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false, true);
                    packed.SetPixels32(pixels); packed.Apply(); File.WriteAllBytes(mapPath, packed.EncodeToPNG());
                    Object.DestroyImmediate(packed); Object.DestroyImmediate(r); if (m != null) Object.DestroyImmediate(m);
                    AssetDatabase.ImportAsset(mapPath);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(mapPath); importer.sRGBTexture = false; importer.maxTextureSize = 1024; importer.SaveAndReimport();
                }
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath));
                material.EnableKeyword("_METALLICGLOSSMAP"); material.SetFloat("_GlossMapScale", 1f);
            }
            material.enableInstancing = true; EditorUtility.SetDirty(material); converted[key] = material;
            return material;
        }

        private static Texture2D Texture(string folder, string stem)
        {
            if (!Directory.Exists(folder)) return null;
            string path = Directory.GetFiles(folder, stem + ".*", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
        }

        private static Texture2D ReadTexture(Texture2D source)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            try
            {
                Graphics.Blit(source, temporary); RenderTexture.active = temporary;
                var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); readable.Apply(); return readable;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }

        private static void SplitFort(GameObject source)
        {
            string folder = LibraryRoot + "/FortModules"; Directory.CreateDirectory(folder);
            foreach (Transform child in source.transform)
            {
                if (child.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                var wrapper = new GameObject(child.name);
                var module = Object.Instantiate(child.gameObject, wrapper.transform);
                try
                {
                    module.transform.localPosition = Vector3.zero;
                    var bounds = BoundsOf(module);
                    module.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    string path = folder + "/" + Safe(child.name) + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(wrapper, path); prefabs["FortModules/" + child.name] = path;
                }
                finally { Object.DestroyImmediate(wrapper); }
            }
        }

        private static void BuildGallery(bool local)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string pavingPath = MaterialRoot + "/Gallery_Paving.mat";
            var paving = AssetDatabase.LoadAssetAtPath<Material>(pavingPath);
            if (paving == null) { paving = new Material(converted["cobblestone_pavement"]); AssetDatabase.CreateAsset(paving, pavingPath); }
            paving.mainTextureScale = new Vector2(25, 30); EditorUtility.SetDirty(paving);
            Primitive("Ground", new Vector3(0, -0.15f, 25), new Vector3(100, 0.3f, 140), paving);
            var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(42, -35, 0); light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.58f, 0.65f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.45f, 0.52f, 0.58f); RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.003f;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
            camera.transform.position = new Vector3(44, 27, -39); camera.transform.LookAt(new Vector3(0, 2, 14));
            camera.fieldOfView = 56; camera.farClipPlane = 250;

            var chinese = new GameObject("01 Chinese pavilion and furniture").transform;
            Place("ChinesePavilion/四角亭", new Vector3(22, 0, 12), 15, chinese);
            Place("chinese_screen_panels/chinese_screen_panels_1k", new Vector3(20, 0, -0.5f), 4, chinese);
            Place("chinese_armchair/chinese_armchair_1k", new Vector3(25, 0, 1), 1.8f, chinese);
            Place("chinese_tea_table/chinese_tea_table_1k", new Vector3(24, 0, -2.5f), 2.3f, chinese);
            Place("chinese_stool/chinese_stool_1k", new Vector3(27, 0, -2.5f), 1, chinese);
            Label("中式木构与陈设 / CC0", new Vector3(22, 0.08f, -7), 0.5f);
            var medieval = new GameObject("02 Medieval modular walls roofs and props").transform;
            string[] samples = { "Wall_Plaster_Straight_R", "Wall_Plaster_Straight_L", "Wall_UnevenBrick_Straight", "DoorFrame_Flat_WoodDark", "Roof_2x4_RoundTile", "Roof_Modular_RoundTiles", "Stairs_Exterior_SingleSide", "Prop_Crate" };
            for (int i = 0; i < samples.Length; i++) Place("QuaterniusMedieval/" + samples[i], new Vector3(-34 + i % 4 * 7, 0, 22 + i / 4 * 8), 6, medieval);
            Place("KayKitMedieval/house", new Vector3(-33, 0, 7), 10, medieval);
            Place("KayKitMedieval/mill", new Vector3(-20, 0, 9), 12, medieval);
            Place("KayKitMedieval/market", new Vector3(-7, 0, 9), 10, medieval);
            string[] town = { "wall-door", "wall-window-shutters", "roof-high-point", "stall-red", "cart", "fountain-round-detail" };
            for (int i = 0; i < town.Length; i++) Place("KenneyFantasyTown/" + town[i], new Vector3(-31 + i * 5.7f, 0, -3), 4, medieval);
            Label("中世纪建筑构件 / CC0", new Vector3(-22, 0.08f, -8), 0.5f);
            var fort = new GameObject("03 Stone fort modules").transform;
            var modules = prefabs.Keys.Where(key => key.StartsWith("FortModules/", StringComparison.Ordinal)).Take(4).ToArray();
            for (int i = 0; i < modules.Length; i++) Place(modules[i], new Vector3(-30 + i * 17, 0, 36), 12, fort);
            Place("large_castle_door/large_castle_door_1k", new Vector3(38, 0, 32), 7, fort);
            var surfaces = new GameObject("04 Prepared Built-in PBR surfaces").transform;
            string[] materials = PhysicalAssets.Skip(6).ToArray();
            for (int i = 0; i < materials.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = materials[i];
                sphere.transform.SetParent(surfaces); sphere.transform.position = new Vector3(-30 + i * 10, 1.8f, -18);
                sphere.transform.localScale = Vector3.one * 3.6f; sphere.GetComponent<Renderer>().sharedMaterial = converted[materials[i]];
                Label(materials[i], new Vector3(-30 + i * 10, 0.06f, -21), 0.2f);
            }
            Directory.CreateDirectory("Documentation/Previews");
            TravellerSceneChecks.Capture(camera, "Documentation/Previews/Architecture_Library.jpg");
            camera.transform.position = new Vector3(39, 9, -6); camera.transform.LookAt(new Vector3(22, 3, 11));
            TravellerSceneChecks.Capture(camera, "Documentation/Previews/Chinese_Pavilion_Assets.jpg");
            camera.transform.position = new Vector3(-4, 11, -19); camera.transform.LookAt(new Vector3(-22, 2, 9));
            TravellerSceneChecks.Capture(camera, "Documentation/Previews/Medieval_Modular_Assets.jpg");
            camera.transform.position = new Vector3(44, 27, -39); camera.transform.LookAt(new Vector3(0, 2, 14));
            string scene = GalleryScene;
            if (local)
            {
                var buildings = new GameObject("05 CS Studio buildings - local use only").transform;
                string[] names = { "SM_Bookstore_01", "SM_Butcher_01", "SM_Clothing_Store_01", "SM_Gazeebo_01", "SM_Horse_Room_01" };
                var positions = new[] { new Vector3(-20, 0, 50), new Vector3(0, 0, 50), new Vector3(20, 0, 50), new Vector3(-12, 0, 70), new Vector3(12, 0, 70) };
                for (int i = 0; i < names.Length; i++) Place("CSStudioLocal/" + names[i], positions[i], 12, buildings);
                // Keep the Chinese sample row visible in its own preview.
                fort.gameObject.SetActive(false); medieval.gameObject.SetActive(false); chinese.gameObject.SetActive(false); surfaces.gameObject.SetActive(false);
                camera.transform.position = new Vector3(35, 22, 12); camera.transform.LookAt(new Vector3(0, 2, 57));
                TravellerSceneChecks.Capture(camera, "Documentation/Previews/Chinese_Building_Assets.jpg");
                scene = LocalGallery;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(scene));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scene);
        }

        private static void Place(string key, Vector3 position, float maximumSize, Transform parent)
        {
            string path;
            if (!prefabs.TryGetValue(key, out path)) { Debug.LogWarning("ARCHITECTURE_GALLERY: optional sample missing: " + key); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.transform.SetParent(parent);
            var bounds = BoundsOf(instance);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (size > 0.001f) instance.transform.localScale *= maximumSize / size;
            bounds = BoundsOf(instance);
            instance.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static Bounds BoundsOf(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);
            var result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }

        private static GameObject Primitive(string name, Vector3 position, Vector3 scale, Material material)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube); result.name = name;
            result.transform.position = position; result.transform.localScale = scale; result.GetComponent<Renderer>().sharedMaterial = material; return result;
        }

        private static void Label(string text, Vector3 position, float size)
        {
            var label = new GameObject(text, typeof(TextMesh)); label.transform.position = position; label.transform.rotation = Quaternion.Euler(90, 0, 0);
            var mesh = label.GetComponent<TextMesh>(); mesh.text = text; mesh.characterSize = size; mesh.fontSize = 64;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.color = new Color(0.13f, 0.16f, 0.17f);
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf");
            mesh.font = font; label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static void ValidateLibrary(string[] sources)
        {
            var errors = new List<string>();
            foreach (string path in prefabs.Values)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { errors.Add("Cannot load " + path); continue; }
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null || material.shader == null || !material.shader.isSupported || material.shader.name != "Standard")
                            errors.Add("Invalid Built-in material in " + path);
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh == null) errors.Add("Missing mesh in " + path);
            }
            var report = new CheckReport { models = sources.Count(path => !path.StartsWith(LocalRoot, StringComparison.Ordinal)),
                localModels = sources.Count(path => path.StartsWith(LocalRoot, StringComparison.Ordinal)),
                preparedPrefabs = prefabs.Count, materialCount = converted.Count, errors = errors.ToArray(), gallery = GalleryScene };
            File.WriteAllText("Documentation/architecture-validation.json", JsonUtility.ToJson(report, true) + "\n");
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors.ToArray()));
        }

        private static string Safe(string name) { return name.Replace('/', '_').Replace('\\', '_').Replace(':', '_'); }
        private static string Csv(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }
    }
}
