#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ThreeBodyProblem
{
    public sealed class LocalVoicePlayerCheck : MonoBehaviour
    {
        [Serializable] private class Report
        {
            public bool passed,functionalPassed,performancePassed,hardwareMicrophoneUsed;
            public int checks,asrRequests,speechRequests,playedSampleRate;
            public float audioStartupSeconds,frameMeanMs,frameP95Ms;
            public string health,finalHealth;public List<string> errors=new List<string>();
        }
        [Serializable] private class SpeechRequest{public string text,npcId;public bool cache;}
        private readonly Report report=new Report();private string output;
        private TravellerStage stage;private TravellerJourney journey;private NpcDialogueDirector dialogue;
        private bool samplingDone,trafficDone;private byte[] synthetic;
        private string savedJournal;private bool hadJournal,captured;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-traveller-local-voice-check");
            if(i<0||i+1>=args.Length||UnityEngine.Object.FindFirstObjectByType<LocalVoicePlayerCheck>()!=null)return;
            var go=new GameObject("Development Local Voice Check");DontDestroyOnLoad(go);go.AddComponent<LocalVoicePlayerCheck>().output=args[i+1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));Application.logMessageReceived+=OnLog;
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            hadJournal=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");savedJournal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");captured=true;
            yield return Frames(12);stage=UnityEngine.Object.FindFirstObjectByType<TravellerStage>();journey=stage.Journey;dialogue=stage.GetComponent<NpcDialogueDirector>();
            using(var web=UnityWebRequest.Get(dialogue.settings.gatewayUrl+"/health"))
            {web.timeout=10;yield return web.SendWebRequest();report.health=web.downloadHandler.text;Check(web.result==UnityWebRequest.Result.Success&&report.health.Contains("qwen3-asr-mlx-8bit")&&report.health.Contains("moss-tts-nano-onnx-cpu"),"The running service did not use the selected neural models.");}
            journey.Begin();stage.player.enabled=false;yield return Frames(8);
            journey.Progress.Tick(stage.profile.gobiStory.nightMeetingSeconds);yield return Realtime(stage.profile.transitionSeconds+.2f);
            stage.player.Teleport(new Vector3(-13,1.75f,12),new Vector3(-16,1.4f,13));
            foreach(var actor in stage.GetComponentsInChildren<TravellerActor>())if(actor.role==TravellerRole.WenWang)actor.transform.position=new Vector3(-16,0,13);
            float until=Time.realtimeSinceStartup+12;while(journey.Progress.Step!=JourneyStep.ShelterTalk&&Time.realtimeSinceStartup<until)yield return null;
            yield return Frames(6);Check(journey.Progress.Step==JourneyStep.ShelterTalk,"Voice check could not reach the shelter.");
            journey.Interact();yield return Frames(3);Check(journey.DialogueOpen,"Voice check cannot open the NPC conversation.");
            float began=Time.realtimeSinceStartup;dialogue.Submit("你是谁？为什么要去朝歌？");
            until=began+35;while((!dialogue.IsSpeaking||string.IsNullOrEmpty(dialogue.LastReply))&&Time.realtimeSinceStartup<until)yield return null;
            report.audioStartupSeconds=Time.realtimeSinceStartup-began;report.playedSampleRate=dialogue.CurrentSpeechSampleRate;
            Check(dialogue.IsSpeaking&&report.playedSampleRate==24000,"Unity did not play the new 24 kHz NPC voice.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"LocalVoiceConversation.png"));
            dialogue.CancelPending();yield return Frames(2);Check(!dialogue.IsSpeaking&&!dialogue.IsBusy,"Interrupt did not stop the NPC voice.");
            dialogue.Submit("现在可以继续赶路吗？");yield return Frames(2);dialogue.CancelPending();yield return Realtime(3);
            Check(!dialogue.IsSpeaking&&!dialogue.IsBusy,"A canceled reply resumed playing later.");
            var clip=stage.profile.gobiStory.warningLine;var samples=new float[clip.samples*clip.channels];clip.GetData(samples,0);synthetic=VoiceWave.EncodeMono16(samples,clip.frequency,clip.channels);
            yield return Frames(180);StartCoroutine(TurnTraffic());var times=new List<float>();float sum=0;
            for(int i=0;i<900;i++){yield return null;float ms=Time.unscaledDeltaTime*1000;sum+=ms;times.Add(ms);}
            samplingDone=true;until=Time.realtimeSinceStartup+40;while(!trafficDone&&Time.realtimeSinceStartup<until)yield return null;
            Check(trafficDone&&report.asrRequests>=2&&report.speechRequests>=2,"The frame sample did not include active ASR and speech turns.");
            times.Sort();report.frameMeanMs=sum/times.Count;report.frameP95Ms=times[(int)(times.Count*.95f)];report.functionalPassed=report.errors.Count==0;
            report.performancePassed=report.frameP95Ms<16.67f;Check(report.performancePassed,"Voice-enabled gameplay exceeded the 60 FPS frame budget: "+report.frameP95Ms);
            using(var web=UnityWebRequest.Get(dialogue.settings.gatewayUrl+"/health")){web.timeout=10;yield return web.SendWebRequest();report.finalHealth=web.downloadHandler.text;}
            report.passed=report.errors.Count==0;Save();Restore();Application.Quit(report.passed?0:1);
        }
        private IEnumerator TurnTraffic()
        {
            int turn=0;
            while(!samplingDone)
            {
                using(var web=new UnityWebRequest(dialogue.settings.gatewayUrl+"/transcribe","POST"))
                {
                    web.uploadHandler=new UploadHandlerRaw(synthetic);web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","audio/wav");web.timeout=32;
                    yield return web.SendWebRequest();Check(web.result==UnityWebRequest.Result.Success&&web.downloadHandler.text.Contains("text"),"In-game neural transcription request failed.");report.asrRequests++;
                }
                using(var web=new UnityWebRequest(dialogue.settings.gatewayUrl+"/speech","POST"))
                {
                    string text="这条夜路走到第"+turn+"处，先看清前面的岩石再走。";
                    web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SpeechRequest{text=text,npcId="wenwang"})));web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","application/json");web.timeout=35;
                    yield return web.SendWebRequest();Check(web.result==UnityWebRequest.Result.Success&&web.downloadHandler.data.Length>44,"In-game neural speech request failed.");report.speechRequests++;
                }
                turn++;
            }
            trafficDone=true;
        }
        private static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        private static IEnumerator Realtime(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        private void Check(bool condition,string message){report.checks++;if(!condition)report.errors.Add(message);Save();}
        private void Save(){File.WriteAllText(output,JsonUtility.ToJson(report,true));}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){report.errors.Add(message);Save();}}
        private void Restore(){if(!captured)return;if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",savedJournal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();captured=false;}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;Restore();}
    }
}
#endif
