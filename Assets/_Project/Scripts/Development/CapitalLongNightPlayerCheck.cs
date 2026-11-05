#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    public sealed class CapitalLongNightPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int checks, citizens, observations, playedSampleRate;
            public float metres;
            public string phase;
            public List<string> errors = new List<string>();
        }
        private const string JournalKey = "ThreeBodyTraveller.Journal.v1";
        private readonly Report report = new Report();
        private TravellerStage stage;
        private TravellerJourney journey;
        private string output, journal;
        private bool hadJournal;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-capital-long-night-check");
            if (i < 0 || i + 1 >= a.Length) return;
            var go = new GameObject("Native long night check");
            DontDestroyOnLoad(go);
            go.AddComponent<CapitalLongNightPlayerCheck>().output = a[i+1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            hadJournal = PlayerPrefs.HasKey(JournalKey);
            journal = PlayerPrefs.GetString(JournalKey, "");
            Application.runInBackground = true;
            Application.logMessageReceived += Log;
            yield return Run();
            report.passed = report.errors.Count == 0;
            Save();
            if (hadJournal) PlayerPrefs.SetString(JournalKey, journal); else PlayerPrefs.DeleteKey(JournalKey);
            PlayerPrefs.Save();
            Application.Quit(report.passed ? 0 : 1);
        }
        private IEnumerator Run()
        {
            yield return Frames(10);
            stage = FindFirstObjectByType<TravellerStage>();
            journey = stage.Journey;
            var args = Environment.GetCommandLineArgs();
            int voice = Array.IndexOf(args, "-capital-check-voice");
            if (voice >= 0 && voice + 1 < args.Length)
                yield return CheckVoice(args[voice+1]);
            Phase("Load Chaoge at the visit to the pool");
            Enter(JourneyStep.WalkToWater);
            yield return Until(() => SceneManager.GetActiveScene().name == TravellerStage.SceneNames[1], 50);
            if (!Check(SceneManager.GetActiveScene().name == TravellerStage.SceneNames[1], "Chaoge did not load.")) yield break;
            yield return Frames(12);
            stage = FindFirstObjectByType<TravellerStage>(); journey = stage.Journey;
            stage.player.enabled = false;
            stage.player.Teleport(stage.profile.waterGoal + Vector3.up * stage.player.eyeHeight, stage.Current.viewTarget);
            var cast = stage.GetComponent<TravellerCastDirector>();
            cast.companion.transform.position = stage.profile.waterGoal;
            var revival = stage.GetComponent<CapitalRevivalDirector>();
            report.citizens = revival.citizens.Length;
            yield return Until(() => stage.IsRevivalComplete, 55);
            if (!Check(report.citizens == 7 && stage.IsRevivalComplete, "The seven citizens did not revive.")) yield break;
            journey.FaceGoal(); stage.RecordObservation();
            Check(journey.Progress.Step == JourneyStep.AfterRevival, "Could not record revival.");

            Phase("Return to court across the real passage");
            yield return Until(() => journey.Progress.Step == JourneyStep.ReturnToCourt, 15);
            if (!Check(journey.Progress.Step == JourneyStep.ReturnToCourt && stage.Current.sunCount == 0 &&
                       !stage.flyingStars.Any(s => s.gameObject.activeSelf), "Missing sunrise or delayed star reveal is incorrect.")) yield break;
            yield return WalkRoute(JourneyStep.ReturnToCourt);
            yield return Until(() => journey.Progress.Step == JourneyStep.CalendarFailed, 45);
            if (!Check(journey.Progress.Step == JourneyStep.CalendarFailed, "The audience did not begin: player at " + stage.player.transform.position + ", companion at " + cast.companion.transform.position + ", goal " + journey.Goal)) yield break;
            yield return Until(() => journey.Speech.Contains("三颗飞星"), 10);
            Check(journey.Speech.Contains("三颗飞星") && stage.flyingStars.Count(s => s.gameObject.activeSelf) == 3, "Messenger did not announce three stars.");
            stage.player.FaceTarget(cast.king.transform.position + Vector3.up);
            Capture("01-Court");
            yield return Until(() => journey.Progress.Step == JourneyStep.LeaveLongNightCourt, 20);
            Check(!cast.king.gameObject.activeSelf && !cast.companion.gameObject.activeSelf, "The king or King Wen did not leave.");
            Check(revival.citizens.All(c => !c.IsRecovered), "Citizens did not dehydrate.");
            Check(stage.GetComponentsInChildren<TextMesh>().Any(m => m.text == "EXIT" && m.gameObject.activeInHierarchy), "Missing EXIT sign.");
            if (!Check(journey.Progress.Step == JourneyStep.LeaveLongNightCourt, "The passage did not open.")) yield break;

            Phase("Walk out and observe the orbit");
            yield return WalkRoute(JourneyStep.LeaveLongNightCourt);
            if (!Check(journey.Progress.Step == JourneyStep.ObserveLongNight && journey.CanObserve, "Could not reach outdoor observation point.")) yield break;
            journey.FaceGoal();
            Vector3 initial = stage.flyingStars[0].position;
            Capture("02-Flying-Stars"); yield return Until(() => Vector3.Distance(initial,stage.flyingStars[0].position) > .15f, 4);
            Check(Vector3.Distance(initial,stage.flyingStars[0].position) > .15f, "Stars do not move.");
            stage.RecordObservation();

            Phase("Water snow, dry ice, atmosphere, and civilization 137");
            var snow = stage.GetComponentsInChildren<ParticleSystem>().FirstOrDefault(p => p.name == "Long night snow near the observer");
            var torch = stage.player.bodyActor.GetComponent<TorchBearer>();
            var covers = stage.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.transform.parent != null && renderer.transform.parent.name == "Long night ground deposits")
                .ToArray();
            if (!Check(covers.Any(cover => cover.name == "Water garden approach") &&
                       covers.Any(cover => cover.name == "Pyramid east garden descent") &&
                       covers.Any(cover => cover.name == "Photographic river-valley terrain"),
                       "Snow cover missing from terrain, paving, or ramp.")) yield break;
            var deposit = covers[0].sharedMaterial;
            stage.player.FaceTarget(stage.player.transform.position + new Vector3(8, -.6f, 15));
            Check(journey.Progress.Step == JourneyStep.AfterLongNight && snow != null && snow.isPlaying &&
                  torch.IsLit && deposit.GetVector("_Deposits").x < .08f, "Water snow did not begin on bare ground with the torch lit.");
            Color water = snow == null ? Color.clear : snow.main.startColor.color;
            Capture("03-Before-Ground-Cover");
            yield return Until(() => journey.Progress.Step == JourneyStep.AfterLongNight && journey.Progress.Elapsed >= 3.5f, 6);
            float waterCover = deposit.GetVector("_Deposits").x;
            Check(waterCover > .4f && waterCover < .6f, "Water snow did not accumulate gradually.");
            Capture("04-Water-Snow");
            yield return Until(() => journey.Progress.Step == JourneyStep.DryIceSnow && journey.Progress.Elapsed >= 3.5f, 12);
            Vector4 dryIce = deposit.GetVector("_Deposits");
            Check(journey.Progress.Step == JourneyStep.DryIceSnow && snow != null && snow.main.startColor.color != water &&
                  dryIce.x == 1f && dryIce.y > .4f && dryIce.y < .6f && dryIce.z == 0 && torch.IsLit,
                  "Dry ice did not accumulate over water snow while the torch still burned.");
            Capture("05-Dry-Ice");
            yield return Until(() => journey.Progress.Step == JourneyStep.AtmosphereFreeze && journey.Progress.Elapsed >= .85f, 10);
            Check(journey.Progress.Step == JourneyStep.AtmosphereFreeze && journey.Speech.Contains("氧与氮") &&
                  stage.Combustion > 0f && stage.Combustion < .75f, "Torches did not fade as atmospheric freezing started.");
            yield return Until(() => journey.Progress.Elapsed >= 5, 8);
            var airIce = deposit.GetVector("_Deposits");
            var fireLights = stage.GetComponentsInChildren<Light>(true).Where(light => light.name == "Brazier light").ToArray();
            Check(snow != null && snow.particleCount >= 100 && airIce.x == 1 && airIce.y == 1 && airIce.z > .4f && airIce.z < .6f,
                  "Oxygen and nitrogen flakes did not accumulate on top of the dry ice.");
            Check(!torch.IsCarried && !torch.IsLit && fireLights.Length > 0 && fireLights.All(light => light.intensity == 0f),
                  "A held torch or brazier still burned without oxygen.");
            torch.ToggleCarrying();
            yield return Frames(4);
            Check(!torch.IsCarried && !torch.IsLit, "The torch could be relit after the atmosphere froze.");
            Capture("06-Air-Ice");
            yield return Until(() => journey.IsCapitalEnding, 15);
            yield return Frames(6);
            Check((Vector3)deposit.GetVector("_Deposits") == Vector3.one && stage.Combustion == 0f,
                  "The frozen ground or extinguished state was lost at the ending.");
            var root = stage.GetComponentInChildren<TravellerHud>().Root;
            Check(journey.IsCapitalEnding && SceneManager.sceneCount == 1 && root.Q("summary-overlay").resolvedStyle.display == DisplayStyle.Flex, "The chapter ending was not shown in Chaoge.");
            Check(root.Query<Label>().ToList().Any(l => l.text.Contains("137") && l.text.Contains("毁灭")) &&
                  root.Q<Button>("next-civilization").resolvedStyle.display == DisplayStyle.Flex, "Civilization 137 or relogin button missing.");
            report.observations = journey.Progress.ObservationsThisRun;
            Check(report.observations == 2, "Revival and the three stars were not recorded.");
            Capture("07-Civilization-137"); yield return Frames(6);

            Phase("Explicit relogin in the following civilization");
            journey.EnterNextCivilization();
            yield return Until(() => SceneManager.GetActiveScene().name == TravellerStage.SceneNames[2], 50);
            Check(SceneManager.GetActiveScene().name == TravellerStage.SceneNames[2], "Relogin did not load the medieval civilization.");
        }
        private void Enter(JourneyStep step) => typeof(JourneyProgress)
            .GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(journey.Progress, new object[] { step });
        private IEnumerator CheckVoice(string url)
        {
            Phase("Play a 24 kHz Qwen reply in Unity");
            var preferences = TravellerUserSettings.Current;
            preferences.gatewayUrl = url;
            preferences.connection.mode = "scripted";
            preferences.speakReplies = true;
            journey.Begin();
            Enter(JourneyStep.OpeningTalk);
            var dialogue = stage.GetComponent<NpcDialogueDirector>();
            var actor = stage.GetComponent<TravellerCastDirector>().companion;
            var identity = actor.GetComponent<NpcIdentity>();
            var point = actor.transform.position + new Vector3(0, stage.player.eyeHeight, -2.6f);
            stage.player.Teleport(point, identity.ConversationPoint);
            yield return Frames(6);
            if (!Check(dialogue.BeginPlayerQuestion() && dialogue.QuestionTarget == identity, "Could not lock the traveller as the speech recipient.")) yield break;
            Check(dialogue.SubmitQuestion("这里是什么地方？", identity, dialogue.QuestionId), "Could not submit the typed speech test.");
            yield return Until(() => dialogue.IsSpeaking && dialogue.CurrentSpeechSampleRate == 24000, 38);
            report.playedSampleRate = dialogue.CurrentSpeechSampleRate;
            Check(dialogue.IsSpeaking && report.playedSampleRate == 24000, "Unity did not play the Qwen PCM reply.");
            dialogue.CancelQuestion();
            journey.CloseDialogue();
        }
        private IEnumerator WalkRoute(JourneyStep step)
        {
            var controller = stage.player.GetComponent<CharacterController>();
            for (int i=0; i<=stage.profile.waterWaypoints.Length && journey.Progress.Step == step; i++)
            {
                var goal = journey.Goal;
                float until = Time.realtimeSinceStartup + 18;
                while (journey.Progress.Step == step && goal == journey.Goal &&
                       Vector3.ProjectOnPlane(goal - stage.player.transform.position, Vector3.up).magnitude > .30f &&
                       Time.realtimeSinceStartup < until)
                {
                    var start = stage.player.transform.position;
                    var delta = Vector3.ProjectOnPlane(goal - start, Vector3.up);
                    stage.player.FaceTarget(goal + Vector3.up * stage.player.eyeHeight);
                    controller.Move(Vector3.ClampMagnitude(delta, .20f) + Vector3.down * .08f);
                    report.metres += Vector3.Distance(start,stage.player.transform.position);
                    yield return null;
                }
                if (!Check(Vector3.ProjectOnPlane(goal-stage.player.transform.position,Vector3.up).magnitude < 1.25f &&
                           Mathf.Abs(goal.y-stage.player.transform.position.y)<.8f,
                           "Route blocked at " + stage.player.transform.position + " toward " + goal)) yield break;
                yield return Frames(3);
                if (journey.Progress.Step == step && journey.Goal == goal) yield break;
            }
        }
        private static IEnumerator Frames(int n) { while(n-- > 0) yield return null; }
        private static IEnumerator Until(Func<bool> pred, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!pred() && Time.realtimeSinceStartup < until) yield return null;
        }
        private bool Check(bool ok, string error) { report.checks++; if (!ok) report.errors.Add(error); Save(); return ok; }
        private void Phase(string name) { report.phase = name; Save(); }
        private void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output), name + ".png"));
        private void Save() => File.WriteAllText(output, JsonUtility.ToJson(report, true));
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) { report.errors.Add(message); Save(); }
        }
    }
}
#endif
