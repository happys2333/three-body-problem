using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        public const string Root = "Assets/_Project";
        public const string SceneFolder = Root + "/Scenes/Traveller";
        private const string MatFolder = Root + "/Materials/Traveller";
        private const string MeshFolder = Root + "/Art/GeneratedMeshes";
        private const string PrefabFolder = Root + "/Prefabs/Traveller";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static Transform world;
        private static TravellerStage stage;
        private static readonly List<ParticleSystem> fireSystems = new List<ParticleSystem>();

        [MenuItem("Tools/Three Body Traveller/Build All Scenes")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before rebuilding scenes.");
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && !string.IsNullOrEmpty(active.path)) EditorSceneManager.SaveScene(active);
            foreach (string folder in new[] { SceneFolder, MatFolder, MeshFolder, PrefabFolder, Root + "/Settings/Traveller" }) Directory.CreateDirectory(folder);
            BackupScenes();
            AssetDatabase.Refresh();
            ConfigureImports();
            Materials.Clear(); Meshes.Clear();
            CreateMaterials();
            CreateHourglassPrefab();
            BuildGobi();
            BuildChaoge();
            BuildMedieval();
            EditorBuildSettings.scenes = TravellerStage.SceneNames.Select(name => new EditorBuildSettingsScene(SceneFolder + "/" + name + ".unity", true)).ToArray();
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 180f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.shadowResolution = ShadowResolution.High;
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneFolder + "/01_GobiJourney.unity");
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(-8f, 5f, 18f), Quaternion.Euler(15f, 5f, 0f), 65f);
            Debug.Log("TRAVELLER_BUILD: three scenes, authored profiles, prefabs and materials are ready.");
        }

        private static void BackupScenes()
        {
            string backup = "Logs/scene-backups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            foreach (string name in TravellerStage.SceneNames)
            {
                string source = SceneFolder + "/" + name + ".unity";
                if (!File.Exists(source)) continue;
                Directory.CreateDirectory(backup);
                File.Copy(source, backup + "/" + name + ".unity", true);
                string profile = Root + "/Settings/Traveller/" + name + ".asset";
                if (File.Exists(profile)) File.Copy(profile, backup + "/" + name + ".asset", true);
            }
        }

        private static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/ThirdParty" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool normal = path.Contains("_nor_gl_") || System.IO.Path.GetFileNameWithoutExtension(path).IndexOf("_normal", StringComparison.OrdinalIgnoreCase) >= 0;
                var desiredType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                int resolution = path.StartsWith("Assets/ThirdParty/Environment/", StringComparison.Ordinal) ? 2048 : 1024;
                bool linear = path.Contains("_rough_") || path.Contains("_ao_");
                bool srgb = !normal && !linear;
                bool changed = importer.textureType != desiredType || importer.maxTextureSize != resolution || importer.sRGBTexture != srgb;
                importer.textureType = desiredType;
                importer.maxTextureSize = resolution;
                importer.sRGBTexture = srgb;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Compressed;
                if (changed) importer.SaveAndReimport();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/ThirdParty" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (importer == null || !importer.importAnimation) continue;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
        }

        private static Material Material(string name, Color color, string shader = "Standard")
        {
            string path = MatFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
            material.enableInstancing = true;
            Materials[name] = material;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateMaterials()
        {
            Material("Sand", new Color(0.64f, 0.52f, 0.38f));
            Material("Rock", new Color(0.72f, 0.62f, 0.48f));
            Material("Stone", new Color(0.46f, 0.43f, 0.37f));
            Material("Paving", new Color(0.49f, 0.47f, 0.42f));
            Material("Bronze", new Color(0.37f, 0.28f, 0.12f)).SetFloat("_Metallic", 0.65f);
            Material("Gold", new Color(0.65f, 0.47f, 0.2f)).SetFloat("_Metallic", 0.75f);
            Material("Timber", new Color(0.18f, 0.105f, 0.065f));
            Material("Vermilion", new Color(0.31f, 0.065f, 0.035f));
            Material("Roof", new Color(0.15f, 0.18f, 0.16f));
            Material("Ivory", new Color(0.67f, 0.61f, 0.48f));
            Material("Robe", new Color(0.25f, 0.27f, 0.25f));
            Material("BlueCloth", new Color(0.08f, 0.17f, 0.22f));
            Material("RedCloth", new Color(0.32f, 0.055f, 0.035f));
            Material("Charcoal", new Color(0.09f, 0.095f, 0.09f));
            Material("Grass", new Color(0.36f, 0.29f, 0.15f));
            Material("Snow", new Color(0.67f, 0.73f, 0.77f));
            Material("Particle", Color.white, "Traveller/Atmosphere Particle");
            var glow = Material("Ember", new Color(1f, 0.28f, 0.025f), "Unlit/Color");
            var star = Material("FlyingStar", new Color(1.9f, 2.2f, 2.8f), "Unlit/Color");
            var water = Material("Water", new Color(0.10f, 0.26f, 0.28f), "Standard");
            water.SetFloat("_Metallic", 0.65f); water.SetFloat("_Glossiness", 0.88f);
            var glass = Material("Glass", new Color(0.6f, 0.78f, 0.78f, 0.2f));
            glass.SetFloat("_Mode", 3f); glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); glass.SetInt("_ZWrite", 0);
            glass.EnableKeyword("_ALPHAPREMULTIPLY_ON"); glass.renderQueue = 3000;
            ApplyTexture("Sand", "dry_riverbed_rock");
            ApplyTexture("Rock", "namaqualand_boulder_02");
            ApplyTexture("Stone", "sandstone_blocks_05");
            ApplyTexture("Paving", "sandstone_blocks_05");
            Material("CastleAtlas", new Color(0.78f, 0.72f, 0.60f)).mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/KenneyCastle/Textures/colormap.png");
            Material("NatureAtlas", new Color(0.7f, 0.63f, 0.48f));
        }

        private static void ApplyTexture(string materialName, string asset)
        {
            string folder = "Assets/ThirdParty/PolyHaven/" + asset + "/" + asset;
            var material = Materials[materialName];
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_diff_1k.jpg");
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "_nor_gl_1k.jpg"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", 0.55f);
        }

        private static void BeginScene(TravellerSceneProfile profile)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Traveller Scene");
            stage = root.AddComponent<TravellerStage>();
            stage.profile = profile;
            world = new GameObject("Environment").transform;
            world.SetParent(root.transform);
            fireSystems.Clear();
            stage.heatDetails = new GameObject("Heat cracks and burnt ground"); stage.heatDetails.transform.SetParent(root.transform);
            stage.frostDetails = new GameObject("Long-night frost"); stage.frostDetails.transform.SetParent(root.transform);
            var player = new GameObject("Traveller", typeof(CharacterController), typeof(TravellerPlayer));
            player.transform.SetParent(root.transform);
            var controller = player.GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = 0.3f; controller.center = Vector3.up * 0.9f; controller.stepOffset = 0.35f;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera"; camera.transform.SetParent(player.transform); camera.transform.localPosition = Vector3.up * 1.65f;
            var cameraComponent = camera.GetComponent<Camera>();
            cameraComponent.fieldOfView = 66f; cameraComponent.nearClipPlane = 0.08f; cameraComponent.farClipPlane = 800f;
            cameraComponent.allowHDR = true; cameraComponent.allowMSAA = true;
            stage.player = player.GetComponent<TravellerPlayer>(); stage.player.viewCamera = cameraComponent;
            var heat = camera.AddComponent<HeatDistortion>(); heat.stage = stage; heat.shader = Shader.Find("Hidden/Traveller/Heat Distortion");
            var lightObject = new GameObject("Sun / Key Light", typeof(Light)); lightObject.transform.SetParent(root.transform);
            stage.sun = lightObject.GetComponent<Light>(); stage.sun.type = LightType.Directional; stage.sun.shadows = LightShadows.Soft;
            stage.sun.shadowBias = 0.035f; stage.sun.shadowNormalBias = 0.25f;
            string skyPath = MatFolder + "/Sky_" + profile.sceneIndex + ".mat";
            stage.skyMaterial = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (stage.skyMaterial == null) { stage.skyMaterial = new Material(Shader.Find("Traveller/Celestial Sky")); AssetDatabase.CreateAsset(stage.skyMaterial, skyPath); }
            stage.flyingStars = new Transform[3];
            Vector3[] locations = { new Vector3(46f, 48f, 185f), new Vector3(-11f, 67f, 180f), new Vector3(-51f, 41f, 175f) };
            for (int i = 0; i < 3; i++)
            {
                var celestial = Shape("Flying star " + (i + 1), PrimitiveType.Sphere, locations[i], Vector3.one * (i == 0 ? 0.6f : 0.8f), "FlyingStar", root.transform, false);
                stage.flyingStars[i] = celestial.transform;
            }
            var hudObject = new GameObject("Field Notes HUD", typeof(UIDocument), typeof(TravellerHud)); hudObject.transform.SetParent(root.transform);
            string panelPath = Root + "/Settings/Traveller/FieldNotesPanel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (panel == null) { panel = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(panel, panelPath); }
            ConfigurePanelSettings(panel);
            hudObject.GetComponent<UIDocument>().panelSettings = panel;
            var hud = hudObject.GetComponent<TravellerHud>(); hud.stage = stage;
            hud.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf");
            hud.stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/UI/TravellerHud.uss");
            var sound = new GameObject("Wind / Room tone", typeof(AudioSource)); sound.transform.SetParent(root.transform);
            stage.ambience = sound.GetComponent<AudioSource>();
            stage.ambience.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/DesertWind.wav");
            stage.ambience.loop = true; stage.ambience.playOnAwake = true; stage.ambience.volume = profile.sceneIndex == 0 ? 0.24f : 0.1f;
        }

        private static void FinishScene(string name)
        {
            stage.fires = fireSystems.ToArray();
            stage.snow = stage.profile.sceneIndex == 1
                ? new[] { Particles("Capital snowfall", new Vector3(0f, 18f, -45f), new Vector3(320f, 3f, 330f), new Color(0.8f, 0.88f, 1f, 0.8f), 100f, 15f, 0.08f, -0.6f),
                    Particles("Water garden snowfall", new Vector3(85f, 12f, 0f), new Vector3(85f, 3f, 100f), new Color(0.8f, 0.88f, 1f, 0.8f), 65f, 15f, 0.08f, -0.6f) }
                : new[] { Particles("Snow", new Vector3(0f, 12f, 5f), new Vector3(90f, 3f, 100f), new Color(0.8f, 0.88f, 1f, 0.8f), 100f, 15f, 0.08f, -0.6f) };
            stage.embers = Particles("Heat ash", new Vector3(0f, 1f, 10f), new Vector3(60f, 2f, 70f), new Color(1f, 0.45f, 0.08f, 0.6f), 30f, 8f, 0.08f, 1f);
            stage.ApplyMoment(0, true);
            if (TravellerStoryCharacters.IsAvailable) TravellerStoryCharacters.ApplyToScene(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), SceneFolder + "/" + name + ".unity");
        }

        public static void ConfigurePanelSettings(PanelSettings panel)
        {
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.scale = 1f;
            EditorUtility.SetDirty(panel);
        }

        private static GameObject Shape(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, string material, Transform parent = null, bool collider = true)
        {
            var value = GameObject.CreatePrimitive(primitive); value.name = name;
            value.transform.SetParent(parent != null ? parent : world); value.transform.position = position; value.transform.localScale = scale;
            value.GetComponent<Renderer>().sharedMaterial = Materials[material];
            if (!collider) Object.DestroyImmediate(value.GetComponent<Collider>());
            else GameObjectUtility.SetStaticEditorFlags(value, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return value;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size, string material, Transform parent = null, bool collider = true)
        {
            var value = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            value.transform.SetParent(parent != null ? parent : world); value.transform.position = position;
            value.GetComponent<MeshFilter>().sharedMesh = CuboidMesh(size);
            value.GetComponent<MeshRenderer>().sharedMaterial = Materials[material];
            if (collider) value.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(value, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return value;
        }

        private static Mesh SaveMesh(string key, List<Vector3> vertices, List<int> triangles, List<Vector2> uv = null)
        {
            if (Meshes.TryGetValue(key, out var cached)) return cached;
            string path = MeshFolder + "/" + key + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = key }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            if (uv != null) mesh.SetUVs(0, uv);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); if (uv != null) mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh); Meshes[key] = mesh; return mesh;
        }

        private static Mesh CuboidMesh(Vector3 size)
        {
            string key = "Box_" + size.x.ToString("0.###") + "_" + size.y.ToString("0.###") + "_" + size.z.ToString("0.###");
            if (Meshes.TryGetValue(key, out var existing)) return existing;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            foreach (var normal in normals)
            {
                Vector3 axis = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 tangent = Vector3.Cross(normal, axis);
                int first = vertices.Count;
                foreach (Vector2 p in new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) })
                {
                    Vector3 point = Vector3.Scale(normal + axis * p.x + tangent * p.y, size * 0.5f);
                    vertices.Add(point);
                    uv.Add(new Vector2(Vector3.Dot(point, axis), Vector3.Dot(point, tangent)) * 0.35f);
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            return SaveMesh(key, vertices, triangles, uv);
        }

        private static GameObject MeshObject(string name, Mesh mesh, Vector3 position, string material, Transform parent = null, bool collider = false)
        {
            var value = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); value.transform.SetParent(parent != null ? parent : world); value.transform.position = position;
            value.GetComponent<MeshFilter>().sharedMesh = mesh; value.GetComponent<MeshRenderer>().sharedMaterial = Materials[material];
            if (collider) value.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(value, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return value;
        }

        private static GameObject Model(string name, string path, Vector3 position, Vector3 size, string material, Transform parent = null, bool collider = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new FileNotFoundException("Missing model " + path);
            // Preserve the imported FBX's axis conversion and unit scale inside
            // a wrapper. Normalising the imported transform itself shrinks
            // centimetre-scaled scans and can leave them floating above the scene.
            var value = new GameObject(name);
            value.transform.SetParent(parent != null ? parent : world); value.transform.position = Vector3.zero;
            var imported = (GameObject)PrefabUtility.InstantiatePrefab(asset); imported.transform.SetParent(value.transform, false);
            var renderers = value.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            value.transform.localScale = new Vector3(size.x / Mathf.Max(bounds.size.x, 0.001f), size.y / Mathf.Max(bounds.size.y, 0.001f), size.z / Mathf.Max(bounds.size.z, 0.001f));
            bounds = renderers[0].bounds; foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (material == "NatureAtlas")
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        if (original == null) return Materials[material];
                        string key = "Nature_" + original.name.Replace("/", "_").Replace("\\", "_");
                        if (!Materials.TryGetValue(key, out var imported))
                            imported = Material(key, original.HasProperty("_Color") ? original.color : new Color(0.27f, 0.32f, 0.19f));
                        return imported;
                    }).ToArray();
                }
                else renderer.sharedMaterials = Enumerable.Repeat(Materials[material], renderer.sharedMaterials.Length).ToArray();
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
            value.transform.position = position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var lod = value.AddComponent<LODGroup>(); lod.SetLODs(new[] { new LOD(0.009f, renderers) }); lod.RecalculateBounds();
            if (collider)
            {
                var collision = new GameObject("Simple collision", typeof(BoxCollider)); collision.transform.SetParent(value.transform, false);
                var box = collision.GetComponent<BoxCollider>(); box.size = new Vector3(size.x / value.transform.localScale.x, size.y / value.transform.localScale.y, size.z / value.transform.localScale.z);
                collision.transform.position = position + Vector3.up * size.y * 0.5f;
            }
            return value;
        }

        private static ParticleSystem Particles(string name, Vector3 position, Vector3 volume, Color color, float rate, float lifetime, float size, float speed)
        {
            var value = new GameObject(name, typeof(ParticleSystem)); value.transform.SetParent(stage.transform); value.transform.position = position;
            var system = value.GetComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.startLifetime = lifetime; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.5f);
            main.startColor = color; main.maxParticles = 1600; main.simulationSpace = ParticleSystemSimulationSpace.World; main.prewarm = true;
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = volume;
            var emission = system.emission; emission.rateOverTime = rate;
            var velocity = system.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.16f); velocity.y = new ParticleSystem.MinMaxCurve(speed, speed); velocity.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.18f);
            var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = Materials["Particle"]; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            system.Play(); return system;
        }

        private static void Fire(Vector3 position)
        {
            fireSystems.Add(Particles("Dry-grass flame", position, new Vector3(0.5f, 0.05f, 0.5f), new Color(1f, 0.31f, 0.015f, 0.8f), 20f, 1.3f, 0.4f, 1.2f));
            fireSystems.Add(Particles("Dry-grass smoke", position + Vector3.up, new Vector3(0.6f, 0.2f, 0.6f), new Color(0.16f, 0.14f, 0.11f, 0.2f), 7f, 5f, 1.3f, 0.8f));
        }
    }
}
