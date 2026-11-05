using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Geometry = ThreeBodyProblem.Editor.MasonryArchitectureLibrary.Geometry;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static void UpgradeCapitalTerrain(ILookup<string, Transform> objects)
        {
            var gate = objects["Outer earth wall"].First().parent;
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in new[] { -210f, 213f })
                {
                    string name = (z < 0 ? "South " : "North ") + (side < 0 ? "west" : "east") + " earth corner";
                    if (gate.Find(name) != null) continue;
                    var root = new GameObject(name).transform;
                    root.SetParent(gate, false);
                    root.position = new Vector3(side * 218f, 0, z);
                    var earth = new Geometry();
                    var outline = new[]
                    {
                        new Vector2(-1, -.72f), new Vector2(-.72f, -1), new Vector2(.72f, -1), new Vector2(1, -.72f),
                        new Vector2(1, .72f), new Vector2(.72f, 1), new Vector2(-.72f, 1), new Vector2(-1, .72f)
                    };
                    var rings = new[]
                    {
                        new Vector2(7.55f, 0), new Vector2(7.3f, .62f), new Vector2(6.7f, 2.1f),
                        new Vector2(5.24f, 6.85f), new Vector2(4.7f, 7.96f)
                    };
                    Vector3 Point(Vector2 corner, Vector2 section) => new Vector3(corner.x * section.x, section.y, corner.y * section.x);
                    for (int ring = 0; ring + 1 < rings.Length; ring++)
                        for (int edge = 0; edge < outline.Length; edge++)
                        {
                            int next = (edge + 1) % outline.Length;
                            var outward = outline[edge] + outline[next];
                            earth.Face(new[] { Point(outline[edge], rings[ring]), Point(outline[next], rings[ring]),
                                Point(outline[next], rings[ring + 1]), Point(outline[edge], rings[ring + 1]) },
                                new Vector3(outward.x, .35f, outward.y));
                        }
                    earth.Face(outline.Select(point => Point(point, rings[0])).ToArray(), Vector3.down);
                    earth.Face(outline.Select(point => Point(point, rings[rings.Length - 1])).ToArray(), Vector3.up);
                    var shell = StructurePart(root, "Continuous battered corner", StructureMesh("ShangEarthCorner", earth),
                        ArchitectureMaterial("CapitalWall"));
                    shell.AddComponent<MeshCollider>().sharedMesh = shell.GetComponent<MeshFilter>().sharedMesh;
                }

            var terrain = objects["Photographic river-valley terrain"].SingleOrDefault()?.GetComponent<Terrain>();
            if (terrain == null) return;
            var meshes = LoadBackdropMeshes("ChaogeLoessRidge");
            if (meshes == null) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/Earth.mat");
            if (material == null) throw new InvalidOperationException("Missing earth surface for the distant river valley.");
            PlaceRiverHighland(terrain, meshes, material, "Western river-valley escarpment", new Vector3(-515, 0, 50),
                new Vector3(300, 90, 150), -84);
            PlaceRiverHighland(terrain, meshes, material, "North-west weathered ridge", new Vector3(-385, 0, 440),
                new Vector3(300, 82, 150), -42);
            PlaceRiverHighland(terrain, meshes, material, "Northern distant saddle", new Vector3(-60, 0, 565),
                new Vector3(280, 68, 145), -6);
            PlaceRiverHighland(terrain, meshes, material, "North-east river terraces", new Vector3(425, 0, 495),
                new Vector3(305, 65, 145), 41);
            PlaceRiverHighland(terrain, meshes, material, "Eastern far-bank escarpment", new Vector3(550, 0, -115),
                new Vector3(270, 75, 160), 105);
            PlaceRiverHighland(terrain, meshes, material, "South-west distant foothill", new Vector3(-460, 0, -480),
                new Vector3(280, 60, 140), -136);
        }

        private static void PlaceRiverHighland(Terrain terrain, Mesh[] meshes, Material material, string name,
            Vector3 site, Vector3 dimensions, float yaw)
        {
            var parent = terrain.transform.parent;
            float floor = float.MaxValue;
            var rotation = Quaternion.Euler(0, yaw, 0);
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                {
                    var point = site + rotation * new Vector3(x * dimensions.x * .25f, 0, z * dimensions.z * .25f);
                    floor = Mathf.Min(floor, terrain.SampleHeight(point) + terrain.transform.position.y);
                }
            site.y = floor - dimensions.y * .06f;
            var existing = parent.Find(name);
            if (existing != null)
            {
                existing.SetPositionAndRotation(site, rotation);
                var bounds = meshes[0].bounds.size;
                existing.localScale = new Vector3(dimensions.x / bounds.x, dimensions.y / bounds.y, dimensions.z / bounds.z);
            }
            else PlaceBackdrop(parent, meshes, name, site, dimensions, yaw, material);
        }
    }
}
