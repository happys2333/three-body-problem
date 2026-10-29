#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Profiling;

namespace ThreeBodyProblem
{
    // Real player check. The optional -act-one-real-wait flag waits the full 600 gameplay seconds.
    public sealed class ActOnePlayerCheck : MonoBehaviour
    {
        [Serializable] private class Metric{public int moment;public float meanMs,p95Ms,gpuMeanMs,mainThreadMeanMs;public long triangles,batches;}
        [Serializable] private class Report
        {
            public bool passed,mechanicsPassed,performancePassed,realTenMinuteWait,hardwareMicrophoneUsed;
            public int checks,ignited,burned,shaded,frameRateLimit;public string phase,gpu;public float walkedMetres,shelterWallSeconds,sunsetWallSeconds;
            public List<string> errors=new List<string>();public List<Metric> metrics=new List<Metric>();
            public string asrTranscript,playerTorchViewport;
        }
        private readonly Report report=new Report();private string output,savedJournal;private bool hadJournal,capturedJournal;
        private TravellerStage stage;private TravellerJourney journey;private TravellerHud hud;private SunExposureSystem exposure;
        private GobiStorySettings settings;private GobiVegetationFire fire;private Transform companion;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-act-one-check");
            if(index<0||index+1>=args.Length||UnityEngine.Object.FindFirstObjectByType<ActOnePlayerCheck>()!=null)return;
            var go=new GameObject("Development Act One Check");DontDestroyOnLoad(go);var check=go.AddComponent<ActOnePlayerCheck>();check.output=args[index+1];check.report.realTenMinuteWait=Array.IndexOf(args,"-act-one-real-wait")>=0;
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));hadJournal=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");savedJournal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");capturedJournal=true;
            Application.runInBackground=true;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;report.gpu=SystemInfo.graphicsDeviceName;Application.logMessageReceived+=OnLog;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return Frames(12);Bind();settings=stage.profile.gobiStory;exposure=stage.GetComponent<SunExposureSystem>();fire=stage.GetComponent<GobiVegetationFire>();
            Application.targetFrameRate=settings.frameRateLimit;report.frameRateLimit=settings.frameRateLimit;
            Check(journey.IsTitle&&stage.Current.sunCount==0,"Opening must be a night-time title.");Check(settings.shelterWaitSeconds==600,"Production wait is not 600 seconds.");
            foreach(var size in new[]{new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768)})
            {Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return Frames(8);CheckInside(hud.Root.Q("title-card"));}
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return Frames(8);
            journey.Begin();stage.player.enabled=false;yield return Realtime(.3f);
            var carriers=stage.GetComponentsInChildren<TorchBearer>();Check(carriers.Length==2,"Opening cast must both have torch bearers.");
            foreach(var carrier in carriers){Check(carrier.IsLit&&carrier.Grip!=null,"Opening torch is not lit.");Check(carrier.Grip.GetComponentInChildren<Light>().enabled,"Torch light is disabled.");var hand=carrier.GetComponent<TravellerActor>().animator.GetBoneTransform(HumanBodyBones.LeftHand);Check(Vector3.Distance(hand.position,carrier.HandTarget)<.08f,"Torch hand IK cannot reach the grip: "+carrier.name);}
            foreach(var carrier in carriers)if(carrier.GetComponent<TravellerActor>().role==TravellerRole.PlayerTraveller)
            {var point=stage.player.viewCamera.WorldToViewportPoint(carrier.Grip.TransformPoint(Vector3.up*.77f));report.playerTorchViewport=point.ToString();Check(point.z>0&&point.x>0&&point.x<1&&point.y>0&&point.y<1,"The first-person torch is outside the default view.");}
            Capture("01_NightMeeting");yield return Frames(3);SetPhase("night encounter");
            journey.Progress.Tick(settings.nightMeetingSeconds);yield return Realtime(stage.profile.transitionSeconds+.2f);
            Check(stage.MomentIndex==1&&stage.GetComponent<GobiSequenceController>().SunHasRisen,"Giant sun failed to appear.");
            stage.player.Teleport(new Vector3(9,1.75f,-18),new Vector3(25,9,120));yield return Frames(25);
            Check(exposure.Vitality.Health<100&&exposure.Vitality.Health%10==0,"Sunlight does not cause hidden ten-point damage.");
            Check(exposure.DamageEvents>0,"Sun hit feedback did not receive an event.");Capture("02_SunDamage");yield return Frames(3);
            // Rejoin the authored route and walk the real controller into the shelter.
            stage.player.Teleport(new Vector3(-7,1.75f,-10),new Vector3(-15,1.7f,5));
            yield return Walk(new Vector3(-15,0,5));yield return Walk(new Vector3(-13,0,12));
            float until=Time.realtimeSinceStartup+20;while(journey.Progress.Step!=JourneyStep.ShelterTalk&&Time.realtimeSinceStartup<until)yield return null;
            yield return Frames(3);
            Check(journey.Progress.Step==JourneyStep.ShelterTalk,"Both actors did not reach physical cover.");
            Check(exposure.Vitality.Health==100&&exposure.PlayerIsSheltered,"Entering shade did not refill health.");
            Check(stage.GetComponent<GobiSequenceController>().VegetationIgnited,"Plants were not ignited after both actors entered cover.");
            float shelterStart=Time.realtimeSinceStartup;SetPhase("shelter conversation and fire");
            until=Time.realtimeSinceStartup+15;while(Vector3.Distance(companion.position,stage.player.transform.position)>4.8f&&Time.realtimeSinceStartup<until)yield return null;
            journey.Interact();yield return Frames(3);Check(journey.DialogueOpen,"Shelter conversation cannot open.");CheckInside(hud.Root.Q("dialogue"));
            Check(hud.Root.Q("chat-input")!=null&&hud.Root.Q("microphone")!=null,"Free text or microphone entry is missing.");
            var dialogue=stage.GetComponent<NpcDialogueDirector>();dialogue.Submit("你是谁？为什么带着沙漏？");
            until=Time.realtimeSinceStartup+13;while(dialogue.IsBusy&&Time.realtimeSinceStartup<until)yield return null;
            Check(!string.IsNullOrEmpty(dialogue.LastReply),"Dialogue service produced no visible response.");
            Capture("03_ShelterConversation");yield return Frames(3);
            float elapsed=journey.Progress.Elapsed;yield return Realtime(1.05f);Check(journey.Progress.Elapsed>elapsed+.8f,"Chat incorrectly freezes the shelter clock.");
            hud.SendMessage("TogglePause");yield return Frames(3);elapsed=journey.Progress.Elapsed;
            yield return Realtime(1.05f);Check(Mathf.Abs(journey.Progress.Elapsed-elapsed)<.1f,"Pause did not freeze the shelter clock.");
            hud.SendMessage("TogglePause");yield return Frames(3);
            // No hardware recording in automated QA: transcribe a known synthetic WAV through the same endpoint.
            var samples=new float[settings.warningLine.samples*settings.warningLine.channels];settings.warningLine.GetData(samples,0);
            byte[] wav=VoiceWave.EncodeMono16(samples,settings.warningLine.frequency,settings.warningLine.channels);
            using(var web=new UnityWebRequest(dialogue.settings.gatewayUrl+"/transcribe","POST"))
            {web.uploadHandler=new UploadHandlerRaw(wav);web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","audio/wav");web.timeout=32;yield return web.SendWebRequest();report.asrTranscript=web.downloadHandler.text;Check(web.result==UnityWebRequest.Result.Success&&web.downloadHandler.text.Contains("text")&&!web.downloadHandler.text.Contains("\"text\": \"\""),"Synthetic Chinese speech transcription failed.");}
            journey.CloseDialogue();yield return Frames(3);
            Check(!hud.Root.Query<Label>().ToList().Exists(label=>label.text!=null&&(label.text.Contains("HP")||label.text.Contains("生命值"))),"Hidden health leaked into the HUD.");
            // Exercise zero-health recovery without waiting for nine more damaging intervals.
            stage.player.Teleport(new Vector3(9,1.75f,-18),new Vector3(25,9,120));yield return Frames(15);exposure.Vitality.Tick(true,10);yield return Realtime(2.5f);
            Check(exposure.Vitality.Health==100&&stage.IsInShelter&&!exposure.IsRecovering,"Sunburn checkpoint recovery failed.");
            until=Time.realtimeSinceStartup+settings.burnSeconds+5;
            while(fire.BurnedCount<fire.IgnitedCount&&Time.realtimeSinceStartup<until)yield return null;
            report.ignited=fire.IgnitedCount;report.burned=fire.BurnedCount;report.shaded=fire.ShadedCount;
            Check(report.ignited>0&&report.burned==report.ignited,"Exposed shrubs did not finish as ash.");Check(report.shaded>0,"No shaded plant survived the sunlight.");
            stage.player.FaceTarget(new Vector3(8,1,28));Capture("04_AshesAndShade");yield return Frames(3);
            SetPhase(report.realTenMinuteWait?"waiting the full 600 seconds":"checking the 600-second boundary with injected time");
            if(!report.realTenMinuteWait)
            {
                journey.Progress.Tick(Mathf.Max(0,settings.shelterWaitSeconds-journey.Progress.Elapsed-.2f));
                Check(journey.Progress.Step==JourneyStep.ShelterTalk,"Shelter ended before the configured wait.");journey.Progress.Tick(.25f);
            }
            else
            {
                float next=Time.realtimeSinceStartup+30;
                while(journey.Progress.Step==JourneyStep.ShelterTalk)
                {if(Time.realtimeSinceStartup>next){next+=30;Save();}yield return null;}
            }
            report.shelterWallSeconds=Time.realtimeSinceStartup-shelterStart;
            if(report.realTenMinuteWait)Check(report.shelterWallSeconds>=599,"The full real-time shelter wait was skipped.");
            Check(journey.Progress.Step==JourneyStep.Sunset,"Sunset was not triggered by the clock.");
            float sunsetStart=Time.realtimeSinceStartup;until=sunsetStart+6;
            while(journey.Progress.Step==JourneyStep.Sunset&&Time.realtimeSinceStartup<until)yield return null;
            report.sunsetWallSeconds=Time.realtimeSinceStartup-sunsetStart;
            Check(journey.Progress.Step==JourneyStep.WalkToChaoge&&stage.Current.sunCount==0&&report.sunsetWallSeconds<3.5f,"Sudden sunset did not open the night walk.");
            Capture("05_SuddenNight");yield return Frames(3);SetPhase("night walk to automatic arrival");
            foreach(var point in new[]{new Vector3(-15,0,18),new Vector3(-14,0,26),new Vector3(-12,0,31),new Vector3(2,0,42),new Vector3(2,0,64)})yield return Walk(point);
            until=Time.realtimeSinceStartup+25;while(SceneManager.GetActiveScene().name!=TravellerStage.SceneNames[1]&&Time.realtimeSinceStartup<until)yield return null;
            Check(SceneManager.GetActiveScene().name==TravellerStage.SceneNames[1],"Walking with the companion did not automatically arrive at Chaoge.");
            yield return Frames(10);Bind();Check(SceneManager.sceneCount==1&&!journey.IsTitle,"Arrival retained Gobi or returned to the title.");Capture("06_AutoChaogeArrival");yield return Frames(3);
            report.mechanicsPassed=report.errors.Count==0;
            // Measure the first scene in isolated preview mode after the complete playable sequence.
            journey.ReturnToTitle();var load=SceneManager.LoadSceneAsync(TravellerStage.SceneNames[0]);while(!load.isDone)yield return null;yield return Frames(10);Bind();journey.TogglePreview();stage.player.enabled=false;hud.Root.style.display=DisplayStyle.None;
            SetPhase("six scene performance views");
            for(int moment=0;moment<6;moment++)
            {
                stage.ApplyMoment(moment,true);yield return Frames(180);var frames=new List<float>();float sum=0,gpu=0,mainMs=0;int gpuCount=0,renderCount=0,mainCount=0;long triangles=0,batches=0;var timings=new FrameTiming[1];ulong last=0;
                using(var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
                using(var batch=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1))
                using(var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
                for(int frame=0;frame<360;frame++)
                {
                    FrameTimingManager.CaptureFrameTimings();yield return null;float ms=Time.unscaledDeltaTime*1000;frames.Add(ms);sum+=ms;
                    if(main.Valid&&main.LastValue>0){mainMs+=main.LastValue/1000000f;mainCount++;}
                    if(batch.Valid&&tris.Valid&&batch.LastValue>0){batches+=batch.LastValue;triangles+=tris.LastValue;renderCount++;}
                    if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].frameStartTimestamp!=last&&timings[0].gpuFrameTime>0){last=timings[0].frameStartTimestamp;gpu+=(float)timings[0].gpuFrameTime;gpuCount++;}
                }
                frames.Sort();var metric=new Metric{moment=moment,meanMs=sum/frames.Count,p95Ms=frames[(int)(frames.Count*.95f)],gpuMeanMs=gpuCount>0?gpu/gpuCount:-1,mainThreadMeanMs=mainCount>0?mainMs/mainCount:-1,triangles=renderCount>0?triangles/renderCount:-1,batches=renderCount>0?batches/renderCount:-1};report.metrics.Add(metric);Check(metric.p95Ms<16.67f,"Act one view "+moment+" exceeded 60 FPS budget: "+metric.p95Ms);Capture("View_"+moment);yield return Frames(3);
            }
            report.performancePassed=report.metrics.Count==6&&!report.metrics.Exists(metric=>metric.p95Ms>=16.67f);
            report.passed=report.errors.Count==0;SetPhase("complete");RestoreJournal();Application.Quit(report.passed?0:1);
        }
        private void Bind()
        {
            stage=UnityEngine.Object.FindFirstObjectByType<TravellerStage>();journey=stage.Journey;hud=UnityEngine.Object.FindFirstObjectByType<TravellerHud>();
            foreach(var actor in stage.GetComponentsInChildren<TravellerActor>())if(actor.role==TravellerRole.WenWang){companion=actor.transform;break;}
        }
        private IEnumerator Walk(Vector3 target)
        {
            stage.player.enabled=false;float until=Time.realtimeSinceStartup+40,stalled=0;
            while(Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(target.x,target.z))>.3f&&Time.realtimeSinceStartup<until&&!stage.IsLoading)
            {
                Vector3 before=stage.player.transform.position;float distance=Vector3.ProjectOnPlane(target-before,Vector3.up).magnitude;
                stage.player.FaceTarget(target+Vector3.up*1.6f);stage.player.MoveInput(Vector2.up*Mathf.Min(1,distance/(5.4f*Mathf.Max(.001f,Time.deltaTime))),true,Time.deltaTime);
                float moved=Vector3.Distance(before,stage.player.transform.position);report.walkedMetres+=moved;stalled=moved<.002f?stalled+Time.deltaTime:0;
                if(stalled>2){Check(false,"First-person controller blocked at "+stage.player.transform.position+" toward "+target);yield break;}
                yield return null;
            }
            Check(Time.realtimeSinceStartup<until,"Walk timed out at "+target);
        }
        private void CheckInside(VisualElement element){var outer=hud.Root.worldBound;var inner=element.worldBound;Check(inner.width>0&&inner.height>0&&inner.xMin>=outer.xMin-2&&inner.yMin>=outer.yMin-2&&inner.xMax<=outer.xMax+2&&inner.yMax<=outer.yMax+2,"UI panel outside viewport: "+element.name);}
        private static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        private static IEnumerator Realtime(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        private void Check(bool condition,string error){report.checks++;if(!condition)report.errors.Add(error);Save();}
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private void SetPhase(string value){report.phase=value;Save();}
        private void Save(){File.WriteAllText(output,JsonUtility.ToJson(report,true));}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){report.errors.Add(message);Save();}}
        private void RestoreJournal(){if(!capturedJournal)return;if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",savedJournal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();capturedJournal=false;}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;RestoreJournal();}
    }
}
#endif
