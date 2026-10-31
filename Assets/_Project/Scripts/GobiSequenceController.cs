using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerStage))]
    public sealed class GobiSequenceController : MonoBehaviour
    {
        public GobiStorySettings settings;
        public bool SunHasRisen => journey!=null&&journey.Progress.GiantSunSeen;
        public bool VegetationIgnited { get; private set; }
        public float ExplorationMetres { get; private set; }
        public float WakeProgress => journey==null||journey.Progress.Step!=JourneyStep.Awakening?1:Mathf.Clamp01(journey.Progress.Elapsed/settings.wakeSeconds);
        public bool WorldRunning => stage!=null&&journey!=null&&!journey.IsTitle&&!journey.IsSummary&&!journey.IsPreviewing&&!stage.IsLoading&&(hud==null||!hud.IsWorldPaused);
        public bool BothInShade => companion!=null&&stage.IsInShelter&&Vector3.Distance(companion.position,stage.shelterCenter.position)<stage.shelterRadius+2&&
            SunExposureSystem.IsSheltered(stage.player.transform.position+Vector3.up*1.1f,stage.Current.sunA,stage.player.transform)&&
            SunExposureSystem.IsSheltered(companion.position+Vector3.up*1.1f,stage.Current.sunA,companion);
        private TravellerStage stage;
        private TravellerJourney journey;
        private TravellerHud hud;
        private NpcDialogueDirector dialogue;
        private PlayerMicrophoneInput microphone;
        private GobiVegetationFire vegetation;
        private Transform companion;
        private Vector3 previousPlayer;
        private JourneyStep previousStep=(JourneyStep)(-1);
        private string pendingIntent;
        private float decisionDelay,nextDecision,idleSeconds;
        private float previousShadowDistance,previousTerrainError,previousLodBias;
        private int previousFrameLimit,previousCascades,previousAntiAliasing;
        private ShadowResolution previousShadowResolution;
        private Terrain terrain;
        private bool qualityApplied;

        private void Start()
        {
            stage=GetComponent<TravellerStage>();journey=stage.Journey;hud=GetComponentInChildren<TravellerHud>();
            dialogue=GetComponent<NpcDialogueDirector>();microphone=GetComponent<PlayerMicrophoneInput>();vegetation=GetComponent<GobiVegetationFire>();
            foreach(var actor in GetComponentsInChildren<TravellerActor>())if(actor.role==TravellerRole.WenWang){companion=actor.transform;break;}
            previousPlayer=stage.player.transform.position;
            previousShadowDistance=QualitySettings.shadowDistance;QualitySettings.shadowDistance=settings.shadowDistance;
            previousFrameLimit=Application.targetFrameRate;Application.targetFrameRate=settings.frameRateLimit;
            previousLodBias=QualitySettings.lodBias;previousCascades=QualitySettings.shadowCascades;previousShadowResolution=QualitySettings.shadowResolution;
            previousAntiAliasing=QualitySettings.antiAliasing;QualitySettings.antiAliasing=settings.antiAliasingSamples;
            QualitySettings.lodBias=settings.lodBias;QualitySettings.shadowCascades=settings.shadowCascades;QualitySettings.shadowResolution=ShadowResolution.High;qualityApplied=true;
            terrain=GetComponentInChildren<Terrain>();if(terrain!=null){previousTerrainError=terrain.heightmapPixelError;terrain.heightmapPixelError=settings.terrainPixelError;}
            journey.Progress.ConfigureGobi(settings.minimumOpeningTalkSeconds,settings.minimumShelterTalkSeconds,settings.suddenSunsetSeconds,settings.minimumNightWalkSeconds,settings.minimumNightWalkMetres);
        }

        public void ResetForNewRun()
        {
            VegetationIgnited=false;ExplorationMetres=0;pendingIntent=null;idleSeconds=0;nextDecision=settings.narrativeCheckSeconds;
            previousStep=(JourneyStep)(-1);
            if(vegetation!=null)vegetation.ResetVegetation();
            if(dialogue!=null)dialogue.ResetConversation();
            if(stage!=null)previousPlayer=stage.player.transform.position;
        }

        public void NotePlayerSpeech(){idleSeconds=0;}

        public void OnNpcReply(string intent,bool answeredPlayer,int speechLength)
        {
            if(journey==null||journey.IsTitle||journey.IsPreviewing||stage.IsLoading)return;
            if(answeredPlayer){journey.Progress.RecordNpcAnswer();idleSeconds=0;}
            if(journey.Progress.Step==JourneyStep.OpeningTalk&&journey.Progress.OpeningAnswers>=JourneyProgress.MaximumOpeningAnswers)intent="sunrise";
            if(!journey.Progress.CanChooseStoryIntent(intent))return;
            pendingIntent=intent;
            // Let the current short answer finish before moving the sky. New player speech pauses this delay.
            decisionDelay=Mathf.Clamp(speechLength*.14f,2,10);
        }

        private void Update()
        {
            if(stage==null)return;
            var progress=journey.Progress;
            var step=progress.Step;
            if(!WorldRunning){previousPlayer=stage.player.transform.position;return;}
            float dt=Time.unscaledDeltaTime;
            if(previousStep!=step)
            {
                pendingIntent=null;idleSeconds=0;nextDecision=settings.narrativeCheckSeconds;
                if(step==JourneyStep.ApproachTraveller)PlayLine(settings.callLine);
                if(step==JourneyStep.OpeningTalk)PlayLine(settings.meetingLine);
                if(step==JourneyStep.Sunrise)PlayLine(settings.warningLine);
                if(step==JourneyStep.ShelterTalk)
                {
                    VegetationIgnited=true;if(vegetation!=null)vegetation.IgniteSunlit();PlayLine(settings.shelterLine);
                }
                if(step==JourneyStep.WalkToChaoge)PlayLine(settings.departureLine);
                previousStep=step;
            }
            float metres=Vector3.Distance(previousPlayer,stage.player.transform.position);
            if(step==JourneyStep.Awakening)
            {
                if(progress.Elapsed>=settings.wakeSeconds)progress.WakeUp();
            }
            else if(step==JourneyStep.ExploreDesert)
            {
                if(metres<2)ExplorationMetres+=metres;
                if(ExplorationMetres>=settings.minimumExplorationMetres&&companion!=null&&Vector3.Distance(stage.player.transform.position,companion.position)<=settings.discoverDistance)
                    progress.DiscoverTraveller();
            }
            else if(step==JourneyStep.ApproachTraveller)
            {
                if(companion!=null&&Vector3.Distance(stage.player.transform.position,companion.position)<=settings.greetingDistance&&progress.Elapsed>=1.5f)progress.MeetTraveller();
            }
            else if(step==JourneyStep.Sunrise)
            {
                if(progress.Elapsed>=settings.sunriseSeconds)progress.MarkGiantSunVisible();
            }
            else if(step==JourneyStep.SeekShelter)
            {
                if(BothInShade)progress.Arrive();
            }
            else if(step==JourneyStep.OpeningTalk||step==JourneyStep.ShelterTalk)
            {
                bool occupied=dialogue!=null&&(dialogue.IsBusy||dialogue.IsVocalising)||microphone!=null&&(microphone.IsRecording||microphone.IsTranscribing||microphone.AwaitingPermission);
                if(!occupied)idleSeconds+=dt;
                if(step==JourneyStep.ShelterTalk&&!progress.AskedPlayerOrigin&&progress.Elapsed>=5&&!occupied)
                {
                    journey.Speak("旅人：我叫姬昌，要去朝歌。乱纪元里，你为什么一个人在外面？",9);
                    progress.IntroduceTraveller();PlayLine(settings.identityLine);idleSeconds=0;
                }
                bool safe=step==JourneyStep.OpeningTalk||BothInShade;
                if(pendingIntent!=null&&safe)
                {
                    bool listening=microphone!=null&&(microphone.IsRecording||microphone.IsTranscribing||microphone.AwaitingPermission);
                    if(!listening)decisionDelay-=dt;
                    if(decisionDelay<=0&&!occupied)
                    {
                        string intent=pendingIntent;pendingIntent=null;
                        if(progress.CanChooseStoryIntent(intent)){journey.CloseDialogue();progress.ChooseStoryIntent(intent);}
                    }
                }
                else if(pendingIntent==null&&safe&&!occupied&&progress.Elapsed>=nextDecision)
                {
                    nextDecision=progress.Elapsed+settings.narrativeCheckSeconds;
                    dialogue?.RequestNarrativeDecision();
                }
                // The local fallback remains playable when the gateway cannot be reached at all.
                if(pendingIntent==null&&safe&&!occupied&&dialogue!=null&&dialogue.UsingFallback&&
                    idleSeconds>=(step==JourneyStep.OpeningTalk?settings.offlineOpeningSilenceSeconds:settings.offlineShelterSilenceSeconds))
                    OnNpcReply(step==JourneyStep.OpeningTalk?"sunrise":"sunset",false,0);
            }
            else if(step==JourneyStep.WalkToChaoge)
            {
                if(metres<2)progress.AddNightWalking(metres);
                if(stage.player.transform.position.z>=51&&stage.MomentIndex!=4)stage.ApplyMoment(4,true,false);
                else if(stage.player.transform.position.z>=33&&stage.player.transform.position.z<51&&stage.MomentIndex!=3)stage.ApplyMoment(3,true,false);
                bool together=companion!=null&&Vector3.Distance(companion.position,stage.player.transform.position)<11;
                if(together&&Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(settings.departureGoal.x,settings.departureGoal.z))<5)
                    progress.Arrive();
            }
            previousPlayer=stage.player.transform.position;
        }
        private void PlayLine(AudioClip clip){if(dialogue!=null)dialogue.PlayScriptedLine(clip);}
        private void OnDestroy()
        {
            if(!qualityApplied)return;
            QualitySettings.shadowDistance=previousShadowDistance;QualitySettings.lodBias=previousLodBias;QualitySettings.shadowCascades=previousCascades;QualitySettings.shadowResolution=previousShadowResolution;
            QualitySettings.antiAliasing=previousAntiAliasing;
            if(terrain!=null&&previousTerrainError>0)terrain.heightmapPixelError=previousTerrainError;Application.targetFrameRate=previousFrameLimit;
        }
    }
}
