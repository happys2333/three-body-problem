using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace ThreeBodyProblem
{
    // Present only in the independent art yard. It never boots in story scenes.
    public sealed class EnvironmentAssetPreview : MonoBehaviour
    {
        public TravellerPlayer player;
        public Transform[] stations;
        public string[] assetIds;
        public Vector3[] viewpoints, viewTargets;
        public Transform gate, portal;
        public Font uiFont;
        private string current = "Environment art yard";
        private bool benchmarking;
        private float previousShadowDistance;
        private int previousShadowCascades;
        private int previousFrameRate, previousVSync;
        private bool shadowsConfigured;
        [Serializable] public class Metric { public string asset; public bool viewClear; public float meanFrameMs, p95FrameMs, meanMainThreadMs, meanPresentWaitMs, meanGpuMs, p95GpuMs; public int gpuSamples; public long batches, triangles; public int[] visibleLods; }
        [Serializable] public class Report { public string gpu; public int width, height, frameRateCap; public float shadowDistance; public bool passed; public List<Metric> metrics = new List<Metric>(); public List<string> errors = new List<string>(); public bool gateClear, portalClear, gateWallSolid, portalWallSolid, gateWalkThrough, portalWalkThrough; }

        private IEnumerator Start()
        {
            previousShadowDistance=QualitySettings.shadowDistance;previousShadowCascades=QualitySettings.shadowCascades;
            previousFrameRate=Application.targetFrameRate;previousVSync=QualitySettings.vSyncCount;
            QualitySettings.shadowDistance=60;QualitySettings.shadowCascades=2;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
            shadowsConfigured=true;
            var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-environment-qa");
            if (option < 0) yield break;
            benchmarking = true; player.InputBlocked = true; Application.runInBackground = true;
            // Leave headroom above the 60 FPS target without flooding the macOS presentation queue.
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 120;
            string output = option + 1 < args.Length ? args[option + 1] : Path.Combine(Application.persistentDataPath, "environment-qa.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = new Report { gpu = SystemInfo.graphicsDeviceName, width = Screen.width, height = Screen.height, shadowDistance=QualitySettings.shadowDistance, frameRateCap=Application.targetFrameRate };
            Application.logMessageReceived += (message, trace, type) => { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 30) report.errors.Add(message); };
            yield return null;
            report.gateClear = ApertureClear(gate, 4.7f); report.portalClear = ApertureClear(portal, 2.55f);
            report.gateWallSolid = WallSolid(gate, 2.3f); report.portalWallSolid = WallSolid(portal, 1.2f);
            if (!report.gateClear || !report.portalClear || !report.gateWallSolid || !report.portalWallSolid) report.errors.Add("Aperture or side-wall collision check failed.");
            yield return WalkThrough(portal, value => report.portalWalkThrough = value);
            yield return WalkThrough(gate, value => report.gateWalkThrough = value);
            if (!report.portalWalkThrough || !report.gateWalkThrough) report.errors.Add("First-person controller could not walk through both apertures.");
            for (int i = 0; i < stations.Length; i++)
            {
                current = assetIds[i]; player.Teleport(viewpoints[i], viewTargets[i]);
                for (int warm = 0; warm < 240; warm++) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
                var frames = new List<float>(360); float mainSum = 0; int mainCount = 0; long batches = 0, triangles = 0; int renderCount = 0;
                var gpuFrames = new List<float>(360); var timing = new FrameTiming[1]; ulong lastGpuFrame = 0;
                float presentSum = 0; int presentCount = 0;
                using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
                using (var batch = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
                using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1))
                using (var present = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", 1))
                {
                    for (int frame = 0; frame < 360; frame++)
                    {
                        FrameTimingManager.CaptureFrameTimings(); yield return null; frames.Add(Time.unscaledDeltaTime * 1000);
                        if (main.Valid && main.LastValue > 0) { mainSum += main.LastValue / 1000000f; mainCount++; }
                        if (present.Valid && present.LastValue > 0) { presentSum += present.LastValue / 1000000f; presentCount++; }
                        if (batch.Valid && tris.Valid && batch.LastValue > 0) { batches += batch.LastValue; triangles += tris.LastValue; renderCount++; }
                        if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastGpuFrame && timing[0].gpuFrameTime > 0)
                        { lastGpuFrame = timing[0].frameStartTimestamp; gpuFrames.Add((float)timing[0].gpuFrameTime); }
                    }
                }
                frames.Sort(); gpuFrames.Sort(); var lods = stations[i].GetComponent<LODGroup>().GetLODs();
                var visible = Enumerable.Range(0, lods.Length).Where(level => lods[level].renderers.Any(r => r.isVisible)).ToArray();
                bool clear=!stations.Where((station,index)=>index!=i).Any(station=>station.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds.Contains(viewpoints[i]));
                RaycastHit obstruction;Vector3 sight=viewTargets[i]-viewpoints[i];
                if(Physics.Raycast(viewpoints[i],sight.normalized,out obstruction,sight.magnitude)&&!obstruction.transform.IsChildOf(stations[i]))clear=false;
                var metric = new Metric { asset = current, meanFrameMs = frames.Average(), p95FrameMs = frames[(int)(frames.Count * .95f)],
                    meanMainThreadMs = mainCount > 0 ? mainSum / mainCount : -1, batches = renderCount > 0 ? batches / renderCount : -1,
                    meanPresentWaitMs = presentCount > 0 ? presentSum / presentCount : -1,
                    meanGpuMs = gpuFrames.Count > 0 ? gpuFrames.Average() : -1, p95GpuMs = gpuFrames.Count > 0 ? gpuFrames[(int)(gpuFrames.Count * .95f)] : -1, gpuSamples = gpuFrames.Count,
                    triangles = renderCount > 0 ? triangles / renderCount : -1, visibleLods = visible, viewClear=clear };
                report.metrics.Add(metric);
                if (metric.p95FrameMs > 16.67f) report.errors.Add(current + ": P95 frame time exceeds 16.67 ms.");
                if (visible.Length == 0) report.errors.Add(current + ": target did not render.");
                if (!clear) report.errors.Add(current + ": inspection camera is obstructed by another station.");
                ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output), current + ".png"));
                yield return null; yield return null; yield return null;
            }
            report.passed = report.errors.Count == 0; File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }

        private static bool ApertureClear(Transform root, float height)
        {
            var bounds = root.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds;
            Vector3 start = new Vector3(root.position.x, .35f, bounds.max.z + 1);
            return !Physics.CapsuleCast(start, start + Vector3.up * 1.15f, .3f, Vector3.back, bounds.size.z + 2);
        }
        private static bool WallSolid(Transform root, float offset)
        {
            var bounds = root.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds;
            RaycastHit hit;
            return Physics.Raycast(new Vector3(root.position.x + offset, 1.4f, bounds.max.z + 1), Vector3.back, out hit, bounds.size.z + 2)
                && hit.collider.transform.IsChildOf(root);
        }
        private IEnumerator WalkThrough(Transform root, Action<bool> result)
        {
            var bounds = root.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds;
            Vector3 start = new Vector3(root.position.x, 1.70f, bounds.max.z + 1.4f);
            player.Teleport(start, start + Vector3.back * 10);
            player.enabled = false; player.InputBlocked = false;
            int steps = Mathf.CeilToInt((bounds.size.z + 3.5f) / player.walkSpeed * 60);
            for (int i = 0; i < steps; i++) { player.MoveInput(Vector2.up, false, 1f / 60); yield return null; }
            result(player.transform.position.z < bounds.min.z - .4f && player.transform.position.y > -.2f);
            player.InputBlocked = true; player.enabled = true;
        }
        private void OnGUI()
        {
            if (uiFont != null) GUI.skin.font = uiFont;
            GUI.skin.label.fontSize=16;
            GUI.color = new Color(.05f, .06f, .07f, .8f); GUI.Box(new Rect(18, 18, 620, 66), ""); GUI.color = Color.white;
            GUI.Label(new Rect(32, 26, 590, 26), benchmarking ? current : "环境素材试走场：右键环顾 · WASD 行走 · Shift 快走");
            GUI.Label(new Rect(32, 51, 590, 24), benchmarking ? "材质、碰撞与运行开销检查" : "前方可穿过石门和门楼，两侧可查看建筑与荒漠物件");
        }
        private void OnDestroy(){if(shadowsConfigured){QualitySettings.shadowDistance=previousShadowDistance;QualitySettings.shadowCascades=previousShadowCascades;QualitySettings.vSyncCount=previousVSync;Application.targetFrameRate=previousFrameRate;}}
    }
}
