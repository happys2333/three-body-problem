#if TRAVELLER_STORY_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    // Included only in the separate QA player. Uses the normal settings UI,
    // movement controller, model decisions and story timers without selecting steps.
    public sealed class WindowsStoryPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Turn
        {
            public string npc, phase, question, reply, failure;
            public bool online;
            public int gatewayStatus, providerStatus;
            public float seconds;
        }
        [Serializable] private sealed class Report
        {
            public bool passed, authenticated, gatewayReady, development;
            public int checks, observations, realModelReplies, npcToolConfirmations, transientRetries;
            public string platform, graphicsDevice, model, apiHost, finalStep;
            public List<string> errors = new(), visited = new(), screenshots = new();
            public List<Turn> turns = new();
        }
        private readonly Report report = new();
        private string output, previousStep;
        private NpcApiConnection connection;
        private TravellerStage stage;
        private TravellerJourney journey;
        private TravellerHud hud;
        private NpcDialogueDirector director;
        private float started, nextWrite;
        private bool finished;
        private bool lastAttemptSucceeded;
        private string lastAttemptFailure;
        private int requestSnapshotCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string path = Option("-windows-story-qa");
            if (path == null || FindFirstObjectByType<WindowsStoryPlayerCheck>() != null) return;
            var root = new GameObject("Windows real-model story verification"); DontDestroyOnLoad(root);
            root.AddComponent<WindowsStoryPlayerCheck>().output = path;
        }

        private IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            connection = JsonUtility.FromJson<NpcApiConnection>(File.ReadAllText(Option("-story-qa-config")));
            Application.logMessageReceived += Log;
            Application.runInBackground = true; Application.targetFrameRate = 30; QualitySettings.vSyncCount = 0;
            report.platform = Application.platform.ToString(); report.graphicsDevice = SystemInfo.graphicsDeviceName;
            report.model = connection.model; report.apiHost = new Uri(connection.apiUrl).Host; report.development = Debug.isDebugBuild;
            Check(Application.platform == RuntimePlatform.WindowsPlayer, "The full story must run in the Windows player.");
            Check(!Debug.isDebugBuild, "The QA player must use the release player configuration.");
            yield return Frames(12); Bind();
            Check(journey.IsTitle, "The game did not open on the title."); Capture("01_title");
            yield return Frames(3);
            Click(hud.Root.Q<Button>("start-journey")); yield return Frames(3);
            Check(journey.IsTitle && hud.SettingsPanel.IsOpen, "Missing AI configuration did not block Start.");
            yield return WaitForService();
            var ui = hud.SettingsPanel.Root;
            ui.Q<DropdownField>("settings-provider").index = connection.mode == "deepseek" ? 0 : 1;
            ui.Q<TextField>("settings-api-url").value = connection.apiUrl;
            ui.Q<TextField>("settings-model").value = connection.model;
            ui.Q<IntegerField>("settings-timeout").value = connection.timeoutSeconds;
            ui.Q<TextField>("settings-api-key").value = "deliberately-invalid-story-qa-key";
            Click(ui.Q<Button>("settings-test"));
            yield return Wait(() => ui.Q<Button>("settings-test").enabledSelf, connection.timeoutSeconds + 10, "Invalid-key test did not finish.");
            Check(journey.IsTitle && !ui.Q<Label>("settings-status").ClassListContains("settings-good"), "Invalid API credentials unlocked the journey.");
            ui.Q<TextField>("settings-api-key").value = connection.apiKey;
            Click(ui.Q<Button>("settings-test"));
            yield return Wait(() => ui.Q<Button>("settings-test").enabledSelf, connection.timeoutSeconds + 10, "Real model connection test did not finish.");
            Check(ui.Q<Label>("settings-status").ClassListContains("settings-good"), "Real model connection failed: " + ui.Q<Label>("settings-status").text);
            Check(ui.Q<TextField>("settings-api-key").isPasswordField, "The API key was exposed in the UI.");
            Capture("02_ai_verified");
            yield return Frames(3);
            Click(ui.Q<Button>("settings-graphics-tab"));
            ui.Q<DropdownField>("settings-graphics-quality").value = "低";
            ui.Q<DropdownField>("settings-resolution").value = "960 × 540";
            Click(ui.Q<Button>("settings-save")); yield return Frames(5);
            Check(!hud.SettingsPanel.IsOpen, "Verified settings could not be saved.");
            Check(!PlayerPrefs.GetString(TravellerUserSettings.StorageKey).Contains(connection.apiKey), "The API key was persisted.");
            report.authenticated = true;
            Click(hud.Root.Q<Button>("start-journey")); yield return Frames(3);
            Check(journey.Progress.Step == JourneyStep.Awakening, "Verified Start did not begin the awakening.");
            yield return Wait(() => journey.Progress.Step == JourneyStep.ExploreDesert, 20, "Awakening did not finish.");
            Capture("03_gobi_night");

            Vector3 start = stage.player.transform.position;
            yield return Walk(start + Vector3.right * 5, 30);
            yield return Walk(stage.profile.gobiStory.meetingPosition + Vector3.back * 2, 40);
            yield return Wait(() => journey.Progress.Step == JourneyStep.OpeningTalk, 15, "Approaching the traveller did not start the encounter.");
            foreach (string question in new[] { "这里发生了什么？我应该待在哪里才安全？", "我跟着你，不会独自乱走。这天上的太阳会突然出来吗？", "我明白了，先留在你身边。" })
            {
                if (journey.Progress.Step != JourneyStep.OpeningTalk) break;
                yield return Talk("wenwang", question);
                yield return Delay(4);
            }
            yield return Wait(() => journey.Progress.Step == JourneyStep.SeekShelter, 90, "The model did not bring on sunrise.");
            Capture("04_giant_sun");
            foreach (Vector3 point in new[] { new Vector3(-10, 0, 3), new Vector3(-16, 0, 8), stage.shelterCenter.position })
                yield return Walk(point, 35);
            yield return Wait(() => journey.Progress.Step == JourneyStep.ShelterTalk, 25, "Real shade did not enable shelter dialogue.");
            yield return Wait(() => journey.Progress.KnowsIdentity && journey.Progress.AskedPlayerOrigin, 25, "The traveller did not introduce himself.");
            Capture("05_shelter");
            yield return Talk("wenwang", "我刚才看见太阳突然变得很大，岩石外被晒得发烫。现在我跟你躲在阴影里。这个亲眼所见可以记下来吗？");
            foreach (string question in new[] { "我记不清自己怎么来到这里，只想找个能安身的地方。我愿意跟你一起去朝歌。", "我明白了，等日光退去再走；我没有其他问题，会跟紧你的火光。", "我准备好了，等天黑后一起赶路。" })
            {
                if (journey.Progress.Step != JourneyStep.ShelterTalk) break;
                yield return Talk("wenwang", question); yield return Delay(4);
            }
            yield return Wait(() => journey.Progress.Step == JourneyStep.WalkToChaoge, 120, "The model did not end the shelter talk and sunset.");
            foreach (Vector3 point in new[] { new Vector3(-15,0,18),new Vector3(-14,0,26),new Vector3(-12,0,31),new Vector3(-1,0,42),new Vector3(-1,0,64) })
            {
                yield return Walk(point, 50);
                if (journey.Progress.Step == JourneyStep.WalkToChaoge && stage.MomentIndex >= 3)
                {
                    journey.FaceGoal(); yield return Delay(1); stage.RecordObservation();
                }
            }
            yield return WaitScene(1, 60); Capture("06_chaoge_arrival");
            foreach (var point in stage.profile.courtWaypoints) yield return Walk(point, 90);
            yield return Wait(() => journey.Progress.Step == JourneyStep.CourtTalk, 30, "The palace approach did not reach court dialogue.");
            yield return Wait(() => journey.AudienceReady, 180, "The petition handoff did not finish.");
            Capture("07_court_petition");
            foreach (string question in new[] {
                "表章已经呈毕。我建议沙漏每过一格就记下天窗光斑的位置，连续记录十次，再与历法预测的日影逐次比对；日照吻合后再让百姓复苏，请大王准许按此查验。",
                "臣愿按刚才所说的办法，每一格沙漏记录一次日影，并与表章的预测一一比对，遵照大王的安排查验。",
                "臣已明白这个查验安排，愿立即配合周文王，以天窗光斑和沙漏留下连续记录。" })
            {
                if (journey.Progress.Step != JourneyStep.CourtTalk) break;
                yield return Talk("king", question); yield return Delay(15);
            }
            yield return Wait(() => journey.Progress.Step == JourneyStep.CalendarWait || journey.Progress.Step == JourneyStep.WalkToWater, 75, "The king never granted calendar verification.");
            Capture("08_calendar");
            yield return Wait(() => journey.Progress.Step == JourneyStep.WalkToWater, 90, "Calendar verification did not finish.");
            foreach (var point in stage.profile.waterWaypoints) yield return Walk(point, 75);
            yield return Wait(() => journey.Progress.Step == JourneyStep.ObserveRevival, 20, "The pool approach did not start revival.");
            yield return Wait(() => stage.IsRevivalComplete, 90, "The citizens did not finish revival.");
            Observe(); Capture("09_revival");
            yield return Wait(() => journey.IsLongNightWalk, 30, "The long night did not begin.");
            while (journey.Progress.Step == JourneyStep.ReturnToCourt) yield return Walk(journey.Goal, 75);
            yield return Wait(() => journey.Progress.Step == JourneyStep.CalendarFailed, 20, "The failed calendar audience did not start.");
            Capture("10_failed_calendar");
            yield return Wait(() => journey.Progress.Step == JourneyStep.LeaveLongNightCourt, 50, "The king and King Wen did not leave the game.");
            var cast = stage.GetComponent<TravellerCastDirector>();
            Check(!cast.king.gameObject.activeSelf && !cast.companion.gameObject.activeSelf, "The king or King Wen remained visible after EXIT.");
            while (journey.Progress.Step == JourneyStep.LeaveLongNightCourt) yield return Walk(journey.Goal, 75);
            yield return Wait(() => journey.Progress.Step == JourneyStep.ObserveLongNight, 20, "The player could not leave the frozen court.");
            Observe(); Capture("11_three_flying_stars");
            yield return Wait(() => journey.Progress.Step == JourneyStep.AtmosphereFreeze, 45, "Snow did not progress to atmospheric freezing.");
            yield return Delay(2);
            Check(stage.Combustion < 1, "Combustion did not weaken as the atmosphere froze.");
            Capture("12_blue_snow");
            yield return Wait(() => journey.IsCapitalEnding, 30, "Civilization 137 did not reach its destruction ending.");
            Check(hud.Root.Q("summary-overlay").resolvedStyle.display == DisplayStyle.Flex, "Civilization 137 ending UI is missing.");
            Check(stage.Combustion <= .001f && stage.GetComponentsInChildren<TorchBearer>(true).All(torch => !torch.IsLit), "Torches remained lit after atmospheric freezing.");
            yield return Delay(2);
            Capture("13_civilization_137");
            yield return Frames(4);
            var next = hud.Root.Q<Button>("next-civilization");
            Check(next.enabledInHierarchy && next.worldBound.width > 0 && hud.Root.worldBound.Contains(next.worldBound.center), "The next-civilization button cannot be reached.");
            Click(next); yield return WaitScene(2, 60);
            yield return Walk(new Vector3(0,0,22), 100);
            yield return Wait(() => journey.Progress.Step == JourneyStep.TribunalTalk, 20, "The medieval tribunal did not begin.");
            Capture("14_medieval_tribunal");
            foreach (string question in new[] {
                "我猜飞星可能是远处的太阳，异常的日照可能与它们的远近变化有关。这只是待查验的解释；我们可以先走出去共同观察，再与飞星记录对照，不急着把猜测当成定论。",
                "我同意暂不作定论，先一起出门亲眼观察日照，再把所见与已有的飞星记录比较。",
                "我愿意按刚才讲明的安排出去观察，保留疑问，并认真记录眼前的天象。" })
            {
                if (journey.Progress.Step != JourneyStep.TribunalTalk) break;
                yield return Talk("scholar", question); yield return Delay(15);
            }
            yield return Wait(() => journey.Progress.Step == JourneyStep.WalkOutside, 60, "The tribunal did not allow exterior observation.");
            yield return Walk(new Vector3(0,0,22), 40);
            yield return Walk(new Vector3(0,0,-49), 100);
            yield return Wait(() => journey.Progress.Step == JourneyStep.ObserveThreeSuns, 20, "The exterior approach did not expose the three suns.");
            Observe(); Capture("15_three_suns");
            yield return Wait(() => journey.Progress.Step == JourneyStep.Summary, 45, "The final collapse did not reach Summary.");
            Check(journey.Progress.ObservationsThisRun >= 3, "The main route is missing its required observations.");
            Check(hud.Root.Q("summary-overlay").resolvedStyle.display == DisplayStyle.Flex, "The final summary UI is missing.");
            Capture("16_summary");
            yield return Frames(4);
            report.finalStep = journey.Progress.Step.ToString(); report.observations = journey.Progress.ObservationsThisRun;
            report.npcToolConfirmations = stage.Journal.entries.Count(entry => !string.IsNullOrEmpty(entry.confirmedBy));
            report.passed = report.errors.Count == 0; finished = true; Write(); Application.Quit(report.passed ? 0 : 1);
        }

        private void Bind()
        {
            stage = FindFirstObjectByType<TravellerStage>(); journey = stage.Journey;
            hud = FindFirstObjectByType<TravellerHud>(); director = stage.GetComponent<NpcDialogueDirector>();
            stage.player.enabled = false;
        }
        private IEnumerator WaitForService()
        {
            float until = Time.realtimeSinceStartup + 25;
            while (Time.realtimeSinceStartup < until)
            {
                using (var web = UnityWebRequest.Get(WindowsDialogueService.Url + "/health"))
                {
                    web.timeout = 3; yield return web.SendWebRequest();
                    if (web.result == UnityWebRequest.Result.Success) { report.gatewayReady = true; yield break; }
                }
                yield return Delay(1);
            }
            Check(false, "The packaged AI process did not start.");
        }
        private IEnumerator Talk(string npcId, string question)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                yield return TalkAttempt(npcId, question);
                if (lastAttemptSucceeded) yield break;
                bool transient = lastAttemptFailure == "rate_limited" || lastAttemptFailure == "timeout" || lastAttemptFailure == "unreachable" || lastAttemptFailure == "unavailable";
                Check(transient && attempt < 5, "Real NPC response failed: " + director.Status);
                report.transientRetries++; Write();
                yield return Delay(30 * (attempt + 1));
            }
        }
        private IEnumerator TalkAttempt(string npcId, string question)
        {
            lastAttemptSucceeded = false; lastAttemptFailure = "";
            director.CancelQuestion(); hud.Root.Q<TextField>("chat-input").Blur(); journey.CloseDialogue();
            var npc = stage.GetComponentsInChildren<NpcIdentity>(true).First(value => value.persona != null && value.persona.characterId == npcId && value.CanAnswer);
            if (Vector3.Distance(stage.player.transform.position, npc.transform.position) > 5)
            {
                Vector3 point = npc.transform.position + npc.transform.forward * 2.5f;
                stage.player.Teleport(point + Vector3.up * stage.player.eyeHeight, npc.ConversationPoint);
                yield return Frames(3);
            }
            stage.player.FaceTarget(npc.ConversationPoint); yield return Frames(2);
            hud.BeginTextChat(); yield return Frames(2);
            Check(director.Current != null && director.Current.persona.characterId == npcId, "The input selected the wrong NPC for " + npcId);
            string phase = journey.Progress.Step.ToString();
            hud.Root.Q<TextField>("chat-input").value = question; Click(hud.Root.Q<Button>("send-chat"));
            Check(director.IsBusy, "The typed question did not start a model request.");
            yield return Wait(() => !director.IsBusy, connection.timeoutSeconds + 15, "The model request did not finish.");
            report.turns.Add(new Turn { npc = npcId, phase = phase, question = question, reply = director.LastReply, failure = director.FailureCode, online = !director.UsingFallback,
                gatewayStatus = director.LastGatewayStatus, providerStatus = director.LastProviderStatus, seconds = director.LastRequestSeconds });
            lastAttemptFailure = director.FailureCode;
            if (director.UsingFallback || !string.IsNullOrEmpty(director.FailureCode))
            {
                hud.Root.Q<TextField>("chat-input").Blur(); journey.CloseDialogue(); Write(); yield break;
            }
            Check(!string.IsNullOrWhiteSpace(director.LastReply), "The model returned empty player dialogue.");
            lastAttemptSucceeded = true;
            report.realModelReplies++; Write();
            yield return Wait(() => journey.Progress.Step.ToString() != phase || !director.IsVocalising && journey.SpeechRemainingSeconds <= 0, 50, "NPC speech did not finish.");
            hud.Root.Q<TextField>("chat-input").Blur(); journey.CloseDialogue(); yield return Frames(2);
        }
        private IEnumerator Walk(Vector3 point, float seconds)
        {
            var player = stage.player;
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                if (stage.IsLoading) yield break;
                Vector3 delta = Vector3.ProjectOnPlane(point - player.transform.position, Vector3.up);
                if (delta.magnitude < .65f) { yield return Frames(3); yield break; }
                player.FaceTarget(player.viewCamera.transform.position + delta);
                float dt = Mathf.Min(.2f, Time.unscaledDeltaTime);
                float distance = player.walkSpeed * player.runMultiplier * Mathf.Max(.001f, dt);
                player.MoveInput(Vector2.up * Mathf.Min(1, delta.magnitude / distance), true, dt);
                yield return null;
            }
            Check(false, "Movement blocked in " + journey.Progress.Step + ": " + player.transform.position + " -> " + point);
        }
        private IEnumerator WaitScene(int index, float seconds)
        {
            yield return Wait(() => SceneManager.GetActiveScene().name == TravellerStage.SceneNames[index], seconds, "The chapter scene did not load: " + index);
            yield return Frames(10); Bind(); Check(!journey.IsTitle, "Changing chapters returned to the title.");
        }
        private void Observe() { journey.FaceGoal(); stage.RecordObservation(); }
        private IEnumerator Wait(Func<bool> ready, float seconds, string error)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < until) yield return null;
            Check(ready(), error + " Current phase: " + journey?.Progress.Step);
        }
        private static IEnumerator Delay(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        private static IEnumerator Frames(int frames) { for (int i=0;i<frames;i++) yield return null; }
        private void Update()
        {
            if (finished || output == null) return;
            if (journey != null && previousStep != journey.Progress.Step.ToString())
            {
                previousStep = journey.Progress.Step.ToString(); report.visited.Add(previousStep); report.finalStep = previousStep;
                Debug.Log("WINDOWS_STORY_QA: " + previousStep); Write();
            }
            if (Time.realtimeSinceStartup >= nextWrite) { nextWrite = Time.realtimeSinceStartup + 5; Write(); }
            if (Time.realtimeSinceStartup - started > 1800) Check(false, "Windows story verification exceeded 30 minutes.");
        }
        private void Capture(string name)
        {
            string file = Path.Combine(Path.GetDirectoryName(output), name + ".png");
            ScreenCapture.CaptureScreenshot(file); report.screenshots.Add(name + ".png");
        }
        public static void TraceRequest(string npc, string json)
        {
            var check = FindFirstObjectByType<WindowsStoryPlayerCheck>();
            if (check == null || string.IsNullOrEmpty(check.output)) return;
            string folder = Path.Combine(Path.GetDirectoryName(check.output), "requests");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, (++check.requestSnapshotCount).ToString("D3") + "_" + npc + ".json"), json);
        }
        private void Check(bool value, string error)
        {
            report.checks++;
            if (!value) throw new InvalidOperationException(error);
        }
        private void Log(string message, string trace, LogType type)
        {
            if (finished || type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string safe = connection != null && !string.IsNullOrEmpty(connection.apiKey) ? message.Replace(connection.apiKey, "[REDACTED]") : message;
            report.errors.Add(safe); finished = true; report.passed = false; Write(); Application.Quit(1);
        }
        private void Write()
        {
            if (journey != null) { report.finalStep = journey.Progress.Step.ToString(); report.observations = journey.Progress.ObservationsThisRun; }
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        private static void Click(Button button)
        {
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
        private static string Option(string name) { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    }
}
#endif
