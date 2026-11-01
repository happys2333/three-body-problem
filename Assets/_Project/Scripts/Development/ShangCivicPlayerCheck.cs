#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem
{
    // Explicit opt-in native-player verification. Normal game launches never
    // instantiate this runner or change the story/player controls.
    public sealed class ShangCivicPlayerCheck : MonoBehaviour
    {
        [Serializable] private class Metric { public string view; public float meanMs, p95Ms, warmupMaxMs; public bool budgetPassed; }
        [Serializable] private class Report
        {
            public bool passed;
            public int checks, width, height;
            public string gpu;
            public List<string> errors = new List<string>();
            public List<Metric> performance = new List<Metric>();
        }
        private readonly Report report = new Report();
        private TravellerStage stage;
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-shang-civic-check");
            if (i < 0 || i + 1 >= args.Length || FindFirstObjectByType<ShangCivicPlayerCheck>() != null) return;
            var root = new GameObject("Development Shang civic verification"); DontDestroyOnLoad(root);
            root.AddComponent<ShangCivicPlayerCheck>().output = args[i + 1];
        }
        private IEnumerator Load(int index)
        {
            var load = SceneManager.LoadSceneAsync(TravellerStage.SceneNames[index]); while (!load.isDone) yield return null;
            for (int i = 0; i < 15; i++) yield return null;
            stage = FindFirstObjectByType<TravellerStage>();
            if (!stage.Journey.IsPreviewing) stage.Journey.TogglePreview();
            stage.GetComponentInChildren<TravellerHud>().Root.style.display = UnityEngine.UIElements.DisplayStyle.None;
            stage.player.enabled = false;
            foreach (var actor in stage.GetComponentsInChildren<TravellerActor>()) actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog; Application.runInBackground = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            report.gpu = SystemInfo.graphicsDeviceName; report.width = Screen.width; report.height = Screen.height;
            yield return Load(1);
            var district = stage.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "10 Civic life and working districts");
            Check(district != null, "Prepared civic district missing.");
            foreach (string id in new[] { "ShangWatchtower", "ShangWoodCart", "ShangStorageJar", "ShangGrainBasket", "ShangWoodFootbridge", "ShangVillageWell" })
                Check(district != null && district.GetComponentsInChildren<Transform>().Any(t => t.name == id), "Scene instance missing: " + id);
            Check(stage.GetComponentsInChildren<Transform>().Count(t => t.name == "Prepared civic building") == 71, "Expected 71 replaced buildings.");
            Check(stage.GetComponentsInChildren<Transform>().Count(t => t.name == "Clay casting furnace") == 5, "Expected five furnaces.");
            Check(district != null && district.GetComponentsInChildren<Transform>().Count(t => t.name == "Prepared palace bronze vessel") == 2, "Palace vessel replacements missing.");
            var guards = stage.GetComponentsInChildren<EraGuardEquipment>(); Check(guards.Length == 10, "Expected ten Shang guards including the royal retinue.");
            stage.ApplyMoment(0, true, false); yield return Delay(.7f);
            Check(guards.All(g => !g.GetComponent<TorchBearer>().IsCarried && g.HoldsShield && g.HoldsPolearm), "Day guard equipment is incorrect.");
            foreach (var guard in guards)
            {
                var actor = guard.GetComponent<TravellerActor>();
                Check(guard.profile.role == TravellerRole.ShangGuard && guard.profile.bodySource.Contains("ShangGuard"), "Wrong era body: " + guard.name);
                Check(actor.animator.avatar.isHuman && actor.animator.avatar.isValid, "Invalid Humanoid: " + guard.name);
                var hand = actor.animator.GetBoneTransform(HumanBodyBones.RightHand);
                Check(Vector3.Distance(hand.position, guard.RightHandTarget) < .05f, "Polearm wrist is unreachable: " + guard.name);
                Check(Quaternion.Angle(hand.rotation, guard.RightHandRotation) < 3, "Avatar hand rotation is incorrect: " + guard.name);
                var left = actor.animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Check(Vector3.Distance(left.position, guard.LeftHandTarget) < .05f, "Shield wrist is unreachable: " + guard.name);
            }
            var gate = guards.First(g => g.name.StartsWith("城门", StringComparison.Ordinal));
            yield return View("01_Gate_Guard_Day", gate.transform.position + gate.transform.forward * 2.5f + Vector3.up * 1.65f, gate.transform.position + Vector3.up * 1.1f);
            var palace = guards.First(g => g.profile.bodySource.Contains("ShangGuardV2"));
            yield return View("10_Palace_Guard_Day", palace.transform.position + palace.transform.forward * 2.5f + Vector3.up * 1.65f, palace.transform.position + Vector3.up * 1.1f);
            yield return View("02_Residential_Street", new Vector3(-31, 1.65f, -159), new Vector3(-39, 2.5f, -173));
            yield return View("03_Market_Avenue", new Vector3(16, 1.65f, -155), new Vector3(27, 1.5f, -163));
            yield return View("04_Bronze_Workshop", new Vector3(109, 1.65f, -144), new Vector3(124, 1.7f, -151));
            yield return View("05_Granary", new Vector3(-85, 1.65f, 139), new Vector3(-88, 1.7f, 159));
            yield return View("06_Watercourse", new Vector3(144, 1.65f, 0), new Vector3(154, 1, 8));
            yield return View("07_Western_Residential_Ward", new Vector3(-207, 1.65f, -42), new Vector3(-196, 2.4f, -30));
            yield return BridgeWalk(district);
            stage.player.Teleport(new Vector3(0, 1.65f, -216), gate.transform.position + Vector3.up);
            Vector3 start = gate.transform.position; float moved = 0;
            float until = Time.realtimeSinceStartup + 26;
            while (Time.realtimeSinceStartup < until && moved < .25f)
            {
                moved = Mathf.Max(moved, Vector3.Distance(start, gate.transform.position));
                CheckGuardClearance(gate); yield return null;
            }
            Check(moved > .2f, "Gate guard patrol never moves.");
            stage.player.Teleport(gate.transform.position + gate.transform.forward * 2 + Vector3.up * 1.65f, gate.transform.position + Vector3.up);
            yield return Delay(.4f); start = gate.transform.position; yield return Delay(.8f);
            Check(Vector3.Distance(start, gate.transform.position) < .03f, "Guard fails to stop patrol near player.");
            stage.ApplyMoment(2, true, false); yield return Delay(.8f);
            Check(guards.All(g => g.GetComponent<TorchBearer>().IsCarried && !g.HoldsShield && g.shield.localPosition.z < -.2f), "Night guards fail to stow shields and carry torches.");
            foreach (var guard in guards)
                Check(Vector3.Distance(guard.GetComponent<TravellerActor>().animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                    guard.GetComponent<TorchBearer>().HandTarget) < .05f, "Torch wrist is unreachable: " + guard.name);
            yield return View("08_Gate_Guard_Night", gate.transform.position + gate.transform.forward * 2.5f + Vector3.up * 1.65f, gate.transform.position + Vector3.up * 1.2f);
            yield return Load(2); stage.ApplyMoment(2, true, false); yield return Delay(.8f);
            var medieval = stage.GetComponentsInChildren<EraGuardEquipment>(); Check(medieval.Length == 2, "Expected two medieval guards.");
            Check(medieval.All(g => g.profile.role == TravellerRole.ChurchGuard && g.profile.bodySource.Contains("ChurchGuard")), "Medieval guards use wrong era bodies.");
            foreach (var g in medieval)
            {
                var hand = g.GetComponent<TravellerActor>().animator.GetBoneTransform(HumanBodyBones.RightHand);
                Check(Vector3.Distance(hand.position, g.RightHandTarget) < .05f && Quaternion.Angle(hand.rotation, g.RightHandRotation) < 3, "Medieval guard IK failed.");
            }
            if (medieval.Length > 0)
                yield return View("09_Medieval_Guard", medieval[0].transform.position + medieval[0].transform.forward * 2.5f + Vector3.up * 1.65f, medieval[0].transform.position + Vector3.up);
            report.passed = report.errors.Count == 0; File.WriteAllText(output, JsonUtility.ToJson(report, true)); Application.Quit(report.passed ? 0 : 1);
        }
        private IEnumerator View(string name, Vector3 eye, Vector3 target)
        {
            stage.player.Teleport(eye, target);
            float warmupMax = 0, until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until) { yield return null; warmupMax = Mathf.Max(warmupMax, Time.unscaledDeltaTime * 1000); }
            var frames = new List<float>(300);
            for (int i = 0; i < 300; i++) { yield return null; frames.Add(Time.unscaledDeltaTime * 1000); }
            frames.Sort(); float p95 = frames[(int)(frames.Count * .95f)];
            report.performance.Add(new Metric { view = name, meanMs = frames.Average(), p95Ms = p95, warmupMaxMs = warmupMax, budgetPassed = p95 <= 16.67f });
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output), name + ".png"));
            yield return null; yield return null;
        }
        private IEnumerator BridgeWalk(Transform district)
        {
            if (district == null) yield break;
            var bridge = district.Find("ShangWoodFootbridge");
            if (bridge == null) yield break;
            var controller = stage.player.GetComponent<CharacterController>();
            stage.player.Teleport(new Vector3(154, 1.65f, 1.8f), new Vector3(154, 1.65f, 14));
            int frames = 0; bool walkedOnDeck = false;
            while (stage.player.transform.position.z < 14.1f && frames++ < 500)
            {
                controller.Move(new Vector3(0, -.08f, .08f));
                var p = stage.player.transform.position;
                if (Mathf.Abs(p.z - 8) < 1 && p.y > .08f) walkedOnDeck = true;
                yield return null;
            }
            Check(frames < 500 && Mathf.Abs(stage.player.transform.position.y) < .45f, "Real CharacterController cannot cross the canal bridge.");
            Check(walkedOnDeck, "Controller crossed below the bridge deck.");
        }
        private void CheckGuardClearance(EraGuardEquipment guard)
        {
            var p = guard.transform.position;
            var overlaps = Physics.OverlapCapsule(p + Vector3.up * .36f, p + Vector3.up * 1.45f, .25f, ~0, QueryTriggerInteraction.Ignore);
            if (overlaps.Any(c => !c.transform.IsChildOf(guard.transform)))
            { if (!report.errors.Contains("Patrol intersects a collider.")) Check(false, "Patrol intersects a collider."); }
        }
        private static IEnumerator Delay(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        private void Check(bool value, string error) { report.checks++; if (!value) report.errors.Add(error); }
        private void OnLog(string text, string trace, LogType type) { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 40) report.errors.Add(text); }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
#endif
