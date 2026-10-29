#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    public sealed class TravellerCrowdPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report { public bool passed; public int checks; public List<string> errors = new List<string>(); public List<string> verified = new List<string>(); }
        private readonly Report report = new Report(); private string output; private TravellerStage stage; private TravellerHud hud;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-traveller-crowd-check");
            if (i < 0 || i + 1 >= args.Length || UnityEngine.Object.FindFirstObjectByType<TravellerCrowdPlayerCheck>() != null) return;
            var root = new GameObject("Development Full Crowd Check"); DontDestroyOnLoad(root); root.AddComponent<TravellerCrowdPlayerCheck>().output = args[i + 1];
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)); Application.logMessageReceived += OnLog;
            Application.runInBackground = true; Application.targetFrameRate = 120; QualitySettings.vSyncCount = 0;
            for (int scene = 0; scene < 3; scene++)
            {
                if (SceneManager.GetActiveScene().name != TravellerStage.SceneNames[scene]) { var load = SceneManager.LoadSceneAsync(TravellerStage.SceneNames[scene]); while (!load.isDone) yield return null; }
                yield return Frames(8); stage = UnityEngine.Object.FindFirstObjectByType<TravellerStage>(); hud = stage.GetComponentInChildren<TravellerHud>(); stage.player.enabled = false;
                if (!stage.Journey.IsPreviewing) stage.Journey.TogglePreview(); hud.Root.style.display = DisplayStyle.None;
                stage.ApplyMoment(0, true, false); yield return Frames(8);
                var actors = stage.GetComponentsInChildren<TravellerActor>(); var crowd = stage.GetComponentsInChildren<TravellerCrowdMember>();
                Check(actors.Length == new[] { 2, 28, 6 }[scene] && crowd.Length == new[] { 0, 25, 2 }[scene], "Complete real-character roster in " + TravellerStage.SceneNames[scene]);
                Check(!stage.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("[scene stand-in]")), "No primitive character anchors in " + TravellerStage.SceneNames[scene]);
                foreach (var member in crowd)
                {
                    var actor = member.GetComponent<TravellerActor>();
                    Check(actor.animator.avatar.isHuman && actor.animator.avatar.isValid && actor.GetComponentsInChildren<SkinnedMeshRenderer>().All(s => s.sharedMesh.vertexCount > 2000 && s.sharedMaterials.All(m => m.mainTexture != null && m.shader.isSupported)), "Valid textured Humanoid: " + member.name + " #" + member.variation);
                }
                if (scene == 0) continue;
                var guard = crowd.First(m => m.isGuard).GetComponent<TravellerActor>(); Quaternion heading = guard.transform.rotation;
                stage.player.Teleport(guard.transform.position + new Vector3(2,1.65f,-3.8f), guard.transform.position + Vector3.up*1.2f);
                yield return Frames(65);
                Check(guard.CurrentMotion == "MX_WeightShift" && Quaternion.Angle(heading,guard.transform.rotation)<.1f, "Guard idle keeps the authored watch direction in scene " + scene);
                Check(guard.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh.name.Contains("spear")), "Guard carries attached spear in scene " + scene);
                Capture(scene == 1 ? "Chaoge_ShangeGuard" : "Medieval_ChurchGuard"); yield return Frames(4);
                if (scene == 2) continue;
                var woman = crowd.First(m=>m.GetComponent<TravellerActor>().role==TravellerRole.CitizenFemale&&!m.isReviving).GetComponent<TravellerActor>();
                stage.player.Teleport(woman.transform.position+new Vector3(1.8f,1.65f,-3.4f),woman.transform.position+Vector3.up*1.15f);
                yield return Frames(65);Capture("Chaoge_Citizen_Street");yield return Frames(4);
                var walker=crowd.First(m=>!m.isGuard&&!m.isReviving&&m.strollPoints!=null&&m.strollPoints.Length>0);var walkingActor=walker.GetComponent<TravellerActor>();
                float until=Time.realtimeSinceStartup+25;while(!walkingActor.IsMoving&&Time.realtimeSinceStartup<until)yield return null;
                Vector3 before=walker.transform.position; bool moving=walkingActor.IsMoving&&walkingActor.CurrentMotion=="MX_Walking";
                yield return Frames(40);Check(moving&&Vector3.Distance(before,walker.transform.position)>.15f,"Resident stroll moves the real body and selects a matching walking clip.");
                stage.Journey.ReturnToTitle();yield return Frames(5);var positions=crowd.Select(m=>m.transform.position).ToArray();float time=walkingActor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return Frames(20);Check(crowd.Select((m,i)=>Vector3.Distance(m.transform.position,positions[i])<.001f).All(x=>x)&&Mathf.Abs(time-walkingActor.animator.GetCurrentAnimatorStateInfo(0).normalizedTime)<.001f,"Title pauses all supporting-character movement and animation.");
                stage.Journey.TogglePreview();hud.Root.style.display=DisplayStyle.None;
                int cold=Array.FindIndex(stage.profile.moments,m=>m.frost>.5f);Check(cold>=0,"Capital exposes its authored cold moment.");
                if(cold>=0){stage.ApplyMoment(cold,true,false);yield return Frames(45);Check(crowd.Where(m=>!m.isGuard).All(m=>m.GetComponent<TravellerActor>().CurrentMotion=="MX_Shiver"&&!m.GetComponent<TravellerActor>().IsTravelling),"All residents and revivers respond to cold and stop strolling.");
                    var reviver=crowd.First(m=>m.isReviving);stage.player.Teleport(reviver.transform.position+new Vector3(-2,1.65f,-3.5f),reviver.transform.position+Vector3.up*1.1f);yield return Frames(45);Capture("Chaoge_Reviving_Citizens_Cold");yield return Frames(4);}
            }
            report.passed=report.errors.Count==0;File.WriteAllText(output,JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
        }
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));
        private static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
        private void Check(bool condition,string message){report.checks++;if(condition)report.verified.Add(message);else report.errors.Add(message);}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){report.errors.Add(message);File.WriteAllText(output,JsonUtility.ToJson(report,true));}}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
#endif
