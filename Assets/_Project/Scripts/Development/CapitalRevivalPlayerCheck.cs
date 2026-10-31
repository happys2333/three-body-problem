#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem
{
    public sealed class CapitalRevivalPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int checks, participants;
            public string gpu;
            public int width, height, frameCap;
            public float meanFrameMs, p95FrameMs;
            public bool performanceBudgetPassed;
            public List<string> errors = new List<string>();
            public List<string> states = new List<string>();
        }
        private readonly Report report = new Report();
        private readonly List<float> frames = new List<float>();
        private string output;
        private TravellerStage stage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-capital-revival-check");
            if (option < 0 || option + 1 >= args.Length || FindFirstObjectByType<CapitalRevivalPlayerCheck>() != null) return;
            var root = new GameObject("Capital revival runtime verification"); DontDestroyOnLoad(root);
            root.AddComponent<CapitalRevivalPlayerCheck>().output = args[option + 1];
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var load = SceneManager.LoadSceneAsync(TravellerStage.SceneNames[1]); while (!load.isDone) yield return null;
            for (int i = 0; i < 15; i++) yield return null;
            stage = FindFirstObjectByType<TravellerStage>(); var pool = stage.GetComponent<CapitalRevivalDirector>();
            Check(pool != null, "Capital revival director missing.");
            if (pool == null) { Finish(); yield break; }
            if (!stage.Journey.IsPreviewing) stage.Journey.TogglePreview();
            stage.GetComponentInChildren<TravellerHud>().Root.style.display = UnityEngine.UIElements.DisplayStyle.None;
            stage.player.enabled = false; // Keep review viewpoints fixed; gravity must not move them.
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
            report.gpu = SystemInfo.graphicsDeviceName; report.width = Screen.width; report.height = Screen.height; report.frameCap = Application.targetFrameRate;
            report.participants = pool.citizens.Length; Check(report.participants == 7, "Expected seven participants.");
            var first = pool.citizens[0];
            var street = stage.GetComponentsInChildren<TravellerCrowdMember>().Where(m => !m.isGuard && !m.isReviving).ToArray();
            var scales = pool.citizens.Select(c => c.transform.localScale).ToArray();
            stage.ApplyMoment(0, true, false);
            stage.player.Teleport(first.transform.position + new Vector3(-4, 1.65f, -1), first.transform.position + Vector3.up * .65f);
            yield return Delay(.5f);
            Check(pool.citizens.All(c => c.Phase == RehydrationPhase.DryPaper && c.paperVisual.activeSelf), "Participants must start as paper bodies.");
            Check(pool.citizens.All(c => !c.GetComponent<Collider>().enabled && c.GetComponentsInChildren<SkinnedMeshRenderer>().All(s => !s.enabled)), "Dry paper must not retain standing bodies or capsules.");
            Check(street.All(c => !c.GetComponent<TorchBearer>().IsCarried && !c.GetComponent<TorchBearer>().IsLit), "Daytime civilians still carry torches.");
            Check(street.All(c => !c.GetComponent<TorchBearer>().Grip.gameObject.activeInHierarchy), "Daytime torch handles remain visible.");
            yield return Capture("01_DryPaper", first);
            stage.ApplyMoment(1, true, false);
            Check(!stage.RecordObservation(), "The result was recorded before anyone revived.");
            yield return Delay(.9f); yield return Capture("02_Wetting", first);
            yield return Until(() => first.Hydration >= .48f, 20);
            Check(first.Phase == RehydrationPhase.Inflating && first.Hydration < .85f, "The paper-to-volume transition was skipped.");
            Check(first.paperVisual.activeSelf, "Inflation must use a visible continuous body.");
            Check(pool.citizens.Any(c => c.Hydration < first.Hydration - .1f), "All citizens revived in synchrony.");
            yield return Capture("03_RegainingVolume", first);
            yield return Until(() => first.Phase == RehydrationPhase.GettingUp, 15);
            yield return Delay(3.5f);
            Check(first.GetComponent<TravellerActor>().CurrentMotion == pool.settings.getUpMotion, "The get-up animation is not playing.");
            Check(!first.paperVisual.activeSelf && first.GetComponentsInChildren<SkinnedMeshRenderer>().All(s => s.enabled), "Get-up handoff lost the actual skinned character.");
            yield return Capture("04_GettingUp", first);
            yield return Until(() => pool.IsComplete, 30);
            Check(pool.IsComplete, "The revival process never completes.");
            for (int i = 0; i < pool.citizens.Length; i++)
            {
                var citizen = pool.citizens[i];
                Check(citizen.transform.localScale == scales[i], "A character was inflated by scaling its whole root.");
                Check(citizen.GetComponent<Collider>().enabled && !citizen.GetComponent<TravellerActor>().PresentationControlled, "Recovered person did not regain normal state.");
            }
            bool recorded = false; Action<int> onRecorded = index => { if (index == 1) recorded = true; };
            stage.ObservationRecorded += onRecorded; stage.RecordObservation(); stage.ObservationRecorded -= onRecorded;
            Check(recorded, "The completed revival cannot be recorded."); yield return Capture("05_Recovered", first);

            var resident = street[0]; stage.player.Teleport(resident.transform.position + new Vector3(2,1.65f,-3), resident.transform.position + Vector3.up);
            yield return Delay(.6f); yield return Capture("06_CivilianDay", null);
            stage.ApplyMoment(2, true, false); yield return Delay(.6f);
            Check(street.All(c => c.GetComponent<TorchBearer>().IsCarried && c.GetComponent<TorchBearer>().IsLit), "Dry civilians fail to equip torches at night.");
            Check(pool.citizens.All(c => !c.GetComponent<TorchBearer>().IsCarried), "People still in the soaking pool carry lit torches.");
            var bearer = resident.GetComponent<TorchBearer>(); var actor = resident.GetComponent<TravellerActor>(); actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            yield return Delay(.5f);
            Check(Vector3.Distance(actor.animator.GetBoneTransform(HumanBodyBones.LeftHand).position, bearer.HandTarget) < .13f, "Night torch grip is not reachable.");
            yield return Capture("07_CivilianNight", null);
            stage.ApplyMoment(0, true, false); yield return Delay(.6f);
            Check(street.All(c => !c.GetComponent<TorchBearer>().IsCarried), "Torch carry pose persists after returning to daylight.");
            Check(pool.citizens.All(c => c.Phase == RehydrationPhase.DryPaper), "Replaying the capital does not reset dehydration.");
            var playerTorch=stage.player.bodyActor.GetComponent<TorchBearer>();
            playerTorch.ToggleCarrying();yield return Delay(.4f);
            Check(playerTorch.IsCarried&&playerTorch.IsLit,"The player cannot manually light a torch in daylight.");
            playerTorch.ToggleCarrying();yield return Delay(.4f);
            Check(!playerTorch.IsCarried&&!playerTorch.IsLit,"The player's manual torch cannot be put away.");
            yield return null; yield return null; Finish();
        }

        private IEnumerator Until(Func<bool> condition, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < until) { frames.Add(Time.unscaledDeltaTime * 1000); yield return null; }
            Check(condition(), "Timed out waiting for the next revival phase.");
        }
        private IEnumerator Delay(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) { frames.Add(Time.unscaledDeltaTime * 1000); yield return null; }
        }
        private IEnumerator Capture(string name, RehydratingCitizen citizen)
        {
            if (citizen != null) report.states.Add(name + ": " + citizen.Phase + " hydration=" + citizen.Hydration.ToString("F3"));
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output), name + ".png"));
            // The file is written after rendering. Keep camera/sky unchanged until the
            // queued frame is consumed rather than labelling the next shot as this one.
            yield return null; yield return null;
        }
        private void Check(bool value, string message) { report.checks++; if (!value) report.errors.Add(message); }
        private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message); }
        private void Finish()
        {
            if (frames.Count > 0) { frames.Sort(); report.meanFrameMs = frames.Sum() / frames.Count; report.p95FrameMs = frames[(int)(frames.Count * .95f)]; }
            report.performanceBudgetPassed = frames.Count > 0 && report.p95FrameMs <= 16.67f;
            report.passed = report.errors.Count == 0; File.WriteAllText(output, JsonUtility.ToJson(report, true)); Application.Quit(report.passed ? 0 : 1);
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
#endif
