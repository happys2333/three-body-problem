#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace ThreeBodyProblem
{
    // Opt-in development-player benchmark. Ordinary launches never create this component.
    public sealed class TravellerPlayerBenchmark : MonoBehaviour
    {
        [Serializable] private class Metric { public string scene; public string moment; public float meanFrameMs; public float p95FrameMs; public float meanMainThreadMs; public float meanPresentWaitMs; public float meanGpuMs; public float p95GpuMs; public int gpuSamples; public long batches; public long triangles; public int width; public int height; }
        [Serializable] private class Report { public string gpu; public float shadowDistance; public int frameRateCap; public bool passed; public List<string> errors = new List<string>(); public List<Metric> metrics = new List<Metric>(); }
        private readonly Report report = new Report();
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-traveller-benchmark");
            if (option < 0 || UnityEngine.Object.FindFirstObjectByType<TravellerPlayerBenchmark>() != null) return;
            var value = new GameObject("Development Player Benchmark"); DontDestroyOnLoad(value);
            var benchmark = value.AddComponent<TravellerPlayerBenchmark>();
            benchmark.output = option + 1 < args.Length ? args[option + 1] : Path.Combine(Application.persistentDataPath, "benchmark.json");
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            var args=Environment.GetCommandLineArgs();int cap=Array.IndexOf(args,"-traveller-benchmark-fps"),parsed;
            report.frameRateCap=cap>=0&&cap+1<args.Length&&int.TryParse(args[cap+1],out parsed)&&parsed>0?parsed:-1;
            Application.targetFrameRate = report.frameRateCap;
            report.gpu = SystemInfo.graphicsDeviceName;
            report.shadowDistance = QualitySettings.shadowDistance;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            for (int sceneIndex = 0; sceneIndex < TravellerStage.SceneNames.Length; sceneIndex++)
            {
                if (SceneManager.GetActiveScene().name != TravellerStage.SceneNames[sceneIndex])
                {
                    var load = SceneManager.LoadSceneAsync(TravellerStage.SceneNames[sceneIndex]);
                    while (load != null && !load.isDone) yield return null;
                }
                yield return null; yield return null;
                var stage = UnityEngine.Object.FindFirstObjectByType<TravellerStage>();
                if (!stage.Journey.IsPreviewing) stage.Journey.TogglePreview();
                for(int view=0;view<(sceneIndex==1?7:1);view++)
                {
                    stage.ApplyMoment(sceneIndex==1?(view==5?2:view==4?1:0):sceneIndex==0?1:2,true);
                    string label=stage.Current.title;
                    if(sceneIndex==1&&view==1){stage.player.Teleport(new Vector3(115,31,-145),new Vector3(0,29,20));label="巨摆与金字塔外观";}
                    if(sceneIndex==1&&view==2){stage.player.Teleport(new Vector3(235,195,-275),new Vector3(0,0,-5));label="王都全景与城邑密度";}
                    if(sceneIndex==1&&view==3){stage.player.Teleport(new Vector3(-5,5.75f,9),new Vector3(0,8.3f,31));UnityEngine.Object.FindFirstObjectByType<PalaceSkylight>().SetOpening(1);label="厚墙石殿与天窗";}
                    if(sceneIndex==1&&view==6){stage.player.Teleport(new Vector3(-18,1.65f,-170),new Vector3(-29,1.1f,-156));label="居民街道近景与完整人群动画";}
                    UnityEngine.Object.FindFirstObjectByType<TravellerHud>().Root.style.display=UnityEngine.UIElements.DisplayStyle.None;
                    for(int warm=0;warm<120;warm++){FrameTimingManager.CaptureFrameTimings();yield return null;}
                    var frames=new List<float>(300);var gpuFrames=new List<float>(300);var timing=new FrameTiming[1];ulong lastGpuFrame=0;
                    float mainSum=0,presentSum=0;int mainCount=0,presentCount=0,renderCount=0;long batches=0,triangles=0;
                    using(var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
                    using(var batch=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
                    using(var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
                    using(var present=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Gfx.WaitForPresentOnGfxThread",1))
                    {
                        for(int frame=0;frame<300;frame++)
                        {
                            FrameTimingManager.CaptureFrameTimings();yield return null;frames.Add(Time.unscaledDeltaTime*1000f);
                            if(main.Valid&&main.LastValue>0){mainSum+=main.LastValue/1000000f;mainCount++;}
                            if(present.Valid&&present.LastValue>0){presentSum+=present.LastValue/1000000f;presentCount++;}
                            if(batch.Valid&&tris.Valid&&batch.LastValue>0){batches+=batch.LastValue;triangles+=tris.LastValue;renderCount++;}
                            if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].frameStartTimestamp!=lastGpuFrame&&timing[0].gpuFrameTime>0)
                            {lastGpuFrame=timing[0].frameStartTimestamp;gpuFrames.Add((float)timing[0].gpuFrameTime);}
                        }
                    }
                    frames.Sort();float sum=0;foreach(float frame in frames)sum+=frame;
                    gpuFrames.Sort();float gpuSum=0;foreach(float frame in gpuFrames)gpuSum+=frame;
                    var metric=new Metric{scene=TravellerStage.SceneNames[sceneIndex],moment=label,meanFrameMs=sum/frames.Count,p95FrameMs=frames[(int)(frames.Count*0.95f)],meanMainThreadMs=mainCount==0?-1:mainSum/mainCount,
                        meanPresentWaitMs=presentCount==0?-1:presentSum/presentCount,meanGpuMs=gpuFrames.Count==0?-1:gpuSum/gpuFrames.Count,p95GpuMs=gpuFrames.Count==0?-1:gpuFrames[(int)(gpuFrames.Count*0.95f)],gpuSamples=gpuFrames.Count,
                        batches=renderCount==0?-1:batches/renderCount,triangles=renderCount==0?-1:triangles/renderCount,width=Screen.width,height=Screen.height};
                    report.metrics.Add(metric);if(metric.p95FrameMs>16.67f)report.errors.Add(label+": P95 exceeds 60 FPS frame budget.");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"player_"+sceneIndex+"_"+view+".png"));
                    yield return null;yield return null;yield return null;
                }
            }
            report.passed = report.errors.Count == 0;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }

        private void OnLog(string message, string trace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 20) report.errors.Add(message);
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
#endif
