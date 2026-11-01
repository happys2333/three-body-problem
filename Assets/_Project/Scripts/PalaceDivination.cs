using UnityEngine;

namespace ThreeBodyProblem
{
    // A presentation of Ji Chang's theory. Story progression remains owned by
    // JourneyProgress; the effect never changes health or decides the outcome.
    public sealed class PalaceDivination : MonoBehaviour
    {
        public TravellerStage stage;
        public PalaceRitualSettings settings;
        public TravellerActor companion;
        public Transform ritualCentre;
        public MeshRenderer glow;
        public ParticleSystem motes;
        private TravellerJourney journey;
        private TravellerHud hud;
        private TorchBearer torch;
        private MaterialPropertyBlock block;
        private bool active, casting, ownsPresentation, ownsApproach;
        private float age, intensity;
        private Transform previousFace;
        private bool previousTorchSuppressed;
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int TimeId = Shader.PropertyToID("_EffectTime");
        public bool IsCasting => casting;
        public float Intensity => intensity;
        public float Reveal => casting ? Mathf.Clamp01(age / Mathf.Max(.1f, settings.revealSeconds)) : 0;

        private void Start()
        {
            journey = stage.Journey; hud = stage.GetComponentInChildren<TravellerHud>();
            torch = companion.GetComponent<TorchBearer>(); block = new MaterialPropertyBlock();
            ritualCentre.localScale = Vector3.one * Mathf.Max(.5f, settings.radius) / 3.2f;
            journey.Progress.ConfigureCalendar(settings.calendarSeconds);
            journey.Changed += Refresh;
            SetGlow(0, 0); if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Refresh();
        }

        private void Refresh()
        {
            bool requested = !journey.IsPreviewing && !journey.IsTitle && journey.Progress.Step == JourneyStep.CalendarWait;
            if (requested == active) return;
            active = requested;
            if (active)
            {
                casting = false; age = 0; ownsApproach = false;
            }
            else ReleaseCompanion();
        }

        private void Update()
        {
            if (journey == null || hud != null && hud.IsWorldPaused) return;
            if (active && !casting)
            {
                if(stage.HasPendingCourtAudience)return;
                if(!ownsApproach)
                {
                    ownsApproach=true;journey.GuideCompanionForCalendar(ritualCentre.position);
                    previousFace=companion.FaceTarget;companion.FaceTarget=null;
                    previousTorchSuppressed=torch!=null&&torch.CarrySuppressed;if(torch!=null)torch.CarrySuppressed=true;
                }
                var delta = companion.transform.position - ritualCentre.position; delta.y = 0;
                if (delta.sqrMagnitude <= settings.arrivalDistance * settings.arrivalDistance)
                {
                    casting = true; age = 0;
                    companion.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                    companion.BeginPresentation(settings.castingMotion, 0); ownsPresentation = true;
                    journey.Speak(settings.castingLine, 9);
                    if (motes != null) motes.Play(true);
                }
            }
            if (active && casting) age += Time.deltaTime;
            float target = active && casting ? settings.glowIntensity : 0;
            float seconds = target > intensity ? settings.revealSeconds : settings.fadeSeconds;
            intensity = Mathf.MoveTowards(intensity, target, Time.deltaTime * settings.glowIntensity / Mathf.Max(.05f, seconds));
            if (active || intensity > 0) SetGlow(intensity, Mathf.Clamp01(age / Mathf.Max(.1f, settings.revealSeconds)));
            else if (glow.enabled) SetGlow(0, 0);
        }

        private void SetGlow(float value, float reveal)
        {
            glow.enabled = value > .001f;
            block.SetFloat(IntensityId, value); block.SetFloat(RevealId, reveal); block.SetFloat(TimeId, age);
            glow.SetPropertyBlock(block);
        }

        private void ReleaseCompanion()
        {
            casting = false;
            if (ownsPresentation) { companion.EndPresentation("MX_Idle"); ownsPresentation = false; }
            if (ownsApproach && companion != null) companion.FaceTarget = previousFace;
            if (ownsApproach && torch != null) torch.CarrySuppressed = previousTorchSuppressed;
            ownsApproach=false;
            if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnDisable()
        {
            if (journey == null) return;
            active = false; ReleaseCompanion(); intensity = 0; SetGlow(0, 0);
        }
        private void OnDestroy() { if (journey != null) journey.Changed -= Refresh; }
    }
}
