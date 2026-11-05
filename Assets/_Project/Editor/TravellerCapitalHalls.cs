using System.Linq;
using UnityEngine;
using Geometry = ThreeBodyProblem.Editor.MasonryArchitectureLibrary.Geometry;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static void UpgradeCapitalHalls(ILookup<string, Transform> objects, Material timber)
        {
            foreach (var hall in objects["Ancestral temple hall"])
                BuildPostAndBeamHall(hall, new Vector3(53, 8, 24), 0, false, timber);
            foreach (var hall in objects["Ancestral precinct side hall"])
                BuildPostAndBeamHall(hall, new Vector3(38, 5, 12), hall.position.x < -107 ? -90 : 90, false, timber);
            foreach (var hall in objects["Pool shelter"])
                BuildPostAndBeamHall(hall, new Vector3(30, 4.5f, 12), 0, true, timber);
        }

        private static void BuildPostAndBeamHall(Transform hall, Vector3 size, float yaw, bool open, Material timber)
        {
            if (hall.Find("Post and beam hall") != null) return;
            var original = hall.Find("Detailed timber earth and reed structure");
            var detail = new GameObject("Post and beam hall").transform;
            detail.SetParent(hall, false);
            detail.localRotation = Quaternion.Euler(0, yaw, 0);
            string key = open ? "ShangBathingPavilion" : size.x > 40 ? "ShangAncestralHall" : "ShangAncestralSideHall";
            float width = size.x, height = size.y, depth = size.z;
            const float floor = .28f;
            var paving = ArchitectureMaterial("CapitalPaving");
            var reed = ArchitectureMaterial(open ? "CapitalThatch" : "CapitalDarkThatch");

            var platform = new Geometry();
            platform.Box(new Vector3(0, floor * .5f, 0), new Vector3(width + 1f, floor, depth + .86f),
                Matrix4x4.identity, .075f);
            var basePart = StructurePart(detail, "Dressed stone sill platform", StructureMesh(key + "_Sill", platform), paving);
            basePart.AddComponent<MeshCollider>().sharedMesh = basePart.GetComponent<MeshFilter>().sharedMesh;

            var carpentry = new Geometry();
            var silhouette = new Geometry();
            var plinths = new Geometry();
            int bays = Mathf.CeilToInt(width / 7.8f);
            float support = height + .22f - floor;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * (depth * .5f - .3f);
                for (int bay = 0; bay <= bays; bay++)
                {
                    float x = Mathf.Lerp(-width * .5f + .38f, width * .5f - .38f, (float)bay / bays);
                    plinths.Box(new Vector3(x, floor + .16f, z), new Vector3(1.06f, .32f, 1.06f), Matrix4x4.identity, .11f);
                    var center = new Vector3(x, floor + support * .5f + .08f, z);
                    carpentry.Box(center, new Vector3(.59f, support, .59f), Matrix4x4.identity, .105f);
                    silhouette.Box(center, new Vector3(.64f, support, .64f), Matrix4x4.identity, 0);
                    foreach (float direction in new[] { -1f, 1f })
                    {
                        if (bay == 0 && direction < 0 || bay == bays && direction > 0) continue;
                        CutBeam(carpentry, new Vector3(x, height - 1.03f, z),
                            new Vector3(x + direction * 1.3f, height + .22f, z), .35f, .39f, .045f);
                    }
                }
                carpentry.Box(new Vector3(0, height + .21f, z), new Vector3(width + .53f, .54f, .60f), Matrix4x4.identity, .06f);
                silhouette.Box(new Vector3(0, height + .21f, z), new Vector3(width + .53f, .54f, .60f), Matrix4x4.identity, 0);
            }
            for (int bay = 0; bay <= bays; bay++)
            {
                float x = Mathf.Lerp(-width * .5f + .38f, width * .5f - .38f, (float)bay / bays);
                carpentry.Box(new Vector3(x, height + .17f, 0), new Vector3(.38f, .46f, depth + .46f), Matrix4x4.identity, .044f);
            }
            if (!open) BuildHallEnclosure(detail, key, size, floor, carpentry);
            var posts = StructurePart(detail, "Joined timber colonnade", StructureMesh(key + "_Posts", carpentry), timber);
            posts.AddComponent<MeshCollider>().sharedMesh = posts.GetComponent<MeshFilter>().sharedMesh;
            StructurePart(detail, "Cut foot stones", StructureMesh(key + "_Footstones", plinths), paving);
            var (roof, frame) = GateRoofMeshes(new Vector3(width + 2.5f, depth * .25f, depth + 2.5f));
            var eaves = Vector3.up * (height + .55f);
            StructurePart(detail, "Lapped reed roof", roof, reed).transform.localPosition = eaves;
            StructurePart(detail, "Exposed rafters", frame, timber).transform.localPosition = eaves;

            var group = hall.GetComponent<LODGroup>();
            var levels = group.GetLODs();
            levels[0].renderers = detail.GetComponentsInChildren<Renderer>();
            if (open)
            {
                Object.DestroyImmediate(hall.Find("Distant mass").gameObject);
                var distant = new GameObject("Open pavilion distant silhouette").transform;
                distant.SetParent(hall, false);
                StructurePart(distant, "Stone sill silhouette", basePart.GetComponent<MeshFilter>().sharedMesh, paving, false);
                StructurePart(distant, "Open timber frames", StructureMesh(key + "_Far", silhouette), timber, false);
                StructurePart(distant, "Reed roof silhouette", roof, reed, false).transform.localPosition = eaves;
                levels[levels.Length - 1].renderers = distant.GetComponentsInChildren<Renderer>();
            }
            group.SetLODs(levels);
            group.RecalculateBounds();
            foreach (var former in hall.GetComponents<BoxCollider>()) Object.DestroyImmediate(former);
            Object.DestroyImmediate(original.gameObject);
        }

        private static void BuildHallEnclosure(Transform hall, string key, Vector3 size, float floor, Geometry carpentry)
        {
            float width = size.x, height = size.y, depth = size.z;
            float front = -depth * .29f, back = depth * .5f - .58f;
            float door = Mathf.Clamp(width * .12f, 2.8f, 5.4f), lintel = height * .69f;
            var earth = new Geometry();
            void Infill(float x0, float x1, float y0, float y1)
            {
                earth.Box(new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, front),
                    new Vector3(x1 - x0, y1 - y0, .64f), Matrix4x4.identity, .055f);
            }
            Infill(-door * .5f, door * .5f, lintel, height);
            foreach (float side in new[] { -1f, 1f })
            {
                float mid = width * .28f, opening = width * .074f;
                float lower = height * .37f, upper = height * .67f;
                float left = side < 0 ? -width * .5f + .6f : door * .5f;
                float right = side < 0 ? -door * .5f : width * .5f - .6f;
                Infill(left, side * mid - opening, floor, height);
                Infill(side * mid + opening, right, floor, height);
                Infill(side * mid - opening, side * mid + opening, floor, lower);
                Infill(side * mid - opening, side * mid + opening, upper, height);
                for (int slat = -3; slat <= 3; slat++)
                    carpentry.Box(new Vector3(side * mid + slat * opening / 3.2f, (lower + upper) * .5f, front - .23f),
                        new Vector3(.16f, upper - lower + .15f, .20f), Matrix4x4.identity, .026f);
                carpentry.Box(new Vector3(side * door * .5f, (lintel + floor) * .5f, front - .06f),
                    new Vector3(.39f, lintel - floor, .91f), Matrix4x4.identity, .056f);
                earth.Box(new Vector3(side * (width * .5f - .61f), (floor + height) * .5f, (front + back) * .5f),
                    new Vector3(.59f, height - floor, back - front + .56f), Matrix4x4.identity, .061f);
            }
            earth.Box(new Vector3(0, (floor + height) * .5f, back), new Vector3(width - 1f, height - floor, .64f),
                Matrix4x4.identity, .065f);
            var walls = StructurePart(hall, "Plastered walls with real openings", StructureMesh(key + "_Walls", earth),
                ArchitectureMaterial("CapitalPlaster"));
            walls.AddComponent<MeshCollider>().sharedMesh = walls.GetComponent<MeshFilter>().sharedMesh;
        }
    }
}
