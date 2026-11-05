using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Geometry = ThreeBodyProblem.Editor.MasonryArchitectureLibrary.Geometry;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static Material ArchitectureMaterial(string key)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/" + key + ".mat");
            return material != null ? material : throw new InvalidOperationException("Missing material " + key);
        }

        private static Mesh StructureMesh(string key, Geometry geometry) =>
            SaveMesh(key, geometry.vertices, geometry.indices, geometry.uv.Select(uv => uv * .35f).ToList());

        private static GameObject StructurePart(Transform parent, string name, Mesh mesh, Material material,
            bool shadows = true)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.OccludeeStatic);
            return part;
        }

        private static void StripOriginalVisual(Transform anchor)
        {
            Object.DestroyImmediate(anchor.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(anchor.GetComponent<MeshFilter>());
        }

        private static void CutBeam(Geometry geometry, Vector3 from, Vector3 to, float width, float depth, float bevel = .025f)
        {
            var delta = to - from;
            var transform = Matrix4x4.TRS((from + to) * .5f,
                Quaternion.FromToRotation(Vector3.up, delta.normalized), Vector3.one);
            geometry.Box(Vector3.zero, new Vector3(width, delta.magnitude, depth), transform, bevel);
        }

        private static void UpgradeChaogeGate(ILookup<string, Transform> objects)
        {
            var wood = ArchitectureMaterial("CapitalTimber");
            var reed = ArchitectureMaterial("CapitalDarkThatch");
            var bronze = ArchitectureMaterial("CapitalBronze");
            foreach (var roof in objects["Gate watch hall roof"].Concat(objects["Gate central shelter"]))
            {
                var old = roof.Find("Hipped reed roof");
                if (old == null) continue;
                Vector3 size = old.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                var previous = roof.Cast<Transform>().ToArray();
                var (thatch, frame) = GateRoofMeshes(size);
                StructurePart(roof, "Lapped reed roof", thatch, reed);
                StructurePart(roof, "Eave rafters and beams", frame, wood);
                foreach (var piece in previous) Object.DestroyImmediate(piece.gameObject);
            }
            foreach (var post in objects["Gate timber post"])
            {
                if (post.GetComponent<MeshFilter>() == null) continue;
                var joined = new Geometry();
                joined.Box(Vector3.zero, new Vector3(.86f, 2f, .86f), Matrix4x4.identity, .12f);
                foreach (float y in new[] { -.86f, .82f })
                    joined.Box(new Vector3(0, y, 0), new Vector3(1.08f, .18f, 1.08f), Matrix4x4.identity, .06f);
                post.GetComponent<MeshFilter>().sharedMesh = StructureMesh("ChaogeGateTenonedPost", joined);
            }
            foreach (var bridge in objects["Timber gate bridge"])
            {
                if (bridge.Find("Pegged bridge deck") != null) continue;
                var geometry = new Geometry();
                for (int plank = 0; plank < 24; plank++)
                    geometry.Box(new Vector3(-11.5f + plank, .78f, 0), new Vector3(.98f, .44f, 9f),
                        Matrix4x4.identity, .045f);
                foreach (float z in new[] { -3.85f, -1.28f, 1.28f, 3.85f })
                    geometry.Box(new Vector3(0, -.05f, z), new Vector3(25f, 1.34f, .72f), Matrix4x4.identity, .08f);
                foreach (float z in new[] { -4.23f, 4.23f })
                    geometry.Box(new Vector3(0, .63f, z), new Vector3(25f, .48f, .33f), Matrix4x4.identity, .045f);
                StructurePart(bridge, "Pegged bridge deck", StructureMesh("ChaogeGateBridgeDeck", geometry), wood);
                StripOriginalVisual(bridge);
            }
            foreach (var leaf in objects["Gate timber leaf open"])
            {
                var boards = leaf.Find("Braced timber gate leaf");
                if (boards == null)
                {
                    var (planks, fittings) = GateLeafMeshes();
                    boards = StructurePart(leaf, "Braced timber gate leaf", planks, wood).transform;
                    boards.localRotation = Quaternion.Euler(0, leaf.position.x < 0 ? 90f : -90f, 0);
                    StructurePart(boards, "Bronze gate fittings", fittings, bronze);
                    StripOriginalVisual(leaf);
                }
                var cast = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/PalaceInterior/Materials/PatinatedBronze.mat");
                PlaceLocalModel(boards, "ShangGateBronze", "Cast relief plate", cast != null ? cast : bronze,
                    new Vector3(0, 1.06f, .403f), new Vector3(1.72f, 2.43f, .205f));
                if (boards.Find("Cast relief plate") != null && boards.Find("Carved plaque mount") == null)
                {
                    var mount = new Geometry();
                    mount.Box(new Vector3(0, 1.06f, .25f), new Vector3(1.77f, 2.48f, .116f),
                        Matrix4x4.identity, .032f);
                    StructurePart(boards, "Carved plaque mount", StructureMesh("ChaogeGatePlaqueMount", mount), wood);
                }
            }
        }

        private static (Mesh reed, Mesh frame) GateRoofMeshes(Vector3 size)
        {
            string key = FormattableString.Invariant($"ChaogeReedRoof_{size.x:0.##}_{size.z:0.##}_{size.y:0.##}");
            if (Meshes.TryGetValue(key, out var existing)) return (existing, Meshes[key + "_Frame"]);
            var reed = new Geometry();
            var frame = new Geometry();
            float x = size.x * .5f, z = size.z * .5f, rise = size.y;
            var eaves = new[] { new Vector3(-x, 0, -z), new Vector3(x, 0, -z),
                new Vector3(x, 0, z), new Vector3(-x, 0, z) };
            var ridge = eaves.Select(p => new Vector3(Mathf.Sign(p.x) * x * .54f, rise, 0)).ToArray();
            const int courses = 7;
            for (int face = 0; face < 4; face++)
            {
                int next = (face + 1) % 4;
                Vector3 outward = (eaves[face] + eaves[next]).normalized;
                for (int row = 0; row < courses; row++)
                {
                    float low = (float)row / courses, high = (float)(row + 1) / courses;
                    Vector3 start = Vector3.Lerp(eaves[face], ridge[face], low);
                    Vector3 end = Vector3.Lerp(eaves[next], ridge[next], low);
                    Vector3 topStart = Vector3.Lerp(eaves[face], ridge[face], high);
                    Vector3 topEnd = Vector3.Lerp(eaves[next], ridge[next], high);
                    var lap = Vector3.up * (row == 0 ? .11f : .16f);
                    reed.Face(new[] { start + lap, end + lap, topEnd, topStart }, Vector3.up);
                    reed.Face(new[] { start, end, end + lap, start + lap }, outward);
                }
                Vector3 soffit = Vector3.down * .24f;
                reed.Face(new[] { eaves[face] + soffit, eaves[next] + soffit, ridge[next] + soffit, ridge[face] + soffit }, Vector3.down);
                reed.Face(new[] { eaves[face], eaves[next], eaves[next] + soffit, eaves[face] + soffit }, outward);
                CutBeam(frame, eaves[face] + soffit, eaves[next] + soffit, .42f, .28f);
                int rafters = Mathf.CeilToInt(Vector3.Distance(eaves[face], eaves[next]) / 2.3f);
                for (int i = 0; i <= rafters; i++)
                {
                    float t = (float)i / rafters;
                    var foot = Vector3.Lerp(eaves[face], eaves[next], t) + Vector3.down * .29f;
                    var head = Vector3.Lerp(ridge[face], ridge[next], t) + Vector3.down * .3f;
                    CutBeam(frame, foot, head, .18f, .23f);
                }
                var tieStart = eaves[face] * .59f + Vector3.down * .43f;
                var tieEnd = eaves[next] * .59f + Vector3.down * .43f;
                CutBeam(frame, tieStart, tieEnd, .48f, .48f);
            }
            CutBeam(frame, ridge[0] + new Vector3(-.22f, .07f, 0),
                ridge[1] + new Vector3(.22f, .07f, 0), .38f, .36f);
            return (StructureMesh(key, reed), StructureMesh(key + "_Frame", frame));
        }

        private static (Mesh planks, Mesh fittings) GateLeafMeshes()
        {
            const string key = "ChaogeGateBoardedLeaf";
            if (Meshes.TryGetValue(key, out var existing)) return (existing, Meshes[key + "_Fittings"]);
            var planks = new Geometry();
            var fittings = new Geometry();
            for (int i = 0; i < 11; i++)
            {
                float x = -4.52f + i * .904f;
                planks.Box(new Vector3(x, 0, 0), new Vector3(.893f, 8.5f - .05f * (i % 3), .39f),
                    Matrix4x4.identity, .055f);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float depth = side * .22f;
                foreach (float x in new[] { -4.62f, 4.62f })
                    CutBeam(planks, new Vector3(x, -4.24f, depth), new Vector3(x, 4.24f, depth), .42f, .15f, .06f);
                foreach (float y in new[] { -3.72f, -.48f, 3.7f })
                {
                    CutBeam(planks, new Vector3(-4.83f, y, depth), new Vector3(4.83f, y, depth), .40f, .15f, .06f);
                    for (int i = 0; i < 11; i++)
                        fittings.Box(new Vector3(-4.52f + i * .904f, y, side * .293f),
                            new Vector3(.12f, .12f, .025f), Matrix4x4.identity, .026f);
                }
                CutBeam(planks, new Vector3(-4.5f, -3.55f, depth), new Vector3(4.5f, 3.58f, depth), .28f, .14f, .05f);
                foreach (float y in new[] { -2.6f, 2.6f })
                    fittings.Box(new Vector3(-4.66f, y, side * .279f), new Vector3(.54f, .32f, .055f),
                        Matrix4x4.identity, .055f);
            }
            return (StructureMesh(key, planks), StructureMesh(key + "_Fittings", fittings));
        }

        private static void PlaceLocalModel(Transform parent, string source, string name, Material material,
            Vector3 center, Vector3 size, float yaw = 0, bool closeShadows = false)
        {
            var meshes = LoadBackdropMeshes(source);
            if (meshes == null) return;
            var current = parent.Find(name);
            var bounds = meshes[0].bounds;
            var scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            var rotation = Quaternion.Euler(0, yaw, 0);
            if (current != null)
            {
                current.localPosition = center;
                current.localRotation = rotation;
                current.localScale = scale;
                foreach (var renderer in current.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
                current.GetComponent<LODGroup>().RecalculateBounds();
                return;
            }
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            root.transform.localRotation = rotation;
            root.transform.localScale = scale;
            var group = root.AddComponent<LODGroup>();
            var levels = meshes.Select((mesh, i) =>
            {
                var detail = StructurePart(root.transform, "Relief LOD " + i, mesh, material, closeShadows && i == 0);
                detail.transform.localPosition = -bounds.center;
                return new LOD(i == 0 ? .16f : .008f, new Renderer[] { detail.GetComponent<MeshRenderer>() });
            }).ToArray();
            group.SetLODs(levels);
            group.RecalculateBounds();
        }
    }
}
