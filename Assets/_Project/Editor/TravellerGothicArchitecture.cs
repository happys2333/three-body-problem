using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Geometry = ThreeBodyProblem.Editor.MasonryArchitectureLibrary.Geometry;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private const float GothicEave = 17.8f;
        private const float GothicRidge = 5.1f;
        private const float NaveHalfWidth = 16.5f;
        private const float RoseCentre = 17.25f;
        private const float RoseRadius = 1.62f;
        private const int ArchSlices = 16;

        private static float GableHeight(float x) => GothicEave + GothicRidge * (1f - Mathf.Abs(x) / NaveHalfWidth);

        private static Transform ArchitectureAnchor(Transform parent, string name, Vector3 position,
            float yaw = 0)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            return anchor;
        }

        private static void UpgradeGothicHall(ILookup<string, Transform> objects)
        {
            var existingFront = objects["Gothic front gable"].SingleOrDefault();
            var masonry = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/Masonry.mat");
            if (masonry == null) throw new InvalidOperationException("Missing cathedral masonry material.");
            if (existingFront == null)
            {
                var roof = objects["Cathedral pitched roof"].Single();
                var root = ArchitectureAnchor(roof.parent, "Gothic nave shell", Vector3.zero);
                var glass = GothicGlassMaterial();
                var stone = ArchitectureMaterial("Stone");
                var lead = ArchitectureMaterial("Charcoal");
                foreach (var wall in objects["Stone wall lower"])
                {
                    float side = Mathf.Sign(wall.position.x);
                    float yaw = side * 90f;
                    var blank = ArchitectureAnchor(root, "West end masonry bay", new Vector3(side * 14f, 0, -21.6f), yaw);
                    var blankMesh = new Geometry();
                    blankMesh.Box(new Vector3(0, GableHeight(14) * .5f, 0), new Vector3(10.8f, GableHeight(14), 1.4f),
                        Matrix4x4.identity, 0);
                    SolidStructure(blank, "Masonry", StructureMesh("GothicNaveWestEnd", blankMesh), stone);
                    foreach (var pier in objects["Window pier"].Where(p => Mathf.Sign(p.position.x) == side))
                    {
                        var bay = ArchitectureAnchor(root, "Gothic lancet bay",
                            new Vector3(side * 14f, 0, pier.position.z + 5.8f), yaw);
                        BuildLancetBay(bay, "Nave", 12f, 1.4f, 6.1f, 2.34f, 10.55f, 1.05f,
                            _ => GableHeight(14), stone, masonry, glass, lead);
                    }
                }
                existingFront = ArchitectureAnchor(root, "Gothic front gable", new Vector3(0, 0, -27f));
                const float entryWidth = 4.9f, entrySpring = 8.7f, entryCrown = 1.45f;
                SolidStructure(existingFront, "Open portal and rose aperture", StructureMesh("GothicPortalMasonry",
                    OpeningWall(29.4f, 2f, 0, entryWidth, entrySpring, entryCrown, GableHeight, true)), stone);
                var portal = new Geometry();
                AppendArch(portal, 0, entryWidth, entrySpring, entryCrown, .48f, 2.26f);
                AppendArch(portal, 0, entryWidth + .46f, entrySpring, entryCrown, .27f, 2.56f);
                StructurePart(existingFront, "Recessed portal mouldings", StructureMesh("GothicPortalMoulding", portal), masonry);
                AddRoseWindow(existingFront, masonry, lead, glass);
                BuildOpenPortalDoors(existingFront, lead);

                var apse = ArchitectureAnchor(root, "Gothic chancel rear", new Vector3(0, 0, 43f));
                BuildLancetBay(apse, "Chancel", 29.4f, 1.5f, 5.65f, 3.2f, 12.7f, 1.12f,
                    GableHeight, stone, masonry, glass, lead);
                var chancel = new Geometry();
                foreach (float x in new[] { -11f, -6f, 6f, 11f })
                {
                    chancel.Box(new Vector3(x, 2.18f, -1.15f), new Vector3(1.12f, 4.35f, .72f),
                        Matrix4x4.identity, .12f);
                    chancel.Box(new Vector3(x, 4.35f, -1.16f), new Vector3(1.48f, .32f, 1.05f),
                        Matrix4x4.identity, .07f);
                }
                chancel.Box(new Vector3(0, 3.92f, -.9f), new Vector3(29f, .28f, .44f),
                    Matrix4x4.identity, .045f);
                StructurePart(apse, "Chancel masonry dado", StructureMesh("GothicChancelDado", chancel), masonry);
                UpgradeChancelSteps(objects, masonry);
                FinishBellTowers(objects, root, masonry);
                BuildNaveRoof(root);
                foreach (var old in objects["Pointed nave rib"])
                {
                    var support = ArchitectureAnchor(root, "Stone vault rib", new Vector3(0, 0, old.position.z));
                    var rib = new Geometry();
                    AppendArch(rib, 10.4f, 9.64f, 10.4f, 1.08f, .56f, .93f);
                    AppendArch(rib, 10.4f, 9.47f, 10.4f, 1.08f, .21f, .53f);
                    StructurePart(support, "Vault mouldings", StructureMesh("GothicVaultRib", rib), masonry);
                }
                foreach (string name in new[] { "Stone wall lower", "Stone wall upper", "Window pier",
                             "Stained glass pane", "Window mullion", "Stained-glass pointed frame", "Window sill",
                             "Front wall mass", "Great entry arch", "Entry lintel above arch", "Apse wall",
                             "Cathedral pitched roof", "Pointed nave rib" })
                    foreach (var old in objects[name]) Object.DestroyImmediate(old.gameObject);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                string name = side < 0 ? "West blind tracery" : "East blind tracery";
                var center = new Vector3(side * 9.24f, 10.2f, -1.15f);
                PlaceLocalModel(existingFront, "GothicGableRelief", name, masonry, center, new Vector3(2.42f, 3.88f, .47f), 180f);
                if (existingFront.Find(name) == null || existingFront.Find(name + " surround") != null) continue;
                var frame = new Geometry();
                AppendArch(frame, -2.14f, 1.33f, .42f, .36f, .21f, .42f);
                frame.Box(new Vector3(0, -2.23f, 0), new Vector3(3.12f, .19f, .51f), Matrix4x4.identity, .045f);
                var surround = StructurePart(existingFront, name + " surround", StructureMesh("GothicBlindTraceryBorder", frame), masonry);
                surround.transform.localPosition = center;
            }
            UpgradeNaveBraziers(objects);
        }

        private static void SolidStructure(Transform anchor, string name, Mesh mesh, Material material)
        {
            var part = StructurePart(anchor, name, mesh, material);
            GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            part.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void BuildOpenPortalDoors(Transform front, Material iron)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/Wood.mat");
            var boards = new Geometry();
            var hinges = new Geometry();
            const float leafWidth = 4.46f;
            const float leafHeight = 8.45f;
            for (int plank = 0; plank < 7; plank++)
                boards.Box(new Vector3((plank + .5f) * leafWidth / 7f, leafHeight * .5f + .05f, 0),
                    new Vector3(leafWidth / 7f - .015f, leafHeight, .25f), Matrix4x4.identity, .028f);
            foreach (float y in new[] { 1.07f, 4.21f, 7.35f })
            {
                CutBeam(boards, new Vector3(.04f, y, -.155f), new Vector3(leafWidth - .04f, y, -.155f), .30f, .13f);
                CutBeam(hinges, new Vector3(.025f, y, .149f), new Vector3(3.12f, y, .149f), .165f, .06f);
                for (int stud = 0; stud < 6; stud++)
                    hinges.Box(new Vector3(.13f + stud * .55f, y, .196f), new Vector3(.093f, .093f, .066f),
                        Matrix4x4.identity, .022f);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var leaf = new GameObject("Open oak portal leaf").transform;
                leaf.SetParent(front, false);
                // Hinge at the jamb. Open away from the exterior steps, into the hall.
                leaf.localPosition = new Vector3(side * 4.79f, 0, .73f);
                leaf.localRotation = Quaternion.Euler(0, -90f + side * 4f, 0);
                StructurePart(leaf, "Oak planks and rails", StructureMesh("GothicPortalOakLeaf", boards), wood);
                StructurePart(leaf, "Iron straps and pins", StructureMesh("GothicPortalHinges", hinges), iron);
                var collision = leaf.gameObject.AddComponent<BoxCollider>();
                collision.center = new Vector3(leafWidth * .5f, leafHeight * .5f, 0);
                collision.size = new Vector3(leafWidth, leafHeight, .38f);
            }
        }

        private static void UpgradeChancelSteps(ILookup<string, Transform> objects, Material trim)
        {
            foreach (var step in objects["Tribunal dais"])
            {
                var collider = step.GetComponent<BoxCollider>();
                if (collider == null) continue;
                Vector3 size = collider.size;
                var dressed = new Geometry();
                dressed.Box(Vector3.zero, size, Matrix4x4.identity, .042f);
                float y = size.y * .5f - .09f;
                dressed.Box(new Vector3(0, y, -size.z * .5f), new Vector3(size.x, .16f, .085f),
                    Matrix4x4.identity, .022f);
                step.GetComponent<MeshFilter>().sharedMesh = StructureMesh(
                    FormattableString.Invariant($"GothicChancelStep_{size.x:0.#}_{size.y:0.#}"), dressed);
                step.GetComponent<MeshRenderer>().sharedMaterial = trim;
            }
        }

        private static void FinishBellTowers(ILookup<string, Transform> objects, Transform hall, Material stone)
        {
            var slate = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/RoofTile.mat");
            foreach (string level in new[] { "Bell tower", "Bell tower upper", "Bell tower high roof" })
                foreach (var tower in objects[level])
                    foreach (var renderer in tower.GetComponentsInChildren<MeshRenderer>())
                    {
                        Material material = level == "Bell tower high roof" ? slate : stone;
                        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
            var piers = new Geometry();
            foreach (float x in new[] { -3.66f, 3.66f })
                foreach (float z in new[] { -4.15f, 4.15f })
                {
                    piers.Box(new Vector3(x, 6.05f, z), new Vector3(.82f, 10.45f, .78f),
                        Matrix4x4.identity, .12f);
                    piers.Box(new Vector3(x, 1.08f, z), new Vector3(1.31f, 2.16f, 1.22f),
                        Matrix4x4.identity, .10f);
                }
            var mesh = StructureMesh("GothicBellTowerPilasters", piers);
            foreach (float side in new[] { -1f, 1f })
            {
                var footing = ArchitectureAnchor(hall, "Bell tower stone piers", new Vector3(side * 18.5f, 0, -24f));
                StructurePart(footing, "Cut limestone corner piers", mesh, stone);
            }
        }

        private static void UpgradeNaveBraziers(ILookup<string, Transform> objects)
        {
            foreach (var stem in objects["Brazier stem"])
            {
                Vector3 foot = stem.position - Vector3.up * .65f;
                var anchor = ArchitectureAnchor(stem.parent, "Wrought iron brazier", foot);
                ReplaceVisual(anchor, ExpandedEnvironment + "MedievalIronBrazier.prefab", foot,
                    new Vector3(.92f, 1.43f, .92f), Quaternion.identity, 2);
                Object.DestroyImmediate(stem.gameObject);
            }
            foreach (var bowl in objects["Brazier bowl"]) Object.DestroyImmediate(bowl.gameObject);
        }

        private static Material GothicGlassMaterial()
        {
            string path = MatFolder + "/GothicGlass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Traveller/Gothic Glass"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static float ArchHeight(float x, float half, float spring, float centre)
        {
            float radius = half + centre;
            float run = Mathf.Abs(x) + centre;
            return spring + Mathf.Sqrt(Mathf.Max(0, radius * radius - run * run));
        }

        private static void BuildLancetBay(Transform anchor, string key, float width, float depth, float sill,
            float half, float spring, float centre, Func<float, float> wallHeight, Material stone, Material trim,
            Material glass, Material lead)
        {
            SolidStructure(anchor, "Masonry around open lancet", StructureMesh("Gothic" + key + "Masonry",
                OpeningWall(width, depth, sill, half, spring, centre, wallHeight)), stone);
            var frame = new Geometry();
            AppendArch(frame, sill, half, spring, centre, .24f, depth + .19f);
            frame.Box(new Vector3(0, sill - .12f, 0), new Vector3(half * 2f + .72f, .26f, depth + .43f),
                Matrix4x4.identity, .06f);
            StructurePart(anchor, "Stepped stone surround", StructureMesh("Gothic" + key + "Trim", frame), trim);
            PlaceLancetGlass(anchor, key, sill, half, spring, centre, glass, lead);
        }

        private static Geometry OpeningWall(float width, float depth, float sill, float half,
            float spring, float centre, Func<float, float> top, bool hasRose = false)
        {
            var geometry = new Geometry();
            var samples = new SortedSet<float> { -width * .5f, width * .5f };
            for (int i = 0; i <= ArchSlices; i++) samples.Add(Mathf.Lerp(-half, half, (float)i / ArchSlices));
            if (hasRose)
                for (int i = 0; i <= ArchSlices; i++) samples.Add(Mathf.Lerp(-RoseRadius, RoseRadius, (float)i / ArchSlices));
            var x = samples.ToArray();
            for (int i = 0; i + 1 < x.Length; i++)
            {
                float a = x[i], b = x[i + 1], middle = Mathf.Abs((a + b) * .5f);
                if (middle >= half)
                {
                    WallStrip(geometry, a, b, 0, 0, top(a), top(b), depth);
                    continue;
                }
                if (sill > 0) WallStrip(geometry, a, b, 0, 0, sill, sill, depth);
                float floorA = ArchHeight(a, half, spring, centre), floorB = ArchHeight(b, half, spring, centre);
                if (hasRose && middle < RoseRadius)
                {
                    float offsetA = Mathf.Sqrt(Mathf.Max(0, RoseRadius * RoseRadius - a * a));
                    float offsetB = Mathf.Sqrt(Mathf.Max(0, RoseRadius * RoseRadius - b * b));
                    WallStrip(geometry, a, b, floorA, floorB, RoseCentre - offsetA, RoseCentre - offsetB, depth);
                    WallStrip(geometry, a, b, RoseCentre + offsetA, RoseCentre + offsetB, top(a), top(b), depth);
                }
                else WallStrip(geometry, a, b, floorA, floorB, top(a), top(b), depth);
            }
            return geometry;
        }

        private static void WallStrip(Geometry mesh, float left, float right, float lowLeft, float lowRight,
            float highLeft, float highRight, float depth)
        {
            float face = depth * .5f;
            mesh.Prism(new[] { new Vector3(left, lowLeft, face), new Vector3(right, lowRight, face),
                new Vector3(right, highRight, face), new Vector3(left, highLeft, face) }, Vector3.forward, depth, 0, 0, 0);
        }

        private static void AppendArch(Geometry mesh, float sill, float half, float spring, float centre,
            float moulding, float depth)
        {
            foreach (float side in new[] { -1f, 1f })
                if (spring > sill)
                    mesh.Box(new Vector3(side * (half + moulding * .5f), (spring + sill) * .5f, 0),
                        new Vector3(moulding, spring - sill, depth), Matrix4x4.identity, .04f);
            float face = depth * .5f;
            for (int i = 0; i < ArchSlices; i++)
            {
                float a = Mathf.Lerp(-half, half, (float)i / ArchSlices);
                float b = Mathf.Lerp(-half, half, (float)(i + 1) / ArchSlices);
                Vector3 Inner(float x) => new Vector3(x, ArchHeight(x, half, spring, centre), face);
                Vector3 Outer(float x)
                {
                    float outerHalf = half + moulding;
                    x *= outerHalf / half;
                    return new Vector3(x, ArchHeight(x, outerHalf, spring, centre), face);
                }
                mesh.Prism(new[] { Inner(a), Inner(b), Outer(b), Outer(a) }, Vector3.forward, depth, 0, 0, 0);
            }
        }

        private static readonly Color[] GlassColours =
        {
            new(.065f,.19f,.52f,.88f), new(.65f,.33f,.08f,.85f), new(.44f,.075f,.12f,.88f),
            new(.07f,.24f,.17f,.85f), new(.58f,.47f,.26f,.77f)
        };

        private static void PlaceLancetGlass(Transform anchor, string key, float sill, float half,
            float spring, float centre, Material glass, Material lead)
        {
            const int rows = 10;
            var panes = new Geometry();
            var tracery = new Geometry();
            var colours = new List<Color>();
            for (int column = 0; column < ArchSlices; column++)
            {
                float a = Mathf.Lerp(-half, half, (float)column / ArchSlices);
                float b = Mathf.Lerp(-half, half, (float)(column + 1) / ArchSlices);
                float heightA = ArchHeight(a, half, spring, centre);
                float heightB = ArchHeight(b, half, spring, centre);
                int band = Mathf.Min(column, ArchSlices - 1 - column);
                Vector3 Row(float x, float top, int i) => new Vector3(x, Mathf.Lerp(sill, top, (float)i / rows), 0);
                for (int row = 0; row < rows; row++)
                {
                    Vector3 a0 = Row(a, heightA, row), b0 = Row(b, heightB, row);
                    panes.Face(new[] { a0, b0, Row(b, heightB, row + 1), Row(a, heightA, row + 1) }, Vector3.forward);
                    int lozenge = (Mathf.Abs(row - 4) + band) % 5;
                    int colourIndex = band == 0 ? 4 : band == 1 ? 2 : lozenge == 0 ? 1 : lozenge == 1 ? 3 : 0;
                    var colour = GlassColours[colourIndex];
                    colours.AddRange(Enumerable.Repeat(colour * new Color(1f - .055f * ((row + band) % 3), 1, 1, 1), 4));
                    if (row != 0) CutBeam(tracery, a0, b0, .034f, .041f, 0);
                }
                if (column % 2 == 0)
                    CutBeam(tracery, new Vector3(a, sill, 0), new Vector3(a, heightA, 0),
                        column % 4 == 0 ? .091f : .041f, .087f, .01f);
            }
            StructurePart(anchor, "Coloured leaded glass", SaveGlass("Gothic" + key + "Glass", panes, colours), glass, false);
            StructurePart(anchor, "Leaded window divisions", StructureMesh("Gothic" + key + "Lead", tracery), lead, false);
        }

        private static Mesh SaveGlass(string name, Geometry geometry, List<Color> colours)
        {
            var mesh = StructureMesh(name, geometry);
            mesh.SetColors(colours);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AddRoseWindow(Transform front, Material stone, Material lead, Material glass)
        {
            const int sectors = 24;
            var surrounds = new Geometry();
            var panes = new Geometry();
            var tracery = new Geometry();
            var colours = new List<Color>();
            var center = Vector3.up * RoseCentre;
            Vector3 Ring(float radius, int sector, float depth)
            {
                float angle = sector * Mathf.PI * 2f / sectors;
                return center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, depth);
            }
            for (int i = 0; i < sectors; i++)
            {
                surrounds.Prism(new[] { Ring(RoseRadius - .06f, i, 1.10f), Ring(RoseRadius - .06f, i + 1, 1.10f),
                    Ring(RoseRadius + .26f, i + 1, 1.10f), Ring(RoseRadius + .26f, i, 1.10f) },
                    Vector3.forward, 2.2f, 0, 0, 0);
                panes.Face(new[] { center, Ring(.55f, i, 0), Ring(.55f, i + 1, 0) }, Vector3.forward);
                colours.AddRange(Enumerable.Repeat(GlassColours[4], 3));
                panes.Face(new[] { Ring(.55f, i, 0), Ring(1.61f, i, 0), Ring(1.61f, i + 1, 0), Ring(.55f, i + 1, 0) },
                    Vector3.forward);
                colours.AddRange(Enumerable.Repeat(GlassColours[(i / 2) % 4], 4));
                CutBeam(tracery, Ring(.55f, i, 0), Ring(.55f, i + 1, 0), .065f, .065f, .008f);
                if (i % 2 == 0) CutBeam(tracery, center, Ring(1.6f, i, 0), .065f, .065f, .008f);
            }
            StructurePart(front, "Rose window stone ring", StructureMesh("GothicRoseStone", surrounds), stone);
            StructurePart(front, "Rose window lead spokes", StructureMesh("GothicRoseLead", tracery), lead, false);
            StructurePart(front, "Rose window coloured glass", SaveGlass("GothicRoseGlass", panes, colours), glass, false);
        }

        private static void BuildNaveRoof(Transform root)
        {
            var sheets = new Geometry();
            var timber = new Geometry();
            var anchor = ArchitectureAnchor(root, "Slate nave roof and timber trusses", new Vector3(0, GothicEave, 8f));
            const float run = 37.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                const int courses = 13;
                for (int row = 0; row < courses; row++)
                {
                    float a = (float)row / courses, b = (float)(row + 1) / courses;
                    float x0 = side * NaveHalfWidth * (1f - a), x1 = side * NaveHalfWidth * (1f - b);
                    float y0 = GothicRidge * a, y1 = GothicRidge * b;
                    sheets.Face(new[] { new Vector3(x0, y0 + .12f, -run), new Vector3(x0, y0 + .12f, run),
                        new Vector3(x1, y1, run), new Vector3(x1, y1, -run) }, Vector3.up);
                    sheets.Face(new[] { new Vector3(x0, y0, -run), new Vector3(x0, y0, run),
                        new Vector3(x0, y0 + .12f, run), new Vector3(x0, y0 + .12f, -run) }, Vector3.right * side);
                }
                sheets.Face(new[] { new Vector3(side * NaveHalfWidth, -.19f, -run),
                    new Vector3(side * NaveHalfWidth, -.19f, run), new Vector3(0, GothicRidge - .19f, run),
                    new Vector3(0, GothicRidge - .19f, -run) }, Vector3.down);
                CutBeam(timber, new Vector3(side * NaveHalfWidth, -.22f, -run),
                    new Vector3(side * NaveHalfWidth, -.22f, run), .36f, .28f);
            }
            for (int i = 0; i < 6; i++)
            {
                float z = -32f + i * 12f;
                CutBeam(timber, new Vector3(-14.9f, -.63f, z), new Vector3(14.9f, -.63f, z), .35f, .42f);
                foreach (float side in new[] { -1f, 1f })
                    CutBeam(timber, new Vector3(side * 15.9f, -.30f, z), new Vector3(0, GothicRidge - .27f, z), .42f, .45f);
                CutBeam(timber, new Vector3(0, -.62f, z), new Vector3(0, GothicRidge - .32f, z), .29f, .35f);
            }
            var slate = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/RoofTile.mat");
            var oak = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/EnvironmentReady/Materials/Wood.mat");
            SolidStructure(anchor, "Lapped roof slates", StructureMesh("GothicSlateRoof", sheets), slate);
            StructurePart(anchor, "Timber rafters and king posts", StructureMesh("GothicNaveRoofFrame", timber), oak);
        }
    }
}
