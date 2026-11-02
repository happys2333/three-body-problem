#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem
{
    // Rendering changes are exercised with the real controller and sunlight/plant lifecycle.
    // Authored progress choices isolate this check from speech and external LLM services.
    public sealed class GobiRenderRegressionCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report {public bool passed;public int checks,ignited,burned,shaded;public float metresWalked;public string phase;public List<string> errors=new List<string>();}
        private readonly Report report=new Report();private string output,journal;private bool hadJournal,restore;
        private TravellerStage stage;private TravellerJourney journey;private GobiSequenceController sequence;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-gobi-render-regression");if(i<0||i+1>=args.Length)return;
            var go=new GameObject("Development Gobi Render Regression");DontDestroyOnLoad(go);go.AddComponent<GobiRenderRegressionCheck>().output=args[i+1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.logMessageReceived+=OnLog;
            hadJournal=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");journal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");restore=true;
            yield return Frames(12);stage=FindFirstObjectByType<TravellerStage>();journey=stage.Journey;sequence=stage.GetComponent<GobiSequenceController>();
            Check(journey.IsTitle&&stage.Current.sunCount==0,"Opening is not a night-time title.");
            Phase("night and torch");journey.Begin();stage.player.enabled=false;yield return Delay(stage.profile.gobiStory.wakeSeconds+.25f);
            var torch=stage.player.bodyActor.GetComponent<TorchBearer>();Check(torch.IsLit,"Player torch is dark at night.");
            Check(Vector3.Dot(torch.Grip.GetComponentInChildren<TorchFlameBillboard>().transform.up,Vector3.up)>.999f,"Torch flame lost its world-up orientation.");Capture("01_Night");
            journey.Progress.DiscoverTraveller();journey.Progress.MeetTraveller();journey.Progress.RecordNpcAnswer();journey.Progress.ChooseStoryIntent("sunrise");
            yield return Delay(stage.profile.gobiStory.sunriseSeconds+.3f);
            Phase("real sun damage");stage.player.Teleport(new Vector3(9,1.75f,-18),new Vector3(25,9,120));yield return Delay(1.2f);
            var exposure=stage.GetComponent<SunExposureSystem>();Check(exposure.Vitality.Health<100&&exposure.Vitality.Health%10==0,"Sunlight no longer causes hidden ten-point hits.");
            Check(!torch.IsCarried,"Night torch is still automatically carried in daylight.");Capture("02_SunDamage");
            stage.player.Teleport(new Vector3(-7,1.75f,-10),new Vector3(-15,1.7f,5));yield return Walk(new Vector3(-15,0,5));yield return Walk(new Vector3(-13,0,12));
            float deadline=Time.realtimeSinceStartup+20;while(journey.Progress.Step!=JourneyStep.ShelterTalk&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(journey.Progress.Step==JourneyStep.ShelterTalk&&sequence.BothInShade,"Both travellers failed to reach real cover.");
            Check(exposure.PlayerIsSheltered&&exposure.Vitality.Health==100,"Cover failed to restore health.");
            Check(sequence.VegetationIgnited,"Shelter entry did not ignite exposed shrubs.");
            Phase("shelter, fire and ash");Capture("03_ShelterAndShadows");
            // Stop autonomous narrative requests while the real vegetation finishes burning.
            sequence.enabled=false;var vegetation=stage.GetComponent<GobiVegetationFire>();
            yield return Delay(4);Capture("04_BurningPlants");
            deadline=Time.realtimeSinceStartup+stage.profile.gobiStory.burnSeconds+3;
            while(vegetation.BurnedCount<vegetation.IgnitedCount&&Time.realtimeSinceStartup<deadline)yield return null;
            report.ignited=vegetation.IgnitedCount;report.burned=vegetation.BurnedCount;report.shaded=vegetation.ShadedCount;
            Check(report.ignited>0&&report.ignited==report.burned&&report.shaded>0,"Burning plants did not become ash while shaded plants survived.");
            foreach(var fuel in stage.GetComponentsInChildren<VegetationFuel>())
                foreach(var renderer in fuel.GetComponentsInChildren<MeshRenderer>())
                    Check(renderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly,"Burnable plant has an orphan shadow proxy.");
            Capture("05_Ash");
            journey.Progress.IntroduceTraveller();journey.Progress.Tick(stage.profile.gobiStory.minimumShelterTalkSeconds);
            sequence.enabled=true;journey.Progress.ChooseStoryIntent("sunset");yield return Delay(stage.profile.gobiStory.suddenSunsetSeconds+.3f);
            Check(journey.Progress.Step==JourneyStep.WalkToChaoge&&stage.Current.sunCount==0,"Sunset did not start the night walk.");
            Check(torch.IsLit,"The player's torch failed to return at night.");Capture("06_NightDeparture");Phase("walk to Chaoge");
            foreach(var point in new[]{new Vector3(-15,0,18),new Vector3(-14,0,26),new Vector3(-12,0,31),new Vector3(2,0,42),new Vector3(2,0,64)})yield return Walk(point);
            deadline=Time.realtimeSinceStartup+25;while(SceneManager.GetActiveScene().name!=TravellerStage.SceneNames[1]&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(SceneManager.GetActiveScene().name==TravellerStage.SceneNames[1]&&SceneManager.sceneCount==1,"The first act did not transition to separate Chaoge.");
            yield return Frames(8);Capture("07_ChaogeArrival");yield return Frames(3);
            report.passed=report.errors.Count==0;Phase("complete");Restore();Application.Quit(report.passed?0:1);
        }
        private IEnumerator Walk(Vector3 target)
        {
            float deadline=Time.realtimeSinceStartup+40,stalled=0;
            while(!stage.IsLoading&&Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(target.x,target.z))>.3f&&Time.realtimeSinceStartup<deadline)
            {
                Vector3 before=stage.player.transform.position;float distance=Vector3.ProjectOnPlane(target-before,Vector3.up).magnitude;
                stage.player.FaceTarget(target+Vector3.up*1.6f);stage.player.MoveInput(Vector2.up*Mathf.Min(1,distance/(5.4f*Mathf.Max(.001f,Time.deltaTime))),true,Time.deltaTime);
                float moved=Vector3.Distance(before,stage.player.transform.position);report.metresWalked+=moved;stalled=moved<.002f?stalled+Time.deltaTime:0;
                if(stalled>2){Check(false,"Controller is blocked at "+stage.player.transform.position);yield break;}yield return null;
            }
            Check(Time.realtimeSinceStartup<deadline,"Walking timed out at "+target);
        }
        private static IEnumerator Delay(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private void Phase(string value){report.phase=value;Save();}
        private void Check(bool value,string error){report.checks++;if(!value)report.errors.Add(error);Save();}
        private void Save(){File.WriteAllText(output,JsonUtility.ToJson(report,true));}
        private void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){report.errors.Add(message);Save();}}
        private void Restore(){if(!restore)return;if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",journal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();restore=false;}
        private void OnDestroy(){Restore();Application.logMessageReceived-=OnLog;}
    }
}
#endif
