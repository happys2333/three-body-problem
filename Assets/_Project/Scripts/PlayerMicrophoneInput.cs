using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ThreeBodyProblem
{
    public sealed class PlayerMicrophoneInput : MonoBehaviour
    {
        public bool IsRecording {get;private set;}
        public bool IsTranscribing {get;private set;}
        public bool NeedsTextFallback {get;private set;}
        public bool AwaitingPermission => awaitingPermission;
        public event Action<string> TranscriptReady;
        private NpcDialogueDirector dialogue;private TravellerStage stage;
        private TravellerHud hud;
        private AudioClip recording;private string device;private float began;private UnityWebRequest pending;private int generation;private bool awaitingPermission;
        private NpcIdentity recordingTarget;
        private int questionId;
        [Serializable] private class Transcript{public string text,error;}
        private void Start(){dialogue=GetComponent<NpcDialogueDirector>();stage=GetComponent<TravellerStage>();hud=GetComponentInChildren<TravellerHud>();}
        public void ToggleCapture(){if(IsRecording)FinishCapture();else if(!IsTranscribing)StartCoroutine(BeginCapture());}
        public bool PrepareQuestion()
        {
            if(dialogue==null||!dialogue.BeginPlayerQuestion())return false;
            recordingTarget=dialogue.QuestionTarget;questionId=dialogue.QuestionId;return true;
        }
        public IEnumerator BeginCapture(bool fromKeyboard=false)
        {
            if(IsRecording||IsTranscribing||awaitingPermission||stage==null||!PrepareQuestion()||(hud!=null&&hud.IsWorldPaused))yield break;
            NeedsTextFallback=false;
            var sequence=GetComponent<GobiSequenceController>();sequence?.NotePlayerSpeech();
            int token=++generation;awaitingPermission=true;
            dialogue.CancelPending();dialogue.SetStatus("准备麦克风……");
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone))yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(token!=generation)yield break;
            awaitingPermission=false;
            if(!isActiveAndEnabled||stage==null||!stage.Journey.DialogueOpen||(hud!=null&&hud.IsWorldPaused))yield break;
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)){TextFallback("麦克风未获授权，仍可打字交谈");yield break;}
            if(fromKeyboard&&!Input.GetKey(KeyCode.F)){dialogue.CancelQuestion();dialogue.SetStatus("麦克风已就绪，按住 F 说话");yield break;}
            if(Microphone.devices.Length==0){TextFallback("没有找到麦克风，仍可打字交谈");yield break;}
            device=null;recording=Microphone.Start(device,false,25,16000);
            if(recording==null){TextFallback("无法启动麦克风，仍可打字交谈");yield break;}
            began=Time.realtimeSinceStartup;IsRecording=true;
            dialogue.SetStatus("正在向"+dialogue.DisplayName(recordingTarget)+"提问 · 松开 F 自动发送");
        }
        public void FinishCapture()
        {
            if(!IsRecording)return;int frames=Microphone.GetPosition(device);Microphone.End(device);IsRecording=false;
            if(recording==null||frames<1600){if(recording!=null)Destroy(recording);recording=null;dialogue.CancelQuestion();dialogue.SetStatus("说得太短了，按住 F 重新提问");return;}
            var samples=new float[frames*recording.channels];recording.GetData(samples,0);
            byte[] wav=VoiceWave.EncodeMono16(samples,recording.frequency,recording.channels);Destroy(recording);recording=null;
            StartCoroutine(Transcribe(wav,++generation));
        }
        private IEnumerator Transcribe(byte[] wav,int token)
        {
            var target=recordingTarget;int turn=questionId;
            IsTranscribing=true;dialogue.SetStatus("正在转写……");
            using(var web=new UnityWebRequest(dialogue.settings.gatewayUrl.TrimEnd('/')+"/transcribe","POST"))
            {
                web.uploadHandler=new UploadHandlerRaw(wav);web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","audio/wav");web.timeout=32;pending=web;
                yield return web.SendWebRequest();if(token!=generation)yield break;pending=null;IsTranscribing=false;
                if(web.result!=UnityWebRequest.Result.Success){TextFallback("语音识别暂不可用，请重试或打字");yield break;}
                Transcript result=null;try{result=JsonUtility.FromJson<Transcript>(web.downloadHandler.text);}catch(Exception){}
                if(result==null||string.IsNullOrWhiteSpace(result.text)){TextFallback("没有听清，请重说或打字");yield break;}
                TranscriptReady?.Invoke(result.text);
                if(!dialogue.SubmitQuestion(result.text,target,turn))
                {dialogue.CancelQuestion();dialogue.SetStatus("刚才的提问已取消，按住 F 重新提问");}
            }
        }
        private void Update()
        {
            if(stage==null)return;
            if((hud!=null&&hud.IsWorldPaused)||stage.Journey.IsPreviewing){CancelCapture();return;}
            if(!stage.Journey.DialogueOpen&&(IsRecording||IsTranscribing||awaitingPermission)){CancelCapture();return;}
            if(Input.GetKeyDown(KeyCode.F)&&!awaitingPermission&&(hud==null||!hud.IsEditingText))
            {CancelCapture();StartCoroutine(BeginCapture(true));}
            if(Input.GetKeyUp(KeyCode.F)&&IsRecording)FinishCapture();
            if(IsRecording&&Time.realtimeSinceStartup-began>24)FinishCapture();
        }
        public void CancelCapture()
        {
            bool active=awaitingPermission||IsRecording||IsTranscribing;
            if(awaitingPermission){generation++;awaitingPermission=false;}
            if(IsRecording){Microphone.End(device);IsRecording=false;}
            if(recording!=null){Destroy(recording);recording=null;}
            if(pending!=null){generation++;pending.Abort();pending=null;IsTranscribing=false;}
            recordingTarget=null;
            if(active&&dialogue!=null)dialogue.CancelQuestion();
        }
        private void OnApplicationFocus(bool focused){if(!focused&&(IsRecording||IsTranscribing))CancelCapture();}
        private void TextFallback(string message){NeedsTextFallback=true;dialogue.CancelQuestion();dialogue.SetStatus(message);}
        private void OnApplicationPause(bool paused){if(paused)CancelCapture();}
        private void OnDisable(){CancelCapture();}
    }
}
