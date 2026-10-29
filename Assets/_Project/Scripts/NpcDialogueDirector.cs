using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ThreeBodyProblem
{
    public sealed class NpcDialogueDirector : MonoBehaviour
    {
        public NpcDialogueSettings settings;
        public NpcIdentity Current {get;private set;}
        public string Status {get;private set;}="可以打字，或按住 V 说话";
        public string LastReply {get;private set;}
        public bool IsBusy {get;private set;}
        public bool IsSpeaking => speaker!=null&&speaker.isPlaying;
        public int CurrentSpeechSampleRate => speaker!=null&&speaker.clip!=null?speaker.clip.frequency:0;
        public event Action Changed;
        private TravellerStage stage;private TravellerJourney journey;private NpcIdentity[] identities;
        private AudioSource speaker;private AudioClip dynamicClip;
        private UnityWebRequest pending,synthesis;private int version;
        private readonly Dictionary<string,List<Message>> memories=new Dictionary<string,List<Message>>();
        [Serializable] public class Message{public string role,content;public Message(string role,string content){this.role=role;this.content=content;}}
        [Serializable] private class Context{public string scene,phase,sky,objective,hypothesis;public bool giantSunSeen,inShade,identityKnown,plantsIgnited;public List<string> observations=new List<string>();}
        [Serializable] private class Request{public string npcId,utterance;public Context context;public List<Message> history;}
        [Serializable] private class Reply{public string speech,gesture,mode,error;}
        [Serializable] private class SpeechRequest{public string text,npcId;}

        private void Start()
        {
            stage=GetComponent<TravellerStage>();journey=stage.Journey;identities=GetComponentsInChildren<NpcIdentity>();
            speaker=gameObject.AddComponent<AudioSource>();speaker.spatialBlend=0;speaker.volume=.82f;speaker.playOnAwake=false;
            foreach(var identity in identities)if(identity.persona!=null&&identity.persona.characterId=="wenwang"){Current=identity;break;}
            journey.Changed+=OnProgressChanged;
        }
        public bool TrySelectNearest()
        {
            if(stage.Current.heat>.5f&&!SunExposureSystem.IsSheltered(stage.player.transform.position+Vector3.up*1.05f,stage.Current.sunA,stage.player.transform))
            {journey.Speak("先离开日光，进阴影再说！",4);return false;}
            NpcIdentity closest=null;float best=settings.conversationDistance;
            foreach(var identity in identities)
            {
                if(!identity.interactive||identity.persona==null||!identity.gameObject.activeInHierarchy)continue;
                float distance=Vector3.Distance(stage.player.transform.position,identity.transform.position);
                if(distance<best){closest=identity;best=distance;}
            }
            if(closest==null)return false;
            if(Current!=closest){CancelPending();Current=closest;LastReply=null;}
            Changed?.Invoke();return true;
        }
        public bool TrySelectStoryConversant()
        {
            string id=journey.Progress.Step==JourneyStep.CourtTalk?"king":journey.Progress.Step==JourneyStep.TribunalTalk?"pope":"wenwang";
            float reach=id=="wenwang"?settings.conversationDistance:25;
            foreach(var identity in identities)
            {
                if(identity.persona==null||identity.persona.characterId!=id||!identity.interactive)continue;
                if(Vector3.Distance(identity.transform.position,stage.player.transform.position)>reach)continue;
                if(Current!=identity){CancelPending();Current=identity;LastReply=null;}Changed?.Invoke();return true;
            }
            return TrySelectNearest();
        }
        public void Submit(string text)
        {
            if(IsBusy||string.IsNullOrWhiteSpace(text)||Current==null||!journey.DialogueOpen)return;
            bool courtSpeaker=(Current.persona.characterId=="king"&&journey.Progress.Step==JourneyStep.CourtTalk)||(Current.persona.characterId=="pope"&&journey.Progress.Step==JourneyStep.TribunalTalk);
            if(Vector3.Distance(Current.transform.position,stage.player.transform.position)>(courtSpeaker?25:settings.conversationDistance+1)){SetStatus("走近一点，再继续说话");return;}
            CancelPending();int token=version;IsBusy=true;SetStatus("对方正在思考……");
            StartCoroutine(Ask(text.Trim().Substring(0,Math.Min(text.Trim().Length,1200)),token,Current,journey.Progress.Step));
        }
        private IEnumerator Ask(string text,int token,NpcIdentity identity,JourneyStep step)
        {
            string id=identity.persona.characterId;
            string memoryKey=id+":"+identity.GetInstanceID();
            List<Message> history;if(!memories.TryGetValue(memoryKey,out history)){history=new List<Message>();memories[memoryKey]=history;}
            var request=new Request{npcId=id,utterance=text,context=BuildContext(),history=new List<Message>(history)};
            string json=JsonUtility.ToJson(request);Reply reply=null;
            using(var web=new UnityWebRequest(settings.gatewayUrl.TrimEnd('/')+"/dialogue","POST"))
            {
                web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","application/json");web.timeout=Mathf.CeilToInt(settings.replyTimeoutSeconds);pending=web;
                yield return web.SendWebRequest();
                if(token!=version)yield break;
                if(web.result==UnityWebRequest.Result.Success)
                {try{reply=JsonUtility.FromJson<Reply>(web.downloadHandler.text);}catch(Exception){reply=null;}}
                pending=null;
            }
            if(token!=version||Current!=identity||journey.Progress.Step!=step||!journey.DialogueOpen){IsBusy=false;yield break;}
            bool fallback=reply==null||string.IsNullOrWhiteSpace(reply.speech);
            string speech=fallback?identity.persona.fallbackReply:reply.speech;
            if(speech.Length>240)speech=speech.Substring(0,240);
            history.Add(new Message("user",text));history.Add(new Message("assistant",speech));while(history.Count>12)history.RemoveAt(0);
            LastReply=speech;IsBusy=false;SetStatus(fallback?"暂时离线，使用剧情回复":reply.mode=="scripted"?"离线剧情 · 可继续交流":"可以继续说话");
            if(id=="wenwang"&&(speech.Contains("姬昌")||speech.Contains("周文王")))journey.Progress.IdentifyTraveller();
            string name=id=="wenwang"&&!journey.Progress.KnowsIdentity?"旅人":identity.persona.displayName;
            journey.Speak(name+"："+speech,Mathf.Max(7,speech.Length*.16f));
            var actor=identity.GetComponent<TravellerActor>();if(actor!=null)actor.Gesture(GestureClip(reply==null?null:reply.gesture),5);
            if(settings.synthesiseReplies)StartCoroutine(Synthesise(speech,id,token));
        }
        private Context BuildContext()
        {
            var sequence=GetComponent<GobiSequenceController>();
            var context=new Context{scene=stage.profile.chapter,phase=journey.Progress.Step.ToString(),sky=stage.Current.title,objective=journey.Objective,hypothesis=stage.Journal.hypothesis,
                giantSunSeen=journey.Progress.GiantSunSeen,identityKnown=journey.Progress.KnowsIdentity,plantsIgnited=sequence!=null&&sequence.VegetationIgnited,
                inShade=stage.Current.sunCount==0||SunExposureSystem.IsSheltered(stage.player.transform.position+Vector3.up*1.05f,stage.Current.sunA,stage.player.transform)};
            foreach(var entry in stage.Journal.entries)context.observations.Add(entry.category+"："+entry.text);
            return context;
        }
        public static string GestureClip(string intent)
        {
            switch(intent){case "think":return "MX_Think";case "warn":return "MX_ShieldEyes";case "nod":return "MX_HeadNodYes";case "look_sky":return "MX_LookSky";default:return "MX_TalkCalm";}
        }
        private IEnumerator Synthesise(string text,string id,int token)
        {
            var voice=GetComponent<PlayerMicrophoneInput>();if(voice!=null&&voice.IsRecording)yield break;
            using(var web=new UnityWebRequest(settings.gatewayUrl.TrimEnd('/')+"/speech","POST"))
            {
                web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new SpeechRequest{text=text,npcId=id})));
                web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","application/json");web.timeout=Mathf.CeilToInt(settings.speechTimeoutSeconds);synthesis=web;
                yield return web.SendWebRequest();
                if(token!=version)yield break;synthesis=null;
                if(web.result!=UnityWebRequest.Result.Success){SetStatus("语音暂不可用，可继续看字幕交谈");yield break;}
                AudioClip clip=null;try{clip=VoiceWave.DecodePcm16(web.downloadHandler.data);}catch(Exception){clip=null;}
                if(clip==null)yield break;
                if(dynamicClip!=null)Destroy(dynamicClip);dynamicClip=clip;speaker.clip=clip;speaker.Play();
            }
        }
        public void PlayScriptedLine(AudioClip clip){if(clip==null||speaker==null)return;CancelPending();speaker.clip=clip;speaker.Play();}
        public void StopSpeaking(){if(speaker!=null)speaker.Stop();if(synthesis!=null){synthesis.Abort();synthesis=null;}}
        public void CancelPending(){version++;if(pending!=null){pending.Abort();pending=null;}StopSpeaking();IsBusy=false;}
        public void SetStatus(string value){Status=value;Changed?.Invoke();}
        private void OnProgressChanged(){if(!journey.DialogueOpen&&(IsBusy||synthesis!=null||(speaker!=null&&speaker.isPlaying&&speaker.clip==dynamicClip))){CancelPending();SetStatus("可以打字，或按住 V 说话");}}
        private void OnDestroy(){CancelPending();if(journey!=null)journey.Changed-=OnProgressChanged;if(dynamicClip!=null)Destroy(dynamicClip);}
    }
}
