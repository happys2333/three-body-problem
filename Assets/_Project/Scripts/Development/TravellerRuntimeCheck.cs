#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem.Editor
{
    // Added only during a requested Play-mode check; never serialized into a scene or player.
    public sealed class TravellerRuntimeCheck : MonoBehaviour
    {
        [Serializable] public class Metric { public string scene; public float averageFrameMs; public float p95FrameMs; public float mainThreadMs; public int width; public int height; }
        [Serializable] public class Report { public bool passed; public int checkedMoments; public int recordedObservations; public string gpu; public string measurement = "Unity Editor Play mode, no idle throttling, VSync disabled during sample"; public List<string> errors = new List<string>(); public List<Metric> metrics = new List<Metric>(); }
        private const string JournalKey = "ThreeBodyTraveller.Journal.v1";
        private readonly Report report = new Report();
        private bool journalExisted;
        private string savedJournal;
        private bool restored;
        private bool journalCaptured;
        private int previousIdle;
        private int previousMode;
        private int previousVSync;
        private bool interactionCaptured;

        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode before starting the runtime check.");
            var value = new GameObject("Traveller Runtime QA") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(value);
            var probe = value.AddComponent<TravellerRuntimeCheck>();
            probe.previousIdle = EditorPrefs.GetInt("ApplicationIdleTime", 4);
            probe.previousMode = EditorPrefs.GetInt("InteractionMode", 0);
            probe.previousVSync = QualitySettings.vSyncCount;
            probe.interactionCaptured = true;
            EditorPrefs.SetInt("ApplicationIdleTime", 0);
            EditorPrefs.SetInt("InteractionMode", 1);
            UpdateInteractionMode();
            QualitySettings.vSyncCount = 0;
        }

        private IEnumerator Start()
        {
            journalExisted = PlayerPrefs.HasKey(JournalKey); savedJournal = PlayerPrefs.GetString(JournalKey, ""); journalCaptured = true;
            Application.logMessageReceived += OnLog;
            report.gpu = SystemInfo.graphicsDeviceName;
            Application.runInBackground = true;
            for (int sceneIndex = 0; sceneIndex < TravellerStage.SceneNames.Length; sceneIndex++)
            {
                var current = UnityEngine.Object.FindFirstObjectByType<TravellerStage>();
                if (current.profile.sceneIndex != sceneIndex)
                {
                    current.LoadScene(sceneIndex);
                    float deadline = Time.realtimeSinceStartup + 30f;
                    while (SceneManager.GetActiveScene().name != TravellerStage.SceneNames[sceneIndex] && Time.realtimeSinceStartup < deadline) yield return null;
                }
                yield return null; yield return null;
                var stage = UnityEngine.Object.FindFirstObjectByType<TravellerStage>();
                if (stage == null || stage.profile.sceneIndex != sceneIndex) { report.errors.Add("Scene switch did not complete."); break; }
                var hud = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
                if (hud.rootVisualElement.childCount < 7 || hud.rootVisualElement.Q<Button>() == null) report.errors.Add("HUD did not initialize in " + SceneManager.GetActiveScene().name);
                if (sceneIndex > 0 && stage.Journal.entries.Count == 0) report.errors.Add("Journal did not persist across scene switch.");
                for (int moment = 0; moment < stage.profile.moments.Length; moment++)
                {
                    stage.ApplyMoment(moment, true);
                    yield return null; yield return null;
                    report.checkedMoments++;
                    if (RenderSettings.skybox.GetFloat("_SunCount") != stage.Current.sunCount) report.errors.Add("Sky count differs from authored moment.");
                    if (!string.IsNullOrEmpty(stage.Current.observation))
                    {
                        stage.RecordObservation();
                        if (!stage.Journal.entries.Exists(entry => entry.category == "亲眼观察" && entry.text == stage.Current.observation)) report.errors.Add("Observation not saved: " + stage.Current.title);
                        report.recordedObservations++;
                    }
                    if (moment == 1) ScreenCapture.CaptureScreenshot("Logs/TravellerQA/runtime_" + sceneIndex + ".png");
                }
                stage.ApplyMoment(0, true);
                for (int warmup = 0; warmup < 30; warmup++) yield return null;
                var samples = new List<float>();
                using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 120))
                {
                    for (int frame = 0; frame < 120; frame++) { yield return null; samples.Add(Time.unscaledDeltaTime * 1000f); }
                    samples.Sort(); float sum = 0f; foreach (float sample in samples) sum += sample;
                    report.metrics.Add(new Metric { scene = SceneManager.GetActiveScene().name, averageFrameMs = sum / samples.Count, p95FrameMs = samples[(int)(samples.Count * 0.95f)], mainThreadMs = recorder.Valid ? recorder.LastValue / 1000000f : -1f, width = Screen.width, height = Screen.height });
                }
            }
            RestoreJournal();
            var operation = SceneManager.LoadSceneAsync(TravellerStage.SceneNames[0]);
            while (operation != null && !operation.isDone) yield return null;
            report.passed = report.errors.Count == 0 && report.checkedMoments == 12;
            Directory.CreateDirectory("Logs/TravellerQA");
            File.WriteAllText("Logs/TravellerQA/runtime-check.json", JsonUtility.ToJson(report, true));
            Debug.Log("TRAVELLER_RUNTIME_QA: " + (report.passed ? "PASS" : "FAIL") + "; checked " + report.checkedMoments + " moments and " + report.recordedObservations + " observation entries.");
            EditorApplication.isPlaying = false;
        }

        private void OnLog(string message, string trace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Assert || type == LogType.Exception) && report.errors.Count < 20) report.errors.Add(message);
        }

        private void RestoreJournal()
        {
            if (restored || !journalCaptured) return;
            if (journalExisted) PlayerPrefs.SetString(JournalKey, savedJournal); else PlayerPrefs.DeleteKey(JournalKey);
            PlayerPrefs.Save(); restored = true;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            RestoreJournal();
            if (interactionCaptured)
            {
                EditorPrefs.SetInt("ApplicationIdleTime", previousIdle);
                EditorPrefs.SetInt("InteractionMode", previousMode);
                QualitySettings.vSyncCount = previousVSync;
                UpdateInteractionMode();
            }
        }

        private static void UpdateInteractionMode() => typeof(EditorApplication).GetMethod("UpdateInteractionModeSettings", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }
}
#endif
