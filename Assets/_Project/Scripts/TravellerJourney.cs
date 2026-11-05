using System;
using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class TravellerJourney : MonoBehaviour
    {
        private static JourneyProgress session;
        private static bool previewMode;
        private TravellerStage stage;
        private Transform traveller;
        private PalaceSkylight palaceSkylight;
        private Vector3[] actorPath;
        private int actorWaypoint;
        private JourneyStep appliedStep = (JourneyStep)(-1);
        private string speech = "";
        private GobiSequenceController gobi;
        private TravellerHud hud;
        private NpcDialogueDirector dialogueDirector;
        private float speechUntil;
        private JourneyStep? advanceAfterAnswer;
        private float answerAdvanceAt;
        private bool relevantExplanation;
        private NpcDecisionController decisionController;
        private CapitalLongNight longNight;
        private Vector3[] longNightRoute;
        private int longNightWaypoint;
        public NpcDecisionState Decisions { get; } = new NpcDecisionState();
        public bool AudienceReady => stage != null && !stage.HasPendingCourtAudience;
        public bool CanContinueScriptedAudience => NpcDecisionState.IsConversant(Progress.Step == JourneyStep.CourtTalk ? "king" : "pope", Progress.Step) &&
            AudienceReady && !Decisions.Pursuing && !Decisions.Defeated && dialogueDirector != null && dialogueDirector.IsIntentionalOffline;
        public event Action Changed;
        public event Action<string> DialogueSelected;
        public JourneyProgress Progress => session ?? (session = new JourneyProgress());
        public bool IsPreviewing => previewMode;
        public bool DialogueOpen { get; private set; }
        public string Speech => speech;
        public int SpeechVersion { get; private set; }
        public float SpeechRemainingSeconds => Mathf.Max(0, speechUntil - Time.unscaledTime);
        public bool IsTitle => !previewMode && Progress.Step == JourneyStep.Title;
        public bool IsCapitalEnding => !previewMode && Progress.Step == JourneyStep.CapitalEnding;
        public bool IsSummary => IsCapitalEnding || !previewMode && Progress.Step == JourneyStep.Summary;
        public bool IsLongNightWalk => Progress.Step == JourneyStep.ReturnToCourt || Progress.Step == JourneyStep.LeaveLongNightCourt;
        public bool CanObserve => stage != null && stage.CanObserve;
        public bool IsWalking => IsLongNightWalk || Progress.Step == JourneyStep.SeekShelter || Progress.Step == JourneyStep.WalkToFirstStar || Progress.Step == JourneyStep.WalkToTwoStars || Progress.Step == JourneyStep.WalkToChaoge || Progress.Step == JourneyStep.CourtApproach || Progress.Step == JourneyStep.WalkToWater || Progress.Step == JourneyStep.TribunalApproach || Progress.Step == JourneyStep.WalkOutside;
        public bool HasNavigation => !Decisions.Pursuing && (IsWalking || Progress.Step==JourneyStep.ExploreDesert || Progress.Step==JourneyStep.ApproachTraveller);
        public Vector3 Goal
        {
            get
            {
                switch (Progress.Step)
                {
                    case JourneyStep.ExploreDesert: return gobi!=null&&gobi.ExplorationMetres<stage.profile.gobiStory.minimumExplorationMetres?stage.profile.gobiStory.investigationGoal:traveller!=null?traveller.position:stage.profile.gobiStory.meetingPosition;
                    case JourneyStep.ApproachTraveller: return traveller!=null?traveller.position:stage.profile.gobiStory.meetingPosition;
                    case JourneyStep.SeekShelter: return stage.shelterCenter.position;
                    case JourneyStep.WalkToFirstStar: return new Vector3(2f, 0f, 42f);
                    case JourneyStep.WalkToTwoStars: return new Vector3(2f, 0f, 64f);
                    case JourneyStep.WalkToChaoge: return traveller!=null?traveller.position:stage.profile.gobiStory!=null?stage.profile.gobiStory.departureGoal:new Vector3(2f,0f,64f);
                    case JourneyStep.DepartForCapital: return new Vector3(0f, 0f, 165f);
                    case JourneyStep.CourtApproach: case JourneyStep.CourtTalk: return stage.profile.hasCapitalRoute ? stage.profile.courtGoal : new Vector3(0f, 0f, 13f);
                    case JourneyStep.ReturnToCourt: case JourneyStep.LeaveLongNightCourt:
                        return longNightRoute != null ? longNightRoute[longNightWaypoint] : stage.profile.courtGoal;
                    case JourneyStep.WalkToWater: return stage.profile.hasCapitalRoute ? stage.profile.waterGoal : new Vector3(25f, 0f, -9f);
                    case JourneyStep.TribunalApproach: case JourneyStep.TribunalTalk: return new Vector3(0f, 0f, 22f);
                    case JourneyStep.WalkOutside: return new Vector3(0f, 0f, -49f);
                    default: return stage.player.transform.position;
                }
            }
        }

        public string Objective
        {
            get
            {
                if(Decisions.Pursuing)return stage.profile.sceneIndex==1?"卫兵正在追捕你，设法脱身或向纣王交涉":"卫兵正在追捕你，设法脱身或向教皇交涉";
                switch (Progress.Step)
                {
                    case JourneyStep.Awakening: return "……风声。你在荒漠中醒来。";
                    case JourneyStep.ExploreDesert: return "调查四周，看看发生了什么事";
                    case JourneyStep.ApproachTraveller: return "前方有火光，走近那位旅人";
                    case JourneyStep.OpeningTalk: return "与旅人交谈，了解眼前的处境";
                    case JourneyStep.Sunrise: case JourneyStep.SeekShelter: return "太阳正在急速升起！跟随旅人躲入岩下阴影";
                    case JourneyStep.ShelterTalk: return "留在掩体里，与旅人交谈";
                    case JourneyStep.Sunset: return "等待日落，观察突然加速的天象";
                    case JourneyStep.WalkToFirstStar: return "跟随周文王，前往第一处观星点";
                    case JourneyStep.ObserveFirstStar: return "抬头观察石峰上方的亮点";
                    case JourneyStep.WalkToTwoStars: return "经过休整，前往下一处观星点";
                    case JourneyStep.ObserveTwoStars: return "同时看清两颗亮点，比较它们的位置";
                    case JourneyStep.WalkToChaoge: return "与姬昌一同前往朝歌";
                    case JourneyStep.DepartForCapital: return "沿驿路启程前往朝歌；按住 F 可随时提问";
                    case JourneyStep.CourtApproach: return "随周文王入城，穿过金字塔甬道进入石殿";
                    case JourneyStep.CourtTalk: return stage.HasPendingCourtAudience ? "观看传奏官呈送表章，按住 F 可与人物交谈" : "看向人物，按住 F 参与质询";
                    case JourneyStep.CalendarWait: return stage.HasPendingCourtAudience ? "等候传奏官呈送表章" : "观看姬昌推演，观察沙漏与天窗光斑";
                    case JourneyStep.WalkToWater: return "从侧门出殿，前往复苏水边";
                    case JourneyStep.ObserveRevival: return stage.IsRevivalComplete ? "看向池边重新站起的人" : "观察纸片浸水、恢复形体并重新站起";
                    case JourneyStep.AfterRevival: return "等待下一次日出；按住 F 可随时提问";
                    case JourneyStep.WaitForDawn: return "日出应当到了……继续观察天空";
                    case JourneyStep.ReturnToCourt: return "沿侧甬道返回石殿，听取关于长夜的报告";
                    case JourneyStep.CalendarFailed: return "在殿中见证历法失验";
                    case JourneyStep.LeaveLongNightCourt: return "循 EXIT 标志从侧甬道离开石殿";
                    case JourneyStep.ObserveLongNight: return "抬头观察长夜中的天象";
                    case JourneyStep.AfterLongNight: return "留在殿外，观察长夜中的第一场雪";
                    case JourneyStep.DryIceSnow: return "观察沉降的干冰";
                    case JourneyStep.AtmosphereFreeze: return "看着大气凝华，直到这一局结束";
                    case JourneyStep.TribunalApproach: return "以哥白尼之名进入大厅，走近教皇与学者";
                    case JourneyStep.TribunalTalk: return "看向人物，按住 F 提出自己的想法";
                    case JourneyStep.WalkOutside: return "警报打断了质询：走到门外观察天空";
                    case JourneyStep.ObserveThreeSuns: return "看看城外突然变亮的天空";
                    case JourneyStep.AfterThreeSuns: return "继续观察，或先翻阅观察簿";
                    case JourneyStep.Collapse: return "热度正在越过整座城市……";
                    default: return "";
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { session = null; previewMode = false; }

        private void Start()
        {
            stage = GetComponent<TravellerStage>();
            gobi=GetComponent<GobiSequenceController>();hud=GetComponentInChildren<TravellerHud>();dialogueDirector=GetComponent<NpcDialogueDirector>();
            decisionController=GetComponent<NpcDecisionController>();
            if (stage.profile.sceneIndex == 1) longNight = new CapitalLongNight(stage, this);
            if(stage.profile.gobiStory!=null){var settings=stage.profile.gobiStory;Progress.ConfigureGobi(settings.minimumOpeningTalkSeconds,settings.minimumShelterTalkSeconds,settings.suddenSunsetSeconds,settings.minimumNightWalkSeconds,settings.minimumNightWalkMetres);}
            palaceSkylight = GetComponentInChildren<PalaceSkylight>();
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
                if (candidate.name.StartsWith("周文王 [", StringComparison.Ordinal)) { traveller = candidate; break; }
            Progress.Changed += ApplyProgress;
            stage.ObservationRecorded += OnObservation;
            ApplyProgress();
        }

        public void Begin()
        {
            CancelPendingDialogueDecision(); Decisions.Reset(); relevantExplanation=false;
            NpcConversationMemory.Clear();
            stage.ResetObservationWitnesses();
            DialogueOpen = false; previewMode = false; appliedStep = (JourneyStep)(-1); Progress.Begin();
            if (stage.profile.sceneIndex == 0) stage.player.Teleport(stage.profile.moments[0].viewPosition, stage.profile.moments[0].viewTarget);
        }

        public void ReturnToTitle() { DialogueOpen = false; previewMode = false; Progress.ReturnToTitle(); }

        public void EnterNextCivilization()
        {
            if (IsCapitalEnding) Progress.Continue();
        }

        public void TogglePreview()
        {
            previewMode = !previewMode; DialogueOpen = false;
            if (previewMode) longNight?.Reset();
            if (!previewMode) { Progress.ReturnToTitle(); if (stage.profile.sceneIndex != 0) stage.LoadScene(0); }
            Changed?.Invoke();
        }

        public void Interact()
        {
            if(!previewMode)dialogueDirector?.BeginPlayerQuestion();
        }

        public void OnQuestionAnswered(string npcId,string text)
        {
            // Kept for old callers. An answered question alone grants no plot action.
        }
        public void CancelPendingDialogueDecision() { advanceAfterAnswer = null; }
        public NpcDecisionOutcome ResolveNpcDecision(string npcId, string intent, string conduct, bool online,
            JourneyStep answeredStep, int requestRevision)
        {
            if (Progress.Step != answeredStep || Decisions.Defeated || IsTitle || previewMode) return NpcDecisionOutcome.None;
            if (online && requestRevision == Decisions.Revision && answeredStep == JourneyStep.TribunalTalk &&
                NpcDecisionState.IsConversant(npcId, answeredStep) && conduct == "relevant_proposal") relevantExplanation = true;
            var result = Decisions.Apply(npcId, answeredStep, AudienceReady, decisionController != null && decisionController.CanDispatchGuards,
                intent, conduct, online, requestRevision);
            if (result == NpcDecisionOutcome.None) return result;
            CancelPendingDialogueDecision();
            if (result == NpcDecisionOutcome.Advance)
            {
                if (answeredStep == JourneyStep.TribunalTalk) Progress.Explain(relevantExplanation);
                advanceAfterAnswer=answeredStep;answerAdvanceAt=Time.unscaledTime+3;
            }
            decisionController?.Execute(result); Changed?.Invoke(); return result;
        }
        public void ContinueScriptedAudience()
        {
            if (!CanContinueScriptedAudience) return;
            if (Progress.Step == JourneyStep.TribunalTalk) Progress.Explain(false);
            advanceAfterAnswer=Progress.Step;answerAdvanceAt=Time.unscaledTime+1; CloseDialogue();
        }

        public void SelectDialogue(string choice)
        {
            // Gobi questions use the same bounded, voiced channel as microphone and typed input.
            if(gobi!=null&&(choice=="identity"||choice=="hourglass"||choice=="calendar"||choice=="where"||choice=="danger"))
            {
                string question=choice=="identity"?"你是谁？为什么在这里？":choice=="hourglass"?"为什么带着沙漏？":choice=="calendar"?"你如何判断天象？":choice=="where"?"这里是什么地方？":"为什么夜里也这么危险？";
                dialogueDirector?.Submit(question);DialogueSelected?.Invoke(choice);return;
            }
            switch (choice)
            {
                case "identity": Progress.IdentifyTraveller(); Say("周文王：我是姬昌。我要去朝歌，把推演的历法呈给纣王。"); break;
                case "hourglass": Say("旅人：太阳不能替我们可靠地计时。沙漏仍在走，我们要把所见记下来。"); break;
                case "calendar": Say("周文王：两颗飞星预示恒纪元，是我据以推演的经验。朝歌的人们还在等。"); break;
                case "evidence": Say("纣王：一颗、两颗飞星……寡人要看的，是能经受查验的判断。"); break;
                case "verification": Say("纣王：就按历法等待。若日照应验，再让人们复苏。"); break;
                case "explain":
                    Progress.Explain(true);
                    stage.Journal.hypothesis = "飞星可能是远处的太阳。三个太阳与世界的距离变化，可以同时解释飞星和异常日照。";
                    stage.Journal.Save(); Say("学者：这能联系两种观察，但为什么从未留下三个太阳同时出现的记录？教皇仍拒绝承认。", 7f); break;
                case "uncertain": Progress.Explain(false); Say("学者：那就保留疑问。已有的记录，还不足以保证我们理解了一切。"); break;
                case "continue":
                    if(Progress.Step==JourneyStep.ShelterTalk){DialogueOpen=false;break;}
                    if (Progress.Step == JourneyStep.TribunalTalk && !Progress.ConsideredExplanation) return;
                    DialogueOpen = false; Progress.Continue(); break;
                case "close": DialogueOpen = false; Changed?.Invoke(); break;
            }
            DialogueSelected?.Invoke(choice);
            Changed?.Invoke();
        }

        public void FaceGoal()
        {
            Vector3 target = CanObserve ? stage.ObservationTarget() : (Progress.Step == JourneyStep.ShelterTalk || IsWalking) && traveller != null ? traveller.position + Vector3.up * 1.5f : Goal + Vector3.up;
            if (IsLongNightWalk) target = Goal + Vector3.up;
            if(palaceSkylight!=null&&Progress.Step==JourneyStep.CalendarWait)target=palaceSkylight.observationPoint;
            else if(Progress.Step==JourneyStep.CalendarFailed && longNight!=null) target=longNight.CourtFocus;
            else if(palaceSkylight!=null&&Progress.Step==JourneyStep.CourtTalk)target=stage.profile.courtGoal+new Vector3(0,3,20);
            stage.player.FaceTarget(target);
        }

        private void OnObservation(int moment)
        {
            if (!previewMode && Progress.Observe(stage.profile.sceneIndex, moment)) DialogueOpen = false;
        }

        private void ApplyProgress()
        {
            if (stage == null || previewMode) return;
            if (Progress.Step != JourneyStep.Title && stage.profile.sceneIndex != Progress.SceneIndex) { stage.LoadScene(Progress.SceneIndex); return; }
            bool entered = appliedStep != Progress.Step; appliedStep = Progress.Step;
            if (entered)
            {
                if(dialogueDirector==null||!dialogueDirector.HasPlayerQuestion)DialogueOpen = false;
                advanceAfterAnswer=null;
                switch (Progress.Step)
                {
                    case JourneyStep.Awakening:
                        SetMoment(0);SetPath();speech="";
                        if(traveller!=null)traveller.position=stage.profile.gobiStory.meetingPosition;
                        gobi?.ResetForNewRun();break;
                    case JourneyStep.ExploreDesert: Say("寒风卷过砂砾。四周只有戈壁、枯木。",7);break;
                    case JourneyStep.ApproachTraveller: Say("旅人：来这里！",4);break;
                    case JourneyStep.OpeningTalk: SetPath();Say("旅人：快过来，现在是乱纪元，太阳随时可能出现，不要命吗？",8);break;
                    case JourneyStep.Sunrise:
                        stage.ApplyMoment(1,false,false,stage.profile.gobiStory.sunriseSeconds);
                        SetPath(new Vector3(-10f,0f,3f),new Vector3(-16f,0f,8f),new Vector3(-16f,0f,13f));
                        Say("旅人：太阳！快跟我躲进岩石下面！",7);break;
                    case JourneyStep.SeekShelter:
                        break;
                    case JourneyStep.ShelterTalk: SetMoment(1);Say("旅人：进来，别站在日光里。现在可以喘口气了。",6);break;
                    case JourneyStep.Sunset: stage.ApplyMoment(2,false,false,stage.profile.gobiStory.suddenSunsetSeconds);Say("姬昌：看，太阳终于落下去了。跟着我，我们去朝歌。",5);break;
                    case JourneyStep.WalkToFirstStar: SetPath(new Vector3(-12f,0f,31f), new Vector3(-1f,0f,42f)); Say(""); break;
                    case JourneyStep.ObserveFirstStar: SetMoment(3); Say(""); break;
                    case JourneyStep.WalkToTwoStars: SetPath(new Vector3(-1f,0f,54f),new Vector3(-1f,0f,64f)); Say(""); break;
                    case JourneyStep.ObserveTwoStars: SetMoment(4); Say(""); break;
                    case JourneyStep.WalkToChaoge: stage.ApplyMoment(5,true,false); SetPath(new Vector3(-15f,0f,18f),new Vector3(-14f,0f,26f),new Vector3(-12f,0f,31f),new Vector3(-1f,0f,42f),new Vector3(-1f,0f,64f)); Say("姬昌：我在前面照路。跟紧些，我们一同去朝歌。"); break;
                    case JourneyStep.DepartForCapital: Say(""); break;
                    case JourneyStep.CourtApproach:
                        SetMoment(0); if (traveller != null) traveller.position = stage.profile.hasCapitalRoute ? stage.profile.companionArrival : new Vector3(-2f,0f,-15f);
                        SetPath(stage.profile.hasCapitalRoute ? stage.profile.courtWaypoints : new[] { new Vector3(-3.3f,0f,14f) });
                        Say("数日之后，朝歌王都。周文王：到了。巨摆之间的金字塔就是王宫，跟我走进甬道。", 10f); break;
                    case JourneyStep.CourtTalk: Say(stage.Audience!=null?"纣王：将表章呈上来。待寡人看过，再说你的历法。":"纣王：下一次稳定时期何时到来？把能让寡人查验的事说清楚。"); break;
                    case JourneyStep.CalendarWait: Say(stage.HasPendingCourtAudience?"表章正由传奏官送呈。待大王览奏，再演示卦象与历法。":"石板缓缓移开，日光从竖井落入内殿。沙漏继续流下……这一次，天窗光斑接近了推演。", 7f); break;
                    case JourneyStep.WalkToWater: SetMoment(1); SetPath(stage.profile.hasCapitalRoute ? stage.profile.waterWaypoints : new[] { new Vector3(-6f,0f,-9f),new Vector3(13f,0f,-9f),new Vector3(15f,0f,-6f),new Vector3(19f,0f,-6f),new Vector3(28f,0f,-9f) }); Say("周文王：这一次，它与推演相合。沿侧面甬道出殿，去复苏池边看看。"); break;
                    case JourneyStep.ObserveRevival: Say("周文王：看，这些脱水的人正在吸水。等他们恢复形体，重新站起来。", 8f); break;
                    case JourneyStep.WaitForDawn:
                        SetMoment(2); Say("复苏后的第八天。沙漏显示日出时辰已过，天边仍是一片漆黑。", 6f); break;
                    case JourneyStep.ReturnToCourt:
                        SetLongNightRoute(true); Say("姬昌：历法说此刻应该天亮了。随我回殿，我要向大王禀明。", 7f); break;
                    case JourneyStep.CalendarFailed:
                        SetPath(); Say("两日后 · 王殿\n姬昌：请大王再宽限一刻。日光也许还会回来。", 3f); break;
                    case JourneyStep.LeaveLongNightCourt:
                        SetLongNightRoute(false); Say("殿门上亮起 EXIT。随人群沿甬道走向殿外。", 6f); break;
                    case JourneyStep.ObserveLongNight:
                        Say("远处没有晨光。三颗飞星靠得极近，绕着彼此旋转。", 6f); break;
                    case JourneyStep.AfterLongNight:
                        Say("长夜中开始飘雪。那是水结成的雪，渐渐盖住石阶。", 7f); break;
                    case JourneyStep.DryIceSnow:
                        Say("十天后，雪片变得更重。落下来的已经是凝华的二氧化碳。", 7f); break;
                    case JourneyStep.AtmosphereFreeze:
                        Say("又过了十天。氧与氮凝成淡蓝色的雪，火把熄灭，地面逐渐覆上一层冰晶。", 10f); break;
                    case JourneyStep.TribunalApproach: SetMoment(0); Say("文明已经更替。你保留着自己的记录，以哥白尼之名进入新的大厅。", 7f); break;
                    case JourneyStep.TribunalTalk: Say("学者：你的解释，能同时说明飞星和那些异常的日照吗？"); break;
                    case JourneyStep.WalkOutside: SetMoment(1); Say("警报响起。门外骤然明亮，争论被打断了。到外面看看！", 7f); break;
                    case JourneyStep.ObserveThreeSuns: Say(""); break;
                    case JourneyStep.AfterThreeSuns: Say("学者：等等……天上真的有三个。", 5f); break;
                    case JourneyStep.Collapse: SetMoment(2); Say("热风越过城墙。万物在无穷无尽的烈焰中燃烧殆尽。", 9f); break;
                }
                longNight?.Enter(Progress.Step);
            }
            Changed?.Invoke();
        }

        private void SetLongNightRoute(bool returning)
        {
            var points = stage.profile.waterWaypoints ?? Array.Empty<Vector3>();
            bool addGoal = returning || points.Length == 0 || points[points.Length-1] != stage.profile.waterGoal;
            longNightRoute = new Vector3[points.Length + (addGoal ? 1 : 0)];
            if (returning)
            {
                for (int i = 0; i < points.Length; i++) longNightRoute[i] = points[points.Length - 1 - i];
                longNightRoute[points.Length] = stage.profile.courtGoal;
                SetPath(longNightRoute);
            }
            else
            {
                Array.Copy(points, longNightRoute, points.Length);
                if (addGoal) longNightRoute[points.Length] = stage.profile.waterGoal;
            }
            longNightWaypoint = 0;
        }

        private void SetMoment(int index) { if (stage.MomentIndex != index) stage.ApplyMoment(index, false, false); }
        private void SetPath(params Vector3[] points) { actorPath = points; actorWaypoint = 0; }
        public void GuideCompanionForCalendar(Vector3 position)
        {
            if (stage.profile.sceneIndex == 1 && Progress.Step == JourneyStep.CalendarWait) SetPath(position);
        }
        public void GuideCompanionForAudience(params Vector3[] positions)
        {
            if(stage.profile.sceneIndex==1&&(Progress.Step==JourneyStep.CourtTalk||Progress.Step==JourneyStep.CalendarWait))SetPath(positions);
        }
        private void Say(string value, float seconds = 6f) { speech = value; SpeechVersion++; speechUntil = Time.unscaledTime + seconds;dialogueDirector?.RememberAuthoredLine(value); Changed?.Invoke(); }
        public void Speak(string value,float seconds=8){Say(value,seconds);}
        public void CloseDialogue(){SetDialogueOpen(false);}
        public void SetDialogueOpen(bool value){DialogueOpen=value;Changed?.Invoke();}

        private void Update()
        {
            if (stage == null || previewMode || IsTitle || IsSummary || Decisions.Defeated || stage.IsLoading || (hud!=null&&hud.IsWorldPaused)) return;
            longNight?.Update(Progress.Step, Progress.Elapsed);
            if(Progress.Step!=JourneyStep.CalendarWait||!stage.HasPendingCourtAudience)Progress.Tick(gobi!=null?Time.unscaledDeltaTime:Time.deltaTime);
            bool conversing=dialogueDirector!=null&&(dialogueDirector.HasPlayerQuestion||dialogueDirector.IsBusy||dialogueDirector.IsVocalising);
            if(!conversing)
            {
                if(advanceAfterAnswer==Progress.Step&&Time.unscaledTime>=answerAdvanceAt&&SpeechRemainingSeconds<=0){advanceAfterAnswer=null;Progress.Continue();}
                else if(Progress.Elapsed>=3&&(Progress.Step==JourneyStep.DepartForCapital||Progress.Step==JourneyStep.AfterRevival||Progress.Step==JourneyStep.AfterThreeSuns))Progress.Continue();
            }

            if (IsLongNightWalk)
            {
                Vector3 delta = Goal - stage.player.transform.position;
                if (Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude < 1.2f * 1.2f && Mathf.Abs(delta.y) < .8f)
                {
                    if (longNightWaypoint + 1 < longNightRoute.Length) longNightWaypoint++;
                    else Progress.Arrive();
                }
            }
            else if (gobi==null&&IsWalking&&Progress.SceneIndex == stage.profile.sceneIndex&&Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(Goal.x,Goal.z))<6f)Progress.Arrive();
            if (traveller != null && actorPath != null && actorWaypoint < actorPath.Length && !Decisions.Pursuing &&
                !(stage.HasPendingCourtAudience&&stage.Audience.ParticipantSpeaking)&&
                (stage.HasPendingCourtAudience||!ShouldWaitForPlayer(traveller.position, stage.player.transform.position, actorPath[actorPath.Length - 1])))
            {
                var delta = actorPath[actorWaypoint] - traveller.position;
                // The engraved ritual has a tighter arrival radius than travel waypoints.
                float arrivalSquared = Progress.Step == JourneyStep.CalendarWait || Progress.Step==JourneyStep.CourtTalk && stage.HasPendingCourtAudience ? .01f : .16f;
                if (delta.sqrMagnitude < arrivalSquared) actorWaypoint++;
                else
                {
                    float speed=stage.profile.gobiStory!=null&&(Progress.Step==JourneyStep.Sunrise||Progress.Step==JourneyStep.SeekShelter)?stage.profile.gobiStory.dangerRunSpeed:stage.profile.companionWalkSpeed;
                    traveller.position=Vector3.MoveTowards(traveller.position,actorPath[actorWaypoint],speed*Time.deltaTime);
                    var facing=Vector3.ProjectOnPlane(delta,Vector3.up);if(facing.sqrMagnitude>.001f)traveller.rotation=Quaternion.Slerp(traveller.rotation,Quaternion.LookRotation(facing),Time.deltaTime*4);
                }
            }
            if (!string.IsNullOrEmpty(speech) && Time.unscaledTime > speechUntil) { speech = ""; Changed?.Invoke(); }
        }

        public static bool ShouldWaitForPlayer(Vector3 actor, Vector3 player, Vector3 destination)
        {
            return Vector3.Distance(actor, player) > 16f && Vector3.Distance(player, destination) > Vector3.Distance(actor, destination);
        }

        private void OnDestroy()
        {
            longNight?.Dispose();
            if (session != null) session.Changed -= ApplyProgress;
            if (stage != null) stage.ObservationRecorded -= OnObservation;
        }
    }
}
