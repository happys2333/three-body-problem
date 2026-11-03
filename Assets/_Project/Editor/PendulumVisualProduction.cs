using System;
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
    public static class PendulumVisualProduction
    {
        private const string ArtRoot = "Assets/_Project/Art/Pendulum";
        internal const string CapitalScene = "Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity";
        internal const string BobName = "Pendulum bob";
        internal const float HangerOverlap = 0.22f;
        private const string PrefabPath = ArtRoot + "/Pendulum bob.prefab";

        [MenuItem("Tools/Three Body Traveller/Capital/Apply Bronze Pendulums")]
        public static void PrepareAndApply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before editing the pendulums.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save the open scenes first.");
            }

            PreparePrefab();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var backup = "Logs/pendulum-backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            File.Copy(CapitalScene, Path.Combine(backup, Path.GetFileName(CapitalScene)));
            try
            {
                var scene = EditorSceneManager.OpenScene(CapitalScene, OpenSceneMode.Single);
                var pendulums = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PendulumMotion>(true)).ToArray();
                if (pendulums.Length != 7)
                    throw new InvalidDataException($"Expected seven pendulums in Chaoge, found {pendulums.Length}.");
                foreach (var pendulum in pendulums)
                    ReplaceBob(pendulum.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Could not save Chaoge.");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        internal static void PreparePrefab()
        {
            var albedo = ImportTexture("BronzePendulumAlbedo", TextureImporterType.Default, true, false);
            var normal = ImportTexture("BronzePendulumNormal", TextureImporterType.NormalMap, false, false);
            var packed = ImportTexture("BronzePendulumMetallicSmoothness", TextureImporterType.Default, false, true);
            var materialPath = ArtRoot + "/BronzePendulum.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = albedo;
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_GlossMapScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var root = new GameObject(BobName, typeof(LODGroup));
            try
            {
                float[] transitions = { 0.15f, 0.045f, 0.006f };
                var lods = new LOD[transitions.Length];
                for (int index = 0; index < lods.Length; index++)
                {
                    var mesh = ImportMesh(index);
                    var child = new GameObject("LOD " + index, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(root.transform, false);
                    // Rotate the imported +Z axis to hang down along -Y.
                    child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    child.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = index == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    lods[index] = new LOD(transitions[index], new Renderer[] { renderer });
                }
                var group = root.GetComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        internal static bool TryPlaceBob(Transform pivot, Vector3 hanger)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return false;
            var bob = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
            bob.transform.position = hanger;
            return true;
        }

        internal static void ReplaceBob(Transform pivot)
        {
            var bob = pivot.Find(BobName);
            var suspension = pivot.Find("Suspension");
            if (bob == null || suspension == null)
                throw new InvalidDataException($"Pendulum parts are missing at {pivot.position}.");

            if (bob.GetComponent<LODGroup>() == null)
            {
                var bounds = bob.GetComponent<MeshRenderer>().bounds;
                var hanger = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                if (!TryPlaceBob(pivot, hanger))
                    throw new InvalidDataException("Build the bronze pendulum prefab first.");
                Object.DestroyImmediate(bob.gameObject);
                bob = pivot.Find(BobName);
            }

            var lower = bob.position - pivot.up * HangerOverlap;
            var delta = lower - pivot.position;
            suspension.position = (pivot.position + lower) * 0.5f;
            suspension.rotation = Quaternion.FromToRotation(Vector3.up, delta);
            var scale = suspension.localScale;
            scale.y = delta.magnitude * 0.5f;
            suspension.localScale = scale;
        }

        private static Mesh ImportMesh(int level)
        {
            var path = $"{ArtRoot}/Meshes/BronzePendulumBob_LOD{level}.fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                throw new FileNotFoundException("Prepare the pendulum model in Blender first.", path);
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();

            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
            if (Mathf.Abs(mesh.bounds.size.z - 4.6f) > 0.08f || Mathf.Abs(mesh.bounds.min.z) > 0.08f)
                throw new InvalidDataException($"Unexpected hanging bounds in {path}: {mesh.bounds}.");
            return mesh;
        }

        private static Texture2D ImportTexture(string name, TextureImporterType type, bool srgb, bool alpha)
        {
            var path = $"{ArtRoot}/Textures/{name}.png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                throw new FileNotFoundException("Prepare the pendulum textures in Blender first.", path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
