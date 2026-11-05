#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    public sealed class DialogueContextPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int checks;
            public List<string> errors=new List<string>();
        }
        private readonly Report report=new Report();
        private TravellerStage stage;
        private TravellerJourney journey;
        private TravellerHud hud;
        private NpcDialogueDirector director;
        private bool hadPreferences,hadJournal,backedUp;
        private string preferences,journal,output;
        private const string Question="你还记得我叫什么、从哪里来吗？";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string output=Option("-dialogue-context-check");if(output==null||FindFirstObjectByType<DialogueContextPlayerCheck>()!=null)return;
            var go=new GameObject("Development NPC context verification");DontDestroyOnLoad(go);go.AddComponent<DialogueContextPlayerCheck>().output=output;
        }
        private IEnumerator Start()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            Application.logMessageReceived+=OnLog;Directory.CreateDirectory(Path.GetDirectoryName(output));
            hadPreferences=PlayerPrefs.HasKey(TravellerUserSettings.StorageKey);preferences=PlayerPrefs.GetString(TravellerUserSettings.StorageKey,"");
            hadJournal=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");journal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");backedUp=true;
            yield return Frames(15);Bind();
            hud.OpenSettings();var ui=hud.SettingsPanel.Root;ui.Q<DropdownField>("settings-provider").index=2;
            ui.Q<TextField>("settings-api-url").value=Option("-context-test-upstream");ui.Q<TextField>("settings-gateway-url").value=Option("-context-test-gateway");
            ui.Q<TextField>("settings-model").value="qa-context-model";ui.Q<TextField>("settings-api-key").value="test-context-key";
            ui.Q<DropdownField>("settings-context-window").value="1M";ui.Q<Toggle>("settings-speak-replies").value=false;
            yield return Frames(4);Capture("01_OneMillion_Context_Setting");yield return Frames(3);
            hud.SettingsPanel.Apply();
            Check(TravellerUserSettings.Current.connection.contextWindowTokens==1000000,"1M context preference not applied.");
            journey.Begin();var p=journey.Progress;p.WakeUp();p.DiscoverTraveller();p.MeetTraveller();yield return Frames(4);
            stage.player.enabled=false;var companion=stage.GetComponent<TravellerCastDirector>().companion;
            string wenKey=NpcConversationMemory.Key(companion.GetComponent<NpcIdentity>());
            for(int i=0;i<24;i++)NpcConversationMemory.RememberTurn(wenKey,i==0?"我叫阿遥，是个迷路的木匠。":"沿途的问题"+i,i==0?"阿遥，先跟紧我。":"我听见了。"+i);
            Check(NpcConversationMemory.History(wenKey).Count==48,"The client still trims history to twelve messages.");
            journey.Speak("旅人：靠近火光，我们再说。",1);
            Check(NpcConversationMemory.KnownEvents(wenKey).Count>0,"The traveller forgot his authored lines.");
            stage.Journal.hypothesis="PRIVATE_NOTEBOOK_CONTEXT_QA";
            stage.Journal.entries.Add(new JournalEntry{category="亲眼观察",text="PRIVATE_OLD_RUN_CONTEXT_QA"});
            yield return Ask(companion.GetComponent<NpcIdentity>(),Question);
            Check(!director.UsingFallback&&director.LastReply.Contains("阿遥"),"The model request did not use the traveller's conversation history.");
            Check(NpcConversationMemory.History("major:king").Count==0,"The king inherited the traveller's private history.");
            Capture("02_Gobi_Memory_Reply");yield return Frames(3);
            journey.CloseDialogue();
            p.Tick(p.MinimumOpeningTalkSeconds);p.ChooseStoryIntent("sunrise");p.MarkGiantSunVisible();
            stage.ApplyMoment(1,true,false);
            companion.transform.position = new Vector3(4,0,-12);
            var observer = companion.GetComponent<NpcIdentity>();
            stage.Journal.entries.RemoveAll(entry => entry.observationId == "gobi_giant_sun");
            yield return Ask(observer,"我刚才看到三个太阳。");
            Check(!stage.Journal.entries.Exists(entry => entry.observationId == "gobi_giant_sun"),"An unconfirmed claim entered the journal.");
            yield return Ask(observer,"我刚才看到一轮特别大的太阳。");
            var observation = stage.Journal.entries.Find(entry => entry.observationId == "gobi_giant_sun");
            Check(observation != null && observation.category == "交谈确认" && observation.confirmedBy == "旅人",
                "The witnessed fact was not saved with the responding NPC as source.");
            Check(director.LastReply == "你说得对，我也看到了。","The tool call did not produce an acknowledgment.");
            journey.CloseDialogue();
            Vector3 eye = new Vector3(4,1.75f,-12);
            stage.player.Teleport(eye,eye-Vector3.forward*20);
            yield return new WaitForSecondsRealtime(.9f);
            Check(observation != null && observation.category == "交谈确认","Looking away counted as a direct observation.");
            stage.player.FaceTarget(stage.ObservationTarget());
            yield return Until(() => observation != null && observation.category == "亲眼观察",4);
            Check(stage.Journal.entries.FindAll(entry => entry.observationId == "gobi_giant_sun").Count == 1,
                "Direct observation duplicated the confirmed journal entry.");
            Capture("02b_Giant_Sun_Observed");
            p.Arrive();p.IntroduceTraveller();
            p.Tick(p.MinimumShelterTalkSeconds);p.ChooseStoryIntent("sunset");p.Tick(p.SuddenSunsetSeconds);
            p.AddNightWalking(p.MinimumNightWalkMetres);p.Tick(p.MinimumNightWalkSeconds);p.Arrive();
            yield return Until(()=>SceneManager.GetActiveScene().name==TravellerStage.SceneNames[1],30);
            yield return Frames(15);Bind();stage.player.enabled=false;
            companion=stage.GetComponent<TravellerCastDirector>().companion;
            Check(NpcConversationMemory.Key(companion.GetComponent<NpcIdentity>())==wenKey,"The same major character changed memory keys across scenes.");
            Check(NpcConversationMemory.History(wenKey).Count>=50,"The capital traveller lost the Gobi conversation.");
            stage.Journal.hypothesis="PRIVATE_NOTEBOOK_CONTEXT_QA";
            stage.Journal.entries.Add(new JournalEntry{category="亲眼观察",text="PRIVATE_OLD_RUN_CONTEXT_QA"});
            yield return Ask(companion.GetComponent<NpcIdentity>(),Question);
            Check(!director.UsingFallback&&director.LastReply.Contains("阿遥"),"The capital request forgot the player's stated name.");
            Capture("03_Capital_Companion_Memory");yield return Frames(3);
            var king=stage.GetComponent<TravellerCastDirector>().king.GetComponent<NpcIdentity>();
            int remembered=NpcConversationMemory.KnownEvents(wenKey).Count;
            journey.Speak("纣王：当庭先看表章，再议历法。",1);
            Check(NpcConversationMemory.KnownEvents("major:king").Count>0,"The king forgot his own stage line.");
            Check(NpcConversationMemory.KnownEvents(wenKey).Count==remembered,"A distant companion heard a line through the palace walls.");
            yield return Ask(king,"寡人面前的人刚才在戈壁说过什么？");
            Check(!director.UsingFallback&&!director.LastReply.Contains("阿遥"),"The king knew the traveller's private introduction.");
            Check(NpcConversationMemory.History("major:king").Count==2,"The king does not have isolated conversation memory.");
            Capture("04_King_Knowledge_Boundary");yield return Frames(3);
            journey.CloseDialogue();journey.Begin();yield return Frames(5);
            Check(NpcConversationMemory.History(wenKey).Count==0&&NpcConversationMemory.History("major:king").Count==0,"A new journey reused private dialogue from a previous run.");
            Restore();report.passed=report.errors.Count==0;File.WriteAllText(output,JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
        }
        private IEnumerator Ask(NpcIdentity target,string question)
        {
            stage.player.Teleport(target.transform.position+target.transform.forward*2.7f+Vector3.up*1.65f,target.ConversationPoint);
            yield return Frames(3);Check(director.BeginPlayerQuestion()&&director.QuestionTarget==target,"The intended context test recipient was not selected.");
            director.Submit(question);yield return Until(()=>!director.IsBusy,20);
            Check(!string.IsNullOrEmpty(director.LastReply),"No contextual reply arrived.");
        }
        private void Bind(){stage=FindFirstObjectByType<TravellerStage>();journey=stage.Journey;hud=stage.GetComponentInChildren<TravellerHud>();director=stage.GetComponent<NpcDialogueDirector>();}
        private void Check(bool value,string message){report.checks++;if(!value)report.errors.Add(message);}
        private IEnumerator Until(Func<bool> condition,float seconds)
        {float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),"Context test timed out.");}
        private static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private static string Option(string name){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        private void Restore()
        {
            if(!backedUp)return;backedUp=false;
            if(hadPreferences)PlayerPrefs.SetString(TravellerUserSettings.StorageKey,preferences);else PlayerPrefs.DeleteKey(TravellerUserSettings.StorageKey);
            if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",journal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");
            PlayerPrefs.Save();stage?.player?.ReleasePointer();
        }
        private void OnDestroy(){Restore();Application.logMessageReceived-=OnLog;}
    }
}
#endif
