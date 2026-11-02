#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace ThreeBodyProblem
{
    // Identical views and warm-up for before/after profiling. Only runs with its CLI flag.
    public sealed class ActOneRenderBenchmark : MonoBehaviour
    {
        [Serializable] private sealed class Metric
        {
            public string view; public int width,height,gpuSamples,activeVegetationFires;
            public float meanMs,p95Ms,p99Ms,maxMs,gpuMeanMs,gpuP95Ms,cpuWorkMs,renderWorkMs,targetWaitMs;
            public long batches,triangles,setPasses,gcBytesPerFrame,maxGcBytes;
        }
        [Serializable] private sealed class Report
        {
            public string gpu,probe,phase; public bool passed; public int frameCap,warmFrames,sampleFrames;
            public List<string> errors=new List<string>(); public List<Metric> metrics=new List<Metric>();
        }
        private readonly Report report=new Report();
        private string output,journal; private bool hadJournal,restore;
        private TravellerStage stage; private TravellerJourney journey; private GobiVegetationFire vegetation;
        private int onlyView=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-act-one-render-benchmark");if(i<0||i+1>=args.Length)return;
            var b=new GameObject("Development Act One Render Benchmark").AddComponent<ActOneRenderBenchmark>();b.output=args[i+1];
            int p=Array.IndexOf(args,"-render-probe");b.report.probe=p>=0&&p+1<args.Length?args[p+1]:"baseline";
            p=Array.IndexOf(args,"-render-view");if(p>=0&&p+1<args.Length)int.TryParse(args[p+1],out b.onlyView);
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));Application.logMessageReceived+=OnLog;
            hadJournal=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");journal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");restore=true;
            Application.runInBackground=true;QualitySettings.vSyncCount=0;
            report.gpu=SystemInfo.graphicsDeviceName;report.frameCap=90;report.warmFrames=240;report.sampleFrames=360;
            var args=Environment.GetCommandLineArgs();int option=Array.IndexOf(args,"-render-cap"),cap;
            if(option>=0&&option+1<args.Length&&int.TryParse(args[option+1],out cap))report.frameCap=cap;
            yield return Frames(12);
            stage=FindFirstObjectByType<TravellerStage>();journey=stage.Journey;vegetation=stage.GetComponent<GobiVegetationFire>();
            journey.Begin();journey.Progress.WakeUp();stage.GetComponent<GobiSequenceController>().enabled=false;
            journey.enabled=false;stage.player.enabled=false;stage.player.PointerReleased=true;
            stage.GetComponent<SunExposureSystem>().enabled=false;
            stage.GetComponent<TravellerCastDirector>().enabled=false;
            foreach(var actor in stage.GetComponentsInChildren<TravellerActor>())actor.StopTravelling();
            Application.targetFrameRate=report.frameCap;
            ApplyProbe();
            for(int view=0;view<6;view++)
            {
                if(onlyView>=0&&view!=onlyView)continue;
                vegetation.enabled=view==4;vegetation.ResetVegetation();stage.ApplyMoment(view<2||view==5?0:1,true,false);
                Vector3 eye,target;string label;
                switch(view)
                {
                    case 0:eye=new Vector3(0,1.75f,-30);target=new Vector3(20,5,90);label="Night_Opening";break;
                    case 1:eye=new Vector3(-3,1.75f,-5);target=new Vector3(-6,1.2f,-2);label="Night_Traveller";break;
                    case 2:eye=new Vector3(0,1.75f,-30);target=new Vector3(-13,6,20);label="Sun_Approach";break;
                    case 3:eye=new Vector3(-13,1.75f,12);target=new Vector3(-13,4.7f,19);label="Sun_ShelterClose";break;
                    case 4:eye=new Vector3(-13,1.75f,12);target=new Vector3(16,1,21);label="Sun_BurningPlants";break;
                    default:eye=new Vector3(-12,1.75f,31);target=new Vector3(2,2,70);label="Night_Departure";break;
                }
                stage.player.Teleport(eye,target);
                if(view==4)vegetation.IgniteSunlit();
                report.phase=label;Save();
                for(int warm=0;warm<report.warmFrames;warm++){FrameTimingManager.CaptureFrameTimings();yield return null;}
                var frames=new List<float>(report.sampleFrames);var gpu=new List<float>(report.sampleFrames);var timings=new FrameTiming[1];ulong last=0;
                float cpu=0,render=0,wait=0;int cpuCount=0,waitCount=0,counts=0;long batches=0,triangles=0,passes=0,gcSum=0,gcMax=0;
                using(var batch=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
                using(var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
                using(var setPass=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count",1))
                using(var allocated=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
                using(var targetWait=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"WaitForTargetFPS",1))
                for(int f=0;f<report.sampleFrames;f++)
                {
                    FrameTimingManager.CaptureFrameTimings();yield return null;frames.Add(Time.unscaledDeltaTime*1000);
                    if(batch.Valid&&tris.Valid){counts++;batches+=batch.LastValue;triangles+=tris.LastValue;if(setPass.Valid)passes+=setPass.LastValue;}
                    if(allocated.Valid){gcSum+=allocated.LastValue;gcMax=Math.Max(gcMax,allocated.LastValue);}
                    if(targetWait.Valid){wait+=targetWait.LastValue/1000000f;waitCount++;}
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=last)
                    {
                        last=timings[0].frameStartTimestamp;
                        if(timings[0].gpuFrameTime>0)gpu.Add((float)timings[0].gpuFrameTime);
                        cpu+=(float)timings[0].cpuMainThreadFrameTime;render+=(float)timings[0].cpuRenderThreadFrameTime;cpuCount++;
                    }
                }
                frames.Sort();gpu.Sort();
                report.metrics.Add(new Metric {view=label,width=Screen.width,height=Screen.height,meanMs=Mean(frames),p95Ms=Percentile(frames,.95f),p99Ms=Percentile(frames,.99f),maxMs=frames[frames.Count-1],
                    activeVegetationFires=ActiveFires(),gpuSamples=gpu.Count,gpuMeanMs=Mean(gpu),gpuP95Ms=Percentile(gpu,.95f),cpuWorkMs=cpuCount>0?cpu/cpuCount:-1,renderWorkMs=cpuCount>0?render/cpuCount:-1,
                    targetWaitMs=waitCount>0?wait/waitCount:-1,batches=counts>0?batches/counts:-1,triangles=counts>0?triangles/counts:-1,setPasses=counts>0?passes/counts:-1,
                    gcBytesPerFrame=gcSum/report.sampleFrames,maxGcBytes=gcMax});
                Save();ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),label+".png"));yield return Frames(4);
            }
            report.passed=report.errors.Count==0&&!report.metrics.Exists(m=>m.p95Ms>16.67f);report.phase="complete";Save();Restore();Application.Quit(report.passed?0:1);
        }
        private int ActiveFires()
        {int count=0;foreach(var p in vegetation.GetComponentsInChildren<ParticleSystem>())if(p.isPlaying&&p.gameObject.name.StartsWith(vegetation.settings.vegetationFirePrefab.name,StringComparison.Ordinal))count++;return count;}
        private void ApplyProbe()
        {
            switch(report.probe)
            {
                case "lod1":foreach(var g in stage.GetComponentsInChildren<LODGroup>())if(g.GetLODs().Length>1)g.ForceLOD(1);break;
                case "no-shadows":stage.sun.shadows=LightShadows.None;break;
                case "no-terrain":stage.GetComponentInChildren<Terrain>().drawHeightmap=false;break;
                case "no-ui":stage.GetComponentInChildren<UnityEngine.UIElements.UIDocument>().enabled=false;break;
                case "simple-surface":
                    var shader=Shader.Find("Standard");var replacements=new Dictionary<Material,Material>();
                    foreach(var r in stage.GetComponentsInChildren<Renderer>())
                    {
                        var materials=r.sharedMaterials;bool changed=false;
                        for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&materials[i].shader.name=="Traveller/Environment Surface")
                        {Material m;if(!replacements.TryGetValue(materials[i],out m)){m=new Material(shader);m.color=materials[i].color;m.SetFloat("_Glossiness",.08f);m.enableInstancing=materials[i].enableInstancing;replacements.Add(materials[i],m);}materials[i]=m;changed=true;}
                        if(changed)r.sharedMaterials=materials;
                    }
                    break;
            }
        }
        private static float Mean(List<float> values){if(values.Count==0)return -1;float sum=0;foreach(float v in values)sum+=v;return sum/values.Count;}
        private static float Percentile(List<float> values,float p){return values.Count>0?values[Mathf.Min(values.Count-1,Mathf.FloorToInt(values.Count*p))]:-1;}
        private static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        private void Save(){File.WriteAllText(output,JsonUtility.ToJson(report,true));}
        private void OnLog(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){report.errors.Add(message);Save();}}
        private void Restore(){if(!restore)return;if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",journal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();restore=false;}
        private void OnDestroy(){Restore();Application.logMessageReceived-=OnLog;}
    }
}
#endif
