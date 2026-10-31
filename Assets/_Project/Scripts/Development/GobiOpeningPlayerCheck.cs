#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    // Opt-in acceptance run using the real controller, HTTP gateway, geometry and scene loader.
    public sealed class GobiOpeningPlayerCheck : MonoBehaviour
    {
        [Serializable] private class Report
        {
            public bool passed,hardwareMicrophoneUsed,onlineModelUsed,microphoneAutoSendVerified;
            public bool asrStartedFocused,asrFinishedFocused;
            public int asrGenerationStart,asrGenerationEnd;
            public string asrCallbackStatus,asrAutoSendTranscript;
            public int checks,openingAnswers,damageEvents,ignited,burned,shaded;
            public float walkedMetres,frameMeanMs,frameP95Ms,sunriseSeconds,sunsetSeconds;
            public string phase,asrTranscript,gatewayMode,measurement="Native development player at 1920x1080";
            public List<string> errors=new List<string>();
        }
        [Serializable] private class Transcript{public string text;}
        private readonly Report report=new Report();
        private string output,savedJournal;
        private bool journalExisted,captured,restored;
        private TravellerStage stage;
        private TravellerJourney journey;
        private TravellerHud hud;
        private GobiSequenceController sequence;
        private NpcDialogueDirector dialogue;
        private GobiStorySettings settings;
        private Transform companion;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-gobi-opening-check");
            if(i<0||i+1>=args.Length||UnityEngine.Object.FindFirstObjectByType<GobiOpeningPlayerCheck>()!=null)return;
            var go=new GameObject("Development Gobi Awakening Check");DontDestroyOnLoad(go);go.AddComponent<GobiOpeningPlayerCheck>().output=args[i+1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));journalExisted=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");savedJournal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");captured=true;
            Application.logMessageReceived+=OnLog;Application.runInBackground=true;QualitySettings.vSyncCount=0;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return Frames(12);
            stage=UnityEngine.Object.FindFirstObjectByType<TravellerStage>();journey=stage.Journey;hud=stage.GetComponentInChildren<TravellerHud>();
            sequence=stage.GetComponent<GobiSequenceController>();dialogue=stage.GetComponent<NpcDialogueDirector>();settings=stage.profile.gobiStory;
            foreach(var actor in stage.GetComponentsInChildren<TravellerActor>())if(actor.role==TravellerRole.WenWang)companion=actor.transform;
            using(var web=UnityWebRequest.Get(dialogue.settings.gatewayUrl+"/health"))
            {web.timeout=5;yield return web.SendWebRequest();report.gatewayMode=web.downloadHandler.text;Check(web.result==UnityWebRequest.Result.Success,"Local voice gateway is unavailable.");}
            Check(journey.IsTitle&&stage.Current.sunCount==0,"The game did not open at the night title.");
            Phase("waking in the open desert");journey.Begin();stage.player.enabled=false;
            yield return Realtime(.2f);Check(journey.Progress.Step==JourneyStep.Awakening&&stage.player.InputBlocked,"Awakening does not briefly block walking.");Capture("01_Waking");yield return Frames(3);
            yield return Realtime(settings.wakeSeconds+.1f);Check(journey.Progress.Step==JourneyStep.ExploreDesert&&!stage.player.InputBlocked,"Waking did not enable exploration.");
            Check(journey.Objective=="调查四周，看看发生了什么事","The first objective is incorrect.");
            Check(hud.Root.Q("quest-marker").resolvedStyle.display==DisplayStyle.Flex,"Green navigation is missing.");
            Vector3 before=companion.position;yield return Realtime(2);Check(Vector3.Distance(before,companion.position)<.1f,"The stranger walked away before being met.");
            Check(stage.Current.sunCount==0,"A timer raised the sun while the player was still exploring.");Capture("02_Investigate");yield return Frames(3);
            yield return Walk(new Vector3(-4,0,-14));Check(journey.Progress.Step==JourneyStep.ApproachTraveller,"The nearby fire did not trigger the invitation.");Capture("03_ComeHere");yield return Frames(3);
            yield return Walk(new Vector3(-6,0,-5.7f));yield return Frames(3);
            Check(journey.Progress.Step==JourneyStep.OpeningTalk,"Walking up to the stranger did not start the brief conversation.");
            Check(journey.Speech.Contains("快过来，现在是乱纪元，不要命吗？"),"The requested greeting is missing.");
            Check(dialogue.EnsureVoiceConversation(),"Nearby push-to-talk cannot open the conversation.");
            Check(!stage.player.InputBlocked,"Conversation unexpectedly prevents walking.");Capture("04_FirstConversation");yield return Frames(3);
            int openingTurn=0;
            foreach(string question in new[]{"这里是什么地方？","你是谁？","这里是三体游戏，有三个太阳吗？"})
            {
                if(openingTurn==1)
                {
                    // Run the actual ASR coroutine and its auto-send callback using synthetic PCM.
                    // Recording, device permission and environmental audio are deliberately excluded.
                    var mic=stage.GetComponent<PlayerMicrophoneInput>();
                    Check(mic.PrepareQuestion(),"The F question could not lock its initial recipient.");
                    var pcm=new float[settings.meetingLine.samples*settings.meetingLine.channels];settings.meetingLine.GetData(pcm,0);
                    byte[] synthetic=VoiceWave.EncodeMono16(pcm,settings.meetingLine.frequency,settings.meetingLine.channels);
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    int token=(int)typeof(PlayerMicrophoneInput).GetField("generation",flags).GetValue(mic);
                    report.asrStartedFocused=Application.isFocused;report.asrGenerationStart=token;
                    Action<string> captureTranscript=value=>report.asrAutoSendTranscript=value;mic.TranscriptReady+=captureTranscript;
                    mic.StartCoroutine((IEnumerator)typeof(PlayerMicrophoneInput).GetMethod("Transcribe",flags).Invoke(mic,new object[]{synthetic,token}));
                    float transcriptDeadline=Time.realtimeSinceStartup+35;while(mic.IsTranscribing&&Time.realtimeSinceStartup<transcriptDeadline)yield return null;
                    mic.TranscriptReady-=captureTranscript;report.asrCallbackStatus=dialogue.Status;report.asrFinishedFocused=Application.isFocused;
                    report.asrGenerationEnd=(int)typeof(PlayerMicrophoneInput).GetField("generation",flags).GetValue(mic);
                    yield return WaitForReply();
                    report.microphoneAutoSendVerified=journey.Progress.OpeningAnswers==2;
                    Check(report.microphoneAutoSendVerified,"The actual ASR callback did not automatically submit the recognized words.");
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),"-f-dialogue-asr-check")>=0)
                    {report.phase="F ASR callback";report.passed=report.errors.Count==0;Save();Restore();Application.Quit(report.passed?0:2);yield break;}
                }
                else{dialogue.Submit(question);yield return WaitForReply();}
                Check(!string.IsNullOrEmpty(dialogue.LastReply),"The traveller did not answer.");
                Check(!dialogue.LastReply.Contains("姬昌")&&!dialogue.LastReply.Contains("三体")&&!dialogue.LastReply.Contains("三个太阳"),"The opening leaked identity or lore.");
                openingTurn++;
            }
            report.openingAnswers=journey.Progress.OpeningAnswers;Check(report.openingAnswers==3,"The opening answer budget is incorrect.");
            bool fourthPrepared=dialogue.BeginPlayerQuestion();
            bool fourthAccepted=fourthPrepared&&dialogue.SubmitQuestion("再问一个问题。",dialogue.QuestionTarget,dialogue.QuestionId);dialogue.CancelQuestion();
            Check(journey.Progress.OpeningAnswers==3&&fourthAccepted,"The story answer budget must not disable further free questions.");
            Phase("rapid sunrise and real sunlight damage");
            yield return UntilStep(JourneyStep.Sunrise,45);Check(journey.Progress.Step==JourneyStep.Sunrise,"The completed brief conversation did not raise the sun.");
            float started=Time.realtimeSinceStartup,previousY=((Vector3)RenderSettings.skybox.GetVector("_SunA")).y;
            bool rises=true;
            while(journey.Progress.Step==JourneyStep.Sunrise&&Time.realtimeSinceStartup-started<8)
            {
                float y=((Vector3)RenderSettings.skybox.GetVector("_SunA")).y;rises&=y>=previousY-.001f;previousY=y;yield return null;
            }
            report.sunriseSeconds=Time.realtimeSinceStartup-started;Check(rises&&sequence.SunHasRisen,"The sun did not rise continuously from the horizon.");Capture("05_GiantSun");yield return Frames(3);
            var exposure=stage.GetComponent<SunExposureSystem>();stage.player.Teleport(new Vector3(9,1.75f,-18),new Vector3(25,9,120));yield return Realtime(1.2f);
            report.damageEvents=exposure.DamageEvents;Check(exposure.Vitality.Health<100&&exposure.Vitality.Health%10==0&&exposure.HitPulse>0,"Sunlight failed to apply hidden ten-point hits.");Capture("06_SunDamage");yield return Frames(3);
            Check(journey.Progress.Step==JourneyStep.SeekShelter,"The story skipped shelter while the player was exposed.");
            stage.player.Teleport(new Vector3(-7,1.75f,-10),new Vector3(-15,1.7f,5));
            yield return Walk(new Vector3(-15,0,5));yield return Walk(new Vector3(-13,0,12));yield return UntilStep(JourneyStep.ShelterTalk,15);
            Check(journey.Progress.Step==JourneyStep.ShelterTalk&&sequence.BothInShade,"Both actors did not reach real shade.");
            Check(exposure.Vitality.Health==100,"Cover failed to restore hidden health.");
            Check(sequence.VegetationIgnited,"Vegetation was not ignited after both actors entered cover.");
            Phase("shelter introduction, speech and ash");
            yield return Realtime(6);Check(journey.Progress.KnowsIdentity&&journey.Progress.AskedPlayerOrigin,"The traveller did not introduce himself and ask why the player is alone.");Capture("07_JiChang");yield return Frames(3);
            hud.SendMessage("TogglePause");yield return Frames(3);float pausedAt=journey.Progress.Elapsed;yield return Realtime(1.1f);
            Check(Mathf.Abs(journey.Progress.Elapsed-pausedAt)<.1f,"Pausing did not freeze the narrative clock.");hud.SendMessage("TogglePause");
            // Send known synthetic audio through the actual ASR route; no hardware recording.
            var samples=new float[settings.warningLine.samples*settings.warningLine.channels];settings.warningLine.GetData(samples,0);byte[] wav=VoiceWave.EncodeMono16(samples,settings.warningLine.frequency,settings.warningLine.channels);
            using(var web=new UnityWebRequest(dialogue.settings.gatewayUrl+"/transcribe","POST"))
            {
                web.uploadHandler=new UploadHandlerRaw(wav);web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","audio/wav");web.timeout=32;
                yield return web.SendWebRequest();Transcript result=null;try{result=JsonUtility.FromJson<Transcript>(web.downloadHandler.text);}catch(Exception){}
                report.asrTranscript=result==null?"":result.text;Check(web.result==UnityWebRequest.Result.Success&&!string.IsNullOrWhiteSpace(report.asrTranscript),"Synthetic Chinese speech failed to transcribe.");
            }
            Check(dialogue.settings.autoSubmitMicrophone,"Microphone recognition does not automatically send the player's words.");
            var fire=stage.GetComponent<GobiVegetationFire>();float deadline=Time.realtimeSinceStartup+settings.burnSeconds+3;
            while(fire.BurnedCount<fire.IgnitedCount&&Time.realtimeSinceStartup<deadline)yield return null;
            report.ignited=fire.IgnitedCount;report.burned=fire.BurnedCount;report.shaded=fire.ShadedCount;
            Check(report.ignited>0&&report.burned==report.ignited&&report.shaded>0,"Sunlit shrubs did not become ash while shaded plants survived.");Capture("08_ShadeAndAsh");yield return Frames(3);
            yield return Walk(new Vector3(-14,0,12));dialogue.EnsureVoiceConversation();
            dialogue.Submit("你是谁？为什么去朝歌？");yield return WaitForReply();
            Check(dialogue.LastReply!=null&&dialogue.LastReply.Contains("姬昌"),"The safe conversation did not reveal Ji Chang.");
            dialogue.Submit("我迷路了。现在好多了，可以和你一同去朝歌。");yield return WaitForReply();
            dialogue.CancelPending();dialogue.RequestNarrativeDecision();
            Phase("AI narrative intent and reverse sunset");yield return UntilStep(JourneyStep.Sunset,35);
            Check(journey.Progress.Step==JourneyStep.Sunset,"A valid narrative choice did not end the shelter conversation.");
            started=Time.realtimeSinceStartup;previousY=((Vector3)RenderSettings.skybox.GetVector("_SunA")).y;bool descends=true;
            while(journey.Progress.Step==JourneyStep.Sunset&&Time.realtimeSinceStartup-started<6)
            {
                float y=((Vector3)RenderSettings.skybox.GetVector("_SunA")).y;descends&=y<=previousY+.001f;previousY=y;yield return null;
            }
            report.sunsetSeconds=Time.realtimeSinceStartup-started;Check(descends&&stage.Current.sunCount==0,"Sunset did not retrace the rising path into night.");
            Check(journey.Objective=="与姬昌一同前往朝歌","The departure objective did not change.");Capture("09_NightDeparture");yield return Frames(3);
            Phase("follow Ji Chang and arrive at Chaoge");
            foreach(var point in new[]{new Vector3(-15,0,18),new Vector3(-14,0,26),new Vector3(-12,0,31),new Vector3(2,0,42),new Vector3(2,0,64)})yield return Walk(point);
            deadline=Time.realtimeSinceStartup+25;while(SceneManager.GetActiveScene().name!=TravellerStage.SceneNames[1]&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(SceneManager.GetActiveScene().name==TravellerStage.SceneNames[1]&&SceneManager.sceneCount==1,"The night walk did not transition to the separate Chaoge scene.");
            yield return Frames(12);Capture("10_ChaogeArrival");yield return Frames(3);
            var arrival=UnityEngine.Object.FindFirstObjectByType<TravellerStage>();Check(arrival.player.transform.position.z<-220&&!arrival.Journey.IsTitle,"Chaoge did not begin outside its own city gate.");
            // One representative Gobi view, using the final native player and the production quality settings.
            arrival.Journey.ReturnToTitle();var operation=SceneManager.LoadSceneAsync(TravellerStage.SceneNames[0]);while(!operation.isDone)yield return null;yield return Frames(12);
            stage=UnityEngine.Object.FindFirstObjectByType<TravellerStage>();stage.Journey.TogglePreview();stage.ApplyMoment(1,true);stage.player.enabled=false;
            Phase("native Gobi frame sample");yield return Frames(180);var times=new List<float>();float sum=0;
            for(int i=0;i<360;i++){yield return null;float ms=Time.unscaledDeltaTime*1000;sum+=ms;times.Add(ms);}
            times.Sort();report.frameMeanMs=sum/times.Count;report.frameP95Ms=times[(int)(times.Count*.95f)];
            Check(report.frameP95Ms<16.67f,"The representative Gobi view exceeded the 60 FPS frame budget: "+report.frameP95Ms);
            report.passed=report.errors.Count==0;Phase("complete");Restore();Application.Quit(report.passed?0:1);
        }
        private IEnumerator WaitForReply()
        {
            float until=Time.realtimeSinceStartup+15;while(dialogue.IsBusy&&Time.realtimeSinceStartup<until)yield return null;
            Check(!dialogue.IsBusy,"Dialogue request timed out.");yield return Frames(2);
        }
        private IEnumerator UntilStep(JourneyStep step,float seconds)
        {
            float until=Time.realtimeSinceStartup+seconds;while(journey.Progress.Step!=step&&Time.realtimeSinceStartup<until)yield return null;
        }
        private IEnumerator Walk(Vector3 goal)
        {
            float until=Time.realtimeSinceStartup+30;Vector3 previous=stage.player.transform.position;
            while(Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(goal.x,goal.z))>.45f&&Time.realtimeSinceStartup<until&&!stage.IsLoading)
            {
                Vector3 delta=goal-stage.player.transform.position;delta.y=0;stage.player.FaceTarget(goal+Vector3.up*stage.player.eyeHeight);
                Vector3 local=stage.player.transform.InverseTransformDirection(delta.normalized);stage.player.MoveInput(new Vector2(local.x,local.z),false,Time.deltaTime);
                report.walkedMetres+=Vector3.Distance(previous,stage.player.transform.position);previous=stage.player.transform.position;yield return null;
            }
            Check(Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(goal.x,goal.z))<.65f||stage.IsLoading,"Walk route blocked near "+goal);
        }
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        private static IEnumerator Realtime(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        private void Phase(string value){report.phase=value;Save();}
        private void Check(bool condition,string message){report.checks++;if(!condition)report.errors.Add(message);Save();}
        private void Save(){File.WriteAllText(output,JsonUtility.ToJson(report,true));}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){report.errors.Add(message);Save();}}
        private void Restore(){if(restored||!captured)return;if(journalExisted)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",savedJournal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();restored=true;}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;Restore();}
    }
}
#endif
