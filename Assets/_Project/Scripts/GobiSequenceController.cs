using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerStage))]
    public sealed class GobiSequenceController : MonoBehaviour
    {
        public GobiStorySettings settings;
        public bool SunHasRisen { get; private set; }
        public bool VegetationIgnited { get; private set; }
        public bool WorldRunning => stage!=null&&journey!=null&&!journey.IsTitle&&!journey.IsSummary&&!journey.IsPreviewing&&!stage.IsLoading&&(hud==null||!hud.IsWorldPaused);
        private TravellerStage stage;private TravellerJourney journey;private TravellerHud hud;
        private Transform companion;
        private Vector3 previousPlayer;
        private JourneyStep previousStep=(JourneyStep)(-1);
        private bool identityBeat,waitingBeat;
        private bool sunRising;
        private float previousShadowDistance;
        private Terrain terrain;
        private float previousTerrainError;
        private int previousFrameLimit;
        private float previousLodBias;
        private int previousCascades;
        private int previousAntiAliasing;
        private ShadowResolution previousShadowResolution;
        private bool qualityApplied;

        private void Start()
        {
            stage=GetComponent<TravellerStage>();journey=stage.Journey;hud=GetComponentInChildren<TravellerHud>();
            foreach(var actor in GetComponentsInChildren<TravellerActor>())if(actor.role==TravellerRole.WenWang){companion=actor.transform;break;}
            previousPlayer=stage.player.transform.position;
            previousShadowDistance=QualitySettings.shadowDistance;QualitySettings.shadowDistance=settings.shadowDistance;
            previousFrameLimit=Application.targetFrameRate;Application.targetFrameRate=settings.frameRateLimit;
            previousLodBias=QualitySettings.lodBias;previousCascades=QualitySettings.shadowCascades;previousShadowResolution=QualitySettings.shadowResolution;
            previousAntiAliasing=QualitySettings.antiAliasing;QualitySettings.antiAliasing=settings.antiAliasingSamples;
            QualitySettings.lodBias=settings.lodBias;QualitySettings.shadowCascades=settings.shadowCascades;QualitySettings.shadowResolution=ShadowResolution.High;qualityApplied=true;
            terrain=GetComponentInChildren<Terrain>();if(terrain!=null){previousTerrainError=terrain.heightmapPixelError;terrain.heightmapPixelError=settings.terrainPixelError;}
            journey.Progress.ConfigureGobi(settings.shelterWaitSeconds,settings.suddenSunsetSeconds,settings.minimumNightWalkSeconds,settings.minimumNightWalkMetres);
        }

        private void Update()
        {
            if(stage==null)return;
            var step=journey.Progress.Step;
            if(step==JourneyStep.Title)
            {
                if(previousStep!=JourneyStep.Title)ResetSequence();
                previousStep=step;previousPlayer=stage.player.transform.position;return;
            }
            if(!WorldRunning){previousPlayer=stage.player.transform.position;return;}
            if(previousStep!=step)
            {
                if(step==JourneyStep.SeekShelter){ResetSequence();PlayLine(settings.meetingLine);}
                if(step==JourneyStep.ShelterTalk)
                {
                    VegetationIgnited=true;
                    var burn=GetComponent<GobiVegetationFire>();if(burn!=null)burn.IgniteSunlit();
                    PlayLine(settings.shelterLine);
                }
                if(step==JourneyStep.WalkToChaoge)PlayLine(settings.departureLine);
                previousStep=step;
            }
            if(step==JourneyStep.SeekShelter)
            {
                if(!sunRising&&journey.Progress.Elapsed>=settings.nightMeetingSeconds)
                {
                    sunRising=true;journey.CloseDialogue();stage.ApplyMoment(1,false,false);
                    journey.Speak("旅人：太阳不对！别停在亮处，跟我进石下！",7);PlayLine(settings.warningLine);
                }
                if(sunRising&&!SunHasRisen&&journey.Progress.Elapsed>=settings.nightMeetingSeconds+stage.profile.transitionSeconds)
                {SunHasRisen=true;journey.Progress.MarkGiantSunVisible();}
                bool together=companion!=null&&Vector3.Distance(companion.position,stage.shelterCenter.position)<stage.shelterRadius+2;
                if(SunHasRisen&&together&&stage.IsInShelter&&SunExposureSystem.IsSheltered(stage.player.transform.position+Vector3.up*1.1f,stage.Current.sunA,stage.player.transform)&&SunExposureSystem.IsSheltered(companion.position+Vector3.up*1.1f,stage.Current.sunA,companion))
                    journey.Progress.Arrive();
            }
            else if(step==JourneyStep.ShelterTalk)
            {
                float elapsed=journey.Progress.Elapsed;
                if(!identityBeat&&elapsed>18&&!journey.DialogueOpen)
                {identityBeat=true;journey.Progress.IdentifyTraveller();journey.Speak("旅人：我是姬昌。我要去朝歌，把推演的历法呈给纣王。你也在赶路吗？",10);PlayLine(settings.identityLine);}
                if(!waitingBeat&&elapsed>95&&!journey.DialogueOpen)
                {waitingBeat=true;journey.Speak("周文王：沙漏还在走。太阳却未必守时；趁这里安全，你想问什么就问吧。",9);PlayLine(settings.hourglassLine);}
            }
            else if(step==JourneyStep.WalkToChaoge)
            {
                float metres=Vector3.Distance(previousPlayer,stage.player.transform.position);
                if(metres<2)journey.Progress.AddNightWalking(metres);
                // The two observations remain available along the night walk, without blocking travel.
                if(stage.player.transform.position.z>=51&&stage.MomentIndex!=4)stage.ApplyMoment(4,true,false);
                else if(stage.player.transform.position.z>=33&&stage.player.transform.position.z<51&&stage.MomentIndex!=3)stage.ApplyMoment(3,true,false);
                bool together=companion==null||Vector3.Distance(companion.position,stage.player.transform.position)<11;
                if(together&&Vector2.Distance(new Vector2(stage.player.transform.position.x,stage.player.transform.position.z),new Vector2(settings.departureGoal.x,settings.departureGoal.z))<5)
                    journey.Progress.Arrive();
            }
            previousPlayer=stage.player.transform.position;
        }

        private void ResetSequence()
        {
            SunHasRisen=false;sunRising=false;VegetationIgnited=false;identityBeat=false;waitingBeat=false;
            var burn=GetComponent<GobiVegetationFire>();if(burn!=null)burn.ResetVegetation();
        }
        private void PlayLine(AudioClip clip){var voice=GetComponent<NpcDialogueDirector>();if(voice!=null)voice.PlayScriptedLine(clip);}
        private void OnDestroy()
        {
            if(!qualityApplied)return;
            QualitySettings.shadowDistance=previousShadowDistance;QualitySettings.lodBias=previousLodBias;QualitySettings.shadowCascades=previousCascades;QualitySettings.shadowResolution=previousShadowResolution;
            QualitySettings.antiAliasing=previousAntiAliasing;
            if(terrain!=null&&previousTerrainError>0)terrain.heightmapPixelError=previousTerrainError;Application.targetFrameRate=previousFrameLimit;
        }
    }
}
