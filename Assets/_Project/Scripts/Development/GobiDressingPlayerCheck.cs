#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    // Opt-in native-player verification. No effect on an ordinary launch.
    public sealed class GobiDressingPlayerCheck : MonoBehaviour
    {
        [Serializable] private class Metric { public int moment,width,height;public float meanMs,p95Ms,gpuMeanMs;public int gpuSamples;public long batches,triangles; }
        [Serializable] private class Report {
            public bool passed;public int checks;public float metresWalked;public string gpu;
            public List<string> errors=new List<string>();public List<Metric> metrics=new List<Metric>();
        }
        private readonly Report report=new Report();
        private string output,savedJournal;private bool hadJournal,restore;
        private TravellerStage stage;private TravellerJourney journey;private TravellerHud hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-gobi-dressing-check");
            if(i<0||i+1>=args.Length)return;
            new GameObject("Development Gobi Dressing Check").AddComponent<GobiDressingPlayerCheck>().output=args[i+1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            hadJournal=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");savedJournal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");restore=true;
            Application.logMessageReceived+=OnLog;Application.runInBackground=true;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;
            report.gpu=SystemInfo.graphicsDeviceName;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return Frames(10);
            stage=UnityEngine.Object.FindFirstObjectByType<TravellerStage>();journey=stage.Journey;hud=UnityEngine.Object.FindFirstObjectByType<TravellerHud>();
            Check(stage.profile.sceneIndex==0&&SceneManager.sceneCount==1,"Incorrect first scene.");
            Check(journey.IsTitle,"The game does not open at the title.");
            var landscape=stage.transform.Find("Environment/Prepared Gobi landscape");
            Check(landscape!=null&&landscape.childCount>=100,"Prepared Gobi landscape is missing.");
            journey.Begin();stage.player.enabled=false;yield return Frames(4);
            Check(!stage.player.InputBlocked,"First-person movement is blocked after Start.");
            Capture("Opening");
            foreach(var point in new[]{new Vector3(-7,0,-10),new Vector3(-15,0,5),new Vector3(-13,0,12)})yield return Walk(point);
            Check(journey.Progress.Step==JourneyStep.ShelterTalk,"Walking into shelter did not advance the story.");
            Transform companion=null;foreach(var t in stage.GetComponentsInChildren<Transform>())if(t.name.StartsWith("周文王 [",StringComparison.Ordinal)){companion=t;break;}
            float until=Time.realtimeSinceStartup+25;
            while(companion!=null&&Vector3.Distance(stage.player.transform.position,companion.position)>4.8f&&Time.realtimeSinceStartup<until)yield return null;
            journey.Interact();yield return Frames(2);Check(journey.DialogueOpen,"The companion cannot be spoken to under the new rock.");
            journey.SelectDialogue("identity");Check(journey.Progress.KnowsIdentity,"Identity choice failed.");Capture("ShelterDialogue");
            var eye=stage.player.viewCamera.transform.position;
            bool shaded=Physics.Raycast(eye,stage.profile.moments[1].sunA.normalized,150,~0,QueryTriggerInteraction.Ignore);
            Check(shaded,"Shelter does not cast shade toward the giant sun.");
            journey.SelectDialogue("continue");journey.Progress.Tick(8);yield return Frames(3);
            foreach(var point in new[]{new Vector3(-15,0,18),new Vector3(-14,0,26),new Vector3(-12,0,31),new Vector3(2,0,42)})yield return Walk(point);
            Check(journey.Progress.Step==JourneyStep.ObserveFirstStar,"First observation point is unreachable.");
            CheckStars(1);journey.FaceGoal();stage.RecordObservation();yield return Frames(3);
            Check(journey.Progress.Step==JourneyStep.WalkToTwoStars,"First star recording did not advance the route.");Capture("FirstStar");
            yield return Walk(new Vector3(2,0,64));Check(journey.Progress.Step==JourneyStep.ObserveTwoStars,"Second observation point is unreachable.");
            CheckStars(2);journey.FaceGoal();stage.RecordObservation();yield return Frames(3);
            Check(journey.Progress.Step==JourneyStep.WalkToChaoge,"Two-star recording did not unlock departure.");
            yield return Walk(new Vector3(0,0,97));yield return Walk(new Vector3(0,0,128));
            Check(journey.Progress.Step==JourneyStep.DepartForCapital,"The physical route did not reach the departure marker.");
            journey.Progress.Tick(100);yield return Frames(3);
            Check(!stage.IsLoading&&stage.profile.sceneIndex==0,"Waiting unexpectedly loaded the capital.");
            stage.player.FaceTarget(new Vector3(0,5,240));Capture("Departure");
            // A collision probe after completing the uninterrupted story walk.
            yield return ProbeSpur(landscape);
            if(!journey.IsPreviewing)journey.TogglePreview();hud.Root.style.display=DisplayStyle.None;
            for(int moment=0;moment<6;moment++)
            {
                stage.ApplyMoment(moment,true);stage.player.InputBlocked=false;
                yield return Measure(moment);Capture("Moment_"+moment);yield return Frames(3);
            }
            report.passed=report.errors.Count==0;Restore();File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }

        private IEnumerator Walk(Vector3 target)
        {
            float until=Time.realtimeSinceStartup+45,stalled=0;
            while(Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(target.x,target.z))>.25f&&Time.realtimeSinceStartup<until)
            {
                Vector3 before=stage.player.transform.position;float distance=Vector3.ProjectOnPlane(target-before,Vector3.up).magnitude;
                stage.player.FaceTarget(target+Vector3.up*1.65f);
                stage.player.MoveInput(Vector2.up*Mathf.Min(1,distance/(stage.player.walkSpeed*stage.player.runMultiplier*Mathf.Max(.001f,Time.deltaTime))),true,Time.deltaTime);
                float moved=Vector3.Distance(before,stage.player.transform.position);report.metresWalked+=moved;
                stalled=moved<.002f?stalled+Time.deltaTime:0;
                if(stalled>2){Check(false,"Controller blocked before "+target+" at "+stage.player.transform.position);yield break;}
                yield return null;
            }
            Check(Time.realtimeSinceStartup<until,"Walking timed out at "+target);
            Check(Mathf.Abs(stage.player.transform.position.y-target.y)<.5f,"Wrong ground height at "+target);
        }

        private IEnumerator ProbeSpur(Transform landscape)
        {
            Transform spur=null;foreach(Transform child in landscape)if(child.name.StartsWith("Shelter eastern spur",StringComparison.Ordinal))spur=child;
            Check(spur!=null,"Shelter collision spur missing.");if(spur==null)yield break;
            var solid=spur.GetComponentInChildren<MeshCollider>();var bounds=solid.bounds;
            Vector3 origin=new Vector3(bounds.min.x-3,1.1f,bounds.center.z);RaycastHit hit;
            if(!solid.Raycast(new Ray(origin,Vector3.right),out hit,bounds.size.x+8)){Check(false,"Spur has no solid side surface.");yield break;}
            stage.player.Teleport(new Vector3(origin.x,1.75f,origin.z),origin+Vector3.right*20);
            float until=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<until){stage.player.MoveInput(Vector2.up,true,Time.deltaTime);yield return null;}
            Check(stage.player.transform.position.x<hit.point.x+.1f,"The controller passed through the shelter's rock side.");
        }

        private void CheckStars(int count)
        {
            for(int star=0;star<count;star++)
            {
                Vector3 eye=stage.player.viewCamera.transform.position;
                Check(!Physics.Linecast(eye,stage.flyingStars[star].position,~0,QueryTriggerInteraction.Ignore),"Moving star hidden by scenery.");
                stage.player.FaceTarget(stage.flyingStars[star].position);
                var screen=stage.player.viewCamera.WorldToViewportPoint(stage.flyingStars[star].position);
                Check(screen.z>0&&screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1,"Flying star is outside its observation view.");
            }
        }

        private IEnumerator Measure(int moment)
        {
            for(int warm=0;warm<120;warm++){FrameTimingManager.CaptureFrameTimings();yield return null;}
            var frames=new List<float>(360);var timing=new FrameTiming[1];ulong last=0;
            float sum=0,gpu=0;int gpuCount=0,renderCount=0;long batches=0,triangles=0;
            using(var batch=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
            using(var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
            {
                for(int i=0;i<360;i++)
                {
                    FrameTimingManager.CaptureFrameTimings();yield return null;
                    float ms=Time.unscaledDeltaTime*1000;frames.Add(ms);sum+=ms;
                    if(batch.Valid&&tris.Valid&&batch.LastValue>0){batches+=batch.LastValue;triangles+=tris.LastValue;renderCount++;}
                    if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].frameStartTimestamp!=last&&timing[0].gpuFrameTime>0)
                    {last=timing[0].frameStartTimestamp;gpu+=(float)timing[0].gpuFrameTime;gpuCount++;}
                }
            }
            frames.Sort();var metric=new Metric{moment=moment,width=Screen.width,height=Screen.height,meanMs=sum/frames.Count,p95Ms=frames[(int)(frames.Count*.95f)],gpuMeanMs=gpuCount>0?gpu/gpuCount:-1,gpuSamples=gpuCount,batches=renderCount>0?batches/renderCount:-1,triangles=renderCount>0?triangles/renderCount:-1};
            report.metrics.Add(metric);Check(metric.p95Ms<=16.67f,"Gobi moment "+moment+" exceeded the 60 FPS budget: "+metric.p95Ms+" ms.");
            File.WriteAllText(output,JsonUtility.ToJson(report,true));
        }
        private static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private void Check(bool condition,string message){report.checks++;if(!condition)report.errors.Add(message);}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        private void Restore(){if(!restore)return;if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",savedJournal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();restore=false;}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;Restore();}
    }
}
#endif
