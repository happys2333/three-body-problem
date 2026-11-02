using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    // This pass replaces visuals on existing roots. Story anchors, identities,
    // court duties, props and route references remain on their original objects.
    public static class CharacterCompletionProduction
    {
        public const string Root = "Assets/_Project/Art/CharacterCompletion20261102";
        public const string Docs = "Documentation/CharacterProduction/2026-11-02";
        public static string Prefab(string id) => Root + "/Prefabs/" + id + ".prefab";

        public static GameObject ReviewedCharacter(string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(id));
            var animator = asset == null ? null : asset.GetComponentInChildren<Animator>();
            return animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman ? asset : null;
        }

        [MenuItem("Tools/Three Body Traveller/Characters/Import November Completion")]
        public static void ImportAll(bool force = false)
        {
            Guard(); Directory.CreateDirectory(Root + "/Prefabs"); Directory.CreateDirectory(Docs + "/Unity");
            AssetDatabase.Refresh();
            foreach (string id in new[] { "PopeV3", "ShangCourtLady", "MedievalCleric", "PapalThrone", "OpenManuscript" })
            {
                bool character = id != "PapalThrone" && id != "OpenManuscript";
                string path = Root + "/" + id + "/" + id + (character ? "_Rig.fbx" : "_Textured.fbx");
                if (!File.Exists(path)) throw new FileNotFoundException("Completion asset is missing", path);
                string reportPath = Docs + "/Unity/" + id + "-import.json";
                if (!force && File.Exists(reportPath) && File.GetLastWriteTimeUtc(reportPath) >= File.GetLastWriteTimeUtc(path))
                {
                    var prior = JsonUtility.FromJson<HunyuanExpansionLibrary.Check>(File.ReadAllText(reportPath));
                    if (prior.passed && prior.source == path && AssetDatabase.LoadAssetAtPath<GameObject>(prior.prefab) != null && (!character || ReviewedCharacter(id) != null)) continue;
                }
                float size = id == "PopeV3" ? 1.84f : id == "ShangCourtLady" ? 1.65f : id == "MedievalCleric" ? 1.76f : id == "PapalThrone" ? 2.8f : .46f;
                var check = HunyuanExpansionLibrary.PrepareImportedAsset(id, path, character, size, Root + "/Prefabs");
                File.WriteAllText(reportPath, JsonUtility.ToJson(check, true));
                if (!check.passed) throw new InvalidDataException(id + " failed material/avatar validation.");
            }
            CharacterCompletionCloth.RepairCourtSkirt();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Three Body Traveller/Characters/Apply November Completion")]
        public static void Apply()
        {
            Guard();
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            foreach (string id in new[] { "PopeV3", "ShangCourtLady", "MedievalCleric" })
                if (ReviewedCharacter(id) == null) throw new InvalidDataException(id + " is not ready.");
            string backup = "Logs/character-completion-backups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup);
            string[] scenes = { "02_ChaogeCourt", "03_ThreeSunTribunal" };
            // Resolve the actual third scene name from the project, not a guessed path.
            scenes[1] = TravellerStage.SceneNames[2];
            foreach (string name in scenes) File.Copy(TravellerSceneBuilder.SceneFolder + "/" + name + ".unity", backup + "/" + name + ".unity");
            UpgradeAttendantPrefab("RitualAttendant"); UpgradeAttendantPrefab("ChamberAttendant");
            var court = EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/" + scenes[0] + ".unity");
            foreach (var fan in Objects<RoyalFanBearer>(court))
            {
                var actor = fan.GetComponent<TravellerActor>(); SwapBody(actor, "ShangCourtLady");
                var grip = fan.GetComponent<CourtPropGrip>(); grip.sourceMeshes = null;
                GuardGripAuthoring.PrepareCourtHands(actor, grip, "ShangCourtLady_" + fan.GetComponent<CourtRetainer>().stationId, true);
                Record(actor.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(court); EditorSceneManager.SaveScene(court);
            var medieval = EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/" + scenes[1] + ".unity");
            var stage = Objects<TravellerStage>(medieval).Single();
            var pope = Objects<TravellerActor>(medieval).Single(a => a.role == TravellerRole.Pope);
            SwapBody(pope, "PopeV3");
            var parent = stage.transform;
            foreach (string name in new[] { "Papal seat", "Papal seat back", "Scholar lectern", "Open manuscript", "Reviewed papal chair", "Reviewed medieval furnishings" })
                foreach (var t in stage.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray()) Object.DestroyImmediate(t.gameObject);
            var furniture = new GameObject("Reviewed medieval furnishings"); furniture.transform.SetParent(parent, false);
            var throne = Place("PapalThrone", furniture.transform, new Vector3(0, 1.8f, 36.05f), 180);
            AddBox(throne);
            var lectern = Instantiate("Assets/_Project/Art/EnvironmentExpansionReady/Prefabs/MedievalLectern.prefab", furniture.transform);
            lectern.name = "Scholar lectern [PBR]"; lectern.transform.SetPositionAndRotation(new Vector3(4.8f, .01f, 18), Quaternion.Euler(0, 180, 0));
            AddBox(lectern);
            var bounds = Bounds(lectern);
            var book = Place("OpenManuscript", furniture.transform, new Vector3(4.8f, bounds.max.y + .015f, 18), 180);
            book.name = "Open manuscript [PBR]";
            FitLecternBook(book, lectern);
            var clerk = CreateClerk(furniture.transform);
            AlignSeat(pope, throne);
            Record(pope.gameObject); Record(clerk); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(medieval); EditorSceneManager.SaveScene(medieval);
            File.WriteAllText(Docs + "/Unity/integration.json", "{\"backup\":\"" + backup + "\",\"fanBearers\":2,\"newClerks\":1,\"popeReplaced\":true,\"throneReplaced\":true,\"lecternReused\":true,\"openBooks\":2}");
        }

        private static void UpgradeAttendantPrefab(string id)
        {
            string path = CourtRetinueProduction.Root + "/Prefabs/" + id + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SwapBody(root.GetComponent<TravellerActor>(), "ShangCourtLady");
                var grip = root.GetComponent<CourtPropGrip>();
                if (grip != null) { grip.sourceMeshes = null; GuardGripAuthoring.PrepareCourtHands(root.GetComponent<TravellerActor>(), grip, id + "_NewBody", true); }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void PreparePropLods(params string[] ids)
        {
            Guard();
            if (ids.Length == 0) ids = new[] { "PapalThrone", "OpenManuscript" };
            foreach (string id in ids)
            {
                for (int level = 1; level <= 2; level++)
                {
                    string file = id + "_LOD" + level + ".fbx";
                    string source = "Logs/CharacterCompletion20261102/lods/" + id + "/" + file;
                    if (!File.Exists(source)) throw new FileNotFoundException(source);
                    File.Copy(source, Root + "/" + id + "/" + file, true);
                }
            }
            AssetDatabase.Refresh();
            foreach (string id in ids)
            {
                var root = PrefabUtility.LoadPrefabContents(Prefab(id));
                try
                {
                    var high = root.transform.Find("Visual").GetComponentsInChildren<Renderer>();
                    var reference = Bounds(root); var material = high[0].sharedMaterial;
                    var levels = new System.Collections.Generic.List<LOD> { new LOD(.2f, high) };
                    for (int level = 1; level <= 2; level++)
                    {
                        string path = Root + "/" + id + "/" + id + "_LOD" + level + ".fbx";
                        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                        importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = importer.importLights = importer.importCameras = false;
                        importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
                        var old = root.transform.Find("LOD" + level); if (old != null) Object.DestroyImmediate(old.gameObject);
                        var visual = Instantiate(path, root.transform); visual.name = "LOD" + level;
                        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        var b = Bounds(visual); visual.transform.localScale *= Mathf.Max(reference.size.x, reference.size.y, reference.size.z) / Mathf.Max(b.size.x, b.size.y, b.size.z);
                        b = Bounds(visual); visual.transform.position += reference.center - b.center;
                        var renderers = visual.GetComponentsInChildren<Renderer>();
                        foreach (var renderer in renderers) renderer.sharedMaterial = material;
                        if ((Bounds(visual).size - reference.size).magnitude > reference.size.magnitude * .03f) throw new InvalidDataException(id + " LOD bounds/orientation mismatch.");
                        levels.Add(new LOD(level == 1 ? .065f : .007f, renderers));
                    }
                    var lod = root.GetComponent<LODGroup>(); if (lod == null) lod = root.AddComponent<LODGroup>();
                    lod.SetLODs(levels.ToArray()); lod.RecalculateBounds(); AddBox(root);
                    PrefabUtility.SaveAsPrefabAsset(root, Prefab(id));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void ApplyPopeAndThrone()
        {
            Guard(); if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene first.");
            string path = TravellerSceneBuilder.SceneFolder + "/" + TravellerStage.SceneNames[2] + ".unity";
            string backup = "Logs/character-completion-backups/pope_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup); File.Copy(path, backup + "/" + TravellerStage.SceneNames[2] + ".unity");
            var scene = EditorSceneManager.OpenScene(path); var stage = Objects<TravellerStage>(scene).Single();
            var pope = Objects<TravellerActor>(scene).Single(a => a.role == TravellerRole.Pope); SwapBody(pope, "PopeV3");
            foreach (string name in new[] { "Papal seat", "Papal seat back", "Reviewed papal chair" })
                foreach (var t in stage.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray()) Object.DestroyImmediate(t.gameObject);
            var group = new GameObject("Reviewed papal chair"); group.transform.SetParent(stage.transform, false);
            var throne = Place("PapalThrone", group.transform, new Vector3(0, 1.8f, 36.05f), 180); AddBox(throne);
            AlignSeat(pope, throne); Record(pope.gameObject);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        public static void SwapBody(TravellerActor actor, string id)
        {
            var source = ReviewedCharacter(id); if (source == null) throw new InvalidDataException(id);
            if (PrefabUtility.IsAnyPrefabInstanceRoot(actor.gameObject))
                PrefabUtility.UnpackPrefabInstance(actor.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (actor.animator != null)
            {
                Transform old = actor.animator.transform;
                while (old.parent != actor.transform && old.parent != null) old = old.parent;
                if (old == actor.transform) throw new InvalidDataException("Animator must be below the actor root.");
                Object.DestroyImmediate(old.gameObject);
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, actor.gameObject.scene);
            model.transform.SetParent(actor.transform, false); model.name = id + " Visual";
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            actor.animator = model.GetComponentInChildren<Animator>();
            actor.animator.runtimeAnimatorController = actor.settings.controller;
            actor.animator.applyRootMotion = false; actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var lod = actor.GetComponent<LODGroup>(); if (lod == null) lod = actor.gameObject.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.012f, model.GetComponentsInChildren<Renderer>()) }); lod.RecalculateBounds();
            Record(actor.gameObject);
        }

        private static GameObject CreateClerk(Transform parent)
        {
            var root = new GameObject("教廷书记官 [Humanoid]"); root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(-4.4f, .01f, 24.5f), Quaternion.Euler(0, 150, 0));
            var actor = root.AddComponent<TravellerActor>(); actor.role = TravellerRole.CitizenMale;
            actor.settings = AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath);
            SwapBody(actor, "MedievalCleric");
            var capsule = root.AddComponent<CapsuleCollider>(); capsule.height = 1.76f; capsule.radius = .26f; capsule.center = Vector3.up * .88f;
            string personaPath = Root + "/MedievalCleric/ClerkPersona.asset";
            var persona = AssetDatabase.LoadAssetAtPath<NpcPersona>(personaPath);
            if (persona == null) { persona = ScriptableObject.CreateInstance<NpcPersona>(); AssetDatabase.CreateAsset(persona, personaPath); }
            persona.characterId = "citizen"; persona.displayName = "教廷书记官"; persona.aliases = new[] { "书记官", "书吏" };
            persona.prompt = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Dialogue/Prompts/bystander.txt");
            persona.fallbackReply = "我在记录今天的陈述。历法是否可信，仍要由庭上查验；你有什么亲眼见到的事？";
            EditorUtility.SetDirty(persona); root.AddComponent<NpcIdentity>().persona = persona;
            var torch = root.AddComponent<TorchBearer>(); torch.equipped = false;
            torch.torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
            var grip = root.AddComponent<CourtPropGrip>(); GuardGripAuthoring.PrepareCourtHands(actor, grip, "MedievalCleric_Book", false);
            var book = Place("OpenManuscript", root.transform, Vector3.zero, 180); book.name = "Held parchment manuscript";
            book.transform.localScale = Vector3.one * .78f;
            foreach (var collider in book.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var held = root.AddComponent<NpcHeldBook>(); held.book = book.transform;
            book.transform.localPosition = held.readingPosition; book.transform.localRotation = Quaternion.Euler(held.readingEuler);
            // Contacts are in the normalized prop root's metres, not its raw FBX units.
            grip.rightContact = new Vector3(.18f, .015f, -.025f); grip.leftContact = new Vector3(-.18f, .015f, -.025f);
            var lod = root.GetComponent<LODGroup>(); lod.SetLODs(new[] { new LOD(.012f, actor.animator.GetComponentsInChildren<Renderer>()) }); lod.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab("MedievalClerkActor"));
            return root;
        }

        private static void AlignSeat(TravellerActor pope, GameObject throne)
        {
            // Use the upper surface of the generated seat, not the chair's tall back.
            var b = Bounds(throne); float seatY = 0; float seatZ = 0;
            foreach (var filter in throne.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = filter.transform.TransformPoint(vertices[triangles[i]]); var c = filter.transform.TransformPoint(vertices[triangles[i + 1]]); var d = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    var center = (a + c + d) / 3; var normal = Vector3.Cross(c - a, d - a).normalized;
                    if (normal.y < .8f || Mathf.Abs(center.x) > .25f || center.y < b.min.y + b.size.y * .23f || center.y > b.min.y + b.size.y * .43f) continue;
                    if (center.y > seatY) { seatY = center.y; seatZ = center.z; }
                }
            }
            if (seatY == 0) throw new InvalidDataException("Cannot find the generated throne seat.");
            var visual = pope.animator.transform; while (visual.parent != pope.transform) visual = visual.parent;
            TravellerStoryCharacters.WithSample(pope.animator, "MX_SitIdle", .2f, () => {
                var hip = pope.animator.GetBoneTransform(HumanBodyBones.Hips).position;
                visual.position += new Vector3(-hip.x, seatY + .105f - hip.y, seatZ - .12f - hip.z);
            });
            // Generated furniture often has toy-like tall legs. Keep the high
            // back, but shorten the part below the cushion to meet this adult's
            // seated leg length. UVs and all three LOD silhouettes are retained.
            float bottom = float.MaxValue;
            TravellerStoryCharacters.WithSample(pope.animator, "MX_SitIdle", .2f, () => {
                foreach (var skin in pope.animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    try { skin.BakeMesh(mesh, true); foreach (var v in mesh.vertices) bottom = Mathf.Min(bottom, skin.transform.TransformPoint(v).y); }
                    finally { Object.DestroyImmediate(mesh); }
                }
            });
            float lowerBy = bottom - b.min.y - .015f;
            float originalSeatHeight = seatY - b.min.y;
            if (lowerBy > .04f && originalSeatHeight - lowerBy > .25f)
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(throne)) PrefabUtility.UnpackPrefabInstance(throne, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Directory.CreateDirectory(Root + "/Meshes"); AssetDatabase.Refresh(); int index = 0;
                foreach (var filter in throne.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = Object.Instantiate(filter.sharedMesh); var vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var point = filter.transform.TransformPoint(vertices[i]); float height = point.y - b.min.y;
                        point.y -= height < originalSeatHeight ? lowerBy * Mathf.Clamp01(height / originalSeatHeight) : lowerBy;
                        vertices[i] = filter.transform.InverseTransformPoint(point);
                    }
                    mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
                    string path = Root + "/Meshes/PapalThrone_SeatedFit_" + index++ + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                    filter.sharedMesh = saved;
                }
                visual.position -= Vector3.up * lowerBy; seatY -= lowerBy;
                var fitted = Bounds(throne); var box = throne.GetComponent<BoxCollider>();
                if (box != null) { box.center = throne.transform.InverseTransformPoint(fitted.center); box.size = fitted.size; }
                var lod = throne.GetComponent<LODGroup>(); if (lod != null) lod.RecalculateBounds();
                var position = throne.transform.position; var rotation = throne.transform.rotation;
                throne.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(throne, Prefab("PapalThrone_SeatedFit"));
                throne.transform.SetPositionAndRotation(position, rotation);
            }
            var contact = new GameObject("Seat contact"); contact.transform.SetParent(throne.transform); contact.transform.position = new Vector3(0, seatY, seatZ);
        }
        private static GameObject Place(string id, Transform parent, Vector3 position, float yaw)
        { var go = Instantiate(Prefab(id), parent); go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); return go; }
        private static void FitLecternBook(GameObject book, GameObject lectern)
        {
            var target = lectern.transform.position; float highest = float.NegativeInfinity; Vector3 up = Vector3.up;
            var lod = lectern.GetComponent<LODGroup>();
            var filters = lod == null ? lectern.GetComponentsInChildren<MeshFilter>() : lod.GetLODs()[0].renderers.Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null).ToArray();
            foreach (var filter in filters)
            {
                var vertices = filter.sharedMesh.vertices; var triangles = filter.sharedMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = filter.transform.TransformPoint(vertices[triangles[i]]); var b = filter.transform.TransformPoint(vertices[triangles[i + 1]]); var c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    var normal = Vector3.Cross(b - a, c - a).normalized; if (normal.y < .55f) continue;
                    float x1 = b.x - a.x, z1 = b.z - a.z, x2 = c.x - a.x, z2 = c.z - a.z, determinant = x1 * z2 - x2 * z1;
                    if (Mathf.Abs(determinant) < .0000001f) continue;
                    float u = ((target.x - a.x) * z2 - (target.z - a.z) * x2) / determinant;
                    float v = (x1 * (target.z - a.z) - z1 * (target.x - a.x)) / determinant;
                    if (u < 0 || v < 0 || u + v > 1) continue;
                    float y = a.y + u * (b.y - a.y) + v * (c.y - a.y);
                    if (y > highest) { highest = y; up = normal; }
                }
            }
            if (float.IsNegativeInfinity(highest)) throw new InvalidDataException("No lectern reading surface.");
            book.transform.SetPositionAndRotation(new Vector3(target.x, highest + .012f, target.z), Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, 180, 0));
        }
        private static void AddBox(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>() != null) return;
            var b = Bounds(root); var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(b.center);
            box.size = new Vector3(b.size.x / root.transform.lossyScale.x, b.size.y / root.transform.lossyScale.y, b.size.z / root.transform.lossyScale.z);
        }
        private static GameObject Instantiate(string path, Transform parent)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (source == null) throw new FileNotFoundException(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene); go.transform.SetParent(parent, false); return go;
        }
        public static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(); var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds); return b;
        }
        private static T[] Objects<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void Record(GameObject root)
        { foreach (var c in root.GetComponentsInChildren<Component>(true)) if (c != null) { EditorUtility.SetDirty(c); if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c); } }
        private static void Guard()
        { EraGuardProduction.GuardProject(); if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first."); }
    }
}
