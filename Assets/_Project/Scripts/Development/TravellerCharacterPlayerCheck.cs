#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    // Opt-in native-player checks exercise the same movement and performance components as normal play.
    public sealed class TravellerCharacterPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report { public bool passed; public int checks; public List<string> errors = new List<string>(); public List<string> verified = new List<string>(); }
        private readonly Report report = new Report();
        private string output;
        private TravellerStage stage;
        private TravellerCastDirector cast;
        private TravellerHud hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-traveller-character-check");
            if (i < 0 || i + 1 >= args.Length || UnityEngine.Object.FindFirstObjectByType<TravellerCharacterPlayerCheck>() != null) return;
            var root = new GameObject("Development Character Check"); DontDestroyOnLoad(root);
            root.AddComponent<TravellerCharacterPlayerCheck>().output = args[i + 1];
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)); Application.logMessageReceived += OnLog;
            Application.runInBackground = true; Application.targetFrameRate = 120; QualitySettings.vSyncCount = 0;
            yield return Frames(8); Bind(); stage.Journey.Begin(); stage.player.enabled = false;
            yield return Frames(5);
            yield return ValidateTorchGrips();
            var companion = cast.companion; var before = companion.transform.position;
            yield return Frames(90);
            Check(Vector3.Distance(before, companion.transform.position) > 0.6f, "Companion moves along the authored route.");
            Check(companion.CurrentMotion == "MX_Walking", "Companion movement selects the walking clip.");
            Vector3 eye = stage.player.viewCamera.transform.localPosition;
            Quaternion view = stage.player.viewCamera.transform.localRotation;
            foreach (var item in new[] { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0) })
            {
                for (int i = 0; i < 40; i++) { stage.player.MoveInput(item, false, Time.deltaTime); yield return null; }
                Check(cast.playerActor.CurrentMotion == (item.y < 0 ? "MX_WalkBackward" : item.x > 0 ? "MX_RightStrafeWalking" : "MX_Walking"), "First-person body matches movement " + item);
            }
            for (int i = 0; i < 40; i++) { stage.player.MoveInput(Vector2.up, true, Time.deltaTime); yield return null; }
            Check(cast.playerActor.CurrentMotion == "MX_Running", "Fast movement selects the running clip.");
            Check(Vector3.Distance(eye, stage.player.viewCamera.transform.localPosition) < 0.001f && Quaternion.Angle(view, stage.player.viewCamera.transform.localRotation) < 0.01f, "Body animation leaves first-person eye position and aim unchanged.");
            for (int i = 0; i < 25; i++) { stage.player.transform.Rotate(Vector3.up, 2f); yield return null; }
            Check(cast.playerActor.CurrentMotion == "MX_Idle", "Looking around while stationary does not animate a walking body.");
            stage.player.Teleport(new Vector3(-100, 1.65f, -150), Vector3.zero); yield return Frames(12);
            before = companion.transform.position; yield return Frames(80);
            Check(Vector3.Distance(before, companion.transform.position) < 0.05f, "The companion waits for a distant player.");
            Check(companion.CurrentMotion == "MX_Idle", "Waiting companion stops walking in place.");
            stage.player.Teleport(companion.transform.position + new Vector3(0, 1.65f, 2.5f), companion.transform.position + Vector3.up * 1.35f);
            stage.Journey.Progress.Tick(6);stage.Journey.Progress.MarkGiantSunVisible();stage.Journey.Progress.Arrive();stage.Journey.Interact();stage.Journey.SelectDialogue("identity");
            yield return Frames(35);
            Check(companion.CurrentGesture == "MX_TalkCalm", "Dialogue selects a talking performance.");
            Capture("Gobi_FirstPerson_Conversation"); yield return Frames(4);
            stage.Journey.SelectDialogue("close");
            stage.player.FaceTarget(stage.player.transform.position + stage.player.transform.forward * 0.25f + Vector3.up * 0.1f);
            Capture("FirstPerson_LookDown_Body"); yield return Frames(4);
            stage.Journey.ReturnToTitle(); yield return Frames(5);
            float pausedTime = companion.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return Frames(20);
            Check(Mathf.Abs(companion.animator.GetCurrentAnimatorStateInfo(0).normalizedTime - pausedTime) < 0.001f, "Title/menu pauses character animation.");
            stage.Journey.TogglePreview();

            yield return Load(1);
            yield return ValidateTorchGrips();
            Check(cast.king != null && cast.king.seated && cast.companion != null, "Capital contains the new king and companion.");
            var king = cast.king; Vector3 kingRoot = king.transform.position;
            float hipY = king.animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
            king.Gesture("MX_TalkCalm", 3); yield return Frames(55);
            Check(Vector3.Distance(kingRoot, king.transform.position) < 0.001f && Mathf.Abs(king.animator.GetBoneTransform(HumanBodyBones.Hips).position.y - hipY) < 0.04f, "Seated talking keeps the throne root and pelvis in place.");
            Check(king.animator.GetCurrentAnimatorStateInfo(1).IsName("Seated Gestures.MX_TalkCalm"), "King talks through the upper-body layer.");
            stage.player.Teleport(new Vector3(1.7f, 7.45f, 30.8f), king.animator.GetBoneTransform(HumanBodyBones.Head).position);
            Capture("Chaoge_King_Seated"); yield return Frames(4);
            stage.player.Teleport(new Vector3(-3, 5.65f, 10), new Vector3(-5, 5.7f, 17));
            cast.companion.transform.position = new Vector3(-5, 4, 14.8f); cast.companion.Gesture("MX_Bow");
            yield return Frames(45); Capture("Chaoge_Companion_Calendar"); yield return Frames(4);

            yield return Load(2);
            yield return ValidateTorchGrips();
            Check(cast.pope != null && cast.scholar != null && cast.daVinci != null, "Tribunal contains the new pope, scholar and Da Vinci.");
            var pope = cast.pope; Vector3 popeRoot = pope.transform.position;
            pope.Gesture("MX_ShakingHeadNo"); yield return Frames(40);
            Check(Vector3.Distance(popeRoot, pope.transform.position) < 0.001f && pope.animator.GetCurrentAnimatorStateInfo(1).IsName("Seated Gestures.MX_ShakingHeadNo"), "Papal head gesture preserves the seated pose.");
            stage.player.Teleport(new Vector3(1.6f, 3.45f, 32), pope.animator.GetBoneTransform(HumanBodyBones.Head).position);
            Capture("Medieval_Pope_Seated"); yield return Frames(4);
            stage.player.Teleport(new Vector3(2.8f, 1.65f, 19.5f), cast.scholar.animator.GetBoneTransform(HumanBodyBones.Head).position);
            cast.scholar.Gesture("MX_TalkArgue", 6); yield return Frames(45); Capture("Medieval_Scholar_Dialogue"); yield return Frames(4);
            cast.scholar.FollowRoute(cast.scholarExitPath, cast.escapeSpeed);
            cast.daVinci.FollowRoute(cast.daVinciExitPath, cast.escapeSpeed);
            cast.scholar.Gesture("MX_LookSky", 6);
            float until = Time.realtimeSinceStartup + 25;
            while ((cast.scholar.IsTravelling || cast.daVinci.IsTravelling) && Time.realtimeSinceStartup < until) yield return null;
            Check(!cast.scholar.IsTravelling && !cast.daVinci.IsTravelling, "Scholars traverse the aisle and exit to the outdoor observation points.");
            yield return Frames(3);
            Check(cast.scholar.CurrentGesture == "MX_LookSky", "A sky-observation cue received while travelling plays after arrival.");
            stage.ApplyMoment(1, true, false);
            stage.player.Teleport(new Vector3(0, 1.65f, -43), new Vector3(4.5f, 1.4f, -47));
            cast.scholar.Gesture("MX_LookSky", 6); cast.daVinci.Gesture("MX_LookSky", 6);
            yield return Frames(100); Capture("Medieval_ThreeSuns_Reaction"); yield return Frames(4);
            report.passed = report.errors.Count == 0; File.WriteAllText(output, JsonUtility.ToJson(report, true)); Application.Quit(report.passed ? 0 : 1);
        }

        private IEnumerator ValidateTorchGrips()
        {
            var bearers=stage.GetComponentsInChildren<TorchBearer>();
            var modes=new AnimatorCullingMode[bearers.Length];
            for(int i=0;i<bearers.Length;i++){var actor=bearers[i].GetComponent<TravellerActor>();modes[i]=actor.animator.cullingMode;actor.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
            yield return Frames(32);
            for(int i=0;i<bearers.Length;i++)
            {
                var actor=bearers[i].GetComponent<TravellerActor>();var hand=actor.animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Check(bearers[i].Grip!=null&&Vector3.Distance(hand.position,bearers[i].HandTarget)<.12f,"Torch follows reachable hand IK: "+actor.name);
                actor.animator.cullingMode=modes[i];
            }
        }
        private void Bind()
        {
            stage = UnityEngine.Object.FindFirstObjectByType<TravellerStage>(); cast = stage.GetComponent<TravellerCastDirector>(); hud = stage.GetComponentInChildren<TravellerHud>();
        }
        private IEnumerator Load(int index)
        {
            var load = SceneManager.LoadSceneAsync(TravellerStage.SceneNames[index]); while (!load.isDone) yield return null;
            yield return Frames(8); Bind(); stage.player.enabled = false; hud.Root.style.display = DisplayStyle.None;
        }
        private void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output), name + ".png"));
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private void Check(bool condition, string message) { report.checks++; if (condition) report.verified.Add(message); else report.errors.Add(message); }
        private void OnLog(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            report.errors.Add(message); File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
#endif
