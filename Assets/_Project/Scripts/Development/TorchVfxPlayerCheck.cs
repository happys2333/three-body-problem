#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ThreeBodyProblem
{
    // Runs against the real first-person controller, animated hand and saved torch.
    public sealed class TorchVfxPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Pose
        { public string id; public float wickError, upDot, handError; public Vector3 wickViewport; }
        [Serializable] private sealed class Report
        {
            public bool passed; public int checks; public string gpu;
            public float meanFrameMs, p95FrameMs, distanceWalked;
            public List<string> errors = new List<string>(); public List<Pose> poses = new List<Pose>();
        }
        private string output, journal; private bool hadJournal, captured, finished;
        private TravellerStage stage; private TravellerJourney journey; private TorchBearer bearer;
        private readonly Report report = new Report();
        public static TorchVfxPlayerCheck Run(string path)
        { var go = new GameObject("Development Torch VFX Check"); var check = go.AddComponent<TorchVfxPlayerCheck>(); check.output = path; return check; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-torch-vfx-check");
            if (i >= 0 && i + 1 < args.Length) Run(args[i + 1]);
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            hadJournal = PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1"); journal = PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1", ""); captured = true;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            report.gpu = SystemInfo.graphicsDeviceName; Application.logMessageReceived += OnLog;
            yield return Frames(12);
            stage = FindFirstObjectByType<TravellerStage>(); journey = stage.GetComponent<TravellerJourney>();
            journey.Begin(); stage.player.enabled = false; stage.player.PointerReleased = true;
            yield return new WaitForSecondsRealtime(stage.profile.gobiStory.wakeSeconds + .3f);
            bearer = stage.player.bodyActor.GetComponent<TorchBearer>();
            Check(bearer.IsLit && bearer.IsCarried, "First-person torch is not lit after awakening.");
            Vector3 eye = stage.player.viewCamera.transform.position;
            foreach (var pitch in new[] { 0f, -35f, 45f, -80f, 80f })
            {
                SetView(eye, pitch, 66.57f); yield return Frames(20);
                string id = "FirstPerson_" + (pitch == 0 ? "Level" : pitch < 0 ? "LookUp" : "LookDown") + (Mathf.Abs(pitch) > 70 ? "Extreme" : "");
                Record(id); Capture(id);
            }
            SetView(eye, 0, 150); yield return Frames(20); Record("FirstPerson_Turn"); Capture("FirstPerson_Turn");
            SetView(eye, 0, 66.57f); Vector3 start = stage.player.transform.position;
            for (int i = 0; i < 75; i++) { stage.player.MoveInput(Vector2.up, false, Time.deltaTime); yield return new WaitForEndOfFrame(); if (i % 15 == 0) Record("Walking_" + i); }
            report.distanceWalked = Vector3.Distance(start, stage.player.transform.position);
            Check(report.distanceWalked > 1.5f, "Real first-person controller did not walk."); Capture("FirstPerson_Walking");
            foreach (var other in stage.GetComponentsInChildren<TorchBearer>())
            {
                Check(other.IsLit, "Night-time companion torch failed to light.");
                Check(other.Grip.GetComponentInChildren<TorchFlameBillboard>() != null, "A carrier still uses the old fire.");
                var light = other.Grip.GetComponentInChildren<Light>();
                bool nearby = other == bearer || Vector3.Distance(stage.player.viewCamera.transform.position, other.transform.position) < 24;
                Check(light.enabled == nearby, "Night torch illumination disagrees with the existing 24-metre light budget.");
            }
            journey.Progress.DiscoverTraveller(); journey.Progress.MeetTraveller(); journey.Progress.RecordNpcAnswer(); journey.Progress.ChooseStoryIntent("sunrise");
            yield return new WaitForSecondsRealtime(stage.profile.gobiStory.sunriseSeconds + .3f);
            Check(!bearer.IsCarried && !bearer.IsLit, "Automatic daylight torch suppression regressed.");
            bearer.ToggleCarrying(); yield return Frames(12); Check(bearer.IsLit, "Manual daylight torch failed to light.");
            bearer.ToggleCarrying(); yield return Frames(12); Check(!bearer.IsLit, "Manual torch failed to extinguish.");
            foreach (var p in bearer.Grip.GetComponentsInChildren<ParticleSystem>(true)) Check(p.particleCount == 0, "Extinguishing left live particles behind.");
            // Return to night without changing saved scene data; preserve gameplay settings.
            bearer.equipped = true; journey.ReturnToTitle(); journey.Begin(); yield return new WaitForSecondsRealtime(stage.profile.gobiStory.wakeSeconds + .3f);
            SetView(stage.player.viewCamera.transform.position, 0, 66.57f); yield return Frames(30);
            var times = new List<float>();
            for (int i = 0; i < 180; i++) { yield return new WaitForEndOfFrame(); times.Add(Time.unscaledDeltaTime * 1000); }
            foreach (float t in times) report.meanFrameMs += t / times.Count;
            times.Sort(); report.p95FrameMs = times[Mathf.FloorToInt((times.Count - 1) * .95f)];
            Capture("FirstPerson_Final");
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output), "Gameplay_WithHUD.png"));
            yield return Frames(3);
            report.passed = report.errors.Count == 0; Restore(); finished = true;
            File.WriteAllText(output, JsonUtility.ToJson(report, true)); Application.logMessageReceived -= OnLog;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(report.passed ? 0 : 1);
#endif
        }
        private void SetView(Vector3 eye, float pitch, float yaw)
        { stage.player.Teleport(eye, eye + Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * 4); }
        private void Record(string id)
        {
            var flame = bearer.Grip.GetComponentInChildren<TorchFlameBillboard>();
            var p = new Pose { id = id, wickError = Vector3.Distance(flame.transform.position, bearer.Grip.TransformPoint(Vector3.up * .76f)),
                upDot = Vector3.Dot(flame.transform.up, Vector3.up), wickViewport = stage.player.viewCamera.WorldToViewportPoint(flame.transform.position),
                handError = Vector3.Distance(stage.player.bodyActor.animator.GetBoneTransform(HumanBodyBones.LeftHand).position, bearer.HandTarget) };
            report.poses.Add(p); Check(p.wickError < .016f, id + ": fire has separated from the wick.");
            Check(p.upDot > .9999f, id + ": fire is pitching with the handle or camera.");
            Check(p.handError < .08f, id + ": left hand no longer grips the torch.");
            Check(p.wickViewport.z > stage.player.viewCamera.nearClipPlane && p.wickViewport.x > .04f && p.wickViewport.x < .45f && p.wickViewport.y > .04f && p.wickViewport.y < .6f, id + ": first-person wick has left the lower-left view.");
        }
        private void Capture(string name)
        {
            var camera = stage.player.viewCamera; var target = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture; var active = RenderTexture.active; Texture2D texture = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture = new Texture2D(1600, 900, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output), name + ".png"), texture.EncodeToPNG());
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; Destroy(target); if (texture != null) Destroy(texture); }
        }
        private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return new WaitForEndOfFrame(); }
        private void Check(bool value, string error) { report.checks++; if (!value) report.errors.Add(error); }
        private void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(message); }
        private void Restore() { if (!captured) return; if (hadJournal) PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1", journal); else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1"); PlayerPrefs.Save(); captured = false; }
        private void OnDestroy() { Restore(); Application.logMessageReceived -= OnLog; if (!finished) Debug.Log("Torch VFX check stopped before its final report."); }
    }
}
#endif
