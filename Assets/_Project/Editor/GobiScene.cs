using System;
using UnityEditor;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private const string BoulderPath = "Assets/ThirdParty/PolyHaven/namaqualand_boulder_02/namaqualand_boulder_02_1k.fbx";
        private const string NaturePath = "Assets/ThirdParty/KenneyNature/Models/";
        private const string CastlePath = "Assets/ThirdParty/KenneyCastle/Models/";

        private static TravellerSceneProfile Profile(int index, string chapter, string subtitle, params SceneMoment[] moments)
        {
            string path = Root + "/Settings/Traveller/" + TravellerStage.SceneNames[index] + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<TravellerSceneProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<TravellerSceneProfile>(); AssetDatabase.CreateAsset(profile, path); }
            profile.sceneIndex = index; profile.chapter = chapter; profile.subtitle = subtitle; profile.moments = moments;
            profile.transitionSeconds = index == 0 ? 6f : 4f; EditorUtility.SetDirty(profile); return profile;
        }

        private static SceneMoment Moment(string title, string description, string objective, Vector3 position, Vector3 target, string caption = "")
        {
            return new SceneMoment { title = title, description = description, objective = objective, viewPosition = position, viewTarget = target, caption = caption };
        }

        private static SceneMoment Night(SceneMoment moment, int movingStars)
        {
            moment.sunCount = 0; moment.flyingStars = movingStars; moment.stars = 1f;
            moment.zenith = new Color(0.008f, 0.018f, 0.045f); moment.horizon = new Color(0.10f, 0.17f, 0.23f);
            moment.fog = new Color(0.07f, 0.105f, 0.145f); moment.ambient = new Color(0.23f, 0.29f, 0.38f);
            moment.lightColor = new Color(0.43f, 0.62f, 0.9f); moment.lightIntensity = 0.35f; moment.fogDensity = 0.002f;
            moment.sunA = new Vector3(0.2f, 0.6f, -0.6f); return moment;
        }

        private static void BuildGobi()
        {
            var dawn = Moment("乱纪元 · 陌生的晨光", "灰褐色戈壁与沉默的石峰。旅人已在巨岩旁等候，天边的亮光正在变得异常。", "沿石路走近旅人，辨认巨岩下的阴影。", new Vector3(0f, 1.75f, -30f), new Vector3(-6f, 7f, 32f), "旅人：先别往亮处走。现在是乱纪元，天亮未必是好事。");
            dawn.sunA = new Vector3(0.3f, 0.12f, 1f); dawn.sunRadii = new Vector3(0.055f, 0.03f, 0.03f);
            dawn.zenith = new Color(0.08f, 0.15f, 0.20f); dawn.horizon = new Color(0.9f, 0.59f, 0.30f);
            var giant = Moment("巨日 · 岩下避险", "巨日占据低空。岩外枯草起火、地表出现裂隙；岩下留出可通行的退路和观察窗口。", "站在阴影里看向岩外；这里仅能暂避这一次高温。", new Vector3(-13f, 1.75f, 10f), new Vector3(28f, 22f, 130f), "旅人：再往里站。别把这一片阴影，当成永远的安全。");
            giant.sunA = new Vector3(0.30f, 0.24f, 1f); giant.sunRadii = new Vector3(0.23f, 0.03f, 0.03f);
            giant.heat = 1f; giant.lightIntensity = 2.1f; giant.horizon = new Color(1f, 0.58f, 0.23f); giant.fog = new Color(0.60f, 0.39f, 0.22f); giant.ambient = new Color(0.50f, 0.39f, 0.28f);
            giant.observation = "巨日异常庞大，岩外草木燃烧，岩下暂时保有阴影。";
            var sunset = Moment("岩下 · 突然日落", "沙漏继续计时，日光迅速收束。旅人表明身份与前往朝歌献历法的目的，路途重新开放。", "查看沙漏与驿路，沿石路继续前行。", new Vector3(-8f, 1.75f, 21f), new Vector3(4f, 9f, 120f), "周文王：我是姬昌。路能走的时候，就不要再等。");
            sunset.sunA = new Vector3(0.3f, -0.045f, 1f); sunset.lightIntensity = 0.4f; sunset.zenith = new Color(0.035f, 0.07f, 0.13f); sunset.horizon = new Color(0.77f, 0.31f, 0.14f); sunset.ambient = new Color(0.28f, 0.29f, 0.34f);
            var one = Night(Moment("夜路 · 一颗飞星", "石峰上方有一颗明亮天体缓慢移动。背景星空与石峰提供参照，它没有流星式长尾。", "抬头看清一颗移动亮点，按 E 保存亲眼观察。", new Vector3(2f, 1.75f, 42f), new Vector3(46f, 48f, 185f), "周文王：别只看它有多亮，看看它和旁边那些星的位置。"), 1);
            one.observation = "第一次夜间观测：石峰上方有一颗缓慢移动的亮点，没有长尾。";
            var two = Night(Moment("休整后 · 两颗飞星", "经过一段路程与休整，两颗亮点同时进入视野，方向和高度可以区分。观察与周文王的解释分别记录。", "将两颗同时可见的亮点与上一条记录对照。", new Vector3(2f, 1.75f, 64f), new Vector3(15f, 55f, 185f), "周文王：两颗飞星。按我过去的推演，恒纪元将至。"), 2);
            two.observation = "第二次夜间观测：两颗亮点同时可见，高度与位置不同，均有缓慢的相对运动。";
            two.interpretation = "周文王根据两颗飞星判断恒纪元将至；这是人物对观察的解释。";
            var stable = Moment("驿路 · 离开戈壁", "温和的日照回到戈壁。石峰之间的驿路继续伸向远方，朝歌尚在数日路程之外。", "抵达驿路路标后，按 F 启程前往朝歌。", new Vector3(1f, 1.75f, 83f), new Vector3(0f, 5f, 145f));
            stable.sunA = new Vector3(-0.4f, 0.55f, 0.6f); stable.zenith = new Color(0.15f, 0.29f, 0.39f); stable.horizon = new Color(0.76f, 0.73f, 0.61f); stable.ambient = new Color(0.50f, 0.51f, 0.48f);
            BeginScene(Profile(0, "第一幕 / 戈壁旅人", "巨岩避日 · 岩下交谈 · 飞星观察之路", dawn, giant, sunset, one, two, stable));

            BuildNaturalTerrain(true, world);
            var shelter = new GameObject("Giant rock shelter"); shelter.transform.SetParent(world);
            Model("Capstone", BoulderPath, new Vector3(-13f, 5.4f, 17f), new Vector3(24f, 7f, 18f), "Rock", shelter.transform, true);
            Model("West buttress", BoulderPath, new Vector3(-25f, 0f, 20f), new Vector3(7f, 10f, 12f), "Rock", shelter.transform, true);
            Model("East buttress", BoulderPath, new Vector3(-1.5f, 0f, 21f), new Vector3(5f, 9f, 8f), "Rock", shelter.transform, true);
            // Scanned rocks omit unphotographed undersides. Close the overhang
            // and supports with detailed weathered stone so the shelter is solid.
            SealedShelterRock("Continuous weathered rock ceiling",new Vector3(-13,8.2f,17),new Vector3(26,7.6f,21),193,shelter.transform);
            SealedShelterRock("Continuous west rock support",new Vector3(-25,4.8f,20),new Vector3(7.5f,10,12.5f),237,shelter.transform);
            SealedShelterRock("Continuous east rock support",new Vector3(-1.5f,4.3f,21),new Vector3(5.5f,9,8.5f),311,shelter.transform);
            var roofCaster = Box("Shelter roof shadow caster", new Vector3(-13f, 5.5f, 17f), new Vector3(22f, 1f, 15f), "Rock", shelter.transform);
            roofCaster.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            PrefabUtility.SaveAsPrefabAssetAndConnect(shelter, PrefabFolder + "/RockShelter.prefab", InteractionMode.AutomatedAction);
            var safety = new GameObject("Shelter safe observation position"); safety.transform.SetParent(shelter.transform); safety.transform.position = new Vector3(-13f, 0.1f, 12f);
            stage.shelterCenter = safety.transform;
            Character("周文王", new Vector3(-16f, 0f, 13f), "Robe");
            Box("Traveller's satchel", new Vector3(-16.6f, 0.4f, 13.3f), new Vector3(0.45f, 0.65f, 0.26f), "Timber", null, false);
            SealedShelterRock("Weathered stone table",new Vector3(-12,0.38f,15),new Vector3(1.7f,0.75f,1.3f),411,world,true);
            Hourglass(new Vector3(-12f, 0.78f, 15f), 0.7f);

            BuildGobiRockscape(world);
            ObservationMarker(new Vector3(6f, 0f, 42f), 1);
            ObservationMarker(new Vector3(6f, 0f, 64f), 2);
            for (int i = 0; i < 6; i++) Fire(new Vector3(5f + i * 2.6f, 0.1f, 29f + Mathf.Sin(i) * 5f));
            Cracks(new Vector3(12f, 0f, 32f), 9f, 31);
            Particles("Windborne dust", new Vector3(0f, 1.4f, 20f), new Vector3(90f, 3f, 110f), new Color(0.59f, 0.47f, 0.32f, 0.19f), 36f, 10f, 0.13f, 0.08f);
            Box("Departure waystone", new Vector3(5f, 1.15f, 127f), new Vector3(0.85f, 2.3f, 0.7f), "Stone");
            Box("Road direction marker", new Vector3(5f, 1.5f, 126.6f), new Vector3(0.5f, 0.12f, 0.06f), "Bronze", null, false);
            EmbeddedRock("rock_face_01", "Departure pass west", -39, 153, new Vector3(30, 25, 28), world, 125,true);
            EmbeddedRock("rock_face_02", "Departure pass east", 55, 173, new Vector3(33, 24, 28), world, 250,true);
            FinishScene("01_GobiJourney");
        }

        private static void ObservationMarker(Vector3 position, int count)
        {
            Box("Observation waystone " + count, position + Vector3.up * 0.6f, new Vector3(0.55f, 1.2f, 0.45f), "Stone");
            for (int i = 0; i < count; i++) Shape("Bronze observation stud", PrimitiveType.Sphere, position + new Vector3((i - (count - 1) * 0.5f) * 0.2f, 1.03f, -0.235f), Vector3.one * 0.09f, "Gold", null, false);
        }

    }
}
