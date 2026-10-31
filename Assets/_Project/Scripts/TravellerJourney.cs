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
        public event Action Changed;
        public event Action<string> DialogueSelected;
        public JourneyProgress Progress => session ?? (session = new JourneyProgress());
        public bool IsPreviewing => previewMode;
        public bool DialogueOpen { get; private set; }
        public string Speech => speech;
        public bool IsTitle => !previewMode && Progress.Step == JourneyStep.Title;
        public bool IsSummary => !previewMode && Progress.Step == JourneyStep.Summary;
        public bool CanObserve => !previewMode && (Progress.Step != JourneyStep.ObserveRevival || stage.IsRevivalComplete) && ((Progress.Step==JourneyStep.WalkToChaoge&&stage.profile.sceneIndex==0&&(stage.MomentIndex==3||stage.MomentIndex==4))||Progress.Step == JourneyStep.ObserveFirstStar || Progress.Step == JourneyStep.ObserveTwoStars || Progress.Step == JourneyStep.ObserveRevival || Progress.Step == JourneyStep.ObserveLongNight || Progress.Step == JourneyStep.ObserveThreeSuns);
        public bool IsWalking => Progress.Step == JourneyStep.SeekShelter || Progress.Step == JourneyStep.WalkToFirstStar || Progress.Step == JourneyStep.WalkToTwoStars || Progress.Step == JourneyStep.WalkToChaoge || Progress.Step == JourneyStep.CourtApproach || Progress.Step == JourneyStep.WalkToWater || Progress.Step == JourneyStep.TribunalApproach || Progress.Step == JourneyStep.WalkOutside;
        public bool HasNavigation => IsWalking || Progress.Step==JourneyStep.ExploreDesert || Progress.Step==JourneyStep.ApproachTraveller;
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
                    case JourneyStep.ObserveFirstStar: return "抬头辨认一颗飞星，按 E 记录";
                    case JourneyStep.WalkToTwoStars: return "经过休整，前往下一处观星点";
                    case JourneyStep.ObserveTwoStars: return "同时看清两颗飞星，按 E 对照记录";
                    case JourneyStep.WalkToChaoge: return "与姬昌一同前往朝歌";
                    case JourneyStep.DepartForCapital: return "沿驿路启程前往朝歌；按住 F 可随时提问";
                    case JourneyStep.CourtApproach: return "随周文王入城，穿过金字塔甬道进入石殿";
                    case JourneyStep.CourtTalk: return "看向人物，按住 F 参与质询";
                    case JourneyStep.CalendarWait: return "观察沙漏与天窗光斑，等待短期验证";
                    case JourneyStep.WalkToWater: return "从侧门出殿，前往复苏水边";
                    case JourneyStep.ObserveRevival: return stage.IsRevivalComplete ? "见证文明复苏，按 E 记录应验的结果" : "观察纸片浸水、恢复形体并重新站起";
                    case JourneyStep.AfterRevival: return "等待下一次日出；按住 F 可随时提问";
                    case JourneyStep.WaitForDawn: return "日出应当到了……继续观察天空";
                    case JourneyStep.ObserveLongNight: return "记录没有出现的日出，按 E 留下反例";
                    case JourneyStep.AfterLongNight: return "带着记录进入新的文明";
                    case JourneyStep.TribunalApproach: return "以哥白尼之名进入大厅，走近教皇与学者";
                    case JourneyStep.TribunalTalk: return "看向人物，按住 F 提出自己的想法";
                    case JourneyStep.WalkOutside: return "警报打断了质询：走到门外观察天空";
                    case JourneyStep.ObserveThreeSuns: return "先看清三个太阳，再按 E 记录";
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
            DialogueOpen = false; previewMode = false; appliedStep = (JourneyStep)(-1); Progress.Begin();
            if (stage.profile.sceneIndex == 0) stage.player.Teleport(stage.profile.moments[0].viewPosition, stage.profile.moments[0].viewTarget);
        }

        public void ReturnToTitle() { DialogueOpen = false; previewMode = false; Progress.ReturnToTitle(); }

        public void TogglePreview()
        {
            previewMode = !previewMode; DialogueOpen = false;
            if (!previewMode) { Progress.ReturnToTitle(); if (stage.profile.sceneIndex != 0) stage.LoadScene(0); }
            Changed?.Invoke();
        }

        public void Interact()
        {
            if(!previewMode)dialogueDirector?.BeginPlayerQuestion();
        }

        public void OnQuestionAnswered(string npcId,string text)
        {
            if(Progress.Step==JourneyStep.CourtTalk&&npcId=="king"||Progress.Step==JourneyStep.TribunalTalk&&(npcId=="pope"||npcId=="scholar"||npcId=="davinci"))
            {
                if(Progress.Step==JourneyStep.TribunalTalk)Progress.Explain(false);
                advanceAfterAnswer=Progress.Step;answerAdvanceAt=Time.unscaledTime+3;
            }
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
            Vector3 target = CanObserve ? stage.Current.viewTarget : (Progress.Step == JourneyStep.ShelterTalk || IsWalking) && traveller != null ? traveller.position + Vector3.up * 1.5f : Goal + Vector3.up;
            if(palaceSkylight!=null&&Progress.Step==JourneyStep.CalendarWait)target=palaceSkylight.observationPoint;
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
                    case JourneyStep.ExploreDesert: Say("寒风卷过砂砾。四周只有戈壁、枯木，以及远处的一点火光。",7);break;
                    case JourneyStep.ApproachTraveller: Say("旅人：来这里！",4);break;
                    case JourneyStep.OpeningTalk: SetPath();Say("旅人：快过来，现在是乱纪元，不要命吗？",6);break;
                    case JourneyStep.Sunrise:
                        stage.ApplyMoment(1,false,false,stage.profile.gobiStory.sunriseSeconds);
                        SetPath(new Vector3(-10f,0f,3f),new Vector3(-16f,0f,8f),new Vector3(-16f,0f,13f));
                        Say("旅人：太阳！快跟我躲进岩石下面！",7);break;
                    case JourneyStep.SeekShelter:
                        break;
                    case JourneyStep.ShelterTalk: SetMoment(1);Say("旅人：进来，别站在日光里。现在可以喘口气了。",6);break;
                    case JourneyStep.Sunset: stage.ApplyMoment(2,false,false,stage.profile.gobiStory.suddenSunsetSeconds);Say("姬昌：看，太阳落下去了。跟着我，我们去朝歌。",5);break;
                    case JourneyStep.WalkToFirstStar: SetPath(new Vector3(-12f,0f,31f), new Vector3(-1f,0f,42f)); Say("周文王：它落下了。路能走的时候，就不要再等。"); break;
                    case JourneyStep.ObserveFirstStar: SetMoment(3); Say("周文王：看石峰上方。别只看它多亮，看看它和旁边那些星的位置。", 7f); break;
                    case JourneyStep.WalkToTwoStars: SetPath(new Vector3(-1f,0f,54f),new Vector3(-1f,0f,64f)); Say("行路与休整之后，到了下一次观测的时刻。", 6f); break;
                    case JourneyStep.ObserveTwoStars: SetMoment(4); Say("周文王：这一次有两颗飞星。按我过去的推演，恒纪元将至。", 7f); break;
                    case JourneyStep.WalkToChaoge: stage.ApplyMoment(5,true,false); SetPath(new Vector3(-15f,0f,18f),new Vector3(-14f,0f,26f),new Vector3(-12f,0f,31f),new Vector3(-1f,0f,42f),new Vector3(-1f,0f,64f)); Say("姬昌：我在前面照路。跟紧些，我们一同去朝歌。"); break;
                    case JourneyStep.DepartForCapital: Say("周文王：戈壁到这里就走完了。收好观察簿，准备启程入都吧。", 12f); break;
                    case JourneyStep.CourtApproach:
                        SetMoment(0); if (traveller != null) traveller.position = stage.profile.hasCapitalRoute ? stage.profile.companionArrival : new Vector3(-2f,0f,-15f);
                        SetPath(stage.profile.hasCapitalRoute ? stage.profile.courtWaypoints : new[] { new Vector3(-3.3f,0f,14f) });
                        Say("数日之后，朝歌王都。周文王：到了。巨摆之间的金字塔就是王宫，跟我走进甬道。", 10f); break;
                    case JourneyStep.CourtTalk: Say("纣王：下一次稳定时期何时到来？把能让寡人查验的事说清楚。"); break;
                    case JourneyStep.CalendarWait: Say("石板缓缓移开，日光从竖井落入内殿。沙漏继续流下……这一次，天窗光斑接近了推演。", 7f); break;
                    case JourneyStep.WalkToWater: SetMoment(1); SetPath(stage.profile.hasCapitalRoute ? stage.profile.waterWaypoints : new[] { new Vector3(-6f,0f,-9f),new Vector3(13f,0f,-9f),new Vector3(15f,0f,-6f),new Vector3(19f,0f,-6f),new Vector3(28f,0f,-9f) }); Say("周文王：这一次，它与推演相合。沿侧面甬道出殿，去复苏池边看看。"); break;
                    case JourneyStep.ObserveRevival: Say("周文王：看，这些脱水的人正在吸水。等他们恢复形体，重新站起来。", 8f); break;
                    case JourneyStep.WaitForDawn: Say("人们等待着下一次日出。沙漏继续流下，晨光却没有到来。", 6f); break;
                    case JourneyStep.ObserveLongNight: SetMoment(2); Say("周文王：不该是这样……再等一会儿。", 7f); break;
                    case JourneyStep.TribunalApproach: SetMoment(0); Say("文明已经更替。你保留着自己的记录，以哥白尼之名进入新的大厅。", 7f); break;
                    case JourneyStep.TribunalTalk: Say("学者：你的解释，能同时说明飞星和那些异常的日照吗？"); break;
                    case JourneyStep.WalkOutside: SetMoment(1); Say("警报响起。门外骤然明亮，争论被打断了。到外面看看！", 7f); break;
                    case JourneyStep.ObserveThreeSuns: Say("学者：等等……天上真的有三个。", 5f); break;
                    case JourneyStep.Collapse: SetMoment(2); Say("热风越过城墙。认识世界的结构，仍不能保证文明安全。", 9f); break;
                }
            }
            Changed?.Invoke();
        }

        private void SetMoment(int index) { if (stage.MomentIndex != index) stage.ApplyMoment(index, false, false); }
        private void SetPath(params Vector3[] points) { actorPath = points; actorWaypoint = 0; }
        private void Say(string value, float seconds = 6f) { speech = value; speechUntil = Time.unscaledTime + seconds; Changed?.Invoke(); }
        public void Speak(string value,float seconds=8){Say(value,seconds);}
        public void CloseDialogue(){SetDialogueOpen(false);}
        public void SetDialogueOpen(bool value){DialogueOpen=value;Changed?.Invoke();}

        private void Update()
        {
            if (stage == null || previewMode || IsTitle || IsSummary || stage.IsLoading || (hud!=null&&hud.IsWorldPaused)) return;
            Progress.Tick(gobi!=null?Time.unscaledDeltaTime:Time.deltaTime);
            bool conversing=dialogueDirector!=null&&(dialogueDirector.HasPlayerQuestion||dialogueDirector.IsBusy||dialogueDirector.IsVocalising);
            if(!conversing)
            {
                if(advanceAfterAnswer==Progress.Step&&Time.unscaledTime>=answerAdvanceAt){advanceAfterAnswer=null;Progress.Continue();}
                else if(Progress.Elapsed>=3&&(Progress.Step==JourneyStep.DepartForCapital||Progress.Step==JourneyStep.AfterRevival||Progress.Step==JourneyStep.AfterLongNight||Progress.Step==JourneyStep.AfterThreeSuns))Progress.Continue();
            }

            if (gobi==null&&IsWalking&&Progress.SceneIndex == stage.profile.sceneIndex&&Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(Goal.x,Goal.z))<6f)Progress.Arrive();
            if (traveller != null && actorPath != null && actorWaypoint < actorPath.Length && !ShouldWaitForPlayer(traveller.position, stage.player.transform.position, actorPath[actorPath.Length - 1]))
            {
                var delta = actorPath[actorWaypoint] - traveller.position;
                if (delta.sqrMagnitude < 0.16f) actorWaypoint++;
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
            if (session != null) session.Changed -= ApplyProgress;
            if (stage != null) stage.ObservationRecorded -= OnObservation;
        }
    }
}
