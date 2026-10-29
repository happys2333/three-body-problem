using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem.Editor
{
    public static class CharacterMotionGalleryBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CharacterPreview/CharacterMotionGallery.unity";
        private const string RestorePath = "Logs/CharacterMotionQA/editor-preview-restore.json";
        [Serializable] public class RootState { public string name; public int sibling; public bool active; }
        [Serializable] public class PreviewState { public string scenePath; public List<RootState> roots = new List<RootState>(); }
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            {"Idle01", "混元：待机"}, {"Walk01", "混元：行走"}, {"Run", "混元：奔跑"}, {"Turn", "混元：转身"},
            {"Despair", "混元：沮丧"}, {"FallForward", "向前跌倒（高度校正）"}, {"MX_Idle", "自然待机"},
            {"MX_Walking", "行走"}, {"MX_Running", "奔跑"}, {"MX_Jump", "跳跃"}, {"MX_TalkCalm", "普通交谈"},
            {"MX_TalkArgue", "质询与争辩"}, {"MX_HeadNodYes", "点头"}, {"MX_ShakingHeadNo", "摇头"},
            {"MX_PointForward", "向前指引"}, {"MX_LookSky", "空手观天（派生）"}, {"MX_ShieldEyes", "举手遮阳"},
            {"MX_Shiver", "寒冷发抖（自制派生）"}, {"MX_RunToExhaustion", "奔跑后疲惫失衡"}, {"MX_GetUpProne", "趴倒后起身"},
            {"MX_KneelDown", "下跪"}, {"MX_PrayStanding", "站立祈祷"}, {"MX_SitDown", "坐下"},
            {"MX_SitIdle", "坐姿呼吸待机"}, {"MX_StandFromSeat", "从座椅起身"}, {"MX_PickUp", "拾取道具（身体）"},
            {"MX_PutDown", "放下道具（反向派生）"}, {"MX_CarryBoxIdle", "双手持箱待机"}, {"MX_CarryBoxWalk", "持物行走"},
            {"MX_Think", "站立思考"}, {"MX_WriteSeated", "坐姿书写（身体）"}, {"MX_OpenBox", "打开箱盖（身体）"},
            {"MX_Fear", "恐惧反应"}, {"MX_WalkBackward", "后退行走（反向派生）"}, {"MX_LeftTurn90", "左转约 90 度"},
            {"MX_RightTurn90", "向右转身（角度需调校）"}, {"MX_LeftTurn", "向左转身约 180 度"}, {"MX_RightTurn", "向右转身约 180 度"},
            {"MX_LeftStrafeWalking", "向左横移"}, {"MX_RightStrafeWalking", "向右横移"}, {"MX_LeftStrafe", "向左侧跑"},
            {"MX_RightStrafe", "向右侧跑"}, {"MX_DismissingGesture", "挥退手势"}, {"MX_AngryGesture", "愤怒手势"},
            {"MX_Acknowledging", "回应"}, {"MX_RelievedSigh", "松一口气"}, {"MX_HappyHandGesture", "友善手势"},
            {"MX_LookAwayGesture", "转头避开"}, {"MX_WeightShift", "站姿重心变化"}, {"MX_SarcasticHeadNod", "讥讽点头"},
            {"MX_BeingCocky", "傲慢姿态"}, {"MX_AnnoyedHeadShake", "不耐烦摇头"}, {"MX_HardHeadNod", "用力点头"},
            {"MX_LengthyHeadNod", "连续点头"}, {"MX_ThoughtfulHeadShake", "思索摇头"},
            {"MX_LookUpTorchBase", "持火把观天（原始身体动作）"}, {"MX_Bow", "鞠躬致意（通用礼节）"}
        };
        private static readonly Dictionary<string, string> VariantLabels = new Dictionary<string, string>
        {
            {"Idle", "待机"}, {"Idle2", "待机 2"}, {"Idle3", "待机 3"}, {"Idle4", "待机 4"}, {"Idle5", "待机 5"},
            {"Walking", "行走"}, {"Running", "奔跑"}, {"Walk", "行走"}, {"Run", "奔跑"},
            {"LeftTurn", "左转"}, {"RightTurn", "右转"}, {"TurnLeft", "左转"}, {"TurnRight", "右转"},
            {"WalkLeftTurn", "行走左转"}, {"WalkRightTurn", "行走右转"}, {"WalkBackwards", "后退行走"},
            {"BackwardsTurnRight", "后退右转"}, {"BackwardsTurnLeft", "后退左转"},
            {"RunLeftTurn", "奔跑左转"}, {"RunRightTurn", "奔跑右转"}, {"RunBackwards", "后退奔跑"},
            {"RunBackwardsRightTurn", "后退跑右转"}, {"RunBackwardsLeftTurn", "后退跑左转"},
            {"StumbleIdle", "踉跄待机"}, {"WaveIdle", "挥手求助"}, {"HurtingIdle", "疼痛待机"},
            {"RunJump", "跑动跳跃"}, {"StandingJump", "原地跳跃"}, {"FallingIdle", "下落待机"},
            {"StandToCover", "站立进入掩体"}, {"StandToCover2", "站立进入掩体 2"}, {"RunToStop", "奔跑急停"},
            {"JumpingUp", "向上跳跃"}, {"FallingToRoll", "落地翻滚"}, {"HardLanding", "重落地"},
            {"CoverToStand", "离开掩体站起"}, {"CoverToStand2", "离开掩体站起 2"},
            {"CrouchedSneakingLeft", "蹲姿向左潜行"}, {"CrouchedSneakingRight", "蹲姿向右潜行"},
            {"LeftCoverSneak", "沿掩体向左潜行"}, {"RightCoverSneak", "沿掩体向右潜行"}
        };

        [MenuItem("Tools/Three Body Traveller/Build Character Motion Gallery")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before building the gallery.");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            string materialPath = GeneratedCharacterLibrary.Root + "/GalleryFloor.mat";
            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (floorMaterial == null)
            {
                floorMaterial = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(floorMaterial, materialPath);
            }
            floorMaterial.color = new Color(0.18f, 0.21f, 0.25f);
            if (File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Logs/scene-backups");
                File.Copy(ScenePath, "Logs/scene-backups/CharacterMotionGallery_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".unity");
            }
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                RenderSettings.fog = false; RenderSettings.skybox = null;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.46f, 0.49f, 0.55f);
                var cameraObject = new GameObject("Gallery Camera"); var camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera"; cameraObject.AddComponent<AudioListener>();
                camera.orthographic = true; camera.orthographicSize = 3.15f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.045f, 0.065f, 0.085f);
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 50;
                camera.transform.position = new Vector3(0, 2.5f, 8); camera.transform.LookAt(new Vector3(0, 2.3f, 0));
                var key = new GameObject("Gallery key").AddComponent<Light>(); key.type = LightType.Directional;
                key.intensity = 1.3f; key.transform.rotation = Quaternion.Euler(35, 210, 0);
                var fill = new GameObject("Gallery fill").AddComponent<Light>(); fill.type = LightType.Directional;
                fill.intensity = 0.55f; fill.transform.rotation = Quaternion.Euler(10, 130, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Lower platform";
                floor.transform.position = new Vector3(0, -0.075f, 0); floor.transform.localScale = new Vector3(7.5f, 0.15f, 2);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                var upper = GameObject.CreatePrimitive(PrimitiveType.Cube); upper.name = "Upper platform";
                upper.transform.position = new Vector3(0, 2.525f, 0); upper.transform.localScale = new Vector3(7.5f, 0.15f, 2);
                upper.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                var gallery = new GameObject("Character Motion Gallery").AddComponent<CharacterMotionGallery>();
                gallery.viewCamera = camera;
                gallery.platforms = new[] { floor, upper };
                gallery.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf");
                gallery.roleNames = new[] { "玩家", "周文王", "纣王", "教皇", "学者", "达芬奇" };
                gallery.modelRoots = new GameObject[6]; gallery.animators = new Animator[6];
                for (int i = 0; i < GeneratedCharacterLibrary.Roles.Length; i++)
                {
                    string role = GeneratedCharacterLibrary.Roles[i];
                    var actor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                        GeneratedCharacterLibrary.Root + "/Prefabs/" + role + ".prefab"), scene);
                    actor.transform.position = new Vector3((i % 3 - 1) * 2.25f, i < 3 ? 0 : 2.6f, 0);
                    gallery.modelRoots[i] = actor; gallery.animators[i] = actor.GetComponentInChildren<Animator>();
                    gallery.animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                gallery.motions = GeneratedCharacterLibrary.LoadClips().OrderBy(c => c.name).Select(c => new CharacterMotionGallery.Entry {
                    id = c.name, clip = c, label = Label(c.name), source = c.name.StartsWith("MX_") ?
                        (AssetDatabase.GetAssetPath(c).EndsWith(".anim") ? "项目派生动作" : "Mixamo") : "混元"
                }).ToArray();
                gallery.layerName = ((AnimatorController)gallery.animators[0].runtimeAnimatorController).layers[0].name;
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save character gallery.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
            }
            AssetDatabase.Refresh();
        }

        private static string Label(string id)
        {
            string label;
            if (Labels.TryGetValue(id, out label)) return label;
            foreach (var prefix in new[] { "MX_Injured", "MX_Adventure" })
                if (id.StartsWith(prefix) && VariantLabels.TryGetValue(id.Substring(prefix.Length), out label))
                    return (prefix == "MX_Injured" ? "受创：" : "冒险：") + label;
            return id.Replace("MX_Injured", "受创：").Replace("MX_Adventure", "动作：").Replace("MX_", "");
        }

        public static void OpenForRuntimeCheck()
        {
            if (EditorApplication.isPlaying || SceneManager.sceneCount != 1) throw new InvalidOperationException("Use one stopped scene before preview QA.");
            var original = SceneManager.GetActiveScene();
            var state = new PreviewState { scenePath = original.path };
            foreach (var root in original.GetRootGameObjects())
                state.roots.Add(new RootState { name = root.name, sibling = root.transform.GetSiblingIndex(), active = root.activeSelf });
            Directory.CreateDirectory("Logs/CharacterMotionQA");
            File.WriteAllText(RestorePath, JsonUtility.ToJson(state, true));
            // Keep the current scene loaded so its unsaved edits survive the temporary preview.
            foreach (var root in original.GetRootGameObjects()) root.SetActive(false);
            var preview = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
        }

        public static void RestoreAfterRuntimeCheck()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before restoring the editor scene.");
            var state = JsonUtility.FromJson<PreviewState>(File.ReadAllText(RestorePath));
            var original = SceneManager.GetSceneByPath(state.scenePath);
            if (!original.IsValid() || !original.isLoaded) throw new InvalidOperationException("Original scene is no longer loaded.");
            var preview = SceneManager.GetSceneByPath(ScenePath);
            if (preview.IsValid() && preview.isLoaded) EditorSceneManager.CloseScene(preview, true);
            SceneManager.SetActiveScene(original);
            var roots = original.GetRootGameObjects();
            foreach (var saved in state.roots)
            {
                var root = roots.FirstOrDefault(r => r.name == saved.name && r.transform.GetSiblingIndex() == saved.sibling);
                if (root == null) throw new InvalidOperationException("Cannot restore root: " + saved.name);
                root.SetActive(saved.active);
            }
        }
    }
}
