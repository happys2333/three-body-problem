using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class CourtRetinueProduction
    {
        public const string Root = "Assets/_Project/Characters/Court";
        public const string Docs = "Documentation/CourtRetinue/2026-11-01";
        public const string District = "12 Royal attendants and spear sentries";
        private const string ScenePath = "Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity";
        private const string Spear = Root + "/Prefabs/ShangSpearSentry.prefab";
        private static readonly string[] Attendants = { "CourtSteward", "RitualAttendant", "CalendarAttendant", "ChamberAttendant" };
        private static readonly string[] Names = { "殿前礼官", "左执扇侍女", "传奏官", "右执扇侍女" };

        [MenuItem("Tools/Three Body Traveller/Capital/Prepare Court Retinue")]
        public static void Prepare()
        {
            EraGuardProduction.GuardProject();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene first.");
            foreach (string folder in new[] { Root + "/Prefabs", Root + "/Profiles", Root + "/Materials", Root + "/Personas", Docs }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            PrepareSentry();
            for (int i = 0; i < Attendants.Length; i++) PrepareAttendant(i);
            CourtAudienceProduction.PrepareAssets();
            AssetDatabase.SaveAssets();
        }
        private static GameObject CopyForAuthoring(string source, UnityEngine.SceneManagement.Scene scene)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (asset == null) throw new FileNotFoundException(source);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return root;
        }
        private static void PrepareSentry()
        {
            string path = Root + "/Profiles/CourtSpear.asset";
            var profile = AssetDatabase.LoadAssetAtPath<EraGuardProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<EraGuardProfile>(); AssetDatabase.CreateAsset(profile, path); }
            var source = AssetDatabase.LoadAssetAtPath<EraGuardProfile>(EraGuardProduction.Root + "/Profiles/ShangGuard_Bronze.asset");
            EditorUtility.CopySerialized(source, profile); profile.name = "Court spear sentry";
            profile.polearmEuler = Vector3.zero; profile.polearmPosition = new Vector3(.28f, .02f, .24f); profile.idleMotion = "MX_Idle";
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = CopyForAuthoring(TravellerCrowdLibrary.PrefabPath("ShangGuard_Bronze"), scene); root.name = "ShangSpearSentry";
                var actor = root.GetComponent<TravellerActor>(); var equipment = root.GetComponent<EraGuardEquipment>();
                equipment.profile = profile; ReplaceSpear(equipment);
                GuardGripAuthoring.AlignTargets(root, actor.animator, profile, equipment);
                root.GetComponent<GuardPostController>().allowPatrol = false;
                Bind(root, true, AssetDatabase.LoadAssetAtPath<NpcPersona>("Assets/_Project/Dialogue/Personas/shang_guard.asset"));
                PrefabUtility.SaveAsPrefabAsset(root, Spear);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static void ReplaceSpear(EraGuardEquipment equipment)
        {
            equipment.polearm.name = "BronzeSpear";
            equipment.polearm.localPosition = equipment.profile.polearmPosition;
            equipment.polearm.localRotation = Quaternion.identity;
            for (int i = 0; i < 3; i++)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(EraGuardProduction.Root + "/Meshes/BronzeSpear_LOD" + i + ".asset");
                if (mesh == null) throw new FileNotFoundException("Bronze spear LOD" + i);
                equipment.polearm.Find("LOD" + i).GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            equipment.polearm.GetComponent<LODGroup>().RecalculateBounds();
        }
        private static void PrepareAttendant(int index)
        {
            string id = Attendants[index]; bool female = index == 1 || index == 3;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                string source = female ? HunyuanExpansionLibrary.Root + "/Prefabs/CitizenFemale.prefab" : TravellerCrowdLibrary.PrefabPath(index == 0 ? "CitizenMale_Indigo" : "CitizenMale_Hemp");
                var root = CopyForAuthoring(source, scene); root.name = id;
                var actor = root.GetComponent<TravellerActor>(); if (actor == null) actor = root.AddComponent<TravellerActor>();
                actor.role = female ? TravellerRole.CitizenFemale : TravellerRole.CitizenMale;
                actor.settings = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath);
                actor.animator = root.GetComponentInChildren<Animator>(); actor.initialAnimationPhase = index * .19f;
                if (actor.animator.avatar == null || !actor.animator.avatar.isHuman || !actor.animator.avatar.isValid) throw new InvalidDataException(id + " has no valid Avatar.");
                actor.animator.runtimeAnimatorController = actor.settings.controller; actor.animator.applyRootMotion = false;
                actor.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                // The male source already contains a cloth mask that preserves skin colour.
                if (!female)
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var materials = skin.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        string path = Root + "/Materials/" + id + "_" + i + ".mat";
                        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null) { material = new Material(materials[i]); AssetDatabase.CreateAsset(material, path); }
                        else material.CopyPropertiesFromMaterial(materials[i]);
                        material.SetColor("_ClothColor", index == 0 ? new Color(.24f,.19f,.15f) : new Color(.28f,.33f,.30f));
                        EditorUtility.SetDirty(material); materials[i] = material;
                    }
                    skin.sharedMaterials = materials;
                }
                var member = root.GetComponent<TravellerCrowdMember>(); if (member == null) member = root.AddComponent<TravellerCrowdMember>();
                member.isGuard = member.isReviving = false; member.variation = index; member.strollPoints = Array.Empty<Vector3>();
                var capsule = root.GetComponent<CapsuleCollider>(); if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = female ? 1.65f : 1.73f; capsule.radius = .26f; capsule.center = Vector3.up * capsule.height * .5f;
                var lod = root.GetComponent<LODGroup>(); if (lod == null) lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.012f, root.GetComponentsInChildren<Renderer>()) }); lod.RecalculateBounds();
                string personaPath = Root + "/Personas/" + id + ".asset";
                var persona = AssetDatabase.LoadAssetAtPath<NpcPersona>(personaPath);
                if (persona == null) { persona = ScriptableObject.CreateInstance<NpcPersona>(); AssetDatabase.CreateAsset(persona, personaPath); }
                persona.characterId = "citizen"; persona.displayName = Names[index]; persona.aliases = new[] { Names[index], female?"侍女":"官员" };
                persona.prompt = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Dialogue/Prompts/bystander.txt");
                persona.fallbackReply = "大王正在听取献历，请在阶前等候。殿内的事可以问我，历法仍请呈给大王查验。";
                EditorUtility.SetDirty(persona); Bind(root, false, persona);
                root.GetComponent<TorchBearer>().equipped = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + id + ".prefab");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static CourtRetainer Bind(GameObject root, bool guard, NpcPersona persona)
        {
            var member = root.GetComponent<TravellerCrowdMember>(); member.strollPoints = Array.Empty<Vector3>();
            var torch = root.GetComponent<TorchBearer>(); if (torch == null) torch = root.AddComponent<TorchBearer>();
            torch.torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
            var identity = root.GetComponent<NpcIdentity>(); if (identity == null) identity = root.AddComponent<NpcIdentity>();
            identity.persona = persona; identity.interactive = true;
            var retainer = root.GetComponent<CourtRetainer>(); if (retainer == null) retainer = root.AddComponent<CourtRetainer>();
            retainer.spearGuard = guard; return retainer;
        }

        [MenuItem("Tools/Three Body Traveller/Capital/Apply Court Retinue")]
        public static void Apply()
        {
            EraGuardProduction.GuardProject(); var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Open the saved capital scene first.");
            string backup = "Logs/court-retinue-backups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup); File.Copy(ScenePath, backup + "/02_ChaogeCourt.unity");
            var stage = Object.FindFirstObjectByType<TravellerStage>(); var capital = stage.GetComponentsInChildren<Transform>().Single(t => t.name == "Shang Capital");
            var old = capital.Find(District); if (old != null) Object.DestroyImmediate(old.gameObject);
            var district = new GameObject(District); district.transform.SetParent(capital, false);
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(Spear).GetComponent<EraGuardEquipment>();
            foreach (var actor in stage.GetComponentsInChildren<TravellerActor>().Where(a => a.role == TravellerRole.ShangGuard && (a.name.StartsWith("石殿卫兵", StringComparison.Ordinal) || a.name.StartsWith("入口卫兵", StringComparison.Ordinal))).ToArray())
            {
                var equipment = actor.GetComponent<EraGuardEquipment>(); equipment.profile = template.profile;
                equipment.rightHandLocalRotation = template.rightHandLocalRotation; equipment.leftHandLocalRotation = template.leftHandLocalRotation;
                equipment.rightPalmOffset = template.rightPalmOffset; equipment.leftPalmOffset = template.leftPalmOffset; ReplaceSpear(equipment);
                actor.GetComponent<GuardPostController>().allowPatrol = false;
                string side = actor.transform.position.x < 0 ? "west" : "east";
                var marker = Bind(actor.gameObject, true, AssetDatabase.LoadAssetAtPath<NpcPersona>("Assets/_Project/Dialogue/Personas/shang_guard.asset"));
                marker.stationId = (actor.name.StartsWith("石殿", StringComparison.Ordinal) ? "dais-" : "tunnel-") + side;
                if (marker.stationId.StartsWith("dais", StringComparison.Ordinal)) PlaceOnFloor(actor.transform, new Vector3(side == "west" ? -5.5f : 5.5f, 0, 29), 180);
                foreach (var component in actor.GetComponentsInChildren<Component>(true)) if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Spawn(Spear, "御前持矛亲卫", "throne-" + side, new Vector3(side * 4.05f, 0, 34.5f), 180, district.transform);
                Spawn(Spear, "殿门持矛卫兵", "hall-" + side, new Vector3(side * 7.6f, 0, 10.6f), 180, district.transform);
            }
            var positions = new[] { new Vector3(5.8f,0,24.6f), new Vector3(-2.6f,0,35.1f), new Vector3(-5.8f,0,24.6f), new Vector3(2.6f,0,35.1f) };
            float[] angles = { 205,180,155,180 };
            for (int i = 0; i < Attendants.Length; i++) Spawn(Root + "/Prefabs/" + Attendants[i] + ".prefab", Names[i], Attendants[i], positions[i], angles[i], district.transform);
            CourtAudienceProduction.Apply(stage,district.transform);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Docs + "/integration.json", JsonUtility.ToJson(new Integration { scene = ScenePath, backup = backup, spearSentries = 8, chamberSentries = 6, attendants = 4, preservedExistingGuardRoots = 4 }, true));
        }
        [Serializable] private class Integration { public string scene, backup; public int spearSentries, chamberSentries, attendants, preservedExistingGuardRoots; }
        private static void Spawn(string path, string name, string id, Vector3 p, float yaw, Transform parent)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent.gameObject.scene);
            root.transform.SetParent(parent, false); root.name = name + " [Humanoid]"; PlaceOnFloor(root.transform, p, yaw);
            var actor = root.GetComponent<TravellerActor>(); actor.initialAnimationPhase = Mathf.Repeat((p.x + p.z) * .137f, 1);
            root.GetComponent<CourtRetainer>().stationId = id;
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor); PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<CourtRetainer>());
        }
        private static void PlaceOnFloor(Transform root, Vector3 p, float yaw)
        {
            // Query below the ceiling; ignore other people and small furniture.
            var floor = Physics.RaycastAll(new Vector3(p.x, 11.8f, p.z), Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y > .7f && (h.collider.name == "Royal stone dais step" || h.collider.name == "Enclosed palace stone floor"))
                .OrderByDescending(h => h.point.y).FirstOrDefault();
            if (floor.collider == null) throw new InvalidDataException("No palace floor for " + p);
            root.SetPositionAndRotation(new Vector3(p.x, floor.point.y + .01f, p.z), Quaternion.Euler(0, yaw, 0));
        }
    }
}
