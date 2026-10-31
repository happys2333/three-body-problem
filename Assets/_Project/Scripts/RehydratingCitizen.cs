using UnityEngine;

namespace ThreeBodyProblem
{
    public enum RehydrationPhase { DryPaper, Wetting, Inflating, GettingUp, Settling, Recovered }

    [RequireComponent(typeof(TravellerActor))]
    public sealed class RehydratingCitizen : MonoBehaviour
    {
        public CapitalRevivalSettings settings;
        public GameObject paperVisual;
        public Renderer[] paperRenderers;
        public Renderer ripple;
        public float bakedBottom, bakedHeight;
        public AnimationCurve getUpFloorCorrection;
        public int order;
        public RehydrationPhase Phase { get; private set; }
        public float Hydration { get; private set; }
        public bool IsRecovered => Phase == RehydrationPhase.Recovered;
        public float Elapsed => elapsed;
        private TravellerActor actor;
        private TorchBearer torch;
        private SkinnedMeshRenderer[] body;
        private Collider capsule;
        private Vector3 visualPosition;
        private Transform visual;
        private MaterialPropertyBlock properties;
        private float elapsed, getUpDuration;
        private bool began;
        private AnimatorCullingMode culling;

        private void Awake()
        {
            actor = GetComponent<TravellerActor>();
            torch = GetComponent<TorchBearer>();
            capsule = GetComponent<Collider>();
            body = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            visual = actor.animator.transform; visualPosition = visual.localPosition;
            culling = actor.animator.cullingMode;
            properties = new MaterialPropertyBlock();
            getUpDuration = actor.MotionDuration(settings.getUpMotion) * (1 - settings.getUpStartPhase);
            if (torch != null) torch.CarrySuppressed = true;
        }

        public void ResetDry()
        {
            elapsed = 0; began = false; Phase = RehydrationPhase.DryPaper; Hydration = 0;
            actor.StopTravelling(); actor.SetPresentationControl(true, 0);
            actor.animator.enabled = false;
            visual.localPosition = visualPosition;
            foreach (var skin in body) skin.enabled = false;
            if (capsule != null) capsule.enabled = false;
            paperVisual.SetActive(true);
            float scaleY = Mathf.Max(.001f, transform.lossyScale.y);
            paperVisual.transform.localPosition = Vector3.up * ((settings.waterSurfaceY - transform.position.y) / scaleY - bakedBottom);
            ApplySurface(0, 0);
        }

        public void BeginSoaking() { began = true; }

        public void Advance(float seconds)
        {
            if (!began || IsRecovered || actor.Paused) return;
            elapsed += Mathf.Max(0, seconds);
            float t = elapsed - order * settings.staggerSeconds;
            if (t <= 0) return;
            float wet = Mathf.Clamp01(t / settings.wettingSeconds);
            float raw = Mathf.Clamp01((t - settings.wettingSeconds) / settings.inflationSeconds);
            Hydration = Mathf.SmoothStep(0, 1, raw);
            if (t < settings.wettingSeconds) Phase = RehydrationPhase.Wetting;
            else if (raw < 1) Phase = RehydrationPhase.Inflating;
            else if (Phase < RehydrationPhase.GettingUp) StartGettingUp();
            if (Phase < RehydrationPhase.GettingUp) ApplySurface(wet, Hydration);

            float afterInflation = t - settings.wettingSeconds - settings.inflationSeconds;
            if (Phase == RehydrationPhase.GettingUp && afterInflation >= getUpDuration)
            {
                Phase = RehydrationPhase.Settling;
                actor.EndPresentation("MX_Idle");
            }
            if (Phase == RehydrationPhase.Settling && afterInflation >= getUpDuration + settings.settleSeconds)
            {
                Phase = RehydrationPhase.Recovered;
                visual.localPosition = visualPosition;
                actor.animator.cullingMode = culling;
                if (capsule != null) capsule.enabled = true;
                GetComponent<TravellerCrowdMember>().RefreshCondition();
            }
        }

        public void SetSequenceRunning(bool value)
        {
            if (Phase == RehydrationPhase.GettingUp) actor.SetPresentationControl(true, value ? 1 : 0);
        }

        private void StartGettingUp()
        {
            Phase = RehydrationPhase.GettingUp;
            actor.animator.enabled = true;
            actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.BeginPresentation(settings.getUpMotion, settings.getUpStartPhase);
            actor.animator.Update(0);
            foreach (var skin in body) skin.enabled = true;
            paperVisual.SetActive(false);
        }

        private void LateUpdate()
        {
            if (Phase != RehydrationPhase.GettingUp && Phase != RehydrationPhase.Settling) return;
            float t = Mathf.Max(0, elapsed - order * settings.staggerSeconds - settings.wettingSeconds - settings.inflationSeconds);
            float normalized = Mathf.Lerp(settings.getUpStartPhase, 1, Mathf.Clamp01(t / Mathf.Max(.01f, getUpDuration)));
            float lift = getUpFloorCorrection.Evaluate(normalized);
            if (Phase == RehydrationPhase.Settling)
                lift *= 1 - Mathf.SmoothStep(0, 1, (t - getUpDuration) / Mathf.Max(.01f, settings.settleSeconds));
            var worldOffset = Vector3.up * lift * transform.lossyScale.y;
            visual.localPosition = visualPosition + visual.parent.InverseTransformVector(worldOffset);
        }

        private void ApplySurface(float wetness, float hydration)
        {
            properties.Clear();
            properties.SetFloat("_Hydration", hydration);
            properties.SetFloat("_Wetness", wetness);
            properties.SetFloat("_Bottom", bakedBottom);
            properties.SetFloat("_BodyDepth", Mathf.Max(.01f, bakedHeight));
            properties.SetFloat("_PaperThickness", settings.paperThickness / Mathf.Max(.001f, transform.lossyScale.y));
            properties.SetFloat("_Elapsed", elapsed);
            properties.SetFloat("_Seed", order * 1.73f);
            foreach (var renderer in paperRenderers) renderer.SetPropertyBlock(properties);
            if (ripple != null)
            {
                properties.SetFloat("_Opacity", Phase == RehydrationPhase.DryPaper ? 0 : .32f * (1 - hydration * .7f));
                ripple.SetPropertyBlock(properties);
            }
        }
    }
}
