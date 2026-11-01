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
    public sealed class PalaceInteriorPlayerCheck : MonoBehaviour
    {
        [Serializable] private class Report
        {
            public bool passed, performanceBudgetPassed;
            public int checks, width, height;
            public float meanMs, p95Ms;
            public string gpu;
            public List<string> errors = new List<string>(), resolutions = new List<string>();
        }
        private string output, journal;
        private bool hadJournal, saved;
        private int fullSubtitleLength;
        private CourtRetainer[] retainers;
        private Vector3[] retainerPosts;
        private TravellerStage stage;
        private TravellerHud hud;
        private TravellerJourney journey;
        private PalaceDivination ritual;
        private readonly Report report = new Report();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-palace-interior-check");
            if(i<0||i+1>=args.Length||FindFirstObjectByType<PalaceInteriorPlayerCheck>()!=null)return;
            var root=new GameObject("Development interior and HUD verification");DontDestroyOnLoad(root);root.AddComponent<PalaceInteriorPlayerCheck>().output=args[i+1];
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            hadJournal=PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1");journal=PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1","");saved=true;
            yield return Frames(15);Bind();
            Check(journey.IsTitle,"Normal startup must still show the title.");
            journey.Begin();var p=journey.Progress;
            p.WakeUp();p.DiscoverTraveller();p.MeetTraveller();p.RecordNpcAnswer();p.ChooseStoryIntent("sunrise");p.MarkGiantSunVisible();p.Arrive();p.IntroduceTraveller();
            p.Tick(p.MinimumShelterTalkSeconds);p.ChooseStoryIntent("sunset");p.Tick(p.SuddenSunsetSeconds);p.AddNightWalking(p.MinimumNightWalkMetres);p.Tick(p.MinimumNightWalkSeconds);p.Arrive();
            float until=Time.realtimeSinceStartup+30;
            while(SceneManager.GetActiveScene().name!=TravellerStage.SceneNames[1]&&Time.realtimeSinceStartup<until)yield return null;
            yield return Frames(20);Bind();
            Check(stage.profile.sceneIndex==1,"Capital transition failed.");
            stage.player.enabled=false;
            var cast=stage.GetComponent<TravellerCastDirector>();
            cast.companion.transform.position=stage.profile.courtWaypoints.Last();cast.companion.ResetMotionTracking();
            stage.player.Teleport(stage.profile.courtGoal+Vector3.up*1.65f,new Vector3(0,7,34));yield return Frames(5);
            Check(journey.Progress.Step==JourneyStep.CourtTalk,"Approaching the audience did not start the court scene.");
            ritual=stage.GetComponentInChildren<PalaceDivination>();Check(ritual!=null,"Ritual controller missing.");
            var timer=stage.GetComponentInChildren<FiveTierSandTimer>();Check(timer!=null&&timer.piles.Length==5,"Five-tier sand timer missing.");
            var sound=stage.GetComponentInChildren<PalaceSoundscape>();Check(sound!=null&&sound.fires.Length==5,"Interior soundscape missing.");
            Check(stage.GetComponentsInChildren<Transform>().Count(t=>t.name=="RoyalRelief")==2,"Expected two detailed backdrop panels.");
            Check(!ritual.glow.enabled,"The ritual glows before divination begins.");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-check-retinue")>=0)
            {
                retainers=stage.GetComponentsInChildren<CourtRetainer>();retainerPosts=retainers.Select(r=>r.stationId=="CalendarAttendant"&&stage.Audience!=null?stage.Audience.HomePosition:r.transform.position).ToArray();
                Check(retainers.Length==12&&retainers.Count(r=>!r.spearGuard)==4,"Expected eight palace/tunnel sentries and four attendants.");
                Check(retainers.Where(r=>!r.spearGuard&&r.GetComponent<RoyalFanBearer>()==null&&!r.DutyControlled).All(r=>r.GetComponent<TravellerActor>().CurrentGesture=="MX_Bow"),"The stationary court official did not bow at the royal audience.");
            }
            Capture("01_Court_BeforeDivination");yield return Frames(3);
            journey.Progress.Continue();yield return Delay(.2f);
            var sky=stage.GetComponentInChildren<PalaceSkylight>();float opening=sky.Opening,angle=timer.TurnAngle;
            Vector3 pausedActor=cast.companion.transform.position;hud.SendMessage("TogglePause");yield return Delay(1);
            Check(Vector3.Distance(pausedActor,cast.companion.transform.position)<.01f,"Paused companion keeps walking.");
            Check(Mathf.Abs(sky.Opening-opening)<.001f&&Mathf.Abs(timer.TurnAngle-angle)<.001f,"Pause does not freeze the cover and timer.");
            Check(!sound.sand.isPlaying&&sound.fires.All(s=>!s.isPlaying),"Interior Foley keeps playing while paused.");
            hud.SendMessage("TogglePause");until=Time.realtimeSinceStartup+(stage.Audience!=null?120:16);
            Vector3 start=cast.companion.transform.position;
            bool received=false,presented=false,ascent=false;float waitingAt=journey.Progress.Elapsed;
            while(!ritual.IsCasting&&Time.realtimeSinceStartup<until)
            {
                var audience=stage.Audience;
                if(audience!=null&&!audience.IsComplete)
                {
                    if(audience.State==CourtAudienceSequence.AudienceState.Receiving)
                    {
                        stage.player.Teleport(new Vector3(0,5.65f,18),new Vector3(-2.8f,5.12f,19.8f));
                        if(!received&&audience.PhaseSeconds>.6f){Capture("Audience_01_ReceivingPetition");received=true;}
                    }
                    if(audience.State==CourtAudienceSequence.AudienceState.Ascending)
                    {
                        stage.player.Teleport(new Vector3(3.9f,5.65f,24),audience.officer.transform.position+Vector3.up*1.05f);
                        if(!ascent&&audience.officer.transform.position.y>4.8f){Capture("Audience_02_AscendingDais");ascent=true;}
                    }
                    if(audience.State==CourtAudienceSequence.AudienceState.Presenting)
                    {
                        stage.player.Teleport(new Vector3(3.7f,6.55f,29),new Vector3(0,7.0f,33));
                        if(!presented&&audience.PhaseSeconds>.7f){Capture("Audience_03_PresentedToKing");presented=true;}
                    }
                    if(audience.State==CourtAudienceSequence.AudienceState.Returning)
                        stage.player.Teleport(new Vector3(0,5.65f,20),new Vector3(0,7.4f,34));
                }
                yield return null;
            }
            if(stage.Audience!=null)
            {
                Check(received&&ascent&&presented,"The petition presentation skipped a visible phase.");
                Check(stage.Audience.IsComplete&&stage.Audience.PetitionHolder=="纣王","The petition was not received by the king.");
                Check(stage.Audience.WalkedMetres>20,"The courier did not physically walk the delivery and return route.");
                Check(journey.Progress.Elapsed-waitingAt<5,"Calendar verification elapsed before the presentation finished.");
                Check(!cast.companion.GetComponent<CourtPropGrip>().IsHolding&&cast.king.GetComponent<CourtPropGrip>().IsHolding,"Petition ownership did not transfer from Ji Chang to the king.");
            }
            Check(ritual.IsCasting,"Ji Chang never reaches the ritual centre.");
            Check(Vector3.Distance(start,cast.companion.transform.position)>5,"Companion did not walk to the ritual area.");
            Check(Vector3.Distance(cast.companion.transform.position,ritual.ritualCentre.position)<.3f,"Rune circle is not beneath the caster.");
            Check(cast.companion.CurrentMotion==ritual.settings.castingMotion,"Casting animation did not start.");
            Check(!cast.companion.GetComponent<TorchBearer>().IsCarried,"Caster is still holding a lit torch.");
            stage.player.Teleport(new Vector3(3.8f,5.65f,17.7f),ritual.ritualCentre.position+Vector3.up*.6f);
            yield return Delay(1.1f);Check(ritual.Reveal>.2f&&ritual.Reveal<.9f,"Runes skipped the gradual reveal.");Capture("02_Runes_Drawing");yield return Frames(3);
            yield return Delay(2);Check(ritual.Reveal>.99f&&ritual.Intensity>.8f,"Rune glow did not reach its active state.");Capture("03_Runes_FullyLit");yield return Frames(3);
            Check(timer.FlipCount>=1,"Calendar timer never flips.");
            Check(sound.sand.isPlaying&&sound.fires.Any(s=>s.isPlaying),"Indoor sand and fire audio never resume.");
            var hips=cast.king.animator.GetBoneTransform(HumanBodyBones.Hips);
            Check(Mathf.Abs(hips.position.y-(5.8f+.74f+.13f))<.18f,"King floats above the new seat.");
            report.gpu=SystemInfo.graphicsDeviceName;report.width=Screen.width;report.height=Screen.height;
            var frames=new List<float>(240);yield return Delay(2);
            for(int i=0;i<240;i++){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            frames.Sort();report.meanMs=frames.Average();report.p95Ms=frames[(int)(frames.Count*.95f)];report.performanceBudgetPassed=report.p95Ms<=16.67f;
            Check(ritual.IsCasting,"Performance sample did not cover active divination.");
            if(retainers!=null)yield return CheckRetinue();
            bool captureCourt=Array.IndexOf(Environment.GetCommandLineArgs(),"-capture-court")>=0;
            if(captureCourt||Array.IndexOf(Environment.GetCommandLineArgs(),"-capture-ritual")>=0)
            {
                var camera=stage.player.viewCamera.transform;var cameraPosition=camera.position;var cameraRotation=camera.rotation;
                if(captureCourt)stage.player.Teleport(new Vector3(3.7f,6.55f,29),new Vector3(0,7.15f,34));
                string directory=Path.Combine(Path.GetDirectoryName(output),captureCourt?"FanMotionFrames":"AnimationFrames");Directory.CreateDirectory(directory);
                for(int i=0;i<48;i++)
                {
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,"frame_"+i.ToString("D3")+".png"));yield return Delay(.075f);
                }
                if(captureCourt)stage.player.Teleport(cameraPosition,cameraPosition+cameraRotation*Vector3.forward*4);
            }
            string longSpeech="姬昌：我们要把每一次日照记下来。一次应验只是开端，还要等候天窗再次打开，看看天象会不会照推演继续变化。请记住自己亲眼看见的事，也保留尚未得到回答的疑问。";
            fullSubtitleLength=longSpeech.Length;
            journey.Speak(longSpeech,18);
            foreach(var size in new[]{new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1024,768)})
            {
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return Frames(12);report.resolutions.Add(Screen.width+"x"+Screen.height);
                CheckHud();Capture("HUD_"+Screen.width+"x"+Screen.height);yield return Frames(3);
                // Exercise the observation hint beside the longest subtitle without
                // changing the actual story's observation eligibility.
                var hint=hud.Root.Q("action-row").Children().First();var previous=hint.style.display;
                hint.style.display=DisplayStyle.Flex;yield return Frames(2);
                Check(!hint.worldBound.Overlaps(hud.Root.Q("caption-card").worldBound)&&!hint.worldBound.Overlaps(hud.Root.Q("mission").worldBound),"Observation hint covers mission or subtitle.");
                CheckInside(hint);hint.style.display=previous;
            }
            journey.SetDialogueOpen(true);yield return Frames(5);CheckInside(hud.Root.Q("dialogue"));Capture("04_VoiceAndTextPanel");yield return Frames(3);journey.CloseDialogue();
            Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return Frames(10);
            until=Time.realtimeSinceStartup+35;
            while(journey.Progress.Step==JourneyStep.CalendarWait&&Time.realtimeSinceStartup<until)yield return null;
            Check(journey.Progress.Step==JourneyStep.WalkToWater,"Ritual prevents progression to the revival pool.");
            yield return Delay(1.3f);Check(!ritual.glow.enabled&&!ritual.IsCasting,"Magic circle persists after the ritual.");
            Check(!cast.companion.PresentationControlled,"Companion never returns to normal travel.");
            Check(timer.streams.All(t=>!t.gameObject.activeSelf),"Sand streams remain active after verification.");Capture("05_Ritual_Ended");yield return Frames(3);
            if(retainers!=null)
            {
                for(int i=0;i<retainers.Length;i++)Check(Vector3.Distance(retainers[i].transform.position,retainerPosts[i])<(retainers[i].stationId=="CalendarAttendant"?.06f:.025f),"A ceremonial retainer did not return to the post: "+retainers[i].name+" distance="+Vector3.Distance(retainers[i].transform.position,retainerPosts[i]));
                stage.ApplyMoment(2,true,false);yield return Delay(.7f);
                var sentries=retainers.Where(r=>r.spearGuard).ToArray();
                Check(sentries.All(r=>r.GetComponent<EraGuardEquipment>().HoldsPolearm),"Night lighting makes a sentry put down the spear.");
                Check(sentries.All(r=>r.GetComponent<TorchBearer>().IsCarried),"Night sentries cannot carry a torch alongside the spear.");
                Check(sentries.All(r=>!r.GetComponent<EraGuardEquipment>().HoldsShield&&r.GetComponent<EraGuardEquipment>().shield.localPosition.z<-.2f),"A night sentry holds shield and torch in the same hand.");
                Check(retainers.Where(r=>!r.spearGuard).All(r=>!r.GetComponent<TorchBearer>().IsCarried),"Indoor attendants unnecessarily carry torches at night.");
                Capture("08_Night_Watch");yield return Frames(3);
            }
            Restore();report.passed=report.errors.Count==0;File.WriteAllText(output,JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);
        }
        private void Bind(){stage=FindFirstObjectByType<TravellerStage>();journey=stage.Journey;hud=stage.GetComponentInChildren<TravellerHud>();}
        private IEnumerator CheckRetinue()
        {
            var eye=stage.player.viewCamera.transform;var position=eye.position;var rotation=eye.rotation;
            // Offscreen bodies normally skip bone updates. Evaluate every post
            // only for this pose inspection, after the normal performance sample.
            var animators=retainers.Select(r=>r.GetComponent<TravellerActor>().animator).ToArray();
            var culling=animators.Select(a=>a.cullingMode).ToArray();
            foreach(var animator in animators)animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            yield return Delay(.4f);
            var fans=retainers.Select(r=>r.GetComponent<RoyalFanBearer>()).Where(f=>f!=null).ToArray();
            if(stage.Audience!=null)
            {
                Check(fans.Length==2&&fans.All(f=>f.GetComponent<TravellerActor>().role==TravellerRole.CitizenFemale),"Expected two female fan bearers.");
                float roll=fans[0].RollDegrees;yield return Delay(.5f);Check(Mathf.Abs(fans[0].RollDegrees-roll)>.1f,"The large fans never sway.");
                foreach(var fan in fans)
                {
                    var actor=fan.GetComponent<TravellerActor>();var hands=fan.GetComponent<CourtPropGrip>();
                    float right=Vector3.Distance(actor.animator.GetBoneTransform(HumanBodyBones.RightHand).position,hands.RightTarget);
                    float left=Vector3.Distance(actor.animator.GetBoneTransform(HumanBodyBones.LeftHand).position,hands.LeftTarget);
                    Check(right<.06f&&left<.06f,"Fan bearer is not gripping the moving staff: "+fan.name+" right="+right+" left="+left);
                }
            }
            var guards=retainers.Where(r=>r.spearGuard).ToArray();
            Check(guards.Count(r=>r.transform.position.z>0)==6,"Six sentries must visibly guard the audience chamber.");
            foreach(var member in retainers)
            {
                var actor=member.GetComponent<TravellerActor>();
                Check(actor.animator.avatar.isValid&&actor.animator.avatar.isHuman,"Invalid court Humanoid: "+member.name);
                Check(member.GetComponent<NpcIdentity>().CanAnswer,"Court member cannot answer questions: "+member.name);
                Check(!actor.IsMoving&&!actor.IsTravelling,"Court member is wandering: "+member.name);
                if(!member.spearGuard)
                {
                    Check(!member.GetComponent<TorchBearer>().IsCarried,"An attendant is holding an unnecessary torch.");
                    continue;
                }
                var gear=member.GetComponent<EraGuardEquipment>();var hand=actor.animator.GetBoneTransform(HumanBodyBones.RightHand);
                var palm=hand.position+hand.rotation*(gear.rightPalmOffset*member.transform.lossyScale.x);
                var shaft=gear.polearm.TransformPoint(Vector3.up*gear.profile.polearmGripHeight);
                Check(gear.HoldsPolearm&&gear.polearm.gameObject.activeInHierarchy,"Guard is missing the held spear: "+member.name);
                Check(Vector3.Distance(hand.position,gear.RightHandTarget)<.05f&&Vector3.Distance(palm,shaft)<.035f,"Guard is not gripping the spear shaft: "+member.name);
                Check(Vector3.Dot(gear.polearm.up,Vector3.up)>.999f,"Guard spear tilts away from vertical: "+member.name);
                Check(!member.GetComponent<GuardPostController>().allowPatrol,"Ceremonial sentry is configured to patrol.");
            }
            stage.player.Teleport(new Vector3(3.7f,6.55f,29),new Vector3(0,7.15f,34));yield return Delay(.65f);Capture("06_Royal_Retinue");yield return Frames(3);
            var sentry=guards.First(g=>g.stationId.StartsWith("throne",StringComparison.Ordinal));
            stage.player.Teleport(sentry.transform.position+sentry.transform.forward*2.6f+sentry.transform.right*.7f+Vector3.up*1.65f,sentry.transform.position+Vector3.up*1.15f);
            yield return Delay(.5f);Capture("07_Spear_Grip");yield return Frames(3);
            var attendant=retainers.First(r=>r.stationId=="CalendarAttendant");
            stage.player.Teleport(attendant.transform.position+attendant.transform.forward*2.3f+Vector3.up*1.65f,attendant.transform.position+Vector3.up*1.5f);yield return Frames(4);
            var dialogue=stage.GetComponent<NpcDialogueDirector>();Check(dialogue.BeginPlayerQuestion()&&dialogue.QuestionTarget==attendant.GetComponent<NpcIdentity>(),"Looking at an attendant does not select that attendant for F dialogue.");
            dialogue.CancelQuestion();journey.CloseDialogue();
            for(int i=0;i<animators.Length;i++)animators[i].cullingMode=culling[i];
            stage.player.Teleport(position,position+rotation*Vector3.forward*4);yield return Frames(3);
        }
        private void CheckHud()
        {
            var mission=hud.Root.Q("mission");var subtitle=hud.Root.Q("caption-card");
            CheckInside(mission);CheckInside(subtitle);CheckInside(hud.Root.Q("top"));
            Check(hud.Root.Q("tools").worldBound.xMin>hud.Root.worldBound.width*.6f,"Tools shift to the left when contextual help is hidden.");
            Check(!mission.worldBound.Overlaps(subtitle.worldBound),"Mission and subtitle overlap.");
            Check(mission.worldBound.yMin>hud.Root.worldBound.height*.62f,"Mission instruction is not in the lower screen band.");
            Check(subtitle.worldBound.yMin>hud.Root.worldBound.height*.77f,"Subtitles are not near the lower edge.");
            Check(hud.Root.Q<Label>("objective").text.Contains("<color=#F2D35E>"),"Objective target emphasis is missing.");
            Check(hud.Root.Q<Label>("subtitle").text.Length<fullSubtitleLength,"Long dialogue did not paginate.");
        }
        private void CheckInside(VisualElement element)
        {
            var r=hud.Root.worldBound;var b=element.worldBound;
            Check(b.width>0&&b.height>0&&b.xMin>=r.xMin-2&&b.yMin>=r.yMin-2&&b.xMax<=r.xMax+2&&b.yMax<=r.yMax+2,"UI is outside viewport: "+element.name+" "+b);
        }
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        private static IEnumerator Delay(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        private void Check(bool condition,string error){report.checks++;if(!condition)report.errors.Add(error);}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        private void Restore(){if(!saved)return;if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",journal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");PlayerPrefs.Save();saved=false;}
        private void OnDestroy(){Restore();Application.logMessageReceived-=OnLog;}
    }
}
#endif
