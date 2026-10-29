using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private const string EnvironmentSources = "Assets/ThirdParty/Environment/PolyHaven/";
        private const string TerrainFolder = Root + "/Art/Terrain";
        private static readonly Dictionary<string, TerrainLayer> TerrainLayers = new Dictionary<string, TerrainLayer>();

        private static Texture2D EnvironmentTexture(string asset, string suffix)
        {
            string folder = EnvironmentSources + asset + "/textures";
            string path = Directory.GetFiles(folder, asset + "_" + suffix + "_2k.*")
                .FirstOrDefault(p => !p.EndsWith(".meta", StringComparison.Ordinal));
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
        }

        private static void CreateEnvironmentMaterials()
        {
            Directory.CreateDirectory(TerrainFolder); TerrainLayers.Clear();
            foreach (string asset in new[] { "rock_face_01", "rock_face_02", "namaqualand_boulders_01", "shrub_03", "grass_medium_01" })
                EnvironmentMaterial(asset, "Environment_" + asset, "diff", asset.Contains("grass") || asset.Contains("shrub"));
            EnvironmentMaterial("tree_small_02", "Environment_TreeTrunk", "diff", false);
            EnvironmentMaterial("tree_small_02", "Environment_TreeBranch", "branch_diff", false, "branch");
            EnvironmentMaterial("tree_small_02", "Environment_TreeLeaf", "leaves_diff", true, "leaves");
            Materials["Environment_grass_medium_01"].color = new Color(0.65f,0.69f,0.48f);
            Materials["Environment_shrub_03"].color = new Color(0.78f,0.74f,0.62f);
            var shelter = Material("ShelterStone", new Color(0.75f,0.68f,0.55f), "Traveller/Weathered Rock");
            shelter.mainTexture = EnvironmentTexture("rocky_terrain_02","diff");
            shelter.SetTexture("_BumpMap",EnvironmentTexture("rocky_terrain_02","nor_gl"));
        }

        private static void EnvironmentMaterial(string asset, string key, string diffuse, bool foliage, string region = "")
        {
            var material = Material(key, Color.white, foliage ? "Traveller/Photographic Foliage" : "Standard");
            material.shader = Shader.Find(foliage ? "Traveller/Photographic Foliage" : "Standard");
            material.mainTexture = EnvironmentTexture(asset, diffuse);
            string prefix = string.IsNullOrEmpty(region) ? "" : region + "_";
            material.SetTexture("_BumpMap", EnvironmentTexture(asset, prefix + "nor_gl"));
            material.SetFloat("_BumpScale", 0.75f);
            if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness", 0.08f);
            if (foliage)
            {
                material.SetTexture("_AlphaMap", EnvironmentTexture(asset, prefix + "alpha") ?? Texture2D.whiteTexture);
                material.SetFloat("_Cutoff", 0.42f);
                material.SetFloat("_Wind", key.Contains("Trunk") || key.Contains("Branch") ? 0 : 0.035f);
            }
            else
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_OcclusionMap", EnvironmentTexture(asset, prefix+"ao"));
            }
            EditorUtility.SetDirty(material);
        }

        private static TerrainLayer NaturalLayer(string asset, float metres)
        {
            TerrainLayer layer;
            if (TerrainLayers.TryGetValue(asset, out layer)) return layer;
            string path = TerrainFolder + "/" + asset + ".terrainlayer";
            layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer { name = asset }; AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = EnvironmentTexture(asset, "diff");
            layer.normalMapTexture = EnvironmentTexture(asset, "nor_gl");
            layer.normalScale = 0.65f; layer.tileSize = Vector2.one * metres;
            layer.smoothness = 0.03f; layer.metallic = 0;
            EditorUtility.SetDirty(layer); TerrainLayers[asset] = layer; return layer;
        }

        // City foundations and the playable Gobi corridor remain level. Only the
        // surrounding plain is eroded; the two scenes never share TerrainData.
        private static float NaturalHeight(float x, float z, bool gobi)
        {
            float broad = Mathf.PerlinNoise((x + 1723) * 0.0031f, (z + 487) * 0.0031f);
            float ridges = 1f - Mathf.Abs(Mathf.PerlinNoise((x + 511) * 0.009f, (z - 932) * 0.009f) * 2f - 1f);
            float detail = Mathf.PerlinNoise((x + 79) * 0.037f, (z + 29) * 0.037f);
            if (gobi)
            {
                float edge = Mathf.Max(Mathf.Abs(x) - 32f, Mathf.Abs(z - 70f) - 150f);
                float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 180, edge));
                return rise * (14f + broad * 55f + ridges * ridges * 33f + detail * 2.3f);
            }
            float outside = Mathf.Max(Mathf.Abs(x) - 232f, Mathf.Abs(z) - 236f);
            float highlands = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(65, 280, outside));
            float height = highlands * (9 + broad * 52 + ridges * ridges * 26 + detail * 2);
            float riverX = 281 + Mathf.Sin(z * 0.006f) * 14;
            height *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(60, 185, Mathf.Abs(x - riverX)));
            float channel = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17, 32, Mathf.Abs(x - riverX)));
            height = Mathf.Lerp(height, -3.2f, channel);
            // Preserve the low, cultivated south-west approach to the gate.
            if (x < 10 && x > -240 && z > -360 && z < -220) height *= Mathf.SmoothStep(0, 1, (-z - 300) / 60f);
            return height;
        }

        private static Terrain BuildNaturalTerrain(bool gobi, Transform parent)
        {
            if (!Materials.ContainsKey("Environment_rock_face_01")) CreateEnvironmentMaterials();
            string key = gobi ? "GobiErodedValley" : "ShangRiverValley";
            string path = TerrainFolder + "/" + key + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null) { data = new TerrainData { name = key }; AssetDatabase.CreateAsset(data, path); }
            const int resolution = 1025, alpha = 512;
            Vector3 origin = gobi ? new Vector3(-600, -16, -350) : new Vector3(-800, -16, -750);
            Vector3 size = gobi ? new Vector3(1200, 192, 1200) : new Vector3(1600, 192, 1600);
            data.heightmapResolution = resolution; data.size = size;
            data.alphamapResolution = alpha; data.baseMapResolution = 2048;
            data.terrainLayers = new[] { NaturalLayer("brown_mud_dry", 5), NaturalLayer("aerial_rocks_02", 11),
                NaturalLayer("aerial_grass_rock", 8), NaturalLayer("rocky_terrain_02", 6) };
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
                heights[z, x] = (NaturalHeight(origin.x + x * size.x / (resolution - 1), origin.z + z * size.z / (resolution - 1), gobi) - origin.y) / size.y;
            data.SetHeights(0, 0, heights);
            var splats = new float[alpha, alpha, 4];
            for (int z = 0; z < alpha; z++) for (int x = 0; x < alpha; x++)
            {
                float wx = origin.x + x * size.x / (alpha - 1), wz = origin.z + z * size.z / (alpha - 1);
                float noise = Mathf.PerlinNoise((wx + 912) * 0.013f, (wz + 611) * 0.013f);
                float slope = Mathf.Clamp01(data.GetSteepness((float)x / (alpha - 1), (float)z / (alpha - 1)) / 34f);
                float grass = gobi ? 0 : Mathf.Clamp01((noise - 0.28f) * 2.4f);
                // Bare ground inside the walls and on the approach; greener river banks.
                if (!gobi && Mathf.Abs(wx) < 224 && Mathf.Abs(wz) < 220) grass *= 0.17f;
                if (!gobi && wx > 240 && wx < 325) grass = Mathf.Max(grass, 0.65f);
                float rock = slope * 0.8f, grit = gobi ? 0.15f + noise * 0.5f : noise * 0.17f;
                float soil = Mathf.Max(0.05f, 1 - grass - rock - grit), sum = soil + grass + rock + grit;
                splats[z, x, 0] = soil / sum; splats[z, x, 1] = grit / sum;
                splats[z, x, 2] = grass / sum; splats[z, x, 3] = rock / sum;
            }
            data.SetAlphamaps(0, 0, splats); EditorUtility.SetDirty(data);
            var value = Terrain.CreateTerrainGameObject(data); value.name = gobi ? "Photographic Gobi terrain" : "Photographic river-valley terrain";
            value.transform.SetParent(parent); value.transform.position = origin;
            var terrain = value.GetComponent<Terrain>(); terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5; terrain.basemapDistance = 650;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            return terrain;
        }

        private static GameObject NaturalModel(string asset, string name, Vector3 position, Vector3 size, Transform parent, float rotation = 0, bool collision = false)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentSources + asset + "/" + asset + "_1k.fbx");
            if (model == null) throw new FileNotFoundException("Fetch environment sources before rebuilding: " + asset);
            var value = new GameObject(name); value.transform.SetParent(parent); value.transform.position = Vector3.zero;
            var imported = (GameObject)PrefabUtility.InstantiatePrefab(model); imported.transform.SetParent(value.transform, false);
            if (asset == "grass_medium_01")
            {
                PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                string patch = "grass_medium_01_mid_" + (char)('a' + Mathf.Abs(Mathf.FloorToInt(rotation)) % 3) + "_LOD0";
                foreach (var filter in value.GetComponentsInChildren<MeshFilter>())
                    if (filter.name != patch) Object.DestroyImmediate(filter.gameObject);
            }
            var renderers = value.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            value.transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            value.transform.position = position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                {
                    if (asset != "tree_small_02") return Materials["Environment_" + asset];
                    string n = m == null ? "" : m.name.ToLowerInvariant();
                    return Materials[n.Contains("leave") || n.Contains("leaf") ? "Environment_TreeLeaf" : n.Contains("branch") ? "Environment_TreeBranch" : "Environment_TreeTrunk"];
                }).ToArray();
                renderer.receiveShadows = true;
            }
            value.transform.RotateAround(position, Vector3.up, rotation);
            // Photographic geometry remains intact. Vegetation and small stones
            // are culled well before their cost becomes invisible on screen.
            if (asset.StartsWith("rock_face",System.StringComparison.Ordinal) || asset=="namaqualand_boulders_01") RockLods(value,asset,renderers);
            else
            {
                var lod = value.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.018f,renderers) });lod.RecalculateBounds();
                foreach(var renderer in renderers)renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            if (collision)
            {
                var box = value.AddComponent<BoxCollider>();
                box.center = value.transform.InverseTransformPoint(position + Vector3.up * size.y * 0.45f);
                box.size = new Vector3(size.x / value.transform.localScale.x * 0.7f, size.y / value.transform.localScale.y * 0.9f, size.z / value.transform.localScale.z * 0.7f);
            }
            return value;
        }

        private static void BuildGobiRockscape(Transform parent)
        {
            var random = new System.Random(7319);
            for (int i = 0; i < 52; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (36 + (float)random.NextDouble() * 100), z = -62 + (float)random.NextDouble() * 272;
                float height = 3 + (float)random.NextDouble() * 13;
                // Clear the sightline used to compare the flying stars.
                if (z > 110 && x > -18 && x < 70) height *= 0.4f;
                EmbeddedRock(i % 3 == 0 ? "rock_face_01" : "rock_face_02", "Scanned weathered outcrop", x, z, new Vector3(height * 1.4f, height, height * 1.2f), parent, random.Next(360), true, i < 8);
            }
            for (int i = 0; i < 17; i++)
            {
                float x = -390 + i * 49, z = 305 + random.Next(70), height = 21 + random.Next(23);
                EmbeddedRock(i % 2 == 0 ? "rock_face_02" : "rock_face_01", "Scanned distant escarpment", x, z, new Vector3(74, height, 52), parent, 130 + random.Next(90), true);
            }
            for (int i = 0; i < 100; i++)
            {
                float x = -37 + (float)random.NextDouble() * 74, z = -45 + (float)random.NextDouble() * 208;
                if (x > -28 && x < 4 && z > 3 && z < 30 || Mathf.Abs(x) < 4) continue;
                float scale = 0.5f + (float)random.NextDouble() * 1.1f;
                NaturalModel(i % 3 == 0 ? "namaqualand_boulders_01" : "shrub_03", i % 3 == 0 ? "Photographic loose scree" : "Dry scrub", new Vector3(x, 0, z), new Vector3(scale * 1.8f, scale * 0.45f, scale), parent, random.Next(360));
            }
            for (int i = 0; i < 24; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (7 + random.Next(8)), z = -25 + i * 7.5f;
                NaturalModel("grass_medium_01", "Photographic dry grass", new Vector3(x, 0, z), new Vector3(2.2f, 0.55f, 1.7f), parent, random.Next(360));
            }
        }

        private static void BuildCapitalNaturalLandscape(Transform parent)
        {
            BuildNaturalTerrain(false, parent);
            // A continuous meandering river replaces the overlapping square water tiles.
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            const int slices = 150;
            for (int i = 0; i <= slices; i++)
            {
                float z = -680 + i * 10, x = 281 + Mathf.Sin(z * 0.006f) * 14;
                vertices.Add(new Vector3(x - 31, -0.6f, z)); vertices.Add(new Vector3(x + 31, -0.6f, z));
                uv.Add(new Vector2(0, z / 8)); uv.Add(new Vector2(5, z / 8));
                if (i < slices) { int p = i * 2; tris.AddRange(new[] { p, p + 2, p + 1, p + 1, p + 2, p + 3 }); }
            }
            MeshObject("Continuous meandering river", SaveMesh("ShangNaturalRiver", vertices, tris, uv), Vector3.zero, "Water", parent);
            var random = new System.Random(1297);
            for (int i = 0; i < 23; i++)
            {
                float x = -340 + random.Next(720), z = 260 + random.Next(230), height = 8 + random.Next(16);
                EmbeddedRock(i % 2 == 0 ? "rock_face_01" : "rock_face_02", "River valley scanned escarpment", x, z, new Vector3(height * 2.2f, height, height * 1.2f), parent, random.Next(360), false);
            }
            for (int i = 0; i < 18; i++)
            {
                float z = -300 + i * 31, riverX = 281 + Mathf.Sin(z * 0.006f) * 14;
                float x = riverX + (i % 2 == 0 ? -1 : 1) * (39 + random.Next(5));
                NaturalTree(new Vector3(x, NaturalHeight(x, z, false), z), 8 + random.Next(5), parent, random.Next(360));
                NaturalModel("shrub_03", "Riparian photographic scrub", new Vector3(x - 5, NaturalHeight(x - 5, z, false), z + 5), new Vector3(4, 1.1f, 2.4f), parent, random.Next(360));
            }
            for (int i = 0; i < 28; i++)
            {
                float z = -340 + i * 23, riverX = 281 + Mathf.Sin(z * 0.006f) * 14, side = i % 2 == 0 ? -1 : 1;
                float x = riverX + side * (32 + random.Next(5));
                NaturalModel(i % 3 == 0 ? "namaqualand_boulders_01" : "grass_medium_01", "Natural riverbank stones and reeds", new Vector3(x, NaturalHeight(x,z,false)-0.08f, z),
                    i % 3 == 0 ? new Vector3(5,1.3f,3.2f) : new Vector3(4,1.1f,2.4f),parent,random.Next(360));
            }
            for (int i = 0; i < 6; i++)
            {
                float x = -176 + i % 3 * 53, z = -281 - i / 3 * 37;
                Box("Cultivated loess plot", new Vector3(x, 0.01f, z), new Vector3(47, 0.02f, 30), "CapitalField", parent, false);
                for (int row = 0; row < 7; row++)
                    NaturalModel("grass_medium_01", "Cultivated crop bed", new Vector3(x, 0.02f, z - 12 + row * 4), new Vector3(43, 0.65f, 2.3f), parent);
            }
        }

        private static void EmbeddedRock(string asset, string name, float x, float z, Vector3 size, Transform parent, float rotation, bool gobi, bool collision = false)
        {
            float floor = float.MaxValue; var turn = Quaternion.Euler(0, rotation, 0);
            for (int ix = -1; ix <= 1; ix++) for (int iz = -1; iz <= 1; iz++)
            {
                var offset = turn * new Vector3(ix * size.x * 0.5f, 0, iz * size.z * 0.5f);
                floor = Mathf.Min(floor, NaturalHeight(x + offset.x, z + offset.z, gobi));
            }
            // These are one-sided photogrammetric faces rather than watertight
            // boulders. Bury their unphotographed lower portion in the hillside.
            NaturalModel(asset, name, new Vector3(x, floor - size.y * 0.6f, z), size, parent, rotation, collision);
        }
    }
}
