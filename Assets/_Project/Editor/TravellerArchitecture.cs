using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static GameObject Cone(string name, Vector3 position, float radius, float topRadius, float height, string material, Transform parent, int sides = 12)
        {
            string key = "Cone_" + radius + "_" + topRadius + "_" + height + "_" + sides;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                vertices.Add(new Vector3(Mathf.Cos(angle) * topRadius, height, Mathf.Sin(angle) * topRadius));
                uv.Add(new Vector2((float)i / sides, 0f)); uv.Add(new Vector2((float)i / sides, 1f));
                if (i < sides) { int k = i * 2; triangles.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 }); }
            }
            return MeshObject(name, SaveMesh(key, vertices, triangles, uv), position, material, parent);
        }

        private static GameObject Roof(string name, Vector3 position, float width, float depth, float height, string material, Transform parent = null, bool curved = true)
        {
            string key = "Roof_" + width + "_" + depth + "_" + height + "_" + curved;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            int slices = curved ? 16 : 2;
            for (int row = 0; row <= slices; row++)
            {
                float z = row * 2f / slices - 1f;
                float y = height * (1f - Mathf.Pow(Mathf.Abs(z), curved ? 0.65f : 1f)) + (curved ? 0.65f * Mathf.Pow(Mathf.Abs(z), 12f) : 0f);
                vertices.Add(new Vector3(-width * 0.5f, y, z * depth * 0.5f));
                vertices.Add(new Vector3(width * 0.5f, y, z * depth * 0.5f));
                uv.Add(new Vector2(0f, row)); uv.Add(new Vector2(width / 3f, row));
                if (row < slices) { int k = row * 2; triangles.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            }
            int vertexCount = vertices.Count;
            vertices.AddRange(vertices.ToArray()); uv.AddRange(uv.ToArray());
            var frontTriangles = triangles.ToArray();
            for (int i = 0; i < frontTriangles.Length; i += 3)
                triangles.AddRange(new[] { frontTriangles[i] + vertexCount, frontTriangles[i + 2] + vertexCount, frontTriangles[i + 1] + vertexCount });
            var roof = MeshObject(name, SaveMesh(key, vertices, triangles, uv), position, material, parent);
            Beam("Ridge cap", position + new Vector3(-width * 0.5f, height, 0f), position + new Vector3(width * 0.5f, height, 0f), 0.13f, "Bronze", roof.transform);
            return roof;
        }

        private static GameObject Arch(string name, Vector3 position, float opening, float rise, float thickness, float depth, string material, Transform parent = null)
        {
            string key = "Arch_" + opening + "_" + rise + "_" + thickness + "_" + depth;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            const int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float x = i * 2f / segments - 1f;
                float curve = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((Mathf.Abs(x) + 1f) * 0.5f, 2f))) / 0.8660254f;
                vertices.Add(new Vector3(x * opening * 0.5f, curve * rise, -depth * 0.5f));
                vertices.Add(new Vector3(x * (opening * 0.5f + thickness), curve * (rise + thickness), -depth * 0.5f));
                vertices.Add(new Vector3(x * opening * 0.5f, curve * rise, depth * 0.5f));
                vertices.Add(new Vector3(x * (opening * 0.5f + thickness), curve * (rise + thickness), depth * 0.5f));
                for (int j = 0; j < 4; j++) uv.Add(new Vector2(i * 0.35f, j * 0.5f));
                if (i == segments) continue;
                int k = i * 4;
                foreach (var face in new[] { new[] { 0, 1 }, new[] { 3, 2 }, new[] { 2, 0 }, new[] { 1, 3 } })
                    triangles.AddRange(new[] { k + face[0], k + face[1], k + 4 + face[0], k + 4 + face[0], k + face[1], k + 4 + face[1] });
            }
            return MeshObject(name, SaveMesh(key, vertices, triangles, uv), position, material, parent);
        }

        private static GameObject Beam(string name, Vector3 from, Vector3 to, float width, string material, Transform parent = null)
        {
            Vector3 delta = to - from;
            var value = Shape(name, PrimitiveType.Cylinder, (from + to) * 0.5f, new Vector3(width, delta.magnitude * 0.5f, width), material, parent, false);
            value.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
            return value;
        }

        private static void Character(string name, Vector3 position, string cloth, bool crown = false, bool pope = false, bool guard = false)
        {
            var figure = new GameObject(name + " [scene stand-in]"); figure.transform.SetParent(world); figure.transform.position = position;
            Cone("Robe", position, 0.42f, 0.3f, 1.3f, cloth, figure.transform);
            Shape("Shoulders", PrimitiveType.Sphere, position + Vector3.up * 1.28f, new Vector3(0.75f, 0.32f, 0.4f), cloth, figure.transform, false);
            Shape("Head", PrimitiveType.Sphere, position + Vector3.up * 1.66f, new Vector3(0.33f, 0.41f, 0.31f), "Ivory", figure.transform, false);
            Beam("Left arm", position + new Vector3(-0.35f, 1.28f, 0f), position + new Vector3(-0.3f, 0.74f, 0.16f), 0.18f, cloth, figure.transform);
            Beam("Right arm", position + new Vector3(0.35f, 1.28f, 0f), position + new Vector3(0.3f, 0.83f, 0.2f), 0.18f, cloth, figure.transform);
            Shape("Headwear", PrimitiveType.Cylinder, position + Vector3.up * 1.91f, new Vector3(0.43f, 0.07f, 0.42f), crown ? "Gold" : "Charcoal", figure.transform, false);
            if (crown)
                for (int i = 0; i < 5; i++) Box("Crown finial", position + new Vector3((i - 2) * 0.08f, 2.07f, 0f), new Vector3(0.025f, 0.2f, 0.06f), "Gold", figure.transform, false);
            if (pope) Cone("Mitre", position + Vector3.up * 1.94f, 0.24f, 0.01f, 0.65f, "Ivory", figure.transform, 4);
            if (guard)
            {
                Beam("Spear", position + new Vector3(0.56f, 0f, 0.12f), position + new Vector3(0.56f, 2.75f, 0.12f), 0.045f, "Timber", figure.transform);
                Cone("Spear tip", position + new Vector3(0.56f, 2.75f, 0.12f), 0.08f, 0f, 0.3f, "Bronze", figure.transform, 4);
                Box("Shield", position + new Vector3(-0.4f, 1f, 0.24f), new Vector3(0.5f, 0.85f, 0.1f), "Bronze", figure.transform, false);
            }
            var collision = figure.AddComponent<CapsuleCollider>(); collision.height = 1.85f; collision.radius = 0.35f; collision.center = Vector3.up * 0.93f;
            foreach (var part in figure.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
        }

        private static void CreateHourglassPrefab()
        {
            var hourglass = new GameObject("Bronze Hourglass");
            Shape("Lower ring", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.44f, 0.04f, 0.44f), "Bronze", hourglass.transform, false);
            Shape("Upper ring", PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f), new Vector3(0.44f, 0.04f, 0.44f), "Bronze", hourglass.transform, false);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                var p = new Vector3(Mathf.Cos(angle) * 0.18f, 0f, Mathf.Sin(angle) * 0.18f);
                Beam("Frame", p + Vector3.up * 0.08f, p + Vector3.up * 0.62f, 0.035f, "Bronze", hourglass.transform);
            }
            Cone("Upper glass", new Vector3(0f, 0.35f, 0f), 0.02f, 0.16f, 0.27f, "Glass", hourglass.transform, 24);
            Cone("Lower glass", new Vector3(0f, 0.08f, 0f), 0.16f, 0.02f, 0.27f, "Glass", hourglass.transform, 24);
            Cone("Collected sand", new Vector3(0f, 0.09f, 0f), 0.12f, 0f, 0.15f, "Sand", hourglass.transform);
            Beam("Falling sand", new Vector3(0f, 0.15f, 0f), new Vector3(0f, 0.45f, 0f), 0.012f, "Sand", hourglass.transform);
            PrefabUtility.SaveAsPrefabAsset(hourglass, PrefabFolder + "/Hourglass.prefab");
            Object.DestroyImmediate(hourglass);
        }

        private static void Hourglass(Vector3 position, float scale = 1f)
        {
            var value = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Hourglass.prefab"));
            value.transform.SetParent(world); value.transform.position = position; value.transform.localScale = Vector3.one * scale;
        }

        private static void Torch(Vector3 position)
        {
            Shape("Brazier stem", PrimitiveType.Cylinder, position + Vector3.up * 0.65f, new Vector3(0.14f, 0.65f, 0.14f), "Bronze", null, false);
            Cone("Brazier bowl", position + Vector3.up * 1.2f, 0.1f, 0.42f, 0.35f, "Bronze", world);
            Particles("Brazier flame", position + Vector3.up * 1.48f, new Vector3(0.24f, 0.05f, 0.24f), new Color(1f, 0.43f, 0.07f, 0.85f), 24f, 1.3f, 0.4f, 0.7f);
            var light = new GameObject("Brazier light", typeof(Light)); light.transform.SetParent(world); light.transform.position = position + Vector3.up * 1.9f;
            var source = light.GetComponent<Light>(); source.type = LightType.Point; source.range = 10f; source.intensity = 3.2f; source.color = new Color(1f, 0.47f, 0.13f); source.shadows = LightShadows.None;
        }

        private static void Pendulum(Vector3 position, float height, float offset)
        {
            var frame = new GameObject("Great pendulum frame"); frame.transform.SetParent(world); frame.transform.position = position;
            Box("Left pier", position + new Vector3(-height * 0.27f, height * 0.5f, 0f), new Vector3(3.2f, height, 4f), "PyramidStone", frame.transform);
            Box("Right pier", position + new Vector3(height * 0.27f, height * 0.5f, 0f), new Vector3(3.2f, height, 4f), "PyramidStone", frame.transform);
            Box("Lintel", position + Vector3.up * height, new Vector3(height * 0.65f, 3f, 5.2f), "PyramidStone", frame.transform);
            var pivot = new GameObject("Pendulum pivot"); pivot.transform.SetParent(frame.transform); pivot.transform.position = position + Vector3.up * (height - 0.4f);
            var motion = pivot.AddComponent<PendulumMotion>(); motion.period = height * 0.45f; motion.offset = offset;
            Beam("Suspension", pivot.transform.position, position + Vector3.up * 3f, 0.18f, "CapitalBronze", pivot.transform);
            Shape("Pendulum bob", PrimitiveType.Sphere, position + Vector3.up * 2.8f, new Vector3(2.8f, 4.6f, 2.8f), "CapitalBronze", pivot.transform, false);
        }

        private static void Cracks(Vector3 origin, float length, int seed)
        {
            var random = new System.Random(seed);
            for (int branch = 0; branch < 5; branch++)
            {
                var value = new GameObject("Heat fissure", typeof(LineRenderer)); value.transform.SetParent(stage.heatDetails.transform);
                var line = value.GetComponent<LineRenderer>(); line.sharedMaterial = Materials["Ember"]; line.widthMultiplier = 0.055f;
                line.positionCount = 10; line.shadowCastingMode = ShadowCastingMode.Off;
                float angle = branch * 1.256f;
                for (int i = 0; i < 10; i++)
                {
                    float r = i * length / 10f;
                    line.SetPosition(i, origin + new Vector3(Mathf.Cos(angle) * r + (float)random.NextDouble() * 0.7f, 0.025f, Mathf.Sin(angle) * r + (float)random.NextDouble() * 0.8f));
                }
            }
        }

        private static Mesh Landscape(string key, float width, float depth, int count, bool valley)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int z = 0; z <= count; z++)
            for (int x = 0; x <= count; x++)
            {
                float px = (x / (float)count - 0.5f) * width;
                float pz = (z / (float)count - 0.5f) * depth;
                float height = valley ? Mathf.PerlinNoise(px * 0.017f + 30f, pz * 0.021f + 20f) * 7f * Mathf.SmoothStep(0f, 1f, (Mathf.Abs(px) - 29f) / 55f) : 0f;
                vertices.Add(new Vector3(px, height, pz)); uv.Add(new Vector2(px, pz) * 0.16f);
                if (x == count || z == count) continue;
                int k = z * (count + 1) + x;
                triangles.AddRange(new[] { k, k + count + 1, k + 1, k + 1, k + count + 1, k + count + 2 });
            }
            return SaveMesh(key, vertices, triangles, uv);
        }
    }
}
