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
    public static class HunyuanExpansionLibrary
    {
        public const string Root = "Assets/_Project/Art/HunyuanExpansion";
        private const string Documentation = "Documentation/CharacterProduction";
        [Serializable] public class Export { public string projectFile; }
        [Serializable] public class Files { public Export rig, textured; }
        [Serializable] public class Asset { public string id, name, state; public Files files; }
        [Serializable] public class Manifest { public List<Asset> plannedCharacters, props; }
        [Serializable] public class Check
        {
            public string id, kind, source, prefab;
            public bool passed, validHumanAvatar, albedo, normal, metallicSmoothness;
            public int triangles, vertices, skinBones;
            public float heightMetres;
            public string[] textures;
        }
        [Serializable] public class Report { public List<Check> assets = new List<Check>(); }
        [Serializable] public class Motion
        {
            public string id, clip;
            public int poses, checkedVertices;
            public float boneRotationDegrees, vertexDisplacementMetres, largestRadiusMetres;
            public bool passed = true;
        }
        [Serializable] public class MotionReport
        {
            public string scope = "Five evaluated poses per available character/selected clip; bones, baked skin, finite numbers and gross bounds. Does not certify cloth intersections or finger/prop contact.";
            public List<Motion> combinations = new List<Motion>();
        }

        [MenuItem("Tools/Three Body Traveller/Prepare Downloaded Hunyuan Expansion")]
        public static Report PrepareAvailable()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing source models.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Documentation + "/hunyuan-expansion-2026-10-28.json"));
            Directory.CreateDirectory(Root + "/Prefabs"); AssetDatabase.Refresh();
            var report = new Report();
            foreach (var asset in manifest.plannedCharacters)
                if (asset.files != null)
                {
                    if (asset.files.rig != null && File.Exists(asset.files.rig.projectFile)) report.assets.Add(Prepare(asset, true));
                    else if (asset.files.textured != null && File.Exists(asset.files.textured.projectFile)) report.assets.Add(Prepare(asset, true, false));
                }
            foreach (var asset in manifest.props)
                if (asset.files != null && asset.files.textured != null && File.Exists(asset.files.textured.projectFile)) report.assets.Add(Prepare(asset, false));
            AssetDatabase.SaveAssets();
            File.WriteAllText(Documentation + "/hunyuan-expansion-unity-validation.json", JsonUtility.ToJson(report, true));
            if (report.assets.Any(a => !a.passed)) throw new InvalidDataException("An expansion source failed validation; inspect its report.");
            return report;
        }

        private static Check Prepare(Asset asset, bool character, bool rigged = true)
        {
            bool skinned = character && rigged;
            string source = skinned ? asset.files.rig.projectFile : asset.files.textured.projectFile;
            string folder = Path.GetDirectoryName(source), textures = folder + "/Textures";
            var importer = (ModelImporter)AssetImporter.GetAtPath(source);
            importer.animationType = skinned ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None;
            if (skinned)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                // A replacement FBX can retain another body's skeleton in its .meta.
                // This library uses automatic mapping, so regenerate it from this source.
                importer.humanDescription = new HumanDescription {
                    human = Array.Empty<HumanBone>(), skeleton = Array.Empty<SkeletonBone>(),
                    upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                    armStretch = .05f, legStretch = .05f
                };
            }
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            Directory.CreateDirectory(textures); importer.ExtractTextures(textures); AssetDatabase.Refresh();
            var textureFiles = Directory.GetFiles(textures, "*.png").Where(p => !p.EndsWith("_MetallicSmoothness.png")).ToArray();
            foreach (string path in textureFiles)
            {
                bool normal = Contains(path, "normal"), linear = normal || Contains(path, "roughness") || Contains(path, "metallic");
                ConfigureTexture(path, normal, !linear, Contains(path, "roughness") || Contains(path, "metallic"));
            }
            var embeddedMaterials = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Material>().ToArray();
            // Remapped materials no longer appear as FBX sub-assets on a repeat import.
            // Include their source identifiers so replacing a reviewed FBX refreshes every map.
            var materialIds = embeddedMaterials.Select(m => new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name))
                .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material))).Distinct().ToArray();
            foreach (var identifier in materialIds)
            {
                var original = embeddedMaterials.FirstOrDefault(m => m.name == identifier.name);
                string suffix = materialIds.Length == 1 ? "" : "_" + identifier.name.Replace(' ', '_');
                string path = folder + "/" + asset.id + suffix + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
                string albedo = textureFiles.FirstOrDefault(p => original != null && original.mainTexture != null && Path.GetFileNameWithoutExtension(p) == original.mainTexture.name)
                    ?? textureFiles.FirstOrDefault(p => !Contains(p, "normal") && !Contains(p, "metallic") && !Contains(p, "roughness") && !Contains(p, "specular") && !Contains(p, "emissive") && !Contains(p, "occlusion"));
                string normal = textureFiles.FirstOrDefault(p => Contains(p, "normal"));
                string metal = textureFiles.FirstOrDefault(p => Contains(p, "metallic"));
                string rough = textureFiles.FirstOrDefault(p => Contains(p, "roughness"));
                if (albedo == null) throw new InvalidDataException("No extracted albedo: " + asset.id);
                material.color = Color.white; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
                material.SetFloat("_Metallic", 0); material.SetFloat("_Glossiness", .22f);
                if (normal != null) { material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); material.SetFloat("_BumpScale", .7f); material.EnableKeyword("_NORMALMAP"); }
                if (metal != null && rough != null)
                {
                    string packedPath = textures + "/" + asset.id + "_MetallicSmoothness.png";
                    PackMaterialChannels(metal, rough, packedPath);
                    material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
                    material.SetFloat("_GlossMapScale", 1); material.EnableKeyword("_METALLICGLOSSMAP");
                }
                EditorUtility.SetDirty(material);
                importer.AddRemap(identifier, material);
            }
            importer.SaveAndReimport();
            foreach (string path in textureFiles) if (Contains(path, "metallic") || Contains(path, "roughness")) ConfigureTexture(path, false, false, false);
            return CreatePrefab(asset, character, rigged, textureFiles);
        }

        // Rebuild normalized prefabs after an import correction without re-encoding all textures.
        public static Report RebuildPrefabs()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before preparing prefabs.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Documentation + "/hunyuan-expansion-2026-10-28.json"));
            var report = new Report();
            foreach (var asset in manifest.plannedCharacters.Concat(manifest.props))
            {
                if (asset.files == null) continue;
                bool character = manifest.plannedCharacters.Contains(asset);
                bool rigged = character && asset.files.rig != null && File.Exists(asset.files.rig.projectFile);
                string source = rigged ? asset.files.rig.projectFile : asset.files.textured?.projectFile;
                if (source == null || !File.Exists(source)) continue;
                string folder = Path.GetDirectoryName(source) + "/Textures";
                report.assets.Add(CreatePrefab(asset, character, rigged, Directory.GetFiles(folder, "*.png").Where(p => !p.EndsWith("_MetallicSmoothness.png")).ToArray()));
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Documentation + "/hunyuan-expansion-unity-validation.json", JsonUtility.ToJson(report, true));
            if (report.assets.Any(a => !a.passed)) throw new InvalidDataException("An expansion prefab failed validation.");
            return report;
        }

        private static Check CreatePrefab(Asset asset, bool character, bool rigged, string[] textureFiles)
        {
            bool skinned = character && rigged;
            string source = skinned ? asset.files.rig.projectFile : asset.files.textured.projectFile;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(asset.id); SceneManager.MoveGameObjectToScene(root, scene);
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), scene); model.transform.SetParent(visual.transform, false);
                var animator = model.GetComponentInChildren<Animator>();
                if (skinned)
                {
                    var settings = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath);
                    animator.runtimeAnimatorController = settings.controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                float target = character ? Height(asset.id) : asset.id == "CalendarChest" ? .65f : asset.id == "BronzeHourglass" ? .45f : .32f;
                Action normalize = () => {
                    var bounds = Bounds(root);
                    visual.transform.localScale *= target / (character ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z));
                    bounds = Bounds(root); visual.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                };
                // Humanoid initialization can change the bind pose. Measure an evaluated idle
                // and reevaluate after scaling so humanScale and the baked skin agree.
                if (skinned) for (int pass = 0; pass < 3; pass++) TravellerStoryCharacters.WithSample(animator, "MX_Idle", .5f, normalize);
                else normalize();
                float height = 0;
                if (skinned) TravellerStoryCharacters.WithSample(animator, "MX_Idle", .5f, () => height = Bounds(root).size.y);
                else height = Bounds(root).size.y;
                foreach (var component in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                if (animator != null) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                string prefab = Root + "/Prefabs/" + asset.id + ".prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, prefab) == null) throw new IOException("Could not save " + prefab);
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var meshes = skins.Select(s => s.sharedMesh).Concat(root.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh)).ToArray();
                var materials = root.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                var check = new Check { id = asset.id, kind = character ? skinned ? "character" : "character_unrigged_candidate" : "prop", source = source, prefab = prefab,
                    triangles = meshes.Sum(m => m.triangles.Length / 3), vertices = meshes.Sum(m => m.vertexCount),
                    skinBones = skins.Length == 0 ? 0 : skins.Max(s => s.bones.Length), heightMetres = height,
                    validHumanAvatar = skinned && animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                    albedo = materials.Length > 0 && materials.All(m => m != null && m.mainTexture != null && m.shader.isSupported),
                    normal = materials.Length > 0 && materials.All(m => m != null && m.GetTexture("_BumpMap") != null),
                    metallicSmoothness = materials.Length > 0 && materials.All(m => m != null && m.GetTexture("_MetallicGlossMap") != null), textures = textureFiles };
                check.passed = check.albedo && check.normal && check.metallicSmoothness && check.triangles > 2000 && (!skinned || check.validHumanAvatar && check.skinBones >= 20 && Mathf.Abs(height - target) < .01f);
                return check;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static MotionReport CheckMotionsAndCapture()
        {
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Documentation + "/hunyuan-expansion-unity-validation.json"));
            var motions = new MotionReport(); var actors = new List<GameObject>(); var props = new List<GameObject>(); var graphs = new List<PlayableGraph>();
            var scene = EditorSceneManager.NewPreviewScene(); Directory.CreateDirectory(Documentation + "/Previews");
            try
            {
                foreach (var asset in report.assets.Where(a => a.passed))
                {
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(asset.prefab), scene);
                    if (asset.kind == "prop") { props.Add(root); continue; }
                    actors.Add(root);
                    if (asset.kind == "character_unrigged_candidate") continue;
                    var animator = root.GetComponentInChildren<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var skin = root.GetComponentInChildren<SkinnedMeshRenderer>(); skin.updateWhenOffscreen = true;
                    string[] clips = asset.id.StartsWith("Pope") ? new[] { "MX_Idle", "MX_SitIdle", "MX_TalkCalm", "MX_ShakingHeadNo", "MX_Walking" } : new[] { "MX_Idle", "MX_WeightShift", "MX_Walking", "MX_Shiver", "MX_InjuredHurtingIdle" };
                    foreach (string clip in clips)
                    {
                        var check = new Motion { id = asset.id, clip = clip }; var mesh = new Mesh(); Vector3[] baseline = null; Quaternion[] bones = null;
                        try
                        {
                            for (int pose = 0; pose < 5; pose++) TravellerStoryCharacters.WithSample(animator, clip, pose * .22f, () => {
                                skin.BakeMesh(mesh, true); var vertices = mesh.vertices.Select(v => skin.transform.TransformPoint(v) - root.transform.position).ToArray();
                                if (pose == 0) { baseline = vertices; bones = skin.bones.Select(b => b.localRotation).ToArray(); }
                                else for (int b = 0; b < bones.Length; b++) check.boneRotationDegrees = Mathf.Max(check.boneRotationDegrees, Quaternion.Angle(bones[b], skin.bones[b].localRotation));
                                for (int v = 0; v < vertices.Length; v++) { var p = vertices[v]; check.passed &= float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z); check.largestRadiusMetres = Mathf.Max(check.largestRadiusMetres, p.magnitude); if (pose != 0) check.vertexDisplacementMetres = Mathf.Max(check.vertexDisplacementMetres, Vector3.Distance(p, baseline[v])); }
                                check.poses++; check.checkedVertices += vertices.Length;
                            });
                        }
                        finally { Object.DestroyImmediate(mesh); }
                        check.passed &= check.largestRadiusMetres > 1 && check.largestRadiusMetres < 4 && check.boneRotationDegrees > .05f && check.vertexDisplacementMetres > .001f;
                        motions.combinations.Add(check);
                    }
                    var graph = PlayableGraph.Create("Expansion preview"); graphs.Add(graph); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var idle = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath).clips.First(c => c.name == "MX_Idle");
                    var playable = AnimationClipPlayable.Create(graph, idle); AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                    graph.Play(); graph.Evaluate(0); playable.SetTime(.5); graph.Evaluate(0);
                }
                var camera = NewObject("Expansion review camera", scene).AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .065f, .08f); camera.nearClipPlane = .05f; camera.farClipPlane = 40;
                foreach (var item in new[] { new Vector4(35, 205, 0, 1.25f), new Vector4(15, 125, 0, .6f) }) { var light = NewObject("Expansion review light", scene).AddComponent<Light>(); light.type = LightType.Directional; light.intensity = item.w; light.transform.rotation = Quaternion.Euler(item.x, item.y, item.z); }
                if (actors.Count > 0) Capture(camera, actors, props, graphs, false, Documentation + "/Previews/Expansion_Characters_Unity.jpg");
                for (int i = 0; i < graphs.Count; i++)
                {
                    graphs[i].Destroy();
                    var animator = actors[i].GetComponentInChildren<Animator>();
                    var walk = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath).clips.First(c => c.name == "MX_Walking");
                    var graph = PlayableGraph.Create("Expansion walking review"); graphs[i] = graph; graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, walk); AnimationPlayableOutput.Create(graph, "Walking", animator).SetSourcePlayable(playable);
                    graph.Play(); graph.Evaluate(0); playable.SetTime(walk.length * .55f); graph.Evaluate(0);
                }
                if (graphs.Count > 0) Capture(camera, actors, props, graphs, false, Documentation + "/Previews/Expansion_Walking_Unity.jpg");
                if (props.Count > 0) Capture(camera, actors, props, graphs, true, Documentation + "/Previews/Expansion_Props_Unity.jpg");
                File.WriteAllText(Documentation + "/hunyuan-expansion-motion-validation.json", JsonUtility.ToJson(motions, true));
                if (motions.combinations.Any(m => !m.passed)) throw new InvalidDataException("Expansion motion check failed.");
                return motions;
            }
            finally { foreach (var graph in graphs) if (graph.IsValid()) graph.Destroy(); EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Capture(Camera camera, List<GameObject> actors, List<GameObject> props, List<PlayableGraph> graphs, bool showProps, string path)
        {
            var objects = showProps ? props : actors; var temporary = new List<GameObject>(); var meshes = new List<Mesh>();
            try
            {
                foreach (var actor in actors) actor.SetActive(!showProps); foreach (var prop in props) prop.SetActive(showProps);
                for (int i = 0; i < objects.Count; i++) { var root = objects[i]; root.transform.position = new Vector3((i - (objects.Count - 1) * .5f) * (showProps ? 1.05f : 1.55f), 0, 0); if (showProps) root.transform.rotation = Quaternion.Euler(0, -25, 0); }
                if (!showProps) for (int i = 0; i < graphs.Count; i++) graphs[i].Evaluate(0);
                foreach (var skin in objects.SelectMany(o => o.GetComponentsInChildren<SkinnedMeshRenderer>()))
                {
                    var mesh = new Mesh(); meshes.Add(mesh); skin.BakeMesh(mesh, true); var baked = NewObject("Evaluated expansion skin", camera.scene); temporary.Add(baked); baked.transform.SetParent(skin.transform, false);
                    baked.AddComponent<MeshFilter>().sharedMesh = mesh; baked.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
                }
                camera.orthographicSize = showProps ? 1.0f : Mathf.Max(1.65f, actors.Count * .47f);
                camera.transform.position = showProps ? new Vector3(0, 1.1f, 7) : new Vector3(0, 1.6f, 10);
                camera.transform.LookAt(new Vector3(0, showProps ? .28f : .95f, 0)); TravellerSceneChecks.Capture(camera, path);
            }
            finally { foreach (var item in temporary) Object.DestroyImmediate(item); foreach (var mesh in meshes) Object.DestroyImmediate(mesh); foreach (var skin in actors.SelectMany(a => a.GetComponentsInChildren<SkinnedMeshRenderer>(true))) skin.enabled = true; }
        }
        private static GameObject NewObject(string name, Scene scene) { var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value; }
        private static bool Contains(string path, string text) => Path.GetFileName(path).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        private static float Height(string id) => id == "CitizenFemale" ? 1.65f : id == "CitizenMale" ? 1.73f : id == "ChurchGuard" ? 1.85f : id == "ShangGuard" ? 1.83f : 1.8f;
        private static Bounds Bounds(GameObject root)
        {
            bool first = true; var result = new Bounds();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var skin = renderer as SkinnedMeshRenderer;
                var next = renderer.bounds;
                if (skin != null)
                {
                    var mesh = new Mesh();
                    try
                    {
                        // With these FBX rigs, BakeMesh(false) includes the inherited scale.
                        // BakeMesh(true) keeps local vertices stable when the prefab is resized;
                        // TransformPoint then applies its world scale exactly once.
                        skin.BakeMesh(mesh, true); var vertices = mesh.vertices;
                        if (vertices.Length == 0) throw new InvalidDataException("Empty expansion skin.");
                        next = new Bounds(skin.transform.TransformPoint(vertices[0]), Vector3.zero);
                        for (int i = 1; i < vertices.Length; i++) next.Encapsulate(skin.transform.TransformPoint(vertices[i]));
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
                if (first) { result = next; first = false; } else result.Encapsulate(next);
            }
            return result;
        }
        private static void ConfigureTexture(string path, bool normal, bool srgb, bool readable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); if (importer == null) throw new FileNotFoundException(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; importer.sRGBTexture = srgb;
            importer.maxTextureSize = 2048; importer.isReadable = readable; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        }
        internal static void PackMaterialChannels(string metalPath, string roughPath, string output)
        {
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath); var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            if (metal.width != rough.width || metal.height != rough.height) throw new InvalidDataException("PBR source dimensions differ.");
            var pixels = metal.GetPixels32(); var roughPixels = rough.GetPixels32(); for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(pixels[i].r, 0, 0, (byte)(255 - roughPixels[i].r));
            var texture = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true); texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(output, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport); ConfigureTexture(output, false, false, false);
        }
    }
}
