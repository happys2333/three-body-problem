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
    public static class TorchVfxProduction
    {
        public const string PrefabPath = "Assets/_Project/Art/ActOne/Prefabs/Torch.prefab";
        public const string Docs = "Documentation/TorchVfx/2026-11-02";
        public const string BuildOutput = "Builds/TorchVfx20261102/Traveller.app";
        private const string Textures = "Assets/ThirdParty/UnityVfxSamples/Textures/";
        private const string Materials = "Assets/_Project/Art/ActOne/Materials/";
        private const string BuildRequest = "ThreeBody.TorchVfx.BuildPending";

        [MenuItem("Tools/Three Body/Update Torch Fire VFX")]
        public static void UpdateTorch()
        {
            EraGuardProduction.GuardProject();
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before updating the saved torch.");
            ImportTexture("SmallFlame01_15x4.png"); ImportTexture("CandleSmoke01_20x4.png");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        // Also used by the Act One prop builder so a rebuild retains the reviewed effect.
        public static void Configure(GameObject root)
        {
            root.transform.localScale = Vector3.one;
            foreach (Transform child in root.transform)
                if (child.name.StartsWith("LOD ", StringComparison.Ordinal)) child.localScale = new Vector3(.65f, 1, .65f);
            foreach (string name in new[] { "Torch sparks", "Living torch flame", "Torch light", "Torch smoke" })
            { var old = root.transform.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); }

            var fire = Child(root, "Living torch flame", Vector3.up * .75f);
            fire.transform.localScale = new Vector3(.18f, .33f, 1);
            fire.AddComponent<MeshFilter>().sharedMesh = FlameMesh();
            var renderer = fire.AddComponent<MeshRenderer>();
            var flameMaterial = SaveMaterial("TorchFlame", "Traveller/Torch Flame");
            flameMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "SmallFlame01_15x4.png");
            flameMaterial.SetVector("_Grid", new Vector4(15, 4, 0, 0));
            flameMaterial.SetFloat("_FrameRate", 30); flameMaterial.SetFloat("_Intensity", 1.65f);
            EditorUtility.SetDirty(flameMaterial); renderer.sharedMaterial = flameMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            fire.AddComponent<TorchFlameBillboard>();

            var smokeMaterial = SaveMaterial("TorchSmoke", "Traveller/Torch Smoke");
            smokeMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "CandleSmoke01_20x4.png");
            EditorUtility.SetDirty(smokeMaterial);
            var smoke = Particles(root, "Torch smoke", smokeMaterial, 10, 2.5f);
            var main = smoke.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            main.startSize3D = true; main.startSizeX = new ParticleSystem.MinMaxCurve(.12f, .17f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(.30f, .42f); main.startSizeZ = 1;
            main.startColor = new Color(.42f, .36f, .31f, .22f);
            var velocity = smoke.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.025f, .025f); velocity.y = new ParticleSystem.MinMaxCurve(.18f, .28f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.025f, .025f);
            var sheet = smoke.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 20; sheet.numTilesY = 4;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1));
            var smokeRenderer = smoke.GetComponent<ParticleSystemRenderer>(); smokeRenderer.pivot = new Vector3(0, -.5f, 0);
            var colour = smoke.colorOverLifetime; colour.enabled = true; colour.color = AlphaFade(.7f);
            var size = smoke.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .65f, 1, 1.2f));

            var sparks = Particles(root, "Torch sparks", AssetDatabase.LoadAssetAtPath<Material>(Materials + "Flame.mat"), 12, 2);
            main = sparks.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.003f, .007f); main.startColor = new Color(1, .53f, .12f, .85f);
            main.gravityModifier = .055f;
            velocity = sparks.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.1f, .1f); velocity.y = new ParticleSystem.MinMaxCurve(.65f, 1);
            velocity.z = new ParticleSystem.MinMaxCurve(-.1f, .1f);
            colour = sparks.colorOverLifetime; colour.enabled = true; colour.color = AlphaFade(1);

            var lamp = Child(root, "Torch light", Vector3.up * .79f).AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 8.5f; lamp.intensity = 2.3f;
            lamp.color = new Color(1, .64f, .34f); lamp.shadows = LightShadows.None;
            root.GetComponent<LODGroup>().RecalculateBounds();
        }

        private static GameObject Child(GameObject parent, string name, Vector3 position)
        {
            var child = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child, parent.scene);
            child.transform.SetParent(parent.transform, false); child.transform.localPosition = position;
            return child;
        }
        private static ParticleSystem Particles(GameObject parent, string name, Material material, int maximum, float rate)
        {
            var p = Child(parent, name, Vector3.up * .76f).AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main; main.playOnAwake = false; main.loop = true; main.maxParticles = maximum;
            main.startSpeed = 0; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            var shape = p.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .008f;
            var emission = p.emission; emission.rateOverTime = rate;
            var renderer = p.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return p;
        }
        private static Gradient AlphaFade(float peak)
        {
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, .18f), new GradientAlphaKey(0, 1) }); return g;
        }
        private static Mesh FlameMesh()
        {
            const string path = "Assets/_Project/Art/ActOne/Meshes/TorchFlameFoot.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) return saved;
            var mesh = new Mesh { name = "Torch flame with foot pivot" };
            mesh.vertices = new[] { new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0), new Vector3(-.5f, 1, 0), new Vector3(.5f, 1, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Material SaveMaterial(string name, string shader)
        {
            string path = Materials + name + ".mat"; var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (saved == null) { saved = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(saved, path); }
            else saved.shader = Shader.Find(shader);
            return saved;
        }
        private static void ImportTexture(string name)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Textures + name);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        [InitializeOnLoadMethod] private static void ResumeBuild() { EditorApplication.update -= TryBuild; EditorApplication.update += TryBuild; }
        public static void QueueBuild() { EraGuardProduction.GuardProject(); SessionState.SetBool(BuildRequest, true); ResumeBuild(); }
        private static void TryBuild()
        {
            if (!SessionState.GetBool(BuildRequest, false) || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            SessionState.SetBool(BuildRequest, false);
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before building.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene before building.");
            Directory.CreateDirectory(Docs + "/Unity"); Directory.CreateDirectory(Path.GetDirectoryName(BuildOutput));
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = TravellerStage.SceneNames.Select(n => TravellerSceneBuilder.SceneFolder + "/" + n + ".unity").ToArray(),
                locationPathName = BuildOutput, target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.CompressWithLz4 | BuildOptions.DetailedBuildReport });
            var s = build.summary;
            File.WriteAllText(Docs + "/Unity/native-build.json", "{\"result\":\"" + s.result + "\",\"errors\":" + s.totalErrors + ",\"warnings\":" + s.totalWarnings + ",\"seconds\":" + s.totalTime.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
        }
    }
}
