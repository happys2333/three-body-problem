using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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
        public string GatewayUrl => TravellerUserSettings.Current.GatewayUrl(settings);
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
        private bool emittingModelReply;
        private float retryNarrativeAt;
        private NpcDecisionController decisions;
        public string FailureCode { get; private set; } = "";
        public bool IsIntentionalOffline => false;
        private readonly NpcQuestionTurn question=new NpcQuestionTurn();
        public bool HasPlayerQuestion => question.IsActive;
        public int QuestionId => question.Id;
        public NpcIdentity QuestionTarget => question.Recipient;
        [Serializable] public class Message{public string role,content;public Message(string role,string content){this.role=role;this.content=content;}}
        [Serializable] private class Context
        {
            public string scene,phase,sky,objective,hypothesis,courtAudience,petitionHolder,selfName,selfRole,currentActivity,playerCondition,authorityState,guardForce;
            public bool giantSunSeen,inShade,bothInShade,identityKnown,askedPlayerOrigin,plantsIgnited,canSeePlayer,skyVisible;
            public float playerDistance;
            public int openingAnswers,shelterAnswers;
            public int decisionProtocol,decisionRevision;
            public bool guardsAvailable,audienceReady;
            public float phaseElapsed;
            public string[] allowedStoryIntents;
            public List<SceneObservation> witnessedObservations;
            public List<string> knownEvents=new List<string>();
        }
        [Serializable] private class Interlocutor{public string id,npcId,displayName;public string[] aliases;}
        [Serializable] private class Request{public string npcId,recipientId,utterance,kind;public bool allowHandoff;public Context context;public List<Message> history;public List<Interlocutor> interlocutors;public NpcApiConnection connection;}
        [Serializable] private class Reply{public string speech,gesture,mode,error,storyIntent,targetId,conduct,failureCode,observationId;}
        [Serializable] private class SpeechRequest{public string text,npcId;}

        private void Start()
        {
            stage=GetComponent<TravellerStage>();journey=stage.Journey;sequence=GetComponent<GobiSequenceController>();hud=GetComponentInChildren<TravellerHud>();
            decisions=GetComponent<NpcDecisionController>();
            identities=GetComponentsInChildren<NpcIdentity>(true);
            speaker=gameObject.AddComponent<AudioSource>();speaker.spatialBlend=0;speaker.volume=.82f;speaker.playOnAwake=false;
            foreach(var identity in identities)if(identity.persona!=null&&identity.persona.characterId=="wenwang"){Current=identity;break;}
            journey.Changed+=OnProgressChanged;
            RememberAuthoredLine(journey.Speech);
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
            if(stage==null||journey.IsTitle||journey.IsSummary||journey.Decisions.Defeated||journey.IsPreviewing||stage.IsLoading||hud!=null&&hud.IsWorldPaused)return false;
            CancelQuestion();journey.CancelPendingDialogueDecision();FailureCode="";
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
            if(!string.IsNullOrEmpty(FailureCode)&&FailureCode!="offline")
            {
                bool transient = FailureCode=="rate_limited"||FailureCode=="timeout"||FailureCode=="unreachable"||FailureCode=="unavailable";
                if(!transient||Time.realtimeSinceStartup<retryNarrativeAt)return;
            }
            if(HasPlayerQuestion||IsBusy||IsVocalising||journey.SpeechRemainingSeconds>0||sequence==null||!sequence.WorldRunning||Current==null||Current.persona.characterId!="wenwang")return;
            var step=journey.Progress.Step;
            if(step!=JourneyStep.OpeningTalk&&step!=JourneyStep.ShelterTalk)return;
            FailureCode="";CancelPending();IsBusy=true;directing=true;
            StartCoroutine(Ask("",version,Current,step,true));
        }
        private IEnumerator Ask(string text,int token,NpcIdentity identity,JourneyStep step,bool director,bool allowHandoff=true)
        {
            if(identity==null||!identity.CanAnswer){CancelQuestion();SetStatus("刚才的人物已离开，按住 F 重新提问");yield break;}
            string id=identity.persona.characterId;
            string memoryKey=NpcConversationMemory.Key(identity);
            var request=new Request{npcId=id,recipientId=identity.GetInstanceID().ToString(),utterance=text,kind=director?"director":"dialogue",allowHandoff=!director&&allowHandoff,interlocutors=Interlocutors(),context=BuildContext(identity),history=NpcConversationMemory.History(memoryKey),connection=TravellerUserSettings.Current.connection.Copy()};
            int requestRevision=request.context.decisionRevision;
            // The DTO contains plain data only. Serialize large histories away
            // from the render thread, then discard cancelled/outdated requests.
            var encoded=Task.Run(()=>Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
            while(!encoded.IsCompleted)yield return null;
            if(token!=version)yield break;
            if(encoded.IsFaulted){CancelQuestion();SetStatus("交流记录暂时无法整理，请重新提问");yield break;}
            Reply reply=null;string transportFailure="unreachable";
            using(var web=new UnityWebRequest(GatewayUrl+"/dialogue","POST"))
            {
                web.uploadHandler=new UploadHandlerRaw(encoded.Result);web.downloadHandler=new DownloadHandlerBuffer();web.SetRequestHeader("Content-Type","application/json");web.timeout=request.connection.mode=="gateway"?Mathf.Max(Mathf.CeilToInt(settings.replyTimeoutSeconds),request.connection.timeoutSeconds+3):request.connection.timeoutSeconds+3;pending=web;
                yield return web.SendWebRequest();
                if(token!=version)yield break;
                if(web.result==UnityWebRequest.Result.Success)
                {try{reply=JsonUtility.FromJson<Reply>(web.downloadHandler.text);}catch(Exception){reply=null;}transportFailure="invalid_response";}
                else if(web.result==UnityWebRequest.Result.ProtocolError)transportFailure="unavailable";
                else if(web.error!=null&&web.error.IndexOf("timed out",StringComparison.OrdinalIgnoreCase)>=0)transportFailure="timeout";
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
            FailureCode=reply==null?transportFailure:reply.failureCode??"";
            // A missing field from an old gateway cannot grant offline progression
            // after a custom online request. Failures never poison NPC memory.
            if(UsingFallback)
            {
                if(string.IsNullOrEmpty(FailureCode))FailureCode="invalid_response";
                retryNarrativeAt=Time.realtimeSinceStartup+30;
                question.Complete();LastReply="";SetStatus(FailureMessage(FailureCode));hud?.NotifyDialogueFailure(Status);yield break;
            }
            string intent=reply==null?"continue":reply.storyIntent;
            if(director)
            {
                // Director ticks choose only a node. They never invent a player utterance or spend an answer.
                sequence.OnNpcReply(intent,false,0);SetStatus("看向人物，按住 F 说话");yield break;
            }
            string speech=fallback?identity.persona.fallbackReply:reply.speech;
            if(sequence!=null&&id=="wenwang")speech=GobiDialoguePolicy.SafeSpeech(fallback?null:speech,text,step);
            else if(speech.Length>240)speech=speech.Substring(0,240);
            NpcConversationMemory.RememberTurn(memoryKey,text,speech);
            question.Complete();LastReply=speech;SetStatus(FailureCode=="offline"?"当前使用剧情对白；可继续交谈或继续演出":"按住 F 继续提问");
            if(id=="wenwang"&&!GobiDialoguePolicy.IsOpening(step)&&(speech.Contains("姬昌")||speech.Contains("周文王")))journey.Progress.IdentifyTraveller();
            string name=DisplayName(identity);
            if(!UsingFallback&&!string.IsNullOrEmpty(reply.observationId)&&request.context.witnessedObservations!=null)
            {
                var fact=request.context.witnessedObservations.Find(item=>item.id==reply.observationId);
                if(fact!=null)stage.RecordConfirmedObservation(fact,name);
            }
            emittingModelReply=true;
            try{journey.Speak(name+"："+speech,Mathf.Max(7,speech.Length*.16f));}
            finally{emittingModelReply=false;}
            var actor=identity.GetComponent<TravellerActor>();if(actor!=null)actor.Gesture(GestureClip(reply==null?null:reply.gesture,step),5);
            if(sequence!=null&&id=="wenwang"&&journey.Progress.Step==step)sequence.OnNpcReply(intent,true,speech.Length);
            if(settings.synthesiseReplies&&TravellerUserSettings.Current.speakReplies)StartCoroutine(Synthesise(speech,id,token));
            journey.ResolveNpcDecision(id,intent,reply==null?"neutral":reply.conduct,!UsingFallback,step,requestRevision);
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
        private Context BuildContext(NpcIdentity observer)
        {
            var p=journey.Progress;
            bool companion=observer.persona.characterId=="wenwang";
            var allowed=new List<string>{"continue"};
            if(companion&&sequence!=null&&p.CanChooseStoryIntent("sunrise"))allowed.Add("sunrise");
            if(companion&&sequence!=null&&sequence.BothInShade&&p.CanChooseStoryIntent("sunset"))allowed.Add("sunset");
            foreach(var intent in journey.Decisions.Allowed(observer.persona.characterId,p.Step,journey.AudienceReady,decisions!=null&&decisions.CanDispatchGuards))
                if(!allowed.Contains(intent))allowed.Add(intent);
            Vector3 eye=stage.player.viewCamera.transform.position;
            float distance=Vector3.Distance(observer.ConversationPoint,eye);
            bool seesPlayer=distance<40&&CanSee(observer,eye,stage.player.transform);
            bool seesSky=CanSee(observer,observer.ConversationPoint+Vector3.up*200,observer.transform);
            bool seesCourt=stage.Audience!=null&&Vector3.Distance(observer.transform.position,stage.Audience.king.transform.position)<25&&CanSee(observer,stage.Audience.king.transform.position+Vector3.up*1.5f,stage.Audience.king.transform);
            bool shade=seesPlayer&&(stage.Current.sunCount==0||SunExposureSystem.IsSheltered(stage.player.transform.position+Vector3.up*1.05f,stage.Current.sunA,stage.player.transform));
            var exposure=stage.GetComponent<SunExposureSystem>();
            var context=new Context{scene=stage.profile.chapter,phase=p.Step.ToString(),phaseElapsed=p.Elapsed,
                selfName=DisplayName(observer),selfRole=observer.persona.characterId,currentActivity=Activity(observer),
                sky=seesSky?stage.Current.title:"天空被遮挡，只能感知周围明暗。",skyVisible=seesSky,objective=companion?journey.Objective:"",
                courtAudience=seesCourt?stage.Audience.State.ToString():"",petitionHolder=seesCourt?stage.Audience.PetitionHolder:"",
                hypothesis="",giantSunSeen=companion&&p.GiantSunSeen,identityKnown=companion&&p.KnowsIdentity,askedPlayerOrigin=companion&&p.AskedPlayerOrigin,
                openingAnswers=companion?p.OpeningAnswers:0,shelterAnswers=companion?p.ShelterAnswers:0,allowedStoryIntents=allowed.ToArray(),bothInShade=companion&&sequence!=null&&sequence.BothInShade,
                plantsIgnited=companion&&sequence!=null&&sequence.VegetationIgnited,inShade=shade,canSeePlayer=seesPlayer,playerDistance=Mathf.Round(distance),
                playerCondition=!seesPlayer?"听见玩家的声音，看不清其身体状况。":exposure!=null&&exposure.HitPulse>.1f?"玩家正被日光灼伤，显得痛苦。":shade?"玩家目前在遮阴或夜色中。":"玩家在眼前，未见明显伤势。",
                knownEvents=NpcConversationMemory.KnownEvents(NpcConversationMemory.Key(observer)),
                witnessedObservations=stage.ObservationsKnownTo(observer)};
            context.decisionProtocol=1;context.decisionRevision=journey.Decisions.Revision;
            context.authorityState=journey.Decisions.Defeated?"defeated":journey.Decisions.Pursuing?"pursuing":journey.Decisions.WarningActive?"warned":"calm";
            context.guardForce=journey.Decisions.Lethal?"lethal":"detain";
            context.guardsAvailable=decisions!=null&&decisions.CanDispatchGuards;context.audienceReady=journey.AudienceReady;
            context.objective=p.Step==JourneyStep.CourtTalk?"表章呈毕后讨论历法，并决定是否开始查验。":
                p.Step==JourneyStep.TribunalTalk?"就天象记录听取解释，决定是否一起外出观察。":companion?journey.Objective:"";
            if(decisions!=null&&decisions.HitPulse>.1f)context.playerCondition="玩家受到卫兵攻击，显得受伤。";
            // The player's saved notebook, guesses and other people's private
            // conversations are not automatically part of an NPC's knowledge.
            return context;
        }
        private static string Activity(NpcIdentity identity)
        {
            var actor=identity.GetComponent<TravellerActor>();
            if(identity.GetComponent<RoyalFanBearer>()!=null)return "双手执长柄大羽扇，正在缓缓摇扇。";
            var book=identity.GetComponent<NpcHeldBook>();
            if(book!=null&&book.book!=null)return book.IsReading?"双手托着打开的羊皮纸手稿，正在查阅庭上的记录。":"双手托着手稿，正在移动或听取陈述。";
            var post=identity.GetComponent<CourtRetainer>();
            if(post!=null&&post.DutyControlled)return "正在参与接取、呈送表章或返回岗位的礼仪。";
            var gear=identity.GetComponent<EraGuardEquipment>();
            if(gear!=null&&gear.HoldsPolearm)return "持矛执勤，注意守卫通道。";
            if(actor!=null&&actor.seated)return "坐着听取眼前的陈述。";
            if(actor!=null&&actor.IsMoving)return "正在行走。";
            return "停留在当前地点，与玩家交谈。";
        }
        private static bool CanSee(NpcIdentity observer,Vector3 point,Transform ignore)
        {
            var start=observer.ConversationPoint;var delta=point-start;
            foreach(var hit in Physics.RaycastAll(start,delta.normalized,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(observer.transform)||ignore!=null&&hit.transform.IsChildOf(ignore))continue;
                if(hit.collider.GetComponentInParent<TravellerActor>()!=null)continue;
                return false;
            }
            return true;
        }
        public void RememberAuthoredLine(string line)
        {
            if(emittingModelReply||stage==null||journey==null||journey.IsTitle||journey.IsPreviewing||identities==null||string.IsNullOrEmpty(line))return;
            int colon=line.IndexOf('：');if(colon<1)return;
            string prefix=line.Substring(0,colon),speech=line.Substring(colon+1);NpcIdentity speaker=null;
            foreach(var npc in identities)
            {
                if(npc==null||!npc.CanAnswer)continue;
                bool match=false;foreach(string alias in npc.persona.Addresses)if(!string.IsNullOrEmpty(alias)&&prefix.EndsWith(alias,StringComparison.Ordinal)){match=true;break;}
                if(!match)continue;if(speaker!=null)return;speaker=npc;
            }
            if(speaker==null)return;
            string eventId=stage.profile.sceneIndex+":"+journey.Progress.Step+":"+speaker.persona.characterId+":"+line;
            foreach(var npc in identities)
            {
                if(npc==null||!npc.CanAnswer)continue;
                bool own=npc==speaker;
                if(!own&&(Vector3.Distance(npc.ConversationPoint,speaker.ConversationPoint)>16||!CanSee(npc,speaker.ConversationPoint,speaker.transform)))continue;
                NpcConversationMemory.RememberEvent(NpcConversationMemory.Key(npc),eventId,
                    "["+stage.profile.chapter+" / "+journey.Progress.Step+"] "+(own?"你曾说过：":"你在场听见"+DisplayName(speaker)+"说：")+speech);
            }
        }
        public static string GestureClip(string intent)
        {
            switch(intent){case "think":return "MX_Think";case "warn":return "MX_ShieldEyes";case "nod":return "MX_HeadNodYes";case "look_sky":return "MX_LookSky";default:return "MX_TalkCalm";}
        }
        public static string GestureClip(string intent,JourneyStep step)
        {return intent=="warn"&&(step==JourneyStep.CourtTalk||step==JourneyStep.TribunalTalk)?"MX_AngryGesture":GestureClip(intent);}
        private IEnumerator Synthesise(string text,string id,int token)
        {
            var voice=GetComponent<PlayerMicrophoneInput>();if(voice!=null&&voice.IsRecording)yield break;
            using(var web=new UnityWebRequest(GatewayUrl+"/speech","POST"))
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
        public void ResetConversation(){CancelQuestion();NpcConversationMemory.Clear();LastReply=null;UsingFallback=false;FailureCode="";SetStatus("看向人物，按住 F 说话");}
        public void SetStatus(string value){Status=value;Changed?.Invoke();}
        public static string FailureMessage(string code)
        {
            switch(code)
            {
                case "content_rejected":return "这句话被对话服务拒绝，请换个说法；剧情未推进";
                case "timeout":return "等待回答超时，可稍后重试或在设置中增加等待时间；剧情未推进";
                case "authentication":return "对话服务认证失败，请检查设置中的 API Key；剧情未推进";
                case "rate_limited":return "对话服务繁忙或额度不足，请稍后重试；剧情未推进";
                case "invalid_response":return "对话服务未返回有效回答，请重试；剧情未推进";
                default:return "暂时连不上对话服务，请检查网络与设置后重试；剧情未推进";
            }
        }
        private void OnProgressChanged()
        {
            if(!journey.DialogueOpen&&!directing&&(HasPlayerQuestion||IsBusy||synthesis!=null||speaker!=null&&speaker.isPlaying&&speaker.clip==dynamicClip))
            {CancelQuestion();SetStatus("看向人物，按住 F 说话");}
        }
        private void OnDestroy(){CancelPending();if(journey!=null)journey.Changed-=OnProgressChanged;if(dynamicClip!=null)Destroy(dynamicClip);}
    }
}
