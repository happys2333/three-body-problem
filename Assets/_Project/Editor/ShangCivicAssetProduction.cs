using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class ShangCivicAssetProduction
    {
        public const string Root = "Assets/_Project/Art/ShangCapitalReady";
        public const string Work = "ArtSource/ShangCapital/Prepared/2026-11-01";
        public const string Docs = "Documentation/SceneProduction/2026-11-01";
        [Serializable] public class Processing
        {
            public string id, sourcePrefab, usage;
            public string[] modifications, materials;
            public int[] trianglesByLod;
            public float width, height, depth;
        }
        [Serializable] public class Ready
        {
            public string id, prefab, source;
            public int[] triangles;
            public float width, height, depth;
            public bool passed;
        }
        [Serializable] public class Report { public List<Ready> assets = new List<Ready>(); }

        public static Report ImportReady()
        {
            EraGuardProduction.GuardProject();
            foreach (string folder in new[] { Root + "/Meshes", Root + "/Prefabs" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string reportPath = Docs + "/ready-validation.json";
            var report = File.Exists(reportPath) ? JsonUtility.FromJson<Report>(File.ReadAllText(reportPath)) : new Report();
            foreach (string folder in Directory.GetDirectories(Work + "/Output"))
            {
                string info = folder + "/processing.json";
                if (!File.Exists(info)) continue;
                var data = JsonUtility.FromJson<Processing>(File.ReadAllText(info));
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(data.sourcePrefab);
                var original = source.GetComponent<LODGroup>().GetLODs()[0].renderers[0].sharedMaterial;
                // These are unglazed clay, reed and worn timber surfaces.
                // Keep the generated roughness detail while limiting wet shine.
                original.SetFloat("_GlossMapScale", data.id == "ShangStorageJar" || data.id == "ShangCastingFurnace" ? .25f : .45f);
                original.enableInstancing = true; EditorUtility.SetDirty(original);
                var materials = new Material[8];
                for (int i = 0; i < 7; i++)
                    materials[i] = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/" + data.materials[i] + ".mat") ?? original;
                materials[7] = original;
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject(data.id); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                    root.isStatic = true; var lods = new LOD[3]; Mesh collision = null;
                    for (int level = 0; level < 3; level++)
                    {
                        var mesh = ReadMesh(folder + "/LOD" + level + ".meshbin", data.id + "_LOD" + level);
                        var child = new GameObject("LOD " + level, typeof(MeshFilter), typeof(MeshRenderer));
                        child.transform.SetParent(root.transform, false); child.isStatic = true;
                        child.GetComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = level < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        lods[level] = new LOD(level == 0 ? .3f : level == 1 ? .075f : .006f, new Renderer[] { renderer });
                        if (level == 2) collision = mesh;
                    }
                    var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                    var solid = new GameObject("Static collision"); solid.transform.SetParent(root.transform, false); solid.isStatic = true;
                    if (data.id == "ShangCourtyardHall")
                    {
                        // Closed exterior doors: a solid building footprint is intentional.
                        var box = solid.AddComponent<BoxCollider>();
                        box.center = new Vector3(0, data.height * .32f, 0); box.size = new Vector3(data.width * .88f, data.height * .64f, data.depth * .9f);
                    }
                    else if (data.id != "ShangWoodFootbridge")
                        solid.AddComponent<MeshCollider>().sharedMesh = collision;
                    if (data.id == "ShangCastingFurnace")
                    {
                        var torch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
                        foreach (string childName in new[] { "Living torch flame", "Torch sparks", "Torch light" })
                        {
                            var template = torch.GetComponentsInChildren<Transform>(true).First(t => t.name == childName);
                            var fire = Object.Instantiate(template.gameObject, root.transform); fire.name = childName;
                            fire.transform.localPosition = Vector3.up * data.height * (childName == "Torch light" ? .8f : 1.02f);
                            fire.transform.localScale = template.localScale * 1.7f;
                            var light = fire.GetComponent<Light>(); if (light != null) { light.range = 5; light.intensity = 1.2f; light.shadows = LightShadows.None; }
                        }
                    }
                    // Bridge gets a continuous deck, banks and rail colliders in the scene.
                    string prefab = Root + "/Prefabs/" + data.id + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, prefab);
                    var check = new Ready { id = data.id, prefab = prefab, source = data.sourcePrefab,
                        triangles = data.trianglesByLod, width = data.width, height = data.height, depth = data.depth,
                        passed = original != null && original.mainTexture != null && original.GetTexture("_BumpMap") != null &&
                            data.trianglesByLod.Length == 3 && data.trianglesByLod[0] > data.trianglesByLod[1] && data.trianglesByLod[1] > data.trianglesByLod[2] };
                    report.assets.RemoveAll(a => a.id == data.id); report.assets.Add(check);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            AssetDatabase.SaveAssets();
            foreach (string mesh in Directory.GetFiles(Root + "/Meshes", "*.asset"))
                AssetDatabase.ImportAsset(mesh, ImportAssetOptions.ForceUpdate);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            if (report.assets.Any(a => !a.passed)) throw new InvalidDataException("Check civic asset report.");
            return report;
        }

        private static Mesh ReadMesh(string path, string id)
        {
            using (var r = new BinaryReader(File.OpenRead(path)))
            {
                if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(4)) != "TBM1") throw new InvalidDataException(path);
                int count = r.ReadInt32(), indexCount = r.ReadInt32(), slots = r.ReadInt32();
                if (slots != 8 || count < 3 || indexCount % 3 != 0) throw new InvalidDataException(path);
                var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
                for (int i = 0; i < count; i++) { float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(); vertices[i] = new Vector3(x, z, -y); }
                for (int i = 0; i < count; i++) { float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(); normals[i] = new Vector3(x, z, -y).normalized; }
                for (int i = 0; i < count; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                var indices = new int[indexCount]; for (int i = 0; i < indexCount; i++) indices[i] = r.ReadInt32();
                var triangles = Enumerable.Range(0, slots).Select(i => new List<int>()).ToArray();
                for (int i = 0; i < indexCount / 3; i++) { int slot = r.ReadInt32(); for (int c = 0; c < 3; c++) triangles[slot].Add(indices[i * 3 + c]); }
                var mesh = new Mesh { name = id, indexFormat = IndexFormat.UInt32, vertices = vertices, normals = normals, uv = uv, subMeshCount = slots };
                for (int i = 0; i < slots; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                string output = Root + "/Meshes/" + id + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(output);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, output); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                return saved;
            }
        }
    }
}
