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
    public static class EraGuardProduction
    {
        public const string Root = "Assets/_Project/Characters/Guards";
        public const string Docs = "Documentation/SceneProduction/2026-11-01";
        private const string Work = "ArtSource/Guards/2026-11-01/Equipment";
        private static readonly string[] Ids = { "ShangGuard_Bronze", "ShangGuard_Dark", "ChurchGuard_Steel" };
        private static readonly HashSet<string> PreparedSceneEdits = new HashSet<string>();

        [MenuItem("Tools/Three Body Traveller/Guards/Prepare Era Guards")]
        public static void Prepare()
        {
            GuardProject();
            PreparedSceneEdits.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before preparing guard prefabs.");
            foreach (string folder in new[] { Root, Root + "/Profiles", Root + "/Meshes", Root + "/Materials", Root + "/Equipment" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            foreach (var id in Ids) PrepareVariant(id, id.StartsWith("Church", StringComparison.Ordinal) ? TravellerRole.ChurchGuard : TravellerRole.ShangGuard);
            AssetDatabase.SaveAssets();
            // Reload serialized vertex buffers after replacing mesh data in place.
            foreach (string mesh in Directory.GetFiles(Root + "/Meshes", "*.asset"))
                AssetDatabase.ImportAsset(mesh, ImportAssetOptions.ForceUpdate);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) PreparedSceneEdits.Add(SceneManager.GetSceneAt(i).path);
        }

        public static void GuardProject()
        {
            if (!Application.dataPath.EndsWith("/three-body-problem/Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("This production entry belongs to the Three Body project.");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring guard prefabs.");
        }

        public static void PrepareVariant(string id, TravellerRole role)
        {
            GuardProject();
            foreach (string folder in new[] { Root + "/Profiles", Root + "/Meshes", Root + "/Materials", Root + "/Equipment" })
                Directory.CreateDirectory(folder);
            bool medieval = role == TravellerRole.ChurchGuard;
            string source = HunyuanExpansionLibrary.Root + "/Prefabs/" + (medieval ? "ChurchGuard" : "ShangGuard") + ".prefab";
            string revision = HunyuanExpansionLibrary.Root + "/Prefabs/ShangGuardV2.prefab";
            if (id == "ShangGuard_Bronze" && File.Exists(revision)) source = revision;
            var bodyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (bodyAsset == null) throw new FileNotFoundException("Prepare the dedicated Humanoid body first.", source);
            string profilePath = Root + "/Profiles/" + id + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<EraGuardProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<EraGuardProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            profile.role = role; profile.bodySource = source; profile.standingHeight = medieval ? 1.85f : 1.83f;
            if (profile.layoutVersion < 4)
            {
                profile.polearmPosition = new Vector3(.28f, .08f, .24f);
                profile.polearmGripHeight = 1.18f;
                profile.shieldPosition = new Vector3(-.32f, 1.17f, .25f);
                profile.torchGrip = new Vector3(-.30f, 1.20f, .23f);
                profile.knuckleDistance = .10f;
                profile.layoutVersion = 4;
            }
            EditorUtility.SetDirty(profile);
            var materials = EquipmentMaterials(medieval);
            string weapon = medieval ? "MedievalSteelSpear" : id.EndsWith("Bronze", StringComparison.Ordinal) ? "ShangBronzeGe" : "BronzeSpear";
            string shield = medieval ? "MedievalKiteShield" : "ShangLeatherShield";
            string path = TravellerCrowdLibrary.Root + "/Prefabs/" + id + ".prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var child in contents.transform.Cast<Transform>().ToArray())
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                        PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(child.gameObject);
                }
                var body = (GameObject)PrefabUtility.InstantiatePrefab(bodyAsset, contents.scene);
                body.transform.SetParent(contents.transform, false); body.name = "Dedicated " + (medieval ? "medieval" : "Shang") + " guard body";
                var animator = body.GetComponentInChildren<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                    throw new InvalidDataException("Dedicated body has no valid Humanoid Avatar: " + source);
                var actor = contents.GetComponent<TravellerActor>();
                actor.role = role; actor.animator = animator;
                actor.settings = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath);
                animator.runtimeAnimatorController = actor.settings.controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var member = contents.GetComponent<TravellerCrowdMember>(); member.isGuard = true; member.isReviving = false;
                var capsule = contents.GetComponent<CapsuleCollider>();
                capsule.height = profile.standingHeight; capsule.center = Vector3.up * capsule.height * .5f; capsule.radius = .28f;
                var equipment = contents.GetComponent<EraGuardEquipment>();
                if (equipment == null) equipment = contents.AddComponent<EraGuardEquipment>();
                equipment.profile = profile;
                equipment.polearm = BuildEquipment(weapon, contents.transform, materials);
                equipment.shield = BuildEquipment(shield, contents.transform, materials);
                equipment.polearm.localPosition = profile.polearmPosition;
                equipment.polearm.localRotation = Quaternion.Euler(profile.polearmEuler);
                equipment.shield.localPosition = profile.shieldPosition;
                GuardGripAuthoring.Configure(contents, animator, profile, equipment);
                var post = contents.GetComponent<GuardPostController>();
                if (post == null) post = contents.AddComponent<GuardPostController>();
                post.allowPatrol = false;
                var bodyLod = contents.GetComponent<LODGroup>();
                bodyLod.SetLODs(new[] { new LOD(.012f, body.GetComponentsInChildren<Renderer>()) }); bodyLod.RecalculateBounds();
                foreach (var t in contents.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static Transform BuildEquipment(string id, Transform parent, Material[] materials)
        {
            var root = new GameObject(id); root.transform.SetParent(parent, false);
            var group = root.AddComponent<LODGroup>(); var lods = new LOD[3];
            for (int level = 0; level < 3; level++)
            {
                string file = Work + "/" + id + "/" + id + "_LOD" + level + ".meshbin";
                var mesh = ReadEquipmentMesh(file, id + "_LOD" + level);
                var child = new GameObject("LOD" + level, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(root.transform, false); child.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                lods[level] = new LOD(level == 0 ? .2f : level == 1 ? .065f : .012f, new Renderer[] { renderer });
            }
            group.SetLODs(lods); group.RecalculateBounds(); return root.transform;
        }

        private static Mesh ReadEquipmentMesh(string path, string id)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(4)) != "TBM1") throw new InvalidDataException(path);
                int count = r.ReadInt32(), indexCount = r.ReadInt32(), slots = r.ReadInt32();
                if (count < 3 || slots != 3 || indexCount % 3 != 0) throw new InvalidDataException("Invalid equipment mesh: " + id);
                var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
                for (int i = 0; i < count; i++) { float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(); vertices[i] = new Vector3(x, z, -y); }
                for (int i = 0; i < count; i++) { float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(); normals[i] = new Vector3(x, z, -y).normalized; }
                for (int i = 0; i < count; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                var indices = new int[indexCount]; for (int i = 0; i < indexCount; i++) indices[i] = r.ReadInt32();
                var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
                for (int i = 0; i < indexCount / 3; i++) { int slot = r.ReadInt32(); for (int c = 0; c < 3; c++) triangles[slot].Add(indices[i * 3 + c]); }
                var mesh = new Mesh { name = id, indexFormat = IndexFormat.UInt32, vertices = vertices, normals = normals, uv = uv, subMeshCount = 3 };
                for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string assetPath = Root + "/Meshes/" + id + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, assetPath); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                return saved;
            }
        }

        private static Material[] EquipmentMaterials(bool medieval)
        {
            var result = new Material[3];
            string[] names = { "GuardWood", medieval ? "GuardSteel" : "GuardBronze", medieval ? "GuardRedLeather" : "GuardBrownLeather" };
            var wood = AssetDatabase.LoadAssetAtPath<Material>(TravellerCrowdLibrary.Root + "/Materials/CrowdShieldWood.mat");
            for (int i = 0; i < 3; i++)
            {
                string path = Root + "/Materials/" + names[i] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
                m.color = i == 0 ? new Color(.52f, .39f, .24f) : i == 1 ? medieval ? new Color(.45f, .49f, .52f) : new Color(.39f, .32f, .16f) : medieval ? new Color(.24f, .045f, .028f) : new Color(.18f, .12f, .07f);
                m.SetFloat("_Metallic", i == 1 ? .82f : 0); m.SetFloat("_Glossiness", i == 1 ? .35f : .19f);
                if (i == 0 && wood != null)
                {
                    m.mainTexture = wood.mainTexture;
                    var normal = wood.GetTexture("_BumpMap");
                    if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
                }
                EditorUtility.SetDirty(m); result[i] = m;
            }
            return result;
        }

        [MenuItem("Tools/Three Body Traveller/Guards/Apply Era Guards To Scenes")]
        public static void ApplyToScenes()
        {
            GuardProject(); var setup = EditorSceneManager.GetSceneManagerSetup();
            string backup = "Logs/era-guard-backups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup);
            try
            {
                foreach (var name in new[] { "02_ChaogeCourt", "03_MedievalTribunal" })
                {
                    string path = TravellerSceneBuilder.SceneFolder + "/" + name + ".unity";
                    var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.IsValid() && scene.isLoaded;
                    if (loaded && scene.isDirty && !PreparedSceneEdits.Contains(path))
                        throw new InvalidOperationException("Save scene edits before applying guards: " + name);
                    File.Copy(path, backup + "/" + name + ".unity", true);
                    if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    foreach (var actor in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TravellerActor>(true)))
                    {
                        if (actor.role != TravellerRole.ShangGuard && actor.role != TravellerRole.ChurchGuard) continue;
                        var guard = actor.GetComponent<EraGuardEquipment>();
                        if (guard == null || guard.profile == null) throw new InvalidDataException("Guard prefab did not propagate: " + actor.name);
                        var post = actor.GetComponent<GuardPostController>(); post.allowPatrol = actor.name.StartsWith("城门卫兵", StringComparison.Ordinal);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(post);
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    if (!loaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
