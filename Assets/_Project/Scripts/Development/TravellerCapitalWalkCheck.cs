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
    // Move the real CharacterController across the authored city routes, without teleporting between waypoints.
    public sealed class TravellerCapitalWalkCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report { public bool passed; public int checks; public float metresWalked; public List<string> errors=new List<string>(); }
        private string output;
        private TravellerStage stage;
        private CharacterController controller;
        private readonly Report report=new Report();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-traveller-capital-check");
            if(i<0||i+1>=args.Length||UnityEngine.Object.FindFirstObjectByType<TravellerCapitalWalkCheck>()!=null)return;
            var go=new GameObject("Development Capital Walk Check");DontDestroyOnLoad(go);go.AddComponent<TravellerCapitalWalkCheck>().output=args[i+1];
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));Application.runInBackground=true;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;
            Application.logMessageReceived+=OnLog;
            var load=SceneManager.LoadSceneAsync(TravellerStage.SceneNames[1]);while(!load.isDone)yield return null;
            for(int i=0;i<8;i++)yield return null;
            stage=UnityEngine.Object.FindFirstObjectByType<TravellerStage>();controller=stage.player.GetComponent<CharacterController>();
            stage.player.enabled=false;UnityEngine.Object.FindFirstObjectByType<TravellerHud>().Root.style.display=DisplayStyle.None;
            Check(SceneManager.sceneCount==1,"The capital retained another gameplay scene.");
            Check(stage.profile.hasCapitalRoute,"Capital route is not configured.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"walk_arrival.png"));
            foreach(var point in stage.profile.courtWaypoints)yield return Walk(point);
            yield return Walk(stage.profile.courtGoal);
            stage.player.FaceTarget(new Vector3(0,9,34));
            Check(Mathf.Abs(stage.player.transform.position.y-4f)<0.4f,"The player failed to ascend the palace terrace.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"walk_court.png"));
            foreach(var point in stage.profile.waterWaypoints)yield return Walk(point);
            yield return Walk(stage.profile.waterGoal);
            stage.player.FaceTarget(stage.profile.moments[1].viewTarget);
            Check(Mathf.Abs(stage.player.transform.position.y)<0.4f,"The player failed to descend to the revival pool.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"walk_water.png"));
            for(int i=0;i<5;i++)yield return null;
            report.passed=report.errors.Count==0;File.WriteAllText(output,JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
        }

        private IEnumerator Walk(Vector3 target)
        {
            int frames=0,stalled=0;
            while(Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(target.x,target.z))>0.3f&&frames++<2500)
            {
                var before=stage.player.transform.position;
                var delta=Vector3.ProjectOnPlane(target-before,Vector3.up);
                stage.player.FaceTarget(target+Vector3.up*1.6f);
                controller.Move(Vector3.ClampMagnitude(delta,0.38f)+Vector3.down*0.10f);
                float moved=Vector3.Distance(before,stage.player.transform.position);report.metresWalked+=moved;
                stalled=moved<0.015f?stalled+1:0;
                if(stalled>80){Check(false,"Controller blocked en route to "+target+" at "+stage.player.transform.position);yield break;}
                yield return null;
            }
            Check(frames<2500,"Route waypoint timed out: "+target);
            Check(Mathf.Abs(stage.player.transform.position.y-target.y)<0.6f,"Wrong floor at "+target+": "+stage.player.transform.position);
        }

        private void Check(bool condition,string message){report.checks++;if(!condition)report.errors.Add(message);}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
#endif
