using System.Linq;
using UnityEngine;
using Geometry = ThreeBodyProblem.Editor.MasonryArchitectureLibrary.Geometry;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static void UpgradeCapitalStreetDetails(ILookup<string, Transform> objects)
        {
            var paving = ArchitectureMaterial("CapitalPaving");
            var timber = ArchitectureMaterial("CapitalTimber");
            foreach (string edge in new[] { "Pool timber edging", "Pool north edge", "Pool south edge" })
                foreach (var segment in objects[edge]) UpgradePoolCoping(segment, timber, paving);
            foreach (var basin in objects["Revival basin bed"])
                BuildPoolSteps(basin, timber);

            foreach (var drain in objects["Avenue drainage run"])
                UpgradeAvenueDrain(drain, paving);
            UpgradeProcessionalStandards(objects);
            UpgradeCapitalBraziers(objects);
            foreach (var deck in objects["Continuous walkable deck"])
                UpgradeCanalBridgeDeck(deck);
        }

        private static void UpgradePoolCoping(Transform edge, Material timber, Material paving)
        {
            if (edge.Find("Jointed pool coping") != null) return;
            var size = edge.GetComponent<BoxCollider>().size;
            bool alongZ = size.z > size.x;
            float run = alongZ ? size.z : size.x;
            float width = alongZ ? size.x : size.z;
            var basis = Matrix4x4.Rotate(Quaternion.Euler(0, alongZ ? -90f : 0, 0));
            var planks = new Geometry();
            var footing = new Geometry();
            int pieces = Mathf.CeilToInt(run / 3.7f);
            float length = run / pieces;
            for (int i = 0; i < pieces; i++)
            {
                float x = -run * .5f + (i + .5f) * length;
                planks.Box(new Vector3(x, .039f, 0), new Vector3(length - .024f, .32f, width), basis, .048f);
                footing.Box(new Vector3(x, -.13f, 0), new Vector3(length - .025f, .14f, width + .11f), basis, .03f);
                if (i + 1 < pieces)
                    planks.Box(new Vector3(x + length * .5f, .16f, 0),
                        new Vector3(.27f, .082f, width * .96f), basis, .018f);
            }
            string key = alongZ ? "ShangPoolSide" : "ShangPoolEnd";
            StructurePart(edge, "Jointed pool coping", StructureMesh(key + "_Wood", planks), timber);
            StructurePart(edge, "Foot stones", StructureMesh(key + "_Stone", footing), paving);
            StripOriginalVisual(edge);
        }

        private static void BuildPoolSteps(Transform basin, Material timber)
        {
            if (basin.Find("Timber bathing steps") != null) return;
            var mesh = new Geometry();
            const float width = 2.9f, span = .58f;
            for (int i = 0; i < 2; i++)
            {
                float rise = .20f * (i + 1);
                for (int board = 0; board < 5; board++)
                    mesh.Box(new Vector3(i * span, rise * .5f, (board - 2) * width / 5),
                        new Vector3(span, rise, width / 5 - .014f), Matrix4x4.identity, .032f);
            }
            var steps = StructurePart(basin, "Timber bathing steps", StructureMesh("ShangPoolAccessStep", mesh), timber);
            steps.transform.localPosition = new Vector3(-19.31f, .15f, 9.25f);
            var shape = steps.AddComponent<MeshCollider>();
            shape.sharedMesh = steps.GetComponent<MeshFilter>().sharedMesh;
        }

        private static void UpgradeAvenueDrain(Transform drain, Material paving)
        {
            var filter = drain.GetComponent<MeshFilter>();
            if (filter.sharedMesh.name == "ShangAvenueStoneChannel") return;
            const float run = 12.5f;
            var channel = new Geometry();
            channel.Box(new Vector3(0, -.038f, 0), new Vector3(.66f, .075f, run),
                Matrix4x4.identity, .015f);
            const int courses = 6;
            float length = run / courses;
            for (int i = 0; i < courses; i++)
                foreach (float side in new[] { -1f, 1f })
                    channel.Box(new Vector3(side * .248f, .028f, -run * .5f + length * (i + .5f)),
                        new Vector3(.23f, .156f, length - .025f), Matrix4x4.identity, .035f);
            filter.sharedMesh = StructureMesh("ShangAvenueStoneChannel", channel);
            drain.GetComponent<MeshRenderer>().sharedMaterial = paving;
        }

        private static void UpgradeProcessionalStandards(ILookup<string, Transform> objects)
        {
            if (!objects["Processional standard pole"].Any()) return;
            var pole = new Geometry();
            pole.Box(Vector3.zero, new Vector3(.86f, 2f, .86f), Matrix4x4.Rotate(Quaternion.Euler(0, 12, 0)), .13f);
            pole.Box(new Vector3(0, -.94f, 0), new Vector3(1.38f, .12f, 1.38f), Matrix4x4.identity, .04f);
            pole.Box(new Vector3(0, .96f, 0), new Vector3(1.17f, .078f, 1.17f), Matrix4x4.identity, .027f);
            var poleMesh = StructureMesh("ShangCarvedStandardPole", pole);
            foreach (var anchor in objects["Processional standard pole"])
                anchor.GetComponent<MeshFilter>().sharedMesh = poleMesh;

            var arm = new Geometry();
            arm.Box(new Vector3(.655f, 0, 0), new Vector3(1.83f, .135f, .152f), Matrix4x4.identity, .038f);
            foreach (float x in new[] { .29f, .83f, 1.36f })
                arm.Box(new Vector3(x, -.097f, 0), new Vector3(.075f, .20f, .115f), Matrix4x4.identity, .021f);
            var armMesh = StructureMesh("ShangWovenStandardClasp", arm);
            foreach (var anchor in objects["Standard bronze clasp"])
                anchor.GetComponent<MeshFilter>().sharedMesh = armMesh;

            var fabric = new Geometry();
            const int columns = 9, rows = 6;
            Vector3 Cloth(int col, int row)
            {
                float u = (float)col / columns, v = (float)row / rows;
                float bottom = .17f * (1 - v) * (1 - Mathf.Abs(2 * u - 1));
                float depth = Mathf.Sin(u * 3f * Mathf.PI + v * .95f) * (.084f - v * .06f);
                return new Vector3((u - .5f) * 1.6f, -1.6f + 3.32f * v + bottom, depth);
            }
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    var corners = new[] { Cloth(col, row), Cloth(col + 1, row), Cloth(col + 1, row + 1), Cloth(col, row + 1) };
                    fabric.Face(corners, Vector3.forward);
                    fabric.Face(corners.Select(p => p + Vector3.back * .003f).ToArray(), Vector3.back);
                }
            var fabricMesh = StructureMesh("ShangWovenStandardCloth", fabric);
            foreach (var anchor in objects["Woven processional standard"])
                anchor.GetComponent<MeshFilter>().sharedMesh = fabricMesh;
        }

        private static void UpgradeCapitalBraziers(ILookup<string, Transform> objects)
        {
            foreach (var stem in objects["Brazier stem"])
            {
                var foot = stem.position - Vector3.up * .65f;
                var anchor = ArchitectureAnchor(stem.parent, "Cast Shang brazier", foot);
                ReplaceVisual(anchor, ExpandedEnvironment + "ShangBronzeBrazier.prefab", foot,
                    new Vector3(.93f, 1.43f, .93f), Quaternion.identity, 2);
                Object.DestroyImmediate(stem.gameObject);
            }
            foreach (var bowl in objects["Brazier bowl"]) Object.DestroyImmediate(bowl.gameObject);
        }

        private static void UpgradeCanalBridgeDeck(Transform deck)
        {
            if (deck.Find("Fitted bridge boards") != null) return;
            var size = deck.localScale;
            var boards = new Geometry();
            const int count = 18;
            for (int i = 0; i < count; i++)
                boards.Box(new Vector3(-size.x * .5f + size.x * (i + .5f) / count, .006f, 0),
                    new Vector3(size.x / count - .008f, size.y, size.z), Matrix4x4.identity, .011f);
            var skin = StructurePart(deck, "Fitted bridge boards", StructureMesh("ShangCanalBridgeBoards", boards),
                deck.GetComponent<MeshRenderer>().sharedMaterial);
            skin.transform.localScale = new Vector3(1 / size.x, 1 / size.y, 1 / size.z);
            StripOriginalVisual(deck);
        }
    }
}
