using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        // The original scan is used close to the player. Farther levels merge
        // nearby vertices while retaining UV islands and photographic materials.
        internal static Mesh EnvironmentLod(Mesh source, string asset, int level)
        {
            string key = "ScanLOD_" + asset + "_" + source.name + "_" + level;
            string path = MeshFolder + "/" + key + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (saved != null) return saved;
            var positions = source.vertices; var normals = source.normals; var tex = source.uv;
            float cell = Mathf.Max(source.bounds.size.x, source.bounds.size.y, source.bounds.size.z) / (level == 1 ? 42 : 18);
            int uvCells = level == 1 ? 48 : 20;
            var buckets = new Dictionary<Vector3Int, List<int>>();
            var points = new List<Vector3>(); var ns = new List<Vector3>(); var uv = new List<Vector2>(); var counts = new List<int>(); var map = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var keyPosition = new Vector3Int(Mathf.RoundToInt(positions[i].x / cell), Mathf.RoundToInt(positions[i].y / cell), Mathf.RoundToInt(positions[i].z / cell));
                List<int> candidates;
                if (!buckets.TryGetValue(keyPosition, out candidates)) { candidates = new List<int>(); buckets[keyPosition] = candidates; }
                int match = -1;
                foreach (int candidate in candidates)
                {
                    var average = uv[candidate] / counts[candidate];
                    if (Mathf.FloorToInt(average.x * uvCells) == Mathf.FloorToInt(tex[i].x * uvCells) && Mathf.FloorToInt(average.y * uvCells) == Mathf.FloorToInt(tex[i].y * uvCells)
                        && Vector3.Dot(ns[candidate].normalized, normals[i]) > 0.25f) { match = candidate; break; }
                }
                if (match < 0) { match = points.Count; candidates.Add(match); points.Add(Vector3.zero); ns.Add(Vector3.zero); uv.Add(Vector2.zero); counts.Add(0); }
                points[match] += positions[i]; ns[match] += normals[i]; uv[match] += tex[i]; counts[match]++; map[i] = match;
            }
            for (int i = 0; i < points.Count; i++) { points[i] /= counts[i]; ns[i] = ns[i].normalized; uv[i] /= counts[i]; }
            var mesh = new Mesh { name = key, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(points); mesh.SetNormals(ns); mesh.SetUVs(0, uv); mesh.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var input = source.GetIndices(sub); var output = new List<int>();
                for (int i = 0; i < input.Length; i += 3)
                {
                    int a = map[input[i]], b = map[input[i + 1]], c = map[input[i + 2]];
                    if (a != b && b != c && a != c) output.AddRange(new[] { a, b, c });
                }
                mesh.SetTriangles(output, sub);
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void RockLods(GameObject root, string asset, Renderer[] high)
        {
            var lods = new LOD[3]; lods[0] = new LOD(0.25f, high);
            for (int level = 1; level <= 2; level++)
            {
                var renderers = new List<Renderer>();
                foreach (var original in high)
                {
                    var filter = original.GetComponent<MeshFilter>(); if (filter == null) continue;
                    var child = new GameObject("Photographic scan LOD " + level, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(original.transform, false);
                    child.GetComponent<MeshFilter>().sharedMesh = EnvironmentLod(filter.sharedMesh, asset, level);
                    var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterials = original.sharedMaterials;
                    renderer.shadowCastingMode = level == 1 ? ShadowCastingMode.On : ShadowCastingMode.Off; renderers.Add(renderer);
                }
                lods[level] = new LOD(level == 1 ? 0.065f : 0.009f, renderers.ToArray());
            }
            var group = root.GetComponent<LODGroup>(); if (group == null) group = root.AddComponent<LODGroup>();
            group.SetLODs(lods); group.RecalculateBounds();
        }

        private static void SealedShelterRock(string name, Vector3 centre, Vector3 size, int seed, Transform parent, bool collision=false)
        {
            const int rings = 64, segments = 128;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float latitude = -Mathf.PI * 0.5f + ring * Mathf.PI / rings;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float longitude = segment * Mathf.PI * 2 / segments;
                    var direction = new Vector3(Mathf.Cos(latitude) * Mathf.Cos(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(longitude));
                    float grain = Mathf.PerlinNoise(direction.x * 2.3f + seed, direction.z * 2.3f + direction.y * 1.7f + 37);
                    float fine = Mathf.PerlinNoise(direction.x * 12 + seed, direction.z * 12 + direction.y * 8 + 81);
                    float radius = 0.88f + grain * 0.19f + fine * 0.035f;
                    vertices.Add(Vector3.Scale(direction * radius, size * 0.5f));
                    uv.Add(new Vector2((float)segment / segments * 8, (float)ring / rings * 4));
                    if (ring == rings || segment == segments) continue;
                    int p = ring * (segments + 1) + segment;
                    triangles.AddRange(new[] { p, p + segments + 1, p + 1, p + 1, p + segments + 1, p + segments + 2 });
                }
            }
            var rock=MeshObject(name, SaveMesh("SealedShelterRock_" + seed, vertices, triangles, uv), centre, "ShelterStone", parent);
            if(collision)rock.AddComponent<BoxCollider>().size=size;
        }
    }
}
