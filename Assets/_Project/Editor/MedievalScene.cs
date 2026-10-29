using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private static void BuildMedieval()
        {
            var tribunal = Moment("中世纪 · 解释与质询", "新的文明拥有自己的权威。玩家以“哥白尼”为游戏称号进入大厅，上一幕的观察仍保存在观察簿。", "查看教皇、学者与支持者的位置；整理自己的假说。", new Vector3(0f, 1.75f, -16f), new Vector3(0f, 5f, 32f), "学者：你的解释，能同时说明那些飞星和异常的日照吗？");
            tribunal.sunA = new Vector3(-0.5f, 0.8f, -0.2f); tribunal.zenith = new Color(0.09f, 0.19f, 0.27f); tribunal.horizon = new Color(0.57f, 0.64f, 0.66f);
            tribunal.ambient = new Color(0.37f, 0.43f, 0.48f); tribunal.lightIntensity = 1.2f; tribunal.fog = new Color(0.25f, 0.32f, 0.35f); tribunal.fogDensity = 0.0017f;
            var triple = Moment("三日凌空 · 看清三个太阳", "警报打断大厅的争论。先给出清晰的辨认窗口：三个太阳同时可见，它们与夜里的飞星对应着不同距离。", "从外侧观测台抬头，确认三个太阳并按 E 记录。", new Vector3(0f, 1.75f, -49f), new Vector3(0f, 26f, -170f), "学者：等等……天上真的有三个。");
            triple.sunCount = 3; triple.sunA = new Vector3(-0.34f, 0.55f, -1f); triple.sunB = new Vector3(0.05f, 0.78f, -1f); triple.sunC = new Vector3(0.39f, 0.45f, -1f);
            triple.sunRadii = new Vector3(0.065f, 0.085f, 0.045f); triple.zenith = new Color(0.22f, 0.29f, 0.32f); triple.horizon = new Color(0.95f, 0.69f, 0.36f);
            triple.ambient = new Color(0.62f, 0.53f, 0.39f); triple.lightIntensity = 1.8f; triple.heat = 0.25f; triple.fogDensity = 0.003f;
            triple.observation = "中世纪外侧观测：三个具有清晰圆面的太阳同时出现在天空。";
            var collapse = Moment("三日凌空 · 文明崩溃", "热度越过整座城市。尘灰、草火与地裂侵入观测台，第一幕的岩下庇护已无法解释为普遍安全。", "见证高温灾变，并回看观察、判断与自己的假说。", new Vector3(0f, 1.75f, -49f), new Vector3(0f, 26f, -170f), "解释世界的结构之后，能否得到足够可靠的预测？");
            collapse.sunCount = 3; collapse.sunA = triple.sunA; collapse.sunB = triple.sunB; collapse.sunC = triple.sunC;
            collapse.sunRadii = new Vector3(0.09f, 0.12f, 0.065f); collapse.zenith = new Color(0.27f, 0.14f, 0.10f); collapse.horizon = new Color(1f, 0.49f, 0.18f);
            collapse.fog = new Color(0.53f, 0.29f, 0.13f); collapse.ambient = new Color(0.63f, 0.36f, 0.20f); collapse.lightIntensity = 2.5f; collapse.heat = 1f; collapse.fogDensity = 0.006f;
            collapse.observation = "三个太阳同时可见后，整座城市持续升温，地裂与草火出现在观测台周围。";
            triple.viewTarget = collapse.viewTarget = new Vector3(0f, 34f, -170f);
            BeginScene(Profile(2, "第二幕 / 中世纪文明", "带着证据提出解释，见证三日凌空", tribunal, triple, collapse));

            MeshObject("Medieval city earth", Landscape("MedievalGround", 320f, 360f, 40, false), new Vector3(0f, -0.08f, -30f), "Sand", null, true);
            Box("Cathedral foundation", new Vector3(0f, -0.5f, 8f), new Vector3(29f, 1f, 70f), "Stone");
            Box("Long red aisle", new Vector3(0f, 0.025f, 7f), new Vector3(4.4f, 0.04f, 60f), "RedCloth", null, false);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Carpet edging", new Vector3(side * 2.3f, 0.052f, 7f), new Vector3(0.06f, 0.02f, 60f), "Gold", null, false);
                Box("Stone wall lower", new Vector3(side * 14f, 3f, 8f), new Vector3(1.4f, 6f, 69f), "Stone");
                Box("Stone wall upper", new Vector3(side * 14f, 16f, 8f), new Vector3(1.4f, 5f, 69f), "Stone");
                for (int bay = 0; bay < 5; bay++)
                {
                    float z = -16f + bay * 12f;
                    Shape("Nave pillar", PrimitiveType.Cylinder, new Vector3(side * 10f, 5.2f, z), new Vector3(1.15f, 5.2f, 1.15f), "Stone");
                    Box("Column plinth", new Vector3(side * 10f, 0.3f, z), new Vector3(1.7f, 0.6f, 1.7f), "Paving");
                    Box("Capital", new Vector3(side * 10f, 10.2f, z), new Vector3(1.6f, 0.6f, 1.6f), "Ivory");
                    Box("Window pier", new Vector3(side * 14f, 10f, z), new Vector3(1.4f, 8f, 3f), "Stone");
                    Box("Exterior buttress", new Vector3(side * 16.4f, 5.5f, z), new Vector3(3.6f, 11f, 2f), "Stone");
                    StainedWindow(new Vector3(side * 13.95f, 6.1f, z + 5.8f), side);
                }
                Box("Front wall mass", new Vector3(side * 10f, 8f, -27f), new Vector3(10f, 16f, 2f), "Stone");
                Model("Bell tower", CastlePath + "tower-square-base.fbx", new Vector3(side * 18.5f, 0f, -24f), new Vector3(8f, 12f, 9f), "CastleAtlas", null, true);
                Model("Bell tower upper", CastlePath + "tower-square-mid-windows.fbx", new Vector3(side * 18.5f, 12f, -24f), new Vector3(8f, 11f, 9f), "CastleAtlas");
                Model("Bell tower high roof", CastlePath + "tower-square-top-roof-high.fbx", new Vector3(side * 18.5f, 23f, -24f), new Vector3(9f, 11f, 10f), "CastleAtlas");
            }
            for (int bay = 0; bay < 5; bay++) Arch("Pointed nave rib", new Vector3(0f, 10.4f, -16f + bay * 12f), 20f, 7f, 0.7f, 0.8f, "Ivory");
            Roof("Cathedral pitched roof", new Vector3(0f, 17.8f, 8f), 33f, 75f, 4.5f, "Roof", null, false);
            Arch("Great entry arch", new Vector3(0f, 9f, -27f), 10f, 7.5f, 1f, 2f, "Ivory");
            Box("Entry lintel above arch", new Vector3(0f, 18f, -27f), new Vector3(12f, 2f, 2f), "Stone");
            Box("Apse wall", new Vector3(0f, 8f, 43f), new Vector3(29f, 16f, 1.5f), "Stone");
            for (int i = 0; i < 3; i++) Box("Tribunal dais", new Vector3(0f, 0.2f + i * 0.35f, 32f + i), new Vector3(13f - i * 2f, 0.4f + i * 0.7f, 10f - i), "Stone");
            Box("Papal seat", new Vector3(0f, 1.8f, 36f), new Vector3(3.5f, 0.6f, 2.2f), "Timber");
            Box("Papal seat back", new Vector3(0f, 3.5f, 37f), new Vector3(3.7f, 4.3f, 0.4f), "Gold");
            Character("教皇", new Vector3(0f, 2f, 35.2f), "Ivory", false, true);
            Character("学者", new Vector3(5.3f, 0f, 22f), "BlueCloth");
            Character("达芬奇", new Vector3(-5.3f, 0f, 19f), "Robe");
            Character("教廷卫兵", new Vector3(7.2f, 0f, 6f), "Charcoal", false, false, true);
            Character("教廷卫兵", new Vector3(-7.2f, 0f, 6f), "Charcoal", false, false, true);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int row = 0; row < 4; row++)
                {
                    Box("Oak bench", new Vector3(side * 6.2f, 0.6f, -10f + row * 6f), new Vector3(3.5f, 0.28f, 1f), "Timber");
                    Box("Bench leg", new Vector3(side * 6.2f, 0.28f, -10f + row * 6f), new Vector3(2.7f, 0.56f, 0.65f), "Timber");
                }
                Torch(new Vector3(side * 7f, 0f, -20f)); Torch(new Vector3(side * 7f, 0f, 28f));
            }
            Box("Scholar lectern", new Vector3(4.8f, 0.85f, 18f), new Vector3(1.1f, 1.7f, 0.7f), "Timber");
            Box("Open manuscript", new Vector3(4.8f, 1.73f, 18f), new Vector3(0.85f, 0.05f, 0.52f), "Ivory", null, false);

            Box("Exterior observation plaza", new Vector3(0f, -0.1f, -65f), new Vector3(38f, 0.2f, 75f), "Paving");
            for (int i = 0; i < 12; i++)
            {
                float z = -38f - i * 5.5f;
                foreach (float side in new[] { -1f, 1f })
                    Model("Low observation parapet", CastlePath + "wall-half.fbx", new Vector3(side * 18f, 0f, z), new Vector3(1.1f, 1.5f, 5.3f), "CastleAtlas", null, true);
            }
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var pos = new Vector3(side * (31f + i % 3 * 6f), 0f, -40f - i * 11f);
                Box("Medieval town building", pos + Vector3.up * 3.5f, new Vector3(9f, 7f, 10f), "Stone");
                Roof("Steep town roof", pos + Vector3.up * 7f, 11f, 12f, 4.5f, "Roof", null, false);
                foreach (float column in new[] { -3.5f, 3.5f }) Box("Timber facade frame", pos + new Vector3(column, 4f, -5.1f), new Vector3(0.25f, 6f, 0.2f), "Timber", null, false);
                Box("Timber crossbeam", pos + new Vector3(0f, 5.5f, -5.1f), new Vector3(9f, 0.3f, 0.2f), "Timber", null, false);
            }
            for (int i = 0; i < 12; i++) Model("Distant basalt crag", NaturePath + "rock_tallB.fbx", new Vector3(-150f + i * 28f, 0f, -165f - i % 3 * 14f), new Vector3(33f, 22f + i % 4 * 8f, 38f), "Rock");
            for (int i = 0; i < 9; i++)
            {
                Fire(new Vector3(i % 2 == 0 ? -13f : 13f, 0.15f, -54f - i * 4.5f));
                Model("Dry plaza grass", NaturePath + "grass.fbx", new Vector3(i % 2 == 0 ? -13f : 13f, 0f, -54f - i * 4.5f), new Vector3(0.7f, 0.6f, 0.7f), "Grass");
            }
            Cracks(new Vector3(-5f, 0.03f, -62f), 15f, 292);
            Cracks(new Vector3(8f, 0.03f, -78f), 12f, 822);
            Particles("Exterior drifting dust", new Vector3(0f, 1.8f, -64f), new Vector3(40f, 3f, 60f), new Color(0.68f, 0.57f, 0.41f, 0.1f), 20f, 10f, 0.1f, 0.05f);
            FinishScene("03_MedievalTribunal");
        }

        private static void StainedWindow(Vector3 position, float side)
        {
            string[] names = { "WindowAmber", "WindowBlue", "WindowRuby" };
            Color[] colors = { new Color(0.73f, 0.38f, 0.08f), new Color(0.04f, 0.28f, 0.43f), new Color(0.52f, 0.065f, 0.045f) };
            for (int i = 0; i < 3; i++)
            {
                if (!Materials.ContainsKey(names[i]))
                {
                    var material = Material(names[i], colors[i]); material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", colors[i] * 0.5f);
                }
                Box("Stained glass pane", position + new Vector3(0f, 2.3f, (i - 1) * 1.3f), new Vector3(0.12f, 4.6f, 1.22f), names[i], null, false);
                Box("Window mullion", position + new Vector3(-side * 0.12f, 2.3f, (i - 1) * 1.3f - 0.65f), new Vector3(0.17f, 4.8f, 0.07f), "Bronze", null, false);
            }
            var arch = Arch("Stained-glass pointed frame", position + Vector3.up * 4.6f, 4f, 3.5f, 0.35f, 0.3f, "Ivory"); arch.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            for (int i = 0; i < 2; i++) Box("Window sill", position + Vector3.up * (i * 4.6f), new Vector3(0.45f, 0.18f, 4.6f), "Ivory", null, false);
        }
    }
}
