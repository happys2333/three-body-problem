#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem
{
    public sealed class CharacterCompletionPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed, performanceBudgetPassed;
            public int checks;
            public float meanMs, p95Ms, maximumHandError;
            public List<string> errors = new List<string>();
        }
        private readonly Report report = new Report();
        private string output;
        private TravellerStage stage;
        private bool hadJournal;
        private string journal;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-character-completion-check");
            if (i < 0 || i + 1 >= args.Length || FindFirstObjectByType<CharacterCompletionPlayerCheck>() != null) return;
            var go = new GameObject("Development character completion verification"); DontDestroyOnLoad(go);
            go.AddComponent<CharacterCompletionPlayerCheck>().output = args[i + 1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)); Application.logMessageReceived += OnLog;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            hadJournal = PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1"); journal = PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1", "");
            yield return Frames(8);
            bool popeOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-completion-pope-only") >= 0;
            if (!popeOnly)
            {
            yield return Load(1, JourneyStep.CourtApproach);
            var fans = stage.GetComponentsInChildren<RoyalFanBearer>();
            Check(fans.Length == 2, "The court must retain two fan bearers.");
            Check(stage.GetComponentsInChildren<CourtRetainer>().Count(r => !r.spearGuard) == 4, "The court's four attendants changed.");
            Check(stage.GetComponentsInChildren<EraGuardEquipment>().Count(g => g.HoldsPolearm) >= 8, "Court spear guards are missing.");
            foreach (var fan in fans)
            {
                var actor = fan.GetComponent<TravellerActor>(); CheckActor(actor, "ShangCourtLady");
                stage.player.Teleport(fan.transform.position + fan.transform.forward * 2.5f + Vector3.up * 1.55f, fan.transform.position + Vector3.up * 1.1f);
                yield return Delay(1.2f); yield return CheckGrip(actor, fan.GetComponent<CourtPropGrip>());
                Check(!fan.GetComponent<TorchBearer>().IsCarried, "An indoor fan bearer still carries a torch.");
                Capture("Court_" + fan.GetComponent<CourtRetainer>().stationId); yield return Frames(4);
            }
            }
            yield return Load(2, JourneyStep.TribunalApproach);
            var pope = stage.GetComponentsInChildren<TravellerActor>().Single(a => a.role == TravellerRole.Pope);
            CheckActor(pope, "PopeV3");
            var seat = stage.GetComponentsInChildren<Transform>().Single(t => t.name == "Seat contact");
            var hips = pope.animator.GetBoneTransform(HumanBodyBones.Hips);
            Check(Mathf.Abs(hips.position.y - seat.position.y - .105f) < .06f, "Pope is not sitting on the new seat.");
            stage.player.Teleport(new Vector3(2.5f, 3.5f, 31.4f), hips.position + Vector3.up * .3f);
            yield return Delay(.8f); Capture("Medieval_Pope_Seated"); yield return Frames(4);
            pope.Gesture("MX_TalkCalm"); yield return Delay(1); Capture("Medieval_Pope_Talking"); yield return Frames(4);
            Check(Vector3.Distance(hips.position, seat.position) < .5f, "A seated gesture moved the pope away from the chair.");
            if (popeOnly) { Finish(); yield break; }
            var clerk = stage.GetComponentInChildren<NpcHeldBook>(); Check(clerk != null, "The medieval clerk is missing.");
            var actorClerk = clerk.GetComponent<TravellerActor>(); CheckActor(actorClerk, "MedievalCleric");
            var blockers = Physics.OverlapCapsule(clerk.transform.position + Vector3.up * .32f, clerk.transform.position + Vector3.up * 1.48f, .23f, ~0, QueryTriggerInteraction.Ignore);
            Check(!blockers.Any(c => c.GetComponentInParent<TravellerActor>() != actorClerk), "The clerk's body intersects stairs or furniture.");
            Check(clerk.GetComponent<NpcIdentity>().CanAnswer, "The clerk is not registered for NPC dialogue.");
            Check(NpcConversationMemory.Key(clerk.GetComponent<NpcIdentity>()) != NpcConversationMemory.Key(pope.GetComponent<NpcIdentity>()), "Clerk and pope share private dialogue memory.");
            stage.player.Teleport(clerk.transform.position + clerk.transform.forward * 2.6f + Vector3.up * 1.5f, clerk.transform.position + Vector3.up * 1.1f);
            yield return Delay(1); Check(clerk.IsReading, "The clerk did not attach the manuscript.");
            yield return CheckGrip(actorClerk, clerk.GetComponent<CourtPropGrip>());
            Capture("Medieval_Clerk_Reading"); yield return Frames(4);
            var dialogue = stage.GetComponent<NpcDialogueDirector>();
            Check(dialogue.BeginPlayerQuestion() && dialogue.QuestionTarget == clerk.GetComponent<NpcIdentity>(), "Looking at the clerk does not select its own dialogue identity.");
            yield return Delay(.4f); Check(actorClerk.FaceTarget == stage.player.transform, "The clerk ignores the current listener.");
            dialogue.CancelQuestion(); stage.Journey.CloseDialogue();
            var pos = clerk.book.localPosition; var rotation = clerk.book.localRotation; actorClerk.Paused = true;
            yield return Delay(.5f); Check(Vector3.Distance(pos, clerk.book.localPosition) < .001f && Quaternion.Angle(rotation, clerk.book.localRotation) < .01f, "Paused book pose is moving."); actorClerk.Paused = false;
            actorClerk.Gesture("MX_TalkCalm"); yield return Delay(.6f); yield return CheckGrip(actorClerk, clerk.GetComponent<CourtPropGrip>());
            actorClerk.FollowRoute(new[] { clerk.transform.position + clerk.transform.forward * 1.8f }, .9f);
            yield return Delay(1); Check(actorClerk.IsMoving, "The clerk cannot walk while carrying a manuscript.");
            yield return CheckGrip(actorClerk, clerk.GetComponent<CourtPropGrip>());
            Capture("Medieval_Clerk_Carrying"); yield return Frames(4);
            actorClerk.StopTravelling(); yield return Delay(.5f);
            clerk.enabled = false; yield return Frames(2); Check(!clerk.GetComponent<CourtPropGrip>().IsHolding, "Disabling the book did not release the hands.");
            clerk.enabled = true; yield return Delay(.4f); Check(clerk.GetComponent<CourtPropGrip>().IsHolding, "Re-enabling the book did not restore the grip.");
            stage.player.Teleport(new Vector3(3.8f, 1.65f, 15), new Vector3(4.8f, 1, 18));
            yield return Delay(.6f); Capture("Medieval_Lectern_Manuscript"); yield return Frames(4);
            var frames = new List<float>(); float end = Time.realtimeSinceStartup + 8;
            while (Time.realtimeSinceStartup < end) { frames.Add(Time.unscaledDeltaTime * 1000); yield return null; }
            frames.Sort(); report.meanMs = frames.Average(); report.p95Ms = frames[(int)(frames.Count * .95f)]; report.performanceBudgetPassed = report.p95Ms <= 16.67f;
            Finish();
        }
        private IEnumerator Load(int index, JourneyStep step)
        {
            // Development harness only: enter the target authored scene without replaying earlier acts.
            var progress = new JourneyProgress(); typeof(JourneyProgress).GetProperty("Step").SetValue(progress, step);
            typeof(TravellerJourney).GetField("session", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, progress);
            typeof(TravellerJourney).GetField("previewMode", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            yield return SceneManager.LoadSceneAsync(TravellerStage.SceneNames[index]); yield return Frames(20);
            stage = FindFirstObjectByType<TravellerStage>(); stage.player.enabled = false;
        }
        private void CheckActor(TravellerActor actor, string model)
        {
            Check(actor.animator != null && actor.animator.avatar != null && actor.animator.avatar.isValid && actor.animator.avatar.isHuman, "Invalid Humanoid: " + actor.name);
            Check(actor.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith(model + " Visual", StringComparison.Ordinal)), "Wrong reviewed body: " + actor.name);
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Check(renderer.sharedMesh != null && renderer.bones.All(b => b != null), "Missing mesh/bones: " + actor.name);
                Check(renderer.sharedMaterials.All(m => m != null && m.shader.isSupported && m.mainTexture != null && m.GetTexture("_BumpMap") != null), "Missing PBR material: " + actor.name);
            }
        }
        private IEnumerator CheckGrip(TravellerActor actor, CourtPropGrip grip)
        {
            float maximum = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForEndOfFrame();
                maximum = Mathf.Max(maximum, Vector3.Distance(actor.animator.GetBoneTransform(HumanBodyBones.RightHand).position, grip.RightTarget), Vector3.Distance(actor.animator.GetBoneTransform(HumanBodyBones.LeftHand).position, grip.LeftTarget));
            }
            report.maximumHandError = Mathf.Max(report.maximumHandError, maximum);
            Check(maximum < .065f, "Hand/prop contact error: " + actor.name + " " + maximum);
        }
        private void Check(bool value, string error) { report.checks++; if (!value) report.errors.Add(error); }
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private static IEnumerator Delay(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        private void Capture(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output), name + ".png")); }
        private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message); }
        private void Finish()
        {
            if (hadJournal) PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1", journal); else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1"); PlayerPrefs.Save();
            stage.player.ReleasePointer(); report.passed = report.errors.Count == 0; File.WriteAllText(output, JsonUtility.ToJson(report, true)); Application.Quit(report.passed ? 0 : 1);
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
#endif
