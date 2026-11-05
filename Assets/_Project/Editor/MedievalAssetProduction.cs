using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem.Editor
{
    public static class MedievalAssetProduction
    {
        private const string Root = "Assets/_Project/Art/Medieval20261106";
        [Serializable] private sealed class AssetCheck
        {
            public string id, prefab;
            public int[] triangles;
            public Vector3 size;
            public bool passed;
        }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public List<AssetCheck> assets = new();
        }

        public static void PrepareAndCapture()
        {
            string output = Environment.GetEnvironmentVariable("TRAVELLER_MEDIEVAL_REVIEW_OUTPUT")
                ?? "Builds/MedievalProduction20261106/Unity";
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            var report = new Report();
            string only = Environment.GetEnvironmentVariable("TRAVELLER_MEDIEVAL_ONLY");
            foreach (string folder in Directory.GetDirectories(Root).Where(p => File.Exists(p + "/prepared.json") &&
                         (string.IsNullOrEmpty(only) || Path.GetFileName(p) == only)))
            {
                var check = Prepare(folder.Replace('\\', '/'));
                report.assets.Add(check);
                Capture(check, output);
            }
            report.passed = report.assets.Count > 0 && report.assets.All(row => row.passed);
            AssetDatabase.SaveAssets();
            File.WriteAllText(output + "/asset-checks.json", JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidDataException("Medieval model import checks failed.");
        }

        private static AssetCheck Prepare(string folder)
        {
            string id = Path.GetFileName(folder);
            foreach (string path in Directory.GetFiles(folder + "/Textures", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("Normal.png");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = path.EndsWith("Albedo.png");
                importer.maxTextureSize = normal ? 1024 : path.EndsWith("MetallicSmoothness.png") ? 512 : 2048;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            string materialPath = folder + "/" + id + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.color = Color.white;
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/Albedo.png");
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/Normal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/MetallicSmoothness.png"));
            material.SetFloat("_BumpScale", .75f);
            material.SetFloat("_GlossMapScale", .8f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICGLOSSMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = NewObject(id, scene);
                var levels = new LOD[3]; var triangles = new int[3];
                foreach (int level in Enumerable.Range(0, 3))
                {
                    string path = folder + "/LOD" + level + ".obj";
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.isReadable = true; importer.globalScale = 1;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.SaveAndReimport();
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                    model.name = "LOD" + level; model.transform.SetParent(root.transform, false);
                    var renderers = model.GetComponentsInChildren<MeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        renderer.sharedMaterial = material;
                        if (level == 2) renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    triangles[level] = renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
                    levels[level] = new LOD(level == 0 ? .28f : level == 1 ? .1f : .018f, renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                var bounds = Bounds(levels[0].renderers);
                var collision = NewObject("Static collision", scene); collision.transform.SetParent(root.transform, false);
                collision.isStatic = true;
                if (id == "CathedralPortal")
                    collision.AddComponent<MeshCollider>().sharedMesh = levels[2].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                else
                {
                    var box = collision.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
                }
                string prefab = Root + "/Prefabs/" + id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
                return new AssetCheck { id = id, prefab = prefab, triangles = triangles, size = bounds.size,
                    passed = material.shader.isSupported && material.mainTexture != null &&
                        triangles[0] > triangles[1] && triangles[1] > triangles[2] && Mathf.Abs(bounds.min.y) < .02f };
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Capture(AssetCheck entry, string output)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab), scene);
                var group = root.GetComponent<LODGroup>(); group.ForceLOD(0);
                var bounds = Bounds(group.GetLODs()[0].renderers);
                root.transform.localScale = Vector3.one * (3f / bounds.size.y);
                foreach (var value in new[] { new Vector4(35, 205, 0, 1.5f), new Vector4(20, 90, 0, .8f) })
                {
                    var light = NewObject("Review light", scene).AddComponent<Light>(); light.type = LightType.Directional;
                    light.transform.rotation = Quaternion.Euler(value.x, value.y, value.z); light.intensity = value.w;
                }
                var camera = NewObject("Review camera", scene).AddComponent<Camera>(); camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 2.15f;
                camera.nearClipPlane = .05f; camera.farClipPlane = 50;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .18f, .2f);
                camera.transform.position = new Vector3(0, 1.8f, 10); camera.transform.LookAt(new Vector3(0, 1.5f, 0));
                foreach (int yaw in new[] { 0, 35, 180 })
                {
                    root.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    TravellerSceneChecks.Capture(camera, output + "/" + entry.id + "_" + yaw + ".jpg");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Bounds Bounds(IEnumerable<Renderer> renderers)
        {
            var result = renderers.First().bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }
        private static GameObject NewObject(string name, Scene scene)
        {
            var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value;
        }
    }
}
