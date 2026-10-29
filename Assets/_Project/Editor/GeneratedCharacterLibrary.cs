using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class GeneratedCharacterLibrary
    {
        public const string Root = "Assets/_Project/Characters/Hunyuan";
        public static readonly string[] Roles = { "PlayerTraveller", "WenWang", "KingZhou", "Pope", "Scholar", "DaVinci" };

        [Serializable] public class ActorCheck { public string role; public bool validAvatar; public bool humanAvatar; public int triangles; public int skinBones; public bool albedo; public bool normal; public float height; }
        [Serializable] public class ClipCheck { public string name; public float seconds; public bool humanoid; public bool looping; }
        [Serializable] public class LibraryCheck { public List<ActorCheck> actors = new List<ActorCheck>(); public List<ClipCheck> clips = new List<ClipCheck>(); }

        [MenuItem("Tools/Three Body Traveller/Prepare Generated Characters")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before preparing character assets.");
            Directory.CreateDirectory(Root + "/Animations"); Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            foreach (var role in Roles) PrepareMaterial(role);
            var avatar = AvatarFor(Root + "/PlayerTraveller/PlayerTraveller_Rig.fbx");
            var clips = new List<AnimationClip>();
            foreach (var path in Directory.GetFiles(Root + "/Animations", "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; importer.sourceAvatar = avatar;
                importer.importAnimation = true; importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var definitions = importer.defaultClipAnimations;
                foreach (var definition in definitions)
                {
                    definition.name = Path.GetFileNameWithoutExtension(path);
                    definition.loopTime = definition.name.StartsWith("Idle") || definition.name.StartsWith("Walk") || definition.name.StartsWith("Run");
                    definition.loopPose = definition.loopTime;
                    definition.lockRootHeightY = true; definition.lockRootPositionXZ = true;
                    definition.lockRootRotation = !definition.name.StartsWith("Turn");
                }
                importer.clipAnimations = definitions; importer.SaveAndReimport();
                clips.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")));
            }
            string controllerPath = Root + "/GeneratedActors.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var clip in clips)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name) ?? machine.AddState(clip.name);
                state.motion = clip;
                if (clip.name.StartsWith("Idle")) machine.defaultState = state;
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var role in Roles)
                {
                    var actor = new GameObject(role);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, preview);
                    var visual = new GameObject("Visual"); visual.transform.SetParent(actor.transform, false);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + role + "/" + role + "_Rig.fbx"), preview);
                    model.transform.SetParent(visual.transform, false);
                    var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                    float height = role == "KingZhou" ? 1.94f : role == "WenWang" ? 1.72f : 1.82f;
                    // Imported renderer bounds contain pose padding, especially for the long robe.
                    // Measure actual skinned vertices so character size and ground contact are stable.
                    var bounds = SkinnedWorldBounds(skin);
                    visual.transform.localScale = Vector3.one * (height / bounds.size.y);
                    visual.transform.position -= Vector3.up * SkinnedWorldBounds(skin).min.y;
                    var animator = model.GetComponent<Animator>();
                    animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                    PrefabUtility.SaveAsPrefabAsset(actor, Root + "/Prefabs/" + role + ".prefab");
                    Object.DestroyImmediate(actor);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var fall = clips.FirstOrDefault(c => c.name == "FallForward");
            if (fall != null)
            {
                var grounded = PrepareGroundedFall(fall);
                machine.states.First(s => s.state.name == "FallForward").state.motion = grounded;
            }
            var mixamoIdle = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "MX_Idle");
            if (mixamoIdle != null) machine.defaultState = mixamoIdle;
            AssetDatabase.SaveAssets();
            Validate();
        }

        private static void PrepareMaterial(string role)
        {
            string folder = Root + "/" + role, textures = folder + "/Textures";
            var importer = (ModelImporter)AssetImporter.GetAtPath(folder + "/" + role + "_Rig.fbx");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            Directory.CreateDirectory(textures); AssetDatabase.Refresh();
            if (!File.Exists(textures + "/texture_pbr_20250901.png")) importer.ExtractTextures(textures);
            AssetDatabase.Refresh();
            string prefix = textures + "/texture_pbr_20250901";
            ConfigureTexture(prefix + ".png", false, true, false);
            ConfigureTexture(prefix + "_normal.png", true, false, false);
            string packedPath = textures + "/" + role + "_MetallicSmoothness.png";
            if (!File.Exists(packedPath) || File.GetLastWriteTimeUtc(packedPath) < File.GetLastWriteTimeUtc(prefix + "_roughness.png"))
            {
                ConfigureTexture(prefix + "_metallic.png", false, false, true);
                ConfigureTexture(prefix + "_roughness.png", false, false, true);
                var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_metallic.png");
                var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_roughness.png");
                var pixels = metal.GetPixels32(); var roughPixels = rough.GetPixels32();
                if (metal.width != rough.width || metal.height != rough.height) throw new InvalidDataException("PBR map size mismatch: " + role);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(pixels[i].r, 0, 0, (byte)(255 - roughPixels[i].r));
                var packed = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true);
                packed.SetPixels32(pixels); packed.Apply();
                File.WriteAllBytes(packedPath, packed.EncodeToPNG()); Object.DestroyImmediate(packed);
                AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
            }
            ConfigureTexture(packedPath, false, false, false);
            ConfigureTexture(prefix + "_metallic.png", false, false, false);
            ConfigureTexture(prefix + "_roughness.png", false, false, false);
            string materialPath = folder + "/" + role + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, materialPath); }
            material.color = Color.white; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + ".png");
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_normal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            material.SetFloat("_BumpScale", 0.7f); material.SetFloat("_GlossMapScale", 1f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material);
            var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material");
            Object remapped;
            if (!importer.GetExternalObjectMap().TryGetValue(key, out remapped) || remapped != material)
            {
                importer.AddRemap(key, material); importer.SaveAndReimport();
            }
        }

        private static void ConfigureTexture(string path, bool normal, bool srgb, bool readable)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == 2048 && importer.isReadable == readable
                && importer.textureCompression == TextureImporterCompression.CompressedHQ) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb; importer.maxTextureSize = 2048; importer.isReadable = readable;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        }

        public static Avatar AvatarFor(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();

        public static AnimationClip[] LoadClips()
        {
            return Directory.GetFiles(Root + "/Animations", "*.fbx").OrderBy(p => p).SelectMany(path =>
            {
                var corrected = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path.ChangeExtension(path, ".anim"));
                return corrected != null ? new[] { corrected } : AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview")).ToArray();
            }).Concat(MixamoMotionLibrary.LoadClips()).ToArray();
        }

        private static AnimationClip PrepareGroundedFall(AnimationClip source)
        {
            // The downloaded fall rotates the body but holds the hips at standing height.
            // Preserve the FBX, and derive a normalized body-height curve for the Unity clip.
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
            var originalHeight = AnimationUtility.GetEditorCurve(source, binding);
            if (originalHeight == null) throw new InvalidDataException("Fall clip has no humanoid body height curve.");
            var scene = EditorSceneManager.NewPreviewScene();
            var graph = PlayableGraph.Create("Ground generated fall");
            var corrected = Object.Instantiate(source); corrected.name = "FallForward"; corrected.hideFlags = HideFlags.None;
            try
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/PlayerTraveller.prefab"), scene);
                var animator = actor.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
                var playable = AnimationClipPlayable.Create(graph, source);
                AnimationPlayableOutput.Create(graph, "Source fall", animator).SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play(); graph.Evaluate(0);
                int frames = Mathf.CeilToInt(source.length * source.frameRate);
                var keys = new Keyframe[frames + 1];
                for (int i = 0; i <= frames; i++)
                {
                    float time = Mathf.Min(source.length, i / source.frameRate);
                    playable.SetTime(time); graph.Evaluate(0);
                    float lift = Mathf.Max(0, SkinnedWorldBounds(skin).min.y - 0.02f);
                    keys[i] = new Keyframe(time, originalHeight.Evaluate(time) - lift / animator.humanScale);
                }
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(corrected, binding, curve);
                string path = Root + "/Animations/FallForward.anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) { AssetDatabase.CreateAsset(corrected, path); return corrected; }
                EditorUtility.CopySerialized(corrected, existing); EditorUtility.SetDirty(existing); return existing;
            }
            finally
            {
                if (!AssetDatabase.Contains(corrected)) Object.DestroyImmediate(corrected);
                graph.Destroy(); EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static Bounds SkinnedWorldBounds(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, false);
                var vertices = mesh.vertices;
                if (vertices.Length == 0) throw new InvalidDataException("Empty character skin.");
                var bounds = new Bounds(skin.transform.TransformPoint(vertices[0]), Vector3.zero);
                for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(skin.transform.TransformPoint(vertices[i]));
                return bounds;
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        public static LibraryCheck Validate()
        {
            var report = new LibraryCheck();
            foreach (var role in Roles)
            {
                string folder = Root + "/" + role;
                var avatar = AvatarFor(folder + "/" + role + "_Rig.fbx");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + role + ".prefab");
                var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>(); var material = skin.sharedMaterial;
                var check = new ActorCheck { role = role, validAvatar = avatar != null && avatar.isValid, humanAvatar = avatar != null && avatar.isHuman,
                    triangles = skin.sharedMesh.triangles.Length / 3, skinBones = skin.bones.Length, albedo = material.mainTexture != null,
                    normal = material.GetTexture("_BumpMap") != null, height = SkinnedWorldBounds(skin).size.y };
                if (!check.validAvatar || !check.humanAvatar || !check.albedo || !check.normal) throw new InvalidDataException("Character validation failed: " + role);
                report.actors.Add(check);
            }
            foreach (var clip in LoadClips())
                report.clips.Add(new ClipCheck { name = clip.name, seconds = clip.length, humanoid = clip.humanMotion, looping = clip.isLooping });
            Directory.CreateDirectory("Documentation/CharacterProduction");
            File.WriteAllText("Documentation/CharacterProduction/unity-character-validation.json", JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
