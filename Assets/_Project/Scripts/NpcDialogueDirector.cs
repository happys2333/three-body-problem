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
        public string Status {get;private set;}="看向人物，按住 F 说话";
        public string LastReply {get;private set;}
        public bool IsBusy {get;private set;}
        public bool UsingFallback {get;private set;}
        public bool IsSpeaking => speaker!=null&&speaker.isPlaying;
        public bool IsVocalising => IsSpeaking||synthesis!=null;
        public int CurrentSpeechSampleRate => speaker!=null&&speaker.clip!=null?speaker.clip.frequency:0;
        public event Action Changed;
        private TravellerStage stage;
        private TravellerJourney journey;
        private GobiSequenceController sequence;
        private TravellerHud hud;
        private NpcIdentity[] identities;
        private AudioSource speaker;
        private AudioClip dynamicClip;
        private UnityWebRequest pending,synthesis;
        private int version;
        private bool directing;
        private readonly Dictionary<string,List<Message>> memories=new Dictionary<string,List<Message>>();
        private readonly NpcQuestionTurn question=new NpcQuestionTurn();
        public bool HasPlayerQuestion => question.IsActive;
        public int QuestionId => question.Id;
        public NpcIdentity QuestionTarget => question.Recipient;
        [Serializable] public class Message{public string role,content;public Message(string role,string content){this.role=role;this.content=content;}}
        [Serializable] private class Context
        {
            public string scene,phase,sky,objective,hypothesis;
            public bool giantSunSeen,inShade,bothInShade,identityKnown,askedPlayerOrigin,plantsIgnited;
            public int openingAnswers,shelterAnswers;
            public float phaseElapsed;
            public string[] allowedStoryIntents;
            public List<string> observations=new List<string>();
        }
        [Serializable] private class Interlocutor{public string id,npcId,displayName;public string[] aliases;}
        [Serializable] private class Request{public string npcId,recipientId,utterance,kind;public bool allowHandoff;public Context context;public List<Message> history;public List<Interlocutor> interlocutors;}
        [Serializable] private class Reply{public string speech,gesture,mode,error,storyIntent,targetId;}
        [Serializable] private class SpeechRequest{public string text,npcId;}

        private void Start()
        {
            stage=GetComponent<TravellerStage>();journey=stage.Journey;sequence=GetComponent<GobiSequenceController>();hud=GetComponentInChildren<TravellerHud>();
            identities=GetComponentsInChildren<NpcIdentity>(true);
            speaker=gameObject.AddComponent<AudioSource>();speaker.spatialBlend=0;speaker.volume=.82f;speaker.playOnAwake=false;
            foreach(var identity in identities)if(identity.persona!=null&&identity.persona.characterId=="wenwang"){Current=identity;break;}
            journey.Changed+=OnProgressChanged;
        }
        public bool TrySelectNearest()
        {
            NpcIdentity closest=NpcQuestionTargeting.Find(stage.player.viewCamera,identities,stage.player.transform);
            if(closest==null)return false;
            Select(closest);return true;
        }
        private void Select(NpcIdentity identity)
        {
            if(Current!=identity){CancelPending();Current=identity;LastReply=null;}
            Changed?.Invoke();
        }
        public bool TrySelectStoryConversant()
        {
            return TrySelectNearest();
        }
        public bool BeginPlayerQuestion()
        {
            if(stage==null||journey.IsTitle||journey.IsSummary||journey.IsPreviewing||stage.IsLoading||hud!=null&&hud.IsWorldPaused)return false;
            CancelQuestion();
            identities=GetComponentsInChildren<NpcIdentity>(true);
            if(!TrySelectNearest()){SetStatus("当前场景没有可以交谈的人物");return false;}
            question.Begin(Current);journey.SetDialogueOpen(true);return true;
        }
        public bool EnsureVoiceConversation()
        {
            if(stage==null||journey.IsTitle||journey.IsSummary||journey.IsPreviewing||stage.IsLoading||hud!=null&&hud.IsWorldPaused)return false;
            return question.IsActive||BeginPlayerQuestion();
        }
        public void Submit(string text)
        {
            if(IsBusy||string.IsNullOrWhiteSpace(text)||!EnsureVoiceConversation())return;
            SubmitQuestion(text,question.Recipient,question.Id);
        }
        public bool SubmitQuestion(string text,NpcIdentity recipient,int questionId)
        {
            if(string.IsNullOrWhiteSpace(text)||!question.Accepts(questionId,recipient))return false;
            if(!EnsureVoiceConversation())return false;
            if(!question.TrySubmit(questionId,recipient))return false;
            Current=recipient;
            sequence?.NotePlayerSpeech();CancelPending();int token=version;IsBusy=true;directing=false;SetStatus("对方正在思考……");
            StartCoroutine(Ask(text.Trim().Substring(0,Math.Min(text.Trim().Length,1200)),token,Current,journey.Progress.Step,false));
            return true;
        }
        public void RequestNarrativeDecision()
        {
            if(HasPlayerQuestion||IsBusy||IsVocalising||sequence==null||!sequence.WorldRunning||Current==null||Current.persona.characterId!="wenwang")return;
            var step=journey.Progress.Step;
            if(step!=JourneyStep.OpeningTalk&&step!=JourneyStep.ShelterTalk)return;
            CancelPending();IsBusy=true;directing=true;
            StartCoroutine(Ask("",version,Current,step,true));
        }
        private IEnumerator Ask(string text,int token,NpcIdentity identity,JourneyStep step,bool director,bool allowHandoff=true)
        {
            if(identity==null||!identity.CanAnswer){CancelQuestion();SetStatus("刚才的人物已离开，按住 F 重新提问");yield break;}
            string id=identity.persona.characterId;
            string memoryKey=id+":"+identity.GetInstanceID();
            List<Message> history;if(!memories.TryGetValue(memoryKey,out history)){history=new List<Message>();memories[memoryKey]=history;}
            var request=new Request{npcId=id,recipientId=identity.GetInstanceID().ToString(),utterance=text,kind=director?"director":"dialogue",allowHandoff=!director&&allowHandoff,interlocutors=Interlocutors(),context=BuildContext(),history=new List<Message>(history)};
            Reply reply=null;
            using(var web=new UnityWebRequest(settings.gatewayUrl.TrimEnd('/')+"/dialogue","POST"))
            {
                web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","application/json");web.timeout=Mathf.CeilToInt(settings.replyTimeoutSeconds);pending=web;
                yield return web.SendWebRequest();
                if(token!=version)yield break;
                if(web.result==UnityWebRequest.Result.Success)
                {try{reply=JsonUtility.FromJson<Reply>(web.downloadHandler.text);}catch(Exception){reply=null;}}
                pending=null;
            }
            if(token!=version)yield break;
            if(identity==null||!identity.CanAnswer||Current!=identity||director&&journey.Progress.Step!=step||!director&&!journey.DialogueOpen)
            {IsBusy=false;directing=false;question.Complete();yield break;}
            if(!director&&reply!=null&&!string.IsNullOrEmpty(reply.targetId))
            {
                if(!allowHandoff){CancelQuestion();SetStatus("本次提问不能重复转交，按住 F 重新提问");yield break;}
                NpcIdentity addressed=null;
                foreach(var npc in identities)if(npc!=null&&npc.CanAnswer&&npc.GetInstanceID().ToString()==reply.targetId){addressed=npc;break;}
                if(addressed!=null&&addressed!=identity)
                {Current=addressed;SetStatus("正在请"+DisplayName(addressed)+"回答……");yield return Ask(text,token,addressed,step,false,false);yield break;}
                if(addressed==null){CancelQuestion();SetStatus("被提问的人物已离开，按住 F 重新提问");yield break;}
            }
            bool fallback=reply==null||!director&&string.IsNullOrWhiteSpace(reply.speech);
            UsingFallback=fallback||reply.mode!="online";IsBusy=false;directing=false;
            string intent=reply==null?"continue":reply.storyIntent;
            if(director)
            {
                // Director ticks choose only a node. They never invent a player utterance or spend an answer.
                sequence.OnNpcReply(intent,false,0);Changed?.Invoke();yield break;
            }
            string speech=fallback?identity.persona.fallbackReply:reply.speech;
            if(sequence!=null&&id=="wenwang")speech=GobiDialoguePolicy.SafeSpeech(fallback?null:speech,text,step);
            else if(speech.Length>240)speech=speech.Substring(0,240);
            history.Add(new Message("user",text));history.Add(new Message("assistant",speech));while(history.Count>12)history.RemoveAt(0);
            question.Complete();LastReply=speech;SetStatus("按住 F 继续提问");
            if(id=="wenwang"&&!GobiDialoguePolicy.IsOpening(step)&&(speech.Contains("姬昌")||speech.Contains("周文王")))journey.Progress.IdentifyTraveller();
            string name=DisplayName(identity);
            journey.Speak(name+"："+speech,Mathf.Max(7,speech.Length*.16f));
            var actor=identity.GetComponent<TravellerActor>();if(actor!=null)actor.Gesture(GestureClip(reply==null?null:reply.gesture),5);
            if(sequence!=null&&id=="wenwang"&&journey.Progress.Step==step)sequence.OnNpcReply(intent,true,speech.Length);
            if(settings.synthesiseReplies)StartCoroutine(Synthesise(speech,id,token));
            journey.OnQuestionAnswered(id,text);
        }
        public string DisplayName(NpcIdentity identity)
        {return identity==null||identity.persona==null?"人物":identity.persona.characterId=="wenwang"?(journey.Progress.KnowsIdentity||journey.Progress.SceneIndex>0?"姬昌":"旅人"):identity.persona.displayName;}
        private List<Interlocutor> Interlocutors()
        {
            var roster=new List<Interlocutor>();
            foreach(var npc in identities)
            {
                if(npc==null||!npc.CanAnswer)continue;
                var aliases=new List<string>();foreach(string alias in npc.persona.Addresses)if(!string.IsNullOrWhiteSpace(alias)&&!aliases.Contains(alias))aliases.Add(alias);
                roster.Add(new Interlocutor{id=npc.GetInstanceID().ToString(),npcId=npc.persona.characterId,displayName=DisplayName(npc),aliases=aliases.ToArray()});
            }
            return roster;
        }
        private Context BuildContext()
        {
            var p=journey.Progress;
            var allowed=new List<string>{"continue"};
            if(sequence!=null&&p.CanChooseStoryIntent("sunrise"))allowed.Add("sunrise");
            if(sequence!=null&&sequence.BothInShade&&p.CanChooseStoryIntent("sunset"))allowed.Add("sunset");
            var context=new Context{scene=stage.profile.chapter,phase=p.Step.ToString(),phaseElapsed=p.Elapsed,sky=stage.Current.title,objective=journey.Objective,
                hypothesis=sequence==null?stage.Journal.hypothesis:"",giantSunSeen=p.GiantSunSeen,identityKnown=p.KnowsIdentity,askedPlayerOrigin=p.AskedPlayerOrigin,
                openingAnswers=p.OpeningAnswers,shelterAnswers=p.ShelterAnswers,allowedStoryIntents=allowed.ToArray(),bothInShade=sequence!=null&&sequence.BothInShade,
                plantsIgnited=sequence!=null&&sequence.VegetationIgnited,
                inShade=stage.Current.sunCount==0||SunExposureSystem.IsSheltered(stage.player.transform.position+Vector3.up*1.05f,stage.Current.sunA,stage.player.transform)};
            // A saved notebook may contain future chapters from an older run. Never leak it into this awakening.
            if(sequence==null)foreach(var entry in stage.Journal.entries)context.observations.Add(entry.category+"："+entry.text);
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
        public void PlayScriptedLine(AudioClip clip){if(HasPlayerQuestion||clip==null||speaker==null)return;CancelPending();speaker.clip=clip;speaker.Play();}
        public void StopSpeaking(){if(speaker!=null)speaker.Stop();if(synthesis!=null){synthesis.Abort();synthesis=null;}}
        public void CancelPending(){version++;if(pending!=null){pending.Abort();pending=null;}StopSpeaking();IsBusy=false;directing=false;}
        public void CancelQuestion(){question.Cancel();CancelPending();}
        public void ResetConversation(){CancelQuestion();memories.Clear();LastReply=null;UsingFallback=false;SetStatus("看向人物，按住 F 说话");}
        public void SetStatus(string value){Status=value;Changed?.Invoke();}
        private void OnProgressChanged()
        {
            if(!journey.DialogueOpen&&!directing&&(HasPlayerQuestion||IsBusy||synthesis!=null||speaker!=null&&speaker.isPlaying&&speaker.clip==dynamicClip))
            {CancelQuestion();SetStatus("看向人物，按住 F 说话");}
        }
        private void OnDestroy(){CancelPending();if(journey!=null)journey.Changed-=OnProgressChanged;if(dynamicClip!=null)Destroy(dynamicClip);}
    }
}
