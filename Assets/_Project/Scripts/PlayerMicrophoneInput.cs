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
        public event Action<string> TranscriptReady;
        private NpcDialogueDirector dialogue;private TravellerStage stage;
        private TravellerHud hud;
        private AudioClip recording;private string device;private float began;private UnityWebRequest pending;private int generation;private bool awaitingPermission;
        [Serializable] private class Transcript{public string text,error;}
        private void Start(){dialogue=GetComponent<NpcDialogueDirector>();stage=GetComponent<TravellerStage>();hud=GetComponentInChildren<TravellerHud>();}
        public void ToggleCapture(){if(IsRecording)FinishCapture();else if(!IsTranscribing)StartCoroutine(BeginCapture());}
        public IEnumerator BeginCapture()
        {
            if(IsRecording||IsTranscribing||awaitingPermission||stage==null||!stage.Journey.DialogueOpen||(hud!=null&&hud.IsWorldPaused))yield break;
            int token=++generation;awaitingPermission=true;
            dialogue.CancelPending();dialogue.SetStatus("准备麦克风……");
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone))yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if(token!=generation)yield break;
            awaitingPermission=false;
            if(!isActiveAndEnabled||stage==null||!stage.Journey.DialogueOpen||(hud!=null&&hud.IsWorldPaused))yield break;
            if(!Application.HasUserAuthorization(UserAuthorization.Microphone)){dialogue.SetStatus("麦克风未获授权，仍可打字交谈");yield break;}
            if(Microphone.devices.Length==0){dialogue.SetStatus("没有找到麦克风，仍可打字交谈");yield break;}
            device=null;recording=Microphone.Start(device,false,25,16000);
            if(recording==null){dialogue.SetStatus("无法启动麦克风，仍可打字交谈");yield break;}
            began=Time.realtimeSinceStartup;IsRecording=true;
            dialogue.SetStatus("正在听 · 松开 V 或再次点击结束");
        }
        public void FinishCapture()
        {
            if(!IsRecording)return;int frames=Microphone.GetPosition(device);Microphone.End(device);IsRecording=false;
            if(recording==null||frames<1600){if(recording!=null)Destroy(recording);recording=null;dialogue.SetStatus("说得太短了，请重说或打字");return;}
            var samples=new float[frames*recording.channels];recording.GetData(samples,0);
            byte[] wav=VoiceWave.EncodeMono16(samples,recording.frequency,recording.channels);Destroy(recording);recording=null;
            StartCoroutine(Transcribe(wav,++generation));
        }
        private IEnumerator Transcribe(byte[] wav,int token)
        {
            IsTranscribing=true;dialogue.SetStatus("正在转写……");
            using(var web=new UnityWebRequest(dialogue.settings.gatewayUrl.TrimEnd('/')+"/transcribe","POST"))
            {
                web.uploadHandler=new UploadHandlerRaw(wav);web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","audio/wav");web.timeout=32;pending=web;
                yield return web.SendWebRequest();if(token!=generation)yield break;pending=null;IsTranscribing=false;
                if(web.result!=UnityWebRequest.Result.Success){dialogue.SetStatus("语音识别暂不可用，请重试或打字");yield break;}
                Transcript result=null;try{result=JsonUtility.FromJson<Transcript>(web.downloadHandler.text);}catch(Exception){}
                if(result==null||string.IsNullOrWhiteSpace(result.text)){dialogue.SetStatus("没有听清，请重说或打字");yield break;}
                TranscriptReady?.Invoke(result.text);dialogue.SetStatus("核对文字后点击发送，也可以修改");
            }
        }
        private void Update()
        {
            if(stage==null)return;
            if(!stage.Journey.DialogueOpen||(hud!=null&&hud.IsWorldPaused)){CancelCapture();return;}
            if(Input.GetKeyDown(KeyCode.V)&&!IsRecording&&!IsTranscribing&&(hud==null||!hud.IsEditingText))StartCoroutine(BeginCapture());
            if(Input.GetKeyUp(KeyCode.V)&&IsRecording)FinishCapture();
            if(IsRecording&&Time.realtimeSinceStartup-began>24)FinishCapture();
        }
        public void CancelCapture()
        {
            if(awaitingPermission){generation++;awaitingPermission=false;}
            if(IsRecording){Microphone.End(device);IsRecording=false;}
            if(recording!=null){Destroy(recording);recording=null;}
            if(pending!=null){generation++;pending.Abort();pending=null;IsTranscribing=false;}
        }
        private void OnDisable(){CancelCapture();}
    }
}
