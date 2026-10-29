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
    // Opt-in integration check; never created during an ordinary game launch.
    public sealed class TravellerJourneyPlayerCheck : MonoBehaviour
    {
        [Serializable] private class Report { public bool passed; public int checks; public List<string> errors = new List<string>(); public List<string> resolutions = new List<string>(); }
        private readonly Report report = new Report();
        private string output, savedJournal;
        private bool hadJournal, captured;
        private TravellerStage stage;
        private TravellerJourney journey;
        private TravellerHud hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-traveller-entry-check");
            if (index < 0) return;
            var value = new GameObject("Development Journey Check"); DontDestroyOnLoad(value);
            value.AddComponent<TravellerJourneyPlayerCheck>().output = args[index + 1];
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            hadJournal = PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1"); savedJournal = PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1", ""); captured = true;
            Application.logMessageReceived += OnLog; Application.runInBackground = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
            yield return Frames(4); Bind();
            foreach (var size in new[] { new Vector2Int(960,540), new Vector2Int(1280,720), new Vector2Int(1920,1080), new Vector2Int(1024,768) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed); yield return Frames(8);
                report.resolutions.Add(Screen.width + "x" + Screen.height);
                Check(hud.Root.Q("title-overlay").resolvedStyle.display == DisplayStyle.Flex, "The game must open at the title.");
                Check(hud.Root.Q("debug-overlay").resolvedStyle.display == DisplayStyle.None, "Scene tools leaked into the game entry.");
                Check(hud.Root.Q("gameplay").resolvedStyle.display == DisplayStyle.None, "Gameplay HUD is drawn underneath the title.");
                CheckInside(hud.Root.Q("title-card"));
                ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"title_" + Screen.width + "x" + Screen.height + ".png")); yield return Frames(3);
            }
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed); yield return Frames(8);
            journey.Begin(); yield return Frames(3);
            Check(journey.Progress.Step == JourneyStep.SeekShelter, "Start did not begin the opening encounter.");
            Check(!stage.player.InputBlocked, "The player cannot walk after Start.");
            Check(hud.Root.Q("debug-overlay").resolvedStyle.display == DisplayStyle.None, "Preview tools remain visible after Start.");
            Transform actor = FindTraveller(); Vector3 actorBefore = actor.position;
            yield return Frames(90); Check(Vector3.Distance(actor.position,actorBefore) > 0.5f, "The companion does not lead the player.");
            CheckGameplayLayout();
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"opening.png")); yield return Frames(3);
            Check(stage.Current.sunCount==0,"The revised opening must begin at night.");
            journey.Progress.Tick(stage.profile.gobiStory.nightMeetingSeconds+stage.profile.transitionSeconds+1);yield return Frames(3);
            actor.position = new Vector3(-16f,0f,13f); yield return GoToGoal();
            Check(journey.Progress.Step == JourneyStep.ShelterTalk, "Entering the shelter did not start the conversation opportunity.");
            journey.Interact(); yield return Frames(2); Check(journey.DialogueOpen, "Nearby traveller cannot be spoken to.");
            Check(stage.player.InputBlocked, "Dialogue does not release movement for the choices.");
            journey.SelectDialogue("identity"); yield return Frames(2);
            Check(journey.Progress.KnowsIdentity, "Identity was not revealed by the answer.");
            CheckInside(hud.Root.Q("dialogue"));
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"conversation.png")); yield return Frames(3);
            Vector3 playerBefore = stage.player.transform.position; journey.SelectDialogue("continue"); yield return Frames(2);
            Check(Vector3.Distance(playerBefore,stage.player.transform.position) < 0.5f, "Changing the sky teleported the player.");
            Check(journey.Progress.Step==JourneyStep.ShelterTalk,"Closing dialogue skipped the configured shelter wait.");
            journey.Progress.Tick(stage.profile.gobiStory.shelterWaitSeconds);journey.Progress.Tick(stage.profile.gobiStory.suddenSunsetSeconds);yield return Frames(3);
            stage.player.Teleport(new Vector3(2,1.75f,42),stage.profile.moments[3].viewTarget);stage.ApplyMoment(3,true,false);Observe();yield return Frames(2);
            Check(journey.Progress.Step==JourneyStep.WalkToChaoge,"Optional star observation interrupted automatic travel.");
            stage.player.Teleport(new Vector3(2,1.75f,64),stage.profile.moments[4].viewTarget);stage.ApplyMoment(4,true,false);Observe();yield return Frames(2);
            Check(journey.Progress.ObservationsThisRun==2,"Optional observations were not recorded separately.");
            journey.Progress.AddNightWalking(stage.profile.gobiStory.minimumNightWalkMetres);journey.Progress.Tick(stage.profile.gobiStory.minimumNightWalkSeconds);
            actor.position=new Vector3(-1,0,64);yield return Frames(3);
            Check(stage.IsLoading&&hud.Root.Q("travel-overlay").resolvedStyle.display==DisplayStyle.Flex,"The completed night walk did not automatically show the travel transition.");
            yield return WaitForScene(1);
            Check(stage.player.transform.position.z < -220f,"The capital must start outside its own city gate.");
            Check(SceneManager.sceneCount == 1,"Gobi remains loaded alongside the capital.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"capital_arrival.png")); yield return Frames(3);
            yield return GoToGoal();
            journey.Interact(); journey.SelectDialogue("verification"); journey.SelectDialogue("continue");
            float openingUntil=Time.realtimeSinceStartup+1.8f;while(Time.realtimeSinceStartup<openingUntil)yield return null;
            var skylight=UnityEngine.Object.FindFirstObjectByType<PalaceSkylight>();
            Check(skylight!=null&&skylight.Opening>0.99f,"The palace stone plate did not open during calendar verification.");
            Vector3 calendarPosition=stage.player.transform.position;journey.FaceGoal();
            Check(Vector3.Distance(calendarPosition,stage.player.transform.position)<0.01f,"Looking at the skylight result teleported the player.");
            Check(skylight!=null&&Vector3.Dot(stage.player.viewCamera.transform.forward,(skylight.observationPoint-stage.player.viewCamera.transform.position).normalized)>0.99f,"Calendar observation guidance misses the sunlight patch.");
            Check(skylight!=null&&!Physics.Linecast(stage.player.viewCamera.transform.position,skylight.observationPoint,~0,QueryTriggerInteraction.Ignore),"The calendar table or another object hides the sunlight patch.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"calendar_skylight.png"));yield return Frames(3);
            journey.Progress.Tick(7f); yield return Frames(2);
            yield return GoToGoal(); Observe(); yield return Frames(2);
            Check(journey.Progress.Step == JourneyStep.AfterRevival, "Revival observation missing.");
            journey.Interact(); journey.Progress.Tick(5f); yield return Frames(2); Observe(); yield return Frames(2);
            Check(journey.Progress.Step == JourneyStep.AfterLongNight, "Long-night counterexample missing.");
            journey.Interact(); yield return WaitForScene(2); yield return GoToGoal();
            journey.Interact(); journey.SelectDialogue("explain"); journey.SelectDialogue("continue"); yield return Frames(2);
            yield return GoToGoal(); journey.Progress.Tick(3f); Observe(); yield return Frames(2);
            Check(journey.Progress.Step == JourneyStep.AfterThreeSuns, "Three-sun observation missing.");
            journey.Interact(); journey.Progress.Tick(9f); yield return Frames(4);
            Check(journey.Progress.Step == JourneyStep.Summary && journey.Progress.ObservationsThisRun == 5, "The main route did not reach its evidence-based summary.");
            Check(hud.Root.Q("summary-overlay").resolvedStyle.display == DisplayStyle.Flex, "Summary UI is missing."); CheckInside(hud.Root.Q("summary-card"));
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"summary.png")); yield return Frames(4);
            Restore(); report.passed = report.errors.Count == 0; File.WriteAllText(output,JsonUtility.ToJson(report,true)); Application.Quit(report.passed ? 0 : 1);
        }

        private void Bind() { stage = UnityEngine.Object.FindFirstObjectByType<TravellerStage>(); journey = stage.Journey; hud = UnityEngine.Object.FindFirstObjectByType<TravellerHud>(); }
        private static IEnumerator Frames(int frames) { for (int i=0;i<frames;i++) yield return null; }
        private IEnumerator GoToGoal() { var point = journey.Goal; stage.player.Teleport(point + Vector3.up * 1.75f,point + new Vector3(0f,1.75f,10f)); yield return Frames(5); }
        private IEnumerator WaitForScene(int index)
        {
            float until = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name != TravellerStage.SceneNames[index] && Time.realtimeSinceStartup < until) yield return null;
            yield return Frames(5); Bind(); Check(stage.profile.sceneIndex == index,"Story scene transition failed.");
            Check(!journey.IsTitle,"The journey returned to the title while changing chapters.");
        }
        private void Observe() { journey.FaceGoal(); stage.RecordObservation(); }
        private Transform FindTraveller() { foreach(var t in stage.GetComponentsInChildren<Transform>()) if(t.name.StartsWith("周文王 [",StringComparison.Ordinal)) return t; return null; }
        private void CheckGameplayLayout()
        {
            var top=hud.Root.Q("top");var bottom=hud.Root.Q("bottom");
            Check(!top.worldBound.Overlaps(bottom.worldBound),"Gameplay top and bottom panels overlap."); CheckInside(top); CheckInside(bottom);
        }
        private void CheckInside(VisualElement element)
        {
            var outer=hud.Root.worldBound; var inner=element.worldBound;
            Check(inner.width>0f && inner.height>0f && inner.xMin>=outer.xMin-2 && inner.yMin>=outer.yMin-2 && inner.xMax<=outer.xMax+2 && inner.yMax<=outer.yMax+2,"Panel outside viewport: " + element.name + " " + inner);
        }
        private void Check(bool condition,string message) { report.checks++; if(!condition)report.errors.Add(message); }
        private void OnLog(string message,string trace,LogType type)
        {
            if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)return;
            report.errors.Add(message); File.WriteAllText(output,JsonUtility.ToJson(report,true));
        }
        private void Restore() { if(!captured)return; if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",savedJournal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();captured=false; }
        private void OnDestroy() { Application.logMessageReceived-=OnLog;Restore(); }
    }
}
#endif
