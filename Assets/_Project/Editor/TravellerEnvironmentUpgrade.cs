using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private const string ReadyEnvironment = Root + "/Art/EnvironmentReady/Prefabs/";
        private const string ExpandedEnvironment = Root + "/Art/EnvironmentExpansionReady/Prefabs/";
        private const string ModelVisual = "Production mesh";

        [MenuItem("Tools/Three Body Traveller/Assets/Apply Environment Models")]
        public static void UpgradeEnvironmentModels()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in TravellerStage.SceneNames)
                {
                    var scene = EditorSceneManager.OpenScene(SceneFolder + "/" + name + ".unity");
                    UpgradeEnvironmentModels(scene, name);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (previous.Any(item => item.isActive && !string.IsNullOrEmpty(item.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static void UpgradeEnvironmentModels(Scene scene, string name)
        {
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToLookup(item => item.name);
            UpgradeTravelTimepieces(scene, objects);
            UpgradeBackgroundDetail(objects);
            if (name == "01_GobiJourney")
            {
                var ridge = objects["Far eastern ridge / GobiRidge"].SingleOrDefault();
                if (ridge == null) return;
                var layers = LoadBackdropMeshes("GobiStrata");
                if (layers == null) return;
                Vector3 site = ridge.position;
                PlaceBackdrop(ridge.parent, layers, "Eastern strata bluff", site + new Vector3(-18f, 0, -9f),
                    new Vector3(94f, 62f, 57f), 32f);
                PlaceBackdrop(ridge.parent, layers, "Eastern strata shoulder", site + new Vector3(44f, -1f, 21f),
                    new Vector3(107f, 45f, 61f), 198f);
                Object.DestroyImmediate(ridge.gameObject);
            }
            else if (name == "02_ChaogeCourt")
            {
                foreach (string wall in new[] { "Outer earth wall", "Sloped wall footing", "Gate earth bastion",
                             "Gate earth course", "East-west city enclosure", "Earth wall buttress", "North city enclosure" })
                    foreach (var piece in objects[wall]) UpgradeRammedEarth(piece);
                UpgradeChaogeGate(objects);
                UpgradeCapitalStructures(objects);
                UpgradeCapitalStreetDetails(objects);
                UpgradeCapitalTerrain(objects);
            }
            else if (name == "03_MedievalTribunal")
            {
                foreach (var house in objects["Medieval town building"])
                {
                    var site = house.position;
                    ReplaceVisual(house, ReadyEnvironment + "MedievalTownhouse.prefab",
                        new Vector3(site.x, site.y - 3.5f, site.z),
                        new Vector3(9.6f, 11.5f + Mathf.Repeat(site.z, 3f) * .37f, 10.6f),
                        Quaternion.Euler(0, site.x < 0 ? 90 : -90, 0), 2);
                }
                foreach (var bench in objects["Oak bench"])
                {
                    var foot = bench.position - Vector3.up * .6f;
                    ReplaceVisual(bench, ExpandedEnvironment + "MedievalPew.prefab", foot,
                        new Vector3(3.5f, 1.05f, 1f), Quaternion.identity, 1);
                    var collider = bench.GetComponent<BoxCollider>();
                    collider.center = Vector3.down * .075f;
                    collider.size = new Vector3(3.5f, 1.05f, 1f);
                }
                foreach (var buttress in objects["Exterior buttress"])
                {
                    var site = buttress.position;
                    ReplaceVisual(buttress, ReadyEnvironment + "MedievalButtress.prefab",
                        site - Vector3.up * 5.5f, new Vector3(3.6f, 11f, 2f),
                        Quaternion.Euler(0, site.x < 0 ? 90 : -90, 0), 2);
                }
                foreach (var pier in objects["Nave pillar"])
                    pier.GetComponent<MeshFilter>().sharedMesh = GothicNavePier();
                foreach (string old in new[] { "Column plinth", "Capital" })
                    foreach (var piece in objects[old])
                    {
                        Object.DestroyImmediate(piece.GetComponent<MeshRenderer>());
                        Object.DestroyImmediate(piece.GetComponent<MeshFilter>());
                    }
                foreach (string old in new[] { "Steep town roof", "Timber facade frame", "Timber crossbeam", "Bench leg" })
                    foreach (var piece in objects[old]) Object.DestroyImmediate(piece.gameObject);
                var masonry = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/Masonry.mat");
                foreach (var parapet in objects["Low observation parapet"])
                    foreach (var renderer in parapet.GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.sharedMaterials = Enumerable.Repeat(masonry, renderer.sharedMaterials.Length).ToArray();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                var horizon = objects.Where(group => group.Key == "Distant basalt crag" ||
                        group.Key.StartsWith("Medieval horizon ridge ", StringComparison.Ordinal))
                    .SelectMany(group => group).ToArray();
                if (!objects["Medieval weathered foothill west"].Any()) ReplaceMedievalHorizon(horizon);
                UpgradeGothicHall(objects);
            }
        }

        private static void ReplaceMedievalHorizon(Transform[] crags)
        {
            if (crags.Length == 0) return;
            var meshes = LoadBackdropMeshes("MedievalRidge");
            if (meshes == null) return;
            var strata = LoadBackdropMeshes("GobiStrata");
            var heights = new[] { 26f, 31f, 20f, 29f, 28f };
            for (int i = 0; i < heights.Length; i++)
            {
                PlaceBackdrop(crags[0].parent, meshes, "Medieval horizon ridge " + (i + 1),
                    new Vector3(-137f + i * 68f, -3.6f, -181f - i % 2 * 10f),
                    new Vector3(105f, heights[i], 35f + i % 3 * 5f), i % 2 == 0 ? -12f + i * 4f : 178f - i * 7f);
            }
            if (strata != null)
            {
                PlaceBackdrop(crags[0].parent, strata, "Medieval weathered foothill west", new Vector3(-53f, -3.8f, -178f),
                    new Vector3(64f, 18f, 36f), 23f);
                PlaceBackdrop(crags[0].parent, strata, "Medieval weathered foothill east", new Vector3(59f, -4.8f, -181f),
                    new Vector3(75f, 22f, 41f), 204f);
            }
            foreach (var crag in crags) Object.DestroyImmediate(crag.gameObject);
        }

        private static Mesh[] LoadBackdropMeshes(string name)
        {
            var meshes = new[] { 0, 1 }.Select(index => AssetDatabase.LoadAllAssetsAtPath(
                MeshFolder + "/" + name + "_LOD" + index + ".obj").OfType<Mesh>().SingleOrDefault()).ToArray();
            return meshes.Any(mesh => mesh == null) ? null : meshes;
        }

        private static void UpgradeBackgroundDetail(ILookup<string, Transform> objects)
        {
            foreach (var root in objects.SelectMany(group => group))
            {
                bool backdrop = root.name == "Photographic broadleaf tree" || root.name.Contains("skyline") ||
                    root.name.StartsWith("Far ", StringComparison.Ordinal) || root.name.StartsWith("Northern saddle", StringComparison.Ordinal) ||
                    root.name.EndsWith("/ YardangMesa", StringComparison.Ordinal);
                if (!backdrop || root.GetComponent<LODGroup>() == null) continue;
                var levels = root.GetComponent<LODGroup>().GetLODs();
                if (levels.Length != 3 || levels[0].renderers.Length != 1 || levels[1].renderers.Length != 1) continue;
                var filter = levels[0].renderers[0].GetComponent<MeshFilter>();
                var source = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                if (filter.sharedMesh == source) continue;
                filter.sharedMesh = source;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            }
        }

        private static void PlaceBackdrop(Transform parent, Mesh[] meshes, string name, Vector3 position, Vector3 size, float yaw,
            Material material = null)
        {
            material = material != null ? material : AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/Rock.mat");
            if (material == null) throw new InvalidOperationException("Missing rock surface material.");
            Vector3 bounds = meshes[0].bounds.size;
            var ridge = new GameObject(name);
            ridge.transform.SetParent(parent, false);
            ridge.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            ridge.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, size.z / bounds.z);
            var levels = new LOD[meshes.Length];
            for (int lod = 0; lod < meshes.Length; lod++)
            {
                var surface = new GameObject("Mesh " + lod, typeof(MeshFilter), typeof(MeshRenderer));
                surface.transform.SetParent(ridge.transform, false);
                surface.GetComponent<MeshFilter>().sharedMesh = meshes[lod];
                var renderer = surface.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                levels[lod] = new LOD(lod == 0 ? .18f : .006f, new Renderer[] { renderer });
            }
            var group = ridge.AddComponent<LODGroup>();
            group.SetLODs(levels);
            group.RecalculateBounds();
        }

        private static Mesh GothicNavePier()
        {
            const string key = "GothicNavePier";
            if (Meshes.TryGetValue(key, out var mesh)) return mesh;
            const int sides = 32;
            var profile = new[]
            {
                new Vector3(-1f,.73f,0), new Vector3(-.94f,.73f,0), new Vector3(-.925f,.66f,0),
                new Vector3(-.87f,.65f,0), new Vector3(-.85f,.56f,.04f), new Vector3(-.81f,.52f,.11f),
                new Vector3(-.76f,.51f,.14f), new Vector3(.78f,.46f,.13f), new Vector3(.83f,.5f,.08f),
                new Vector3(.855f,.55f,.05f), new Vector3(.89f,.59f,.03f), new Vector3(.935f,.64f,0),
                new Vector3(.955f,.69f,0), new Vector3(1.02f,.69f,0)
            };
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring < profile.Length; ring++)
            {
                Vector3 moulding = profile[ring];
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side * (2f * Mathf.PI / sides);
                    float radius = moulding.y - moulding.z * (.5f - .5f * Mathf.Cos(angle * 8f));
                    vertices.Add(new Vector3(radius * Mathf.Cos(angle), moulding.x, radius * Mathf.Sin(angle)));
                    uv.Add(new Vector2((float)side / sides * 1.5f, (moulding.x + 1f) * 1.8f));
                    if (ring == 0 || side == sides) continue;
                    int lower = (ring - 1) * (sides + 1) + side;
                    int upper = lower + sides + 1;
                    triangles.AddRange(new[] { lower, upper, upper + 1, lower, upper + 1, lower + 1 });
                }
            }
            foreach (int ring in new[] { 0, profile.Length - 1 })
            {
                int center = vertices.Count;
                vertices.Add(new Vector3(0, profile[ring].x, 0));
                uv.Add(Vector2.zero);
                for (int side = 0; side < sides; side++)
                {
                    int edge = ring * (sides + 1) + side;
                    triangles.AddRange(ring == 0 ? new[] { center, edge, edge + 1 } : new[] { center, edge + 1, edge });
                }
            }
            return SaveMesh(key, vertices, triangles, uv);
        }

        private static void ReplaceVisual(Transform structure, string source, Vector3 ground, Vector3 size, Quaternion rotation, int firstLod)
        {
            if (structure.Find(ModelVisual) != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (prefab == null) throw new InvalidOperationException("Missing environment model " + source);
            var lods = prefab.GetComponent<LODGroup>().GetLODs();
            var reference = lods[firstLod].renderers.Single().GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 rotatedSize = rotation * reference.size;
            rotatedSize = new Vector3(Mathf.Abs(rotatedSize.x), Mathf.Abs(rotatedSize.y), Mathf.Abs(rotatedSize.z));
            var wrapper = new GameObject(ModelVisual);
            wrapper.transform.SetParent(structure);
            wrapper.transform.SetPositionAndRotation(ground, Quaternion.identity);
            wrapper.transform.localScale = new Vector3(size.x / rotatedSize.x, size.y / rotatedSize.y, size.z / rotatedSize.z);
            var levels = new List<LOD>();
            for (int index = firstLod; index < lods.Length; index++)
            {
                var sourceRenderer = lods[index].renderers.Single();
                var item = new GameObject("Mesh " + index, typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(wrapper.transform, false);
                item.transform.localRotation = rotation;
                item.transform.localPosition = -(rotation * new Vector3(reference.center.x, reference.min.y, reference.center.z));
                item.GetComponent<MeshFilter>().sharedMesh = sourceRenderer.GetComponent<MeshFilter>().sharedMesh;
                var renderer = item.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = index == firstLod ? ShadowCastingMode.On : ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.OccludeeStatic);
                levels.Add(new LOD(index == lods.Length - 1 ? .004f : .19f, new Renderer[] { renderer }));
            }
            var group = wrapper.AddComponent<LODGroup>();
            group.SetLODs(levels.ToArray());
            group.RecalculateBounds();
            Object.DestroyImmediate(structure.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(structure.GetComponent<MeshFilter>());
        }

        private static void UpgradeRammedEarth(Transform wall)
        {
            var filter = wall.GetComponent<MeshFilter>();
            var original = filter.sharedMesh;
            var size = original.bounds.size;
            string key = FormattableString.Invariant($"RammedEarth_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}");
            bool alongZ = size.z > size.x;
            float halfLength = Mathf.Max(size.x, size.z) * .5f;
            float halfDepth = Mathf.Min(size.x, size.z) * .5f;
            float halfHeight = size.y * .5f;
            var section = new[] { new Vector2(-1f,-1f), new Vector2(-.96f,-.72f), new Vector2(-.85f,-.37f),
                new Vector2(-.78f,.4f), new Vector2(-.73f,.87f), new Vector2(-.65f,1f),
                new Vector2(.65f,1f), new Vector2(.73f,.87f), new Vector2(.78f,.4f),
                new Vector2(.85f,-.37f), new Vector2(.96f,-.72f), new Vector2(1f,-1f) };
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 Point(float run, Vector2 edge)
            {
                var p = new Vector3(run, edge.y * halfHeight, edge.x * halfDepth);
                return alongZ ? new Vector3(p.z, p.y, -p.x) : p;
            }
            for (int i = 0; i < section.Length; i++)
            {
                Vector2 a = section[i], b = section[(i + 1) % section.Length];
                int first = vertices.Count;
                vertices.AddRange(new[] { Point(-halfLength,a), Point(-halfLength,b), Point(halfLength,b), Point(halfLength,a) });
                float vA = (a.y == b.y ? a.x * halfDepth : a.y * halfHeight) * .35f;
                float vB = (a.y == b.y ? b.x * halfDepth : b.y * halfHeight) * .35f;
                uv.AddRange(new[] { new Vector2(0,vA), new Vector2(0,vB), new Vector2(halfLength*.7f,vB), new Vector2(halfLength*.7f,vA) });
                triangles.AddRange(new[] { first, first+1, first+2, first, first+2, first+3 });
            }
            foreach (int end in new[] { -1, 1 })
            {
                int center = vertices.Count;
                vertices.Add(Point(end * halfLength, Vector2.zero));
                uv.Add(Vector2.zero);
                int first = vertices.Count;
                foreach (var edge in section)
                {
                    vertices.Add(Point(end * halfLength, edge));
                    uv.Add(new Vector2(edge.x * halfDepth, edge.y * halfHeight) * .35f);
                }
                for (int i = 0; i < section.Length; i++)
                {
                    int next = first + (i + 1) % section.Length;
                    triangles.AddRange(end < 0 ? new[] { center, next, first+i } : new[] { center, first+i, next });
                }
            }
            filter.sharedMesh = SaveMesh(key, vertices, triangles, uv);
        }
    }
}
