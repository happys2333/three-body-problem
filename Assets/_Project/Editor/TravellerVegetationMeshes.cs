using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static Mesh[] photographicTreeLods;

        // Fit each of the publisher's 30,250 disconnected leaf clusters to its
        // photographic atlas. Full-density leaf cards preserve the canopy shape
        // with far fewer triangles than the original curved leaf surfaces.
        private sealed class LeafPlane
        {
            public int count;
            public float u, v, uu, vv, uv, minU = float.MaxValue, minV = float.MaxValue, maxU = float.MinValue, maxV = float.MinValue;
            public Vector3 p, pu, pv, normal;
            public void Add(Vector3 point, Vector3 n, Vector2 tex)
            {
                count++; u += tex.x; v += tex.y; uu += tex.x * tex.x; vv += tex.y * tex.y; uv += tex.x * tex.y;
                p += point; pu += point * tex.x; pv += point * tex.y; normal += n;
                minU = Mathf.Min(minU, tex.x); maxU = Mathf.Max(maxU, tex.x); minV = Mathf.Min(minV, tex.y); maxV = Mathf.Max(maxV, tex.y);
            }
        }
        private static Mesh[] TreeMeshes()
        {
            if (photographicTreeLods != null && photographicTreeLods[0] != null) return photographicTreeLods;
            photographicTreeLods = new Mesh[3];
            bool existing = true;
            for (int i = 0; i < 3; i++)
            {
                photographicTreeLods[i] = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/PhotographicTreeCardsV3_LOD" + i + ".asset");
                existing &= photographicTreeLods[i] != null;
            }
            if (existing) return photographicTreeLods;
            var sourceFilter = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentSources + "tree_small_02/tree_small_02_1k.fbx").GetComponentInChildren<MeshFilter>();
            var source = sourceFilter.sharedMesh;
            var indices = source.GetIndices(1); var parent = new int[source.vertexCount];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int i = 0; i < indices.Length; i += 3)
            {
                parent[TreeRoot(parent, indices[i + 1])] = TreeRoot(parent, indices[i]);
                parent[TreeRoot(parent, indices[i + 2])] = TreeRoot(parent, indices[i]);
            }
            var positions = source.vertices; var normals = source.normals; var uv = source.uv;
            var transform = sourceFilter.transform.localToWorldMatrix; var normalTransform = transform.inverse.transpose;
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = transform.MultiplyPoint3x4(positions[i]); normals[i] = normalTransform.MultiplyVector(normals[i]).normalized;
            }
            var leaves = new Dictionary<int, LeafPlane>();
            for (int i = 0; i < indices.Length; i++)
            {
                int vertex = indices[i], root = TreeRoot(parent, vertex); LeafPlane leaf;
                if (!leaves.TryGetValue(root, out leaf)) { leaf = new LeafPlane(); leaves[root] = leaf; }
                leaf.Add(positions[vertex], normals[vertex], uv[vertex]);
            }
            for (int level = 0; level < 3; level++)
            {
                int limit = level == 0 ? 1000 : level == 1 ? 650 : 350;
                var remap = new Dictionary<int, int>(); var points = new List<Vector3>(); var ns = new List<Vector3>(); var tex = new List<Vector2>();
                var subsets = new List<int>[3];
                for (int sub = 0; sub < 3; sub++)
                {
                    var input = source.GetIndices(sub); var output = new List<int>(); subsets[sub] = output;
                    if (sub == 1)
                    {
                        foreach (var entry in leaves)
                        {
                            if (unchecked((uint)entry.Key * 2654435761u) % 1000 >= limit) continue;
                            var leaf = entry.Value; float inv = 1f / leaf.count, u = leaf.u * inv, v = leaf.v * inv;
                            float cu = leaf.uu * inv - u * u, cv = leaf.vv * inv - v * v, cross = leaf.uv * inv - u * v;
                            float det = cu * cv - cross * cross;
                            if (Mathf.Abs(det) < 0.00000000001f) continue;
                            Vector3 centre = leaf.p * inv, pu = leaf.pu * inv - centre * u, pv = leaf.pv * inv - centre * v;
                            Vector3 du = (pu * cv - pv * cross) / det, dv = (pv * cu - pu * cross) / det;
                            Vector3 origin = centre - du * u - dv * v; int first = points.Count;
                            foreach (var t in new[] { new Vector2(leaf.minU, leaf.minV), new Vector2(leaf.maxU, leaf.minV), new Vector2(leaf.maxU, leaf.maxV), new Vector2(leaf.minU, leaf.maxV) })
                            { points.Add(origin + du * t.x + dv * t.y); ns.Add(leaf.normal.normalized); tex.Add(t); }
                            bool reverse = Vector3.Dot(Vector3.Cross(du, dv), leaf.normal) < 0;
                            output.AddRange(reverse ? new[] { first, first + 2, first + 1, first, first + 3, first + 2 } : new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                        }
                        continue;
                    }
                    for (int t = 0; t < input.Length; t += 3)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            int original = input[t + c], mapped;
                            if (!remap.TryGetValue(original, out mapped))
                            {
                                mapped = points.Count; remap[original] = mapped;
                                points.Add(positions[original]); ns.Add(normals[original]); tex.Add(uv[original]);
                            }
                            output.Add(mapped);
                        }
                    }
                }
                var mesh = new Mesh { name = "PhotographicTreeCardsV3_LOD" + level, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(points); mesh.SetNormals(ns); mesh.SetUVs(0, tex); mesh.subMeshCount = 3;
                for (int sub = 0; sub < 3; sub++) mesh.SetTriangles(subsets[sub], sub);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                AssetDatabase.CreateAsset(mesh, MeshFolder + "/" + mesh.name + ".asset"); photographicTreeLods[level] = mesh;
            }
            return photographicTreeLods;
        }

        private static int TreeRoot(int[] parents, int i)
        {
            while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; }
            return i;
        }

        private static void NaturalTree(Vector3 position, float height, Transform parent, float rotation)
        {
            var meshes = TreeMeshes(); var root = new GameObject("Photographic broadleaf tree");
            root.transform.SetParent(parent); var bounds = meshes[0].bounds;
            root.transform.localScale = Vector3.one * height / bounds.size.y;
            root.transform.position = position; root.transform.rotation = Quaternion.Euler(0, rotation, 0);
            var lods = new LOD[3]; var materials = new[] { Materials["Environment_TreeBranch"], Materials["Environment_TreeLeaf"], Materials["Environment_TreeTrunk"] };
            for (int i = 0; i < 3; i++)
            {
                var child = new GameObject("Leaf-cluster LOD " + i, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(root.transform, false);
                child.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                child.GetComponent<MeshFilter>().sharedMesh = i==0?meshes[i]:EnvironmentLod(meshes[i],"TreeCards",i);
                var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                renderer.shadowCastingMode=i==0?ShadowCastingMode.On:ShadowCastingMode.Off;
                lods[i] = new LOD(i == 0 ? 0.35f : i == 1 ? 0.12f : 0.018f, new Renderer[] { renderer });
            }
            root.AddComponent<LODGroup>().SetLODs(lods); root.GetComponent<LODGroup>().RecalculateBounds();
        }
    }
}
