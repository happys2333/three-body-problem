#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    public sealed class NpcDecisionPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed; public int checks,guards,damageEvents,healthAfterHit;
            public string phase; public float courtIdleP95,courtPursuitP95;
            public float gpuP95,cpuMainMean,renderMean;public int frameCap;
            public List<string> errors=new List<string>();
        }
        private readonly Report report=new Report();
        private TravellerStage stage; private TravellerJourney journey; private TravellerHud hud;
        private NpcDialogueDirector dialogue; private NpcDecisionController combat;
        private string output; private bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Boot()
        {
            string output=Option("-npc-decision-check");if(output==null||FindFirstObjectByType<NpcDecisionPlayerCheck>()!=null)return;
            var go=new GameObject("Development NPC decision verification");DontDestroyOnLoad(go);go.AddComponent<NpcDecisionPlayerCheck>().output=output;
        }
        private IEnumerator Start()
        {
            Application.runInBackground=true;Application.targetFrameRate=90;QualitySettings.vSyncCount=0;
            Application.logMessageReceived+=OnLog;Directory.CreateDirectory(Path.GetDirectoryName(output));
            yield return Frames(16);Bind();
            string renderProbe=Option("-decision-render-probe");
            if(renderProbe!=null){yield return RenderProbe(renderProbe);yield break;}
            var config=TravellerUserSettings.Current;config.connection.mode="custom";config.connection.apiUrl=Option("-decision-test-upstream");
            config.connection.model="qa-decision-model";config.connection.apiKey="test-decision-only-key";config.connection.timeoutSeconds=5;
            config.connection.contextWindowTokens=32000;config.gatewayUrl=Option("-decision-test-gateway");config.speakReplies=false;
            journey.Begin();var p=journey.Progress;p.WakeUp();p.DiscoverTraveller();p.MeetTraveller();yield return Frames(4);
            var traveller=stage.GetComponent<TravellerCastDirector>().companion.GetComponent<NpcIdentity>();
            Phase("provider failures leave story and memory untouched");
            var questions=new[]{"QA_REJECT","QA_TIMEOUT","QA_AUTH","QA_RATE","QA_INVALID"};
            var codes=new[]{"content_rejected","timeout","authentication","rate_limited","invalid_response"};
            for(int i=0;i<questions.Length;i++)
            {
                yield return Ask(traveller,questions[i]);
                Check(dialogue.UsingFallback&&dialogue.FailureCode==codes[i],"Failure classification: "+questions[i]);
                Check(p.Step==JourneyStep.OpeningTalk&&p.OpeningAnswers==0,"Provider failure advanced the sky or consumed an answer.");
                Check(NpcConversationMemory.History("major:wenwang").Count==0,"Rejected input poisoned NPC memory.");
            }
            Capture("01_Service_Failure_Feedback");yield return Frames(4);
            yield return Ask(traveller,"QA_RECOVER");Check(!dialogue.UsingFallback&&p.OpeningAnswers==1,"A valid reply could not recover after a failed request.");
            NpcConversationMemory.RememberTurn("major:wenwang","我叫阿遥，是个木匠。","阿遥，跟紧我。");
            journey.CloseDialogue();Enter(JourneyStep.CourtApproach);
            yield return Until(()=>SceneManager.GetActiveScene().name==TravellerStage.SceneNames[1],35);yield return Frames(16);Bind();
            report.guards=combat.ReadyGuardCount;Check(combat.CanDispatchGuards,"No reachable court guards were integrated.");
            if(!combat.CanDispatchGuards){Finish();yield break;}
            combat.settings=Instantiate(combat.settings);combat.settings.attackDamage=10;combat.settings.initialGraceSeconds=.3f;
            PlaceForAudience();Enter(JourneyStep.CourtTalk);yield return Frames(4);
            var king=stage.GetComponent<TravellerCastDirector>().king.GetComponent<NpcIdentity>();
            Phase("incomplete ceremony and arbitrary chatter cannot advance");
            yield return Ask(king,"QA_ADVANCE");Check(journey.Progress.Step==JourneyStep.CourtTalk,"Advance bypassed the petition ceremony.");
            journey.CloseDialogue();yield return Until(()=>stage.Audience.IsComplete,65);
            yield return Ask(king,"QA_REJECT");Check(!journey.CanContinueScriptedAudience&&journey.Progress.Step==JourneyStep.CourtTalk,"An online failure exposed an offline completion shortcut.");
            foreach(var text in new[]{"QA_GREETING","QA_CLARIFY","QA_ATTACK_WITHOUT_WARNING","QA_CORRECT_CONCLUSION"})
            {yield return Ask(king,text);Check(journey.Progress.Step==JourneyStep.CourtTalk&&!journey.Decisions.Pursuing&&combat.Health==100,"A harmless/unsupported reply advanced or harmed the player: "+text);}
            journey.OnQuestionAnswered("king","随便一句话");yield return Frames(5);
            Check(journey.Progress.Step==JourneyStep.CourtTalk,"The legacy answer callback still queued progression.");
            Phase("court idle performance");yield return Measure(false);
            Phase("warning then actual guard pursuit and damage");
            yield return Ask(king,"QA_WARN");Check(journey.Decisions.WarningActive&&!journey.Decisions.Pursuing,"First misconduct did not produce a bounded warning.");
            Capture("02_King_Warning");yield return Frames(4);
            var agentsBefore=stage.GetComponentsInChildren<NavMeshAgent>();
            Check(agentsBefore.Length==0,"Guards had active navigation agents before an order.");
            yield return Ask(king,"QA_PURSUIT");Check(journey.Decisions.Pursuing,"A valid repeated threat did not start pursuit.");
            var guards=stage.GetComponentsInChildren<NavMeshAgent>();var starts=new Vector3[guards.Length];
            for(int i=0;i<guards.Length;i++)starts[i]=guards[i].transform.position;
            yield return Delay(1);
            bool moved=false;for(int i=0;i<guards.Length;i++)moved|=Vector3.Distance(starts[i],guards[i].transform.position)>.1f;
            Check(moved,"Ordered guards did not move along navigation.");
            yield return Measure(true);
            yield return Until(()=>combat.DamageEvents>0,35);report.healthAfterHit=combat.Health;
            if(combat.HitPulse<=0)yield return Until(()=>combat.HitPulse>0,3);
            Check(combat.Health<100&&combat.Health>0&&combat.HitPulse>0,"A reachable guard failed to inflict a real hidden-health hit.");
            Capture("03_Guard_Hit_Feedback");yield return Frames(3);
            Phase("apology recalls guards and prevents further damage");
            yield return Ask(king,"QA_APOLOGY");Check(!journey.Decisions.Pursuing&&!journey.Decisions.WarningActive,"Apology did not recall the guards.");
            int damage=combat.DamageEvents;yield return Delay(2);Check(combat.DamageEvents==damage,"Guards kept attacking after recall.");
            yield return Until(()=>Array.TrueForAll(guards,a=>!a.enabled),20);
            Check(Array.TrueForAll(guards,a=>!a.GetComponent<EraGuardEquipment>().CombatControlled),"Returning guards did not release their combat pose.");
            Phase("a solid wall blocks a spear strike");
            var guard=guards[0].GetComponent<TravellerActor>();
            stage.player.Teleport(guard.transform.position+guard.transform.forward*1.25f+Vector3.up*stage.player.eyeHeight,guard.transform.position+Vector3.up*1.2f);
            Physics.SyncTransforms();Check(combat.HasStrikeLine(guard),"The control strike line was already blocked.");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="QA temporary solid shield wall";
            wall.transform.position=(guard.transform.position+stage.player.transform.position)*.5f+Vector3.up;
            wall.transform.rotation=Quaternion.LookRotation(stage.player.transform.position-guard.transform.position);wall.transform.localScale=new Vector3(2,2,.15f);
            Physics.SyncTransforms();Check(!combat.HasStrikeLine(guard),"A spear strike passed through a solid wall.");
            Destroy(wall);yield return Frames(3);Physics.SyncTransforms();Check(combat.HasStrikeLine(guard),"Removing the wall did not restore a clear strike.");
            Phase("lethal encounter and retry checkpoint");
            combat.settings.attackDamage=25;
            yield return Ask(king,"QA_WARN");yield return Ask(king,"QA_LETHAL");Check(journey.Decisions.Lethal,"A lethal order was treated as a nonlethal arrest.");
            yield return Until(()=>combat.IsDefeated,35);report.damageEvents=combat.DamageEvents;
            Check(combat.Health==0&&stage.player.InputBlocked&&hud.IsWorldPaused,"Defeat did not block the player and stop the world.");
            hud.HandleEscape();Check(hud.Root.Q<VisualElement>("pause-overlay").resolvedStyle.display==DisplayStyle.None,"Escape stacked another menu over the defeat screen.");
            yield return Delay(1.4f);Check(hud.Root.Q<VisualElement>("defeat-overlay").resolvedStyle.display==DisplayStyle.Flex,"No defeat/retry UI was shown.");
            Capture("04_Defeat_Retry_Menu");yield return Frames(4);
            int sceneHandle=SceneManager.GetActiveScene().handle;combat.RetryAudience();
            yield return Until(()=>SceneManager.GetActiveScene().handle!=sceneHandle,35);yield return Frames(16);Bind();
            Check(combat.Health==100&&!combat.IsDefeated&&!journey.Decisions.WarningActive,"Retry retained death or hostility.");
            Check(NpcConversationMemory.History("major:king").Count==0,"Retry retained the failed audience's threats.");
            Check(NpcConversationMemory.History("major:wenwang").Exists(m=>m.content.Contains("阿遥")),"Retry forgot the earlier desert relationship.");
            Phase("only a valid substantive decision completes the audience");
            PlaceForAudience();Enter(JourneyStep.CourtTalk);yield return Frames(4);king=stage.GetComponent<TravellerCastDirector>().king.GetComponent<NpcIdentity>();
            yield return Until(()=>stage.Audience.IsComplete,65);
            TravellerUserSettings.Current.connection.mode="scripted";journey.SetDialogueOpen(true);yield return Frames(4);
            Check(journey.CanContinueScriptedAudience&&hud.Root.Q<Button>("continue-scripted-audience").resolvedStyle.display==DisplayStyle.Flex,"Explicit offline play has no manual continuation.");
            yield return Delay(1.2f);Check(journey.Progress.Step==JourneyStep.CourtTalk,"Offline completion ran without an explicit click.");
            TravellerUserSettings.Current.connection.mode="custom";yield return Frames(3);
            yield return Ask(king,"QA_ADVANCE");journey.CloseDialogue();
            yield return Until(()=>journey.Progress.Step!=JourneyStep.CourtTalk,20);
            Check(journey.Progress.Step==JourneyStep.CalendarWait||journey.Progress.Step==JourneyStep.WalkToWater,"A validated advance did not start calendar verification.");
            Phase("tribunal explanation is retained until a real advance");Enter(JourneyStep.TribunalTalk);
            yield return Until(()=>SceneManager.GetActiveScene().name==TravellerStage.SceneNames[2],35);yield return Frames(16);Bind();
            Check(combat.CanDispatchGuards,"The medieval guards have no usable navigation.");
            var pope=stage.GetComponent<TravellerCastDirector>().pope.GetComponent<NpcIdentity>();
            yield return Ask(pope,"QA_WARN");yield return Ask(pope,"QA_PURSUIT");
            Check(journey.Decisions.Pursuing,"The pope cannot issue a valid bounded guard order.");
            yield return Frames(20);yield return Ask(pope,"QA_APOLOGY");
            Check(!journey.Decisions.Pursuing,"The pope could not recall church guards.");
            var scholar=stage.GetComponent<TravellerCastDirector>().scholar.GetComponent<NpcIdentity>();
            yield return Ask(scholar,"QA_EXPLANATION");Check(journey.Progress.Step==JourneyStep.TribunalTalk&&!journey.Progress.ConsideredExplanation,"A continuing proposal prematurely finished the tribunal.");
            yield return Ask(scholar,"QA_TRIBUNAL_ADVANCE");journey.CloseDialogue();yield return Until(()=>journey.Progress.Step==JourneyStep.WalkOutside,20);
            Check(journey.Progress.OfferedExplanation,"The real explanation was replaced with Explain(false).");
            yield return Frames(4);Capture("05_Tribunal_Decision");yield return Frames(4);Finish();
        }
        private void Enter(JourneyStep step)
        { typeof(JourneyProgress).GetMethod("Enter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(journey.Progress,new object[]{step}); }
        private IEnumerator RenderProbe(string probe)
        {
            journey.Begin();Enter(JourneyStep.CourtTalk);
            yield return Until(()=>SceneManager.GetActiveScene().name==TravellerStage.SceneNames[1],35);yield return Frames(16);Bind();
            var king=stage.GetComponent<TravellerCastDirector>().king;
            stage.player.Teleport(king.transform.position+king.transform.forward*2.7f+Vector3.up*stage.player.eyeHeight,king.transform.position+Vector3.up*1.15f);
            if(probe=="shadows"||probe=="combined")QualitySettings.shadowDistance=55;
            if(probe=="lod"||probe=="combined")QualitySettings.lodBias=1.1f;
            if(probe=="aa"||probe=="combined")QualitySettings.antiAliasing=2;
            if(probe=="cap")Application.targetFrameRate=90;
            if(probe=="culling")foreach(var actor in stage.GetComponentsInChildren<TravellerActor>())if(actor.firstPersonPlayer==null)actor.animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            if(probe=="actors")foreach(var actor in stage.GetComponentsInChildren<TravellerActor>()){actor.enabled=false;actor.animator.enabled=false;}
            Phase("court rendering / "+probe);yield return Frames(120);yield return Measure(false);
            Capture("Court_"+probe);yield return Frames(4);Finish();
        }
        private void PlaceForAudience()
        {
            // Skip the already tested city walk, not its arrival placement.
            // Starting CourtTalk with the companion still 250 m outside the
            // capital makes the real presentation quite correctly take minutes.
            var companion=stage.GetComponent<TravellerCastDirector>().companion;
            companion.transform.position=stage.profile.courtGoal+new Vector3(-2,0,-2);
            stage.player.Teleport(stage.profile.courtGoal+Vector3.back*2+Vector3.up*stage.player.eyeHeight,stage.profile.courtGoal+Vector3.forward*20);
        }
        private IEnumerator Ask(NpcIdentity actor,string text)
        {
            stage.player.Teleport(actor.transform.position+actor.transform.forward*2.7f+Vector3.up*stage.player.eyeHeight,actor.ConversationPoint);yield return Frames(3);
            Check(dialogue.BeginPlayerQuestion()&&dialogue.QuestionTarget==actor,"Wrong question recipient: "+text);
            dialogue.Submit(text);yield return Until(()=>!dialogue.IsBusy,12);yield return Frames(2);
        }
        private void Bind()
        {stage=FindFirstObjectByType<TravellerStage>();journey=stage.Journey;hud=stage.GetComponentInChildren<TravellerHud>();dialogue=stage.GetComponent<NpcDialogueDirector>();combat=stage.GetComponent<NpcDecisionController>();stage.player.enabled=false;}
        private IEnumerator Measure(bool pursuit)
        {
            var values=new List<float>();var gpu=new List<float>();var timing=new FrameTiming[1];ulong last=0;float cpu=0,render=0;int count=0;
            for(int i=0;i<180;i++)
            {
                FrameTimingManager.CaptureFrameTimings();yield return null;values.Add(Time.unscaledDeltaTime*1000);
                if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].frameStartTimestamp!=last)
                {last=timing[0].frameStartTimestamp;cpu+=(float)timing[0].cpuMainThreadFrameTime;render+=(float)timing[0].cpuRenderThreadFrameTime;count++;if(timing[0].gpuFrameTime>0)gpu.Add((float)timing[0].gpuFrameTime);}
            }
            values.Sort();float p95=values[Mathf.CeilToInt(values.Count*.95f)-1];if(pursuit)report.courtPursuitP95=p95;else report.courtIdleP95=p95;
            if(Option("-decision-render-probe")==null)Check(p95<=16.67f,"Court frame budget exceeded 16.67 ms: "+p95);
            gpu.Sort();report.gpuP95=gpu.Count>0?gpu[Mathf.CeilToInt(gpu.Count*.95f)-1]:-1;report.cpuMainMean=count>0?cpu/count:-1;report.renderMean=count>0?render/count:-1;report.frameCap=Application.targetFrameRate;
        }
        private IEnumerator Until(Func<bool> condition,float seconds)
        {float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),"Timed out in "+report.phase);}
        private static IEnumerator Delay(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        private void Phase(string value){report.phase=value;File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"phase.txt"),value);}
        private void Check(bool value,string message){report.checks++;if(!value)report.errors.Add(message);}
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private void Finish(){if(finished)return;finished=true;report.passed=report.errors.Count==0;File.WriteAllText(output,JsonUtility.ToJson(report,true));stage?.player?.ReleasePointer();Application.Quit(report.passed?0:1);}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){report.errors.Add(message);Finish();}}
        private static string Option(string name){var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);return index>=0&&index+1<args.Length?args[index+1]:null;}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;stage?.player?.ReleasePointer();}
    }
}
#endif
