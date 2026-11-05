using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Geometry = ThreeBodyProblem.Editor.MasonryArchitectureLibrary.Geometry;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static void UpgradeCapitalStructures(ILookup<string, Transform> objects)
        {
            var stone = AssetDatabase.LoadAssetAtPath<Material>(MasonryArchitectureLibrary.Root + "/Materials/Dressed sandstone blocks.mat");
            if (stone == null) throw new InvalidOperationException("Missing sandstone for the capital structures.");
            var timber = ArchitectureMaterial("CapitalTimber");
            var bronze = ArchitectureMaterial("CapitalBronze");
            foreach (var frame in objects["Great pendulum frame"])
            {
                UpgradePendulumPier(frame.Find("Left pier"), stone);
                UpgradePendulumPier(frame.Find("Right pier"), stone);
                UpgradePendulumLintel(frame, timber, bronze);
            }

            foreach (var terrace in objects["Ancestral hall terrace"])
                UpgradeAncestralTerrace(terrace, false);
            foreach (var altar in objects["Ritual altar"])
                UpgradeAncestralTerrace(altar, true);
            foreach (var distantWall in objects["Distant earth walls"])
                if (distantWall.parent.parent.name != "Pool shelter") UpgradeDistantCapitalWall(distantWall, timber);
            UpgradeCapitalHalls(objects, timber);
            foreach (var cover in objects["Movable stone skylight cover"])
                UpgradeSkylightCover(cover, stone, bronze);
        }

        private static void UpgradePendulumPier(Transform pier, Material stone)
        {
            if (pier.Find("Dressed stone courses") != null) return;
            var original = pier.GetComponent<BoxCollider>();
            Vector3 size = original.size;
            float bottom = -size.y * .5f;
            var footing = new Geometry();
            footing.Box(new Vector3(0, bottom + .37f, 0), new Vector3(5.5f, .74f, 6.1f), Matrix4x4.identity, .16f);
            footing.Box(new Vector3(0, bottom + 1.05f, 0), new Vector3(4.55f, .62f, 5.2f), Matrix4x4.identity, .12f);
            footing.Box(new Vector3(0, bottom + 1.64f, 0), new Vector3(3.85f, .56f, 4.55f), Matrix4x4.identity, .09f);
            footing.Box(new Vector3(0, size.y * .5f - .48f, 0), new Vector3(4.0f, .96f, 4.72f), Matrix4x4.identity, .14f);
            var courses = new Geometry();
            float start = bottom + 1.92f;
            float end = size.y * .5f - .96f;
            const int count = 25;
            float height = (end - start) / count;
            courses.Box(Vector3.up * ((start + end) * .5f),
                new Vector3(size.x - .16f, end - start, size.z - .16f), Matrix4x4.identity, .05f);
            for (int row = 0; row < count; row++)
            {
                float y = start + height * (row + .5f);
                float seam = (row % 2 == 0 ? -.26f : .32f) * size.x;
                courses.Box(new Vector3((seam - size.x * .5f) * .5f, y, 0),
                    new Vector3(size.x * .5f + seam - .027f, height - .022f, size.z), Matrix4x4.identity, .065f);
                courses.Box(new Vector3((seam + size.x * .5f) * .5f, y, 0),
                    new Vector3(size.x * .5f - seam - .027f, height - .022f, size.z), Matrix4x4.identity, .065f);
            }
            var casing = new Geometry();
            casing.Box(Vector3.up * ((start + end) * .5f),
                new Vector3(size.x, end - start, size.z), Matrix4x4.identity, .12f);
            var plinth = StructurePart(pier, "Stone plinth and cap", StructureMesh("ShangPendulumFooting", footing), stone);
            plinth.AddComponent<MeshCollider>().sharedMesh = plinth.GetComponent<MeshFilter>().sharedMesh;
            var near = StructurePart(pier, "Dressed stone courses", StructureMesh("ShangPendulumPierNear", courses), stone);
            var far = StructurePart(pier, "Stone pier silhouette", StructureMesh("ShangPendulumPierFar", casing), stone, false);
            var group = pier.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(.32f, new Renderer[] { near.GetComponent<MeshRenderer>() }),
                new LOD(.006f, new Renderer[] { far.GetComponent<MeshRenderer>() })
            });
            group.RecalculateBounds();
            StripOriginalVisual(pier);
        }

        private static void UpgradePendulumLintel(Transform frame, Material timber, Material bronze)
        {
            var head = frame.Find("Lintel");
            var pivot = frame.Find("Pendulum pivot");
            float axleHeight = pivot.localPosition.y - head.localPosition.y;
            if (head.Find("Joined timber crossbeam") == null)
            {
                var beams = new Geometry();
                var fittings = new Geometry();
                float width = head.GetComponent<BoxCollider>().size.x;
                float pier = frame.Find("Right pier").localPosition.x;
                foreach (float z in new[] { -1.65f, 1.65f })
                {
                    beams.Box(new Vector3(0, -.76f, z), new Vector3(width, 1.36f, 1.74f), Matrix4x4.identity, .11f);
                    beams.Box(new Vector3(0, .65f, z), new Vector3(width - .42f, 1.48f, 1.67f), Matrix4x4.identity, .10f);
                    foreach (float side in new[] { -1f, 1f })
                        CutBeam(beams, new Vector3(side * pier, -5.15f, z), new Vector3(side * (pier - 5.0f), -.96f, z),
                            .94f, 1.12f, .085f);
                }
                foreach (float x in new[] { -pier, -3.55f, 0f, 3.55f, pier })
                    beams.Box(new Vector3(x, .98f, 0), new Vector3(.86f, .81f, 5.38f), Matrix4x4.identity, .075f);
                foreach (float x in new[] { -pier, -3.55f, 3.55f, pier })
                    foreach (float z in new[] { -2.53f, 2.53f })
                        fittings.Box(new Vector3(x, .02f, z), new Vector3(.53f, 2.97f, .19f), Matrix4x4.identity, .05f);
                CutBeam(fittings, new Vector3(0, axleHeight, -2.82f), new Vector3(0, axleHeight, 2.82f), .68f, .68f, .15f);
                foreach (float z in new[] { -2.63f, 2.63f })
                    fittings.Box(new Vector3(0, axleHeight, z), new Vector3(2.62f, 2.35f, .35f), Matrix4x4.identity, .13f);
                StructurePart(head, "Joined timber crossbeam", StructureMesh("ShangPendulumHead", beams), timber);
                StructurePart(head, "Axle and bronze straps", StructureMesh("ShangPendulumStraps", fittings), bronze);
                StripOriginalVisual(head);
            }
            foreach (float side in new[] { -1f, 1f })
                PlaceLocalModel(head, "ShangPendulumBearing", side < 0 ? "South bronze bearing" : "North bronze bearing",
                    bronze, new Vector3(0, axleHeight, side * 2.84f), new Vector3(2.52f, 2.29f, .54f), side < 0 ? 180f : 0f);
        }

        private static void UpgradeAncestralTerrace(Transform terrace, bool altar)
        {
            var box = terrace.GetComponent<BoxCollider>();
            float top = box != null ? box.size.y * .5f : (altar ? .9f : 1.5f);
            if (terrace.Find("Rammed earth platform") == null && box != null)
            {
                var size = box.size;
                var platform = new Geometry();
                var steps = new Geometry();
                float bottom = -top;
                var heights = new[] { .17f, .66f, .17f };
                for (int course = 0; course < heights.Length; course++)
                {
                    float height = size.y * heights[course];
                    float inset = .28f * course;
                    platform.Box(new Vector3(0, bottom + height * .5f, 0),
                        new Vector3(size.x - inset * 2, height, size.z - inset * 2), Matrix4x4.identity, .07f);
                    bottom += height;
                }
                int risers = Mathf.CeilToInt(size.y / .24f);
                float rise = size.y / risers;
                float run = altar ? .44f : .56f;
                float width = altar ? 4.8f : 8.3f;
                for (int i = 0; i < risers; i++)
                {
                    float tread = rise * (i + 1);
                    float z = -size.z * .5f - (risers - i - .5f) * run + .55f;
                    steps.Box(new Vector3(0, -top + tread * .5f, z),
                        new Vector3(width, tread, run + .012f), Matrix4x4.identity, .026f);
                }
                var deck = StructurePart(terrace, "Rammed earth platform",
                    StructureMesh(altar ? "ShangAltarTerrace" : "ShangHallTerrace", platform), ArchitectureMaterial("CapitalEarth"));
                deck.AddComponent<MeshCollider>().sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
                var stair = StructurePart(terrace, "Dressed stone stair",
                    StructureMesh(altar ? "ShangAltarStair" : "ShangHallStair", steps), ArchitectureMaterial("CapitalPaving"));
                stair.AddComponent<MeshCollider>().sharedMesh = stair.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(box);
                StripOriginalVisual(terrace);
            }
            if (!altar) return;
            const string offering = "Carved stone offering table";
            PlaceLocalModel(terrace, "ShangAltarStone", offering, ArchitectureMaterial("CapitalPaving"),
                new Vector3(0, top + .56f, 2.75f), new Vector3(3.55f, 1.12f, 1.95f), 180f, true);
            var table = terrace.Find(offering);
            if (table != null && table.GetComponent<BoxCollider>() == null)
                table.gameObject.AddComponent<BoxCollider>().size = table.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size;
        }

        private static void UpgradeDistantCapitalWall(Transform wall, Material timber)
        {
            var filter = wall.GetComponent<MeshFilter>();
            if (!filter.sharedMesh.name.StartsWith("Box_", StringComparison.Ordinal)) return;
            Vector3 size = filter.sharedMesh.bounds.size;
            string key = FormattableString.Invariant($"ShangDistantEarth_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}");
            float width = Mathf.Min(size.x * .22f, 3f);
            float lintel = size.y * .28f;
            var shell = new Geometry();
            shell.Box(new Vector3(0, -size.y * .5f - .25f, 0), new Vector3(size.x + .65f, .5f, size.z + .65f),
                Matrix4x4.identity, .095f);
            shell.Box(new Vector3(0, size.y * .36f, 0), new Vector3(size.x, lintel, size.z),
                Matrix4x4.identity, .065f);
            float sideWidth = (size.x - width) * .5f;
            foreach (float side in new[] { -1f, 1f })
                shell.Box(new Vector3(side * (width + sideWidth) * .5f, -lintel * .5f, 0),
                    new Vector3(sideWidth, size.y - lintel, size.z), Matrix4x4.identity, .08f);
            shell.Box(new Vector3(0, -lintel * .5f, .20f), new Vector3(width, size.y - lintel, size.z - .40f),
                Matrix4x4.identity, .04f);
            filter.sharedMesh = StructureMesh(key, shell);
            var trim = new Geometry();
            trim.Box(new Vector3(0, -lintel * .5f, -size.z * .5f + .385f),
                new Vector3(width * .96f, size.y - lintel, .075f), Matrix4x4.identity, .012f);
            StructurePart(wall, "Recessed timber door", StructureMesh(key + "_Door", trim), timber, false);
            var group = wall.parent.parent.GetComponent<LODGroup>();
            var levels = group.GetLODs();
            levels[levels.Length - 1].renderers = wall.parent.GetComponentsInChildren<Renderer>();
            group.SetLODs(levels);
            group.RecalculateBounds();
        }

        private static void UpgradeSkylightCover(Transform cover, Material stone, Material bronze)
        {
            if (cover.Find("Carved stone shutter") != null) return;
            var slab = new Geometry();
            slab.Box(new Vector3(0, -.055f, 0), new Vector3(4.65f, .61f, 4.65f), Matrix4x4.identity, .13f);
            slab.Box(new Vector3(0, .293f, 0), new Vector3(4.26f, .135f, 4.25f), Matrix4x4.identity, .045f);
            var shoes = new Geometry();
            foreach (float z in new[] { -1.91f, 1.91f })
                shoes.Box(new Vector3(0, -.295f, z), new Vector3(4.32f, .125f, .32f), Matrix4x4.identity, .035f);
            var plate = StructurePart(cover, "Carved stone shutter", StructureMesh("ShangSkylightSlab", slab), stone);
            var bearings = StructurePart(cover, "Bronze sliding shoes", StructureMesh("ShangSkylightShoes", shoes), bronze);
            GameObjectUtility.SetStaticEditorFlags(plate, 0);
            GameObjectUtility.SetStaticEditorFlags(bearings, 0);
            StripOriginalVisual(cover);
        }
    }
}
