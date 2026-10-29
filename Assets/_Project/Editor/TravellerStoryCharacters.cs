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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class TravellerStoryCharacters
    {
        public const string Root = "Assets/_Project/Characters/Story";
        public const string SettingsPath = Root + "/StoryCast.asset";
        public const string BodyMeshPath = Root + "/PlayerFirstPersonBody.asset";
        private static readonly string[] Names = { "玩家", "周文王", "纣王", "教皇", "学者", "达芬奇" };
        private static readonly float[] Heights = { 1.78f, 1.70f, 1.90f, 1.84f, 1.76f, 1.76f };
        public static bool IsAvailable => File.Exists(GeneratedCharacterLibrary.Root + "/Prefabs/PlayerTraveller.prefab");

        [MenuItem("Tools/Three Body Traveller/Integrate Story Characters And First Person Player")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before replacing scene characters.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var backup = "Logs/scene-backups/StoryCharacters_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            PrepareAssets();
            try
            {
                foreach (var name in TravellerStage.SceneNames)
                {
                    string path = TravellerSceneBuilder.SceneFolder + "/" + name + ".unity";
                    File.Copy(path, backup + "/" + name + ".unity", true);
                    var existing = SceneManager.GetSceneByPath(path);
                    bool wasLoaded = existing.IsValid() && existing.isLoaded;
                    var scene = wasLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    ApplyToScene(scene);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
        }

        public static void PrepareAssets()
        {
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<TravellerCastSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/StoryActors.controller");
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "/StoryActors.controller");
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(Root + "/SeatedUpperBody.mask");
            if (mask == null) { mask = new AvatarMask { name = "Seated upper body" }; AssetDatabase.CreateAsset(mask, Root + "/SeatedUpperBody.mask"); }
            for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, false);
            foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                mask.SetHumanoidBodyPartActive(part, true);
            if (controller.layers.Length < 2) controller.AddLayer("Seated Gestures");
            var layers = controller.layers; layers[1].avatarMask = mask; layers[1].defaultWeight = 0; controller.layers = layers;
            var clips = GeneratedCharacterLibrary.LoadClips();
            for (int layer = 0; layer < 2; layer++)
            {
                var machine = controller.layers[layer].stateMachine;
                foreach (var clip in clips)
                {
                    var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name) ?? machine.AddState(clip.name);
                    state.motion = clip; state.writeDefaultValues = true;
                }
                machine.defaultState = machine.states.First(s => s.state.name == (layer == 0 ? "MX_Idle" : "MX_SitIdle")).state;
                EditorUtility.SetDirty(machine);
            }
            settings.controller = controller; settings.clips = clips;
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(MixamoMotionLibrary.Root + "/YBot_TPose.fbx");
            settings.referenceHumanScale = reference.GetComponent<Animator>().humanScale;
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(mask);
            PrepareFirstPersonMesh(); AssetDatabase.SaveAssets();
        }

        private static GameObject CreateModel(TravellerRole role, Transform parent, Scene scene)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedCharacterLibrary.Root + "/Prefabs/" + role + ".prefab");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            model.transform.SetParent(parent, false);
            var animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(SettingsPath).controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Sample(animator, "MX_Idle", 0.2f);
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var bounds = GeneratedCharacterLibrary.SkinnedWorldBounds(skin);
            model.transform.localScale *= Heights[(int)role] / bounds.size.y;
            model.transform.position += Vector3.up * (parent.position.y + 0.01f - GeneratedCharacterLibrary.SkinnedWorldBounds(skin).min.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            foreach (var part in model.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
            return model;
        }

        public static void ApplyToScene(Scene scene)
        {
            var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TravellerStage>(true)).Single();
            if (AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(SettingsPath) == null) PrepareAssets();
            var director = stage.GetComponent<TravellerCastDirector>();
            if (director == null) director = stage.gameObject.AddComponent<TravellerCastDirector>();
            // All generated replacements live under the old placement anchors, preserving route references.
            foreach (TravellerRole role in Enum.GetValues(typeof(TravellerRole)))
            {
                if (role == TravellerRole.PlayerTraveller || (int)role >= Names.Length) continue;
                var anchor = stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(Names[(int)role] + " [", StringComparison.Ordinal));
                if (anchor == null) continue;
                bool seated = role == TravellerRole.KingZhou || role == TravellerRole.Pope;
                if (role == TravellerRole.Pope) anchor.position = new Vector3(0, 1.8f, 35.2f);
                for (int i = anchor.childCount - 1; i >= 0; i--) Object.DestroyImmediate(anchor.GetChild(i).gameObject);
                anchor.name = Names[(int)role] + " [Humanoid]";
                var model = CreateModel(role, anchor, scene);
                var actor = anchor.GetComponent<TravellerActor>();
                if (actor == null) actor = anchor.gameObject.AddComponent<TravellerActor>();
                ConfigureActor(actor, role, model, seated);
                var collision = anchor.GetComponent<CapsuleCollider>();
                if (collision != null) { collision.height = seated ? 1.25f : Heights[(int)role]; collision.center = Vector3.up * collision.height * 0.5f; collision.radius = 0.3f; }
                anchor.rotation = Quaternion.Euler(0, 180, 0);
                if (role == TravellerRole.WenWang) { director.companion = actor; AddHourglass(model, actor.animator, scene); }
                else if (role == TravellerRole.KingZhou) director.king = actor;
                else if (role == TravellerRole.Pope) director.pope = actor;
                else if (role == TravellerRole.Scholar) { director.scholar = actor; anchor.rotation = Quaternion.LookRotation(new Vector3(-5.3f, 0, -5)); }
                else if (role == TravellerRole.DaVinci) { director.daVinci = actor; anchor.rotation = Quaternion.LookRotation(new Vector3(5.3f, 0, 0)); }
                if (seated)
                {
                    Sample(actor.animator, "MX_SitIdle", 0.2f);
                    string seatName = role == TravellerRole.KingZhou ? "Royal stone seat" : "Papal seat";
                    var seat = stage.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == seatName);
                    var seatCollider = seat == null ? null : seat.GetComponent<Collider>();
                    if (seatCollider != null)
                    {
                        float hipY = actor.animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
                        model.transform.position += Vector3.up * (seatCollider.bounds.max.y + 0.11f - hipY);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                    }
                }
                var lod = anchor.GetComponent<LODGroup>();
                if (lod == null) lod = anchor.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.012f, model.GetComponentsInChildren<Renderer>()) }); lod.RecalculateBounds();
                foreach (var part in anchor.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
            }
            ApplyPlayer(stage, director, scene);
            director.scholarExitPath = new[] { new Vector3(3, 0, 14), new Vector3(3, 0, -20), new Vector3(2.5f, 0, -32), new Vector3(4.5f, 0, -47) };
            director.daVinciExitPath = new[] { new Vector3(-3, 0, 10), new Vector3(-3, 0, -20), new Vector3(-2.5f, 0, -32), new Vector3(-4.5f, 0, -47) };
            EditorUtility.SetDirty(director);
            if (TravellerCrowdLibrary.IsPrepared) TravellerCrowdLibrary.ApplyToScene(scene);
        }

        private static void ConfigureActor(TravellerActor actor, TravellerRole role, GameObject model, bool seated)
        {
            actor.role = role; actor.settings = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(SettingsPath);
            actor.animator = model.GetComponentInChildren<Animator>(); actor.seated = seated;
            EditorUtility.SetDirty(actor);
        }

        private static void ApplyPlayer(TravellerStage stage, TravellerCastDirector director, Scene scene)
        {
            var player = stage.player;
            var old = player.transform.Find("First person body");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var body = new GameObject("First person body"); SceneManager.MoveGameObjectToScene(body, scene); body.transform.SetParent(player.transform, false);
            // Keep the eye in front of the cut collar, so looking down sees the outer clothing surface.
            body.transform.localPosition = Vector3.back * 0.27f;
            var model = CreateModel(TravellerRole.PlayerTraveller, body.transform, scene);
            var actor = body.AddComponent<TravellerActor>(); ConfigureActor(actor, TravellerRole.PlayerTraveller, model, false);
            actor.firstPersonPlayer = player; player.bodyActor = actor; director.playerActor = actor;
            var shadow = model.GetComponentInChildren<SkinnedMeshRenderer>();
            shadow.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            PrefabUtility.RecordPrefabInstancePropertyModifications(shadow);
            var visible = new GameObject("Visible body without head").AddComponent<SkinnedMeshRenderer>();
            visible.transform.SetParent(shadow.transform.parent, false);
            visible.transform.localPosition = shadow.transform.localPosition; visible.transform.localRotation = shadow.transform.localRotation; visible.transform.localScale = shadow.transform.localScale;
            visible.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            visible.sharedMaterials = shadow.sharedMaterials; visible.bones = shadow.bones; visible.rootBone = shadow.rootBone;
            visible.localBounds = shadow.localBounds; visible.shadowCastingMode = ShadowCastingMode.Off;
            visible.updateWhenOffscreen = true;
            player.walkSpeed = 3f; player.eyeHeight = 1.65f;
            player.viewCamera.transform.SetParent(player.transform, false); player.viewCamera.transform.localPosition = Vector3.up * player.eyeHeight;
            player.viewCamera.nearClipPlane = 0.06f;
            EditorUtility.SetDirty(player); EditorUtility.SetDirty(player.viewCamera);
        }

        private static void PrepareFirstPersonMesh()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("First person mesh source"); SceneManager.MoveGameObjectToScene(root, scene);
                var model = CreateModel(TravellerRole.PlayerTraveller, root.transform, scene);
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh = Object.Instantiate(skin.sharedMesh); mesh.name = "Player body without camera-occluding head";
                var baked = new Mesh(); skin.BakeMesh(baked, false);
                var vertices = baked.vertices; var weights = mesh.boneWeights;
                var head = model.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                var neck = model.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Neck);
                var safe = new bool[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i]; float headWeight = 0;
                    foreach (int bone in new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 })
                    {
                        if (skin.bones[bone] != head && skin.bones[bone] != neck) continue;
                        if (bone == w.boneIndex0) headWeight += w.weight0;
                        if (bone == w.boneIndex1) headWeight += w.weight1;
                        if (bone == w.boneIndex2) headWeight += w.weight2;
                        if (bone == w.boneIndex3) headWeight += w.weight3;
                    }
                    safe[i] = headWeight < 0.05f && skin.transform.TransformPoint(vertices[i]).y < 1.42f;
                }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var input = mesh.GetTriangles(sub); var kept = new List<int>();
                    for (int i = 0; i < input.Length; i += 3)
                        if (safe[input[i]] && safe[input[i + 1]] && safe[input[i + 2]]) { kept.Add(input[i]); kept.Add(input[i + 1]); kept.Add(input[i + 2]); }
                    mesh.SetTriangles(kept, sub);
                }
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
                if (existing == null) AssetDatabase.CreateAsset(mesh, BodyMeshPath);
                else { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); }
                Object.DestroyImmediate(baked);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AddHourglass(GameObject model, Animator animator, Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Traveller/Hourglass.prefab");
            if (prefab == null) return;
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); prop.name = "Traveller back-mounted hourglass";
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            prop.transform.position = model.transform.position + Vector3.up * 0.72f - model.transform.forward * 0.28f;
            prop.transform.rotation = model.transform.rotation; prop.transform.localScale = Vector3.one * 0.85f;
            prop.transform.SetParent(chest, true);
            foreach (var collider in prop.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
        }

        public static void Sample(Animator animator, string id, float normalized)
        {
            WithSample(animator, id, normalized, () => { });
        }

        // Bone attachments and prefab overrides must be evaluated while the graph still owns the pose.
        // Destroying a graph restores bone transforms even when a cached BakeMesh still shows its last skin.
        public static void WithSample(Animator animator, string id, float normalized, Action action)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(SettingsPath);
            var clip = settings.clips.First(c => c.name == id);
            var graph = PlayableGraph.Create("Character placement pose");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play(); graph.Evaluate(0); playable.SetTime(clip.length * normalized); graph.Evaluate(0);
                action();
            }
            finally { graph.Destroy(); }
        }
    }
}
