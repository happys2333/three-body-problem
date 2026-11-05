using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeBodyProblem
{
    internal sealed class CapitalSnowCover : IDisposable
    {
        private static readonly int Deposits = Shader.PropertyToID("_Deposits");
        private static readonly int CoverBounds = Shader.PropertyToID("_Bounds");
        private readonly GameObject root;
        private readonly Material material;
        private readonly Mesh ground;
        private readonly Mesh paving;

        internal CapitalSnowCover(TravellerStage stage)
        {
            var terrain = stage.GetComponentInChildren<Terrain>();
            var shader = Resources.Load<Shader>("ChaogeSnowCover");
            if (terrain == null || shader == null) return;

            root = new GameObject("Long night ground deposits");
            root.transform.SetParent(stage.transform, false);
            root.SetActive(false);
            material = new Material(shader) { name = "Water snow, dry ice and air ice" };
            var center = stage.profile.waterGoal;
            var min = new Vector2(center.x - 96, center.z - 110);
            var max = new Vector2(center.x + 116, center.z + 132);
            material.SetVector(CoverBounds, new Vector4(min.x, min.y, max.x, max.y));
            ground = BuildGround(terrain, min, max);
            AddSurface(ground, terrain.name, terrain.transform.position, terrain.transform.rotation, terrain.transform.lossyScale);
            paving = new Mesh
            {
                name = "Snow cover for flat paving",
                vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f),
                    new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f) },
                triangles = new[] { 0, 1, 2, 2, 1, 3 }
            };
            paving.RecalculateNormals();
            paving.UploadMeshData(true);

            // Cover flat paving and the frozen pool at their actual top surfaces.
            foreach (var renderer in stage.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Water garden approach" && renderer.name != "Revival pool ice" &&
                    renderer.name != "Ceremonial forecourt" && renderer.name != "Royal processional road") continue;
                var bounds = renderer.bounds;
                var top = new Vector3(bounds.center.x, bounds.max.y + .004f, bounds.center.z);
                AddSurface(paving, renderer.name, top, Quaternion.identity, new Vector3(bounds.size.x, 1, bounds.size.z));
            }
            // The ramp's render mesh is batched. Its collision mesh follows the same slope.
            foreach (var collider in stage.GetComponentsInChildren<MeshCollider>())
                if (collider.name == "Pyramid east garden descent")
                    AddSurface(collider.sharedMesh, collider.name, collider.transform.position + Vector3.up * .009f,
                        collider.transform.rotation, collider.transform.lossyScale);
        }

        internal void SetDeposits(Vector3 amounts)
        {
            if (root == null) return;
            material.SetVector(Deposits, amounts);
            root.SetActive(amounts.x > 0);
        }

        private void AddSurface(Mesh mesh, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (mesh == null) return;
            var surface = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(root.transform, false);
            surface.transform.SetPositionAndRotation(position, rotation);
            surface.transform.localScale = scale;
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
        }

        private static Mesh BuildGround(Terrain terrain, Vector2 min, Vector2 max)
        {
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            var spacing = data.heightmapScale;
            int last = data.heightmapResolution - 1;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - origin.x) / spacing.x), 0, last);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - origin.z) / spacing.z), 0, last);
            int width = Mathf.Clamp(Mathf.CeilToInt((max.x - origin.x) / spacing.x), x0, last) - x0 + 1;
            int depth = Mathf.Clamp(Mathf.CeilToInt((max.y - origin.z) / spacing.z), z0, last) - z0 + 1;
            float[,] heights = data.GetHeights(x0, z0, width, depth);
            var vertices = new Vector3[width * depth];
            var indices = new int[(width - 1) * (depth - 1) * 6];
            int face = 0;
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                vertices[i] = new Vector3((x0 + x) * spacing.x, heights[z, x] * data.size.y + .022f, (z0 + z) * spacing.z);
                if (x == width - 1 || z == depth - 1) continue;
                indices[face++] = i;
                indices[face++] = i + width;
                indices[face++] = i + 1;
                indices[face++] = i + 1;
                indices[face++] = i + width;
                indices[face++] = i + width + 1;
            }
            var mesh = new Mesh { name = "Chaoge terrain snow cover", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            mesh.UploadMeshData(true);
            return mesh;
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (material != null) UnityEngine.Object.Destroy(material);
            if (ground != null) UnityEngine.Object.Destroy(ground);
            if (paving != null) UnityEngine.Object.Destroy(paving);
        }
    }
}
