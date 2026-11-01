using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class FiveTierSandTimer : MonoBehaviour
    {
        public TravellerStage stage;
        public PalaceRitualSettings settings;
        public Transform casing;
        public Transform[] piles, streams;
        private TravellerJourney journey;
        private TravellerHud hud;
        private Vector3[] pilePositions;
        private float elapsed;
        private bool running;
        private JourneyStep lastStep = (JourneyStep)(-1);
        public int FlipCount { get; private set; }
        public float TurnAngle { get; private set; }

        private void Start()
        {
            if (stage == null) stage = FindFirstObjectByType<TravellerStage>();
            if (stage == null || settings == null || casing == null) { enabled = false; return; }
            journey = stage.Journey; hud = stage.GetComponentInChildren<TravellerHud>();
            pilePositions = new Vector3[piles.Length];
            for (int i = 0; i < piles.Length; i++) pilePositions[i] = piles[i].localPosition;
            journey.Changed += Refresh; Refresh();
        }
        private void Refresh()
        {
            bool requested = !journey.IsPreviewing && journey.Progress.Step == JourneyStep.CalendarWait && !stage.HasPendingCourtAudience;
            if (requested && !running) { elapsed = 0; FlipCount = 0; }
            running = requested;
            if (!running) foreach (var stream in streams) stream.gameObject.SetActive(false);
            if (lastStep != journey.Progress.Step && (journey.IsTitle || journey.Progress.Step == JourneyStep.CourtApproach))
            { elapsed = 0; FlipCount = 0; TurnAngle = 0; casing.localRotation = Quaternion.identity; }
            lastStep = journey.Progress.Step;
        }
        private void Update()
        {
            if (journey == null || hud != null && hud.IsWorldPaused) return;
            if (running) elapsed += Time.deltaTime;
            float interval = Mathf.Max(.8f, settings.timerFlipSeconds);
            int completed = Mathf.FloorToInt(elapsed / interval);
            float phase = elapsed - completed * interval;
            float turn = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase / Mathf.Max(.1f, settings.timerTurnSeconds)));
            FlipCount = completed; TurnAngle = (completed + turn) * 180;
            casing.localRotation = Quaternion.Euler(0, 0, TurnAngle);
            float sand = Mathf.Clamp01((phase - settings.timerTurnSeconds) / Mathf.Max(.2f, interval - settings.timerTurnSeconds));
            var gravityInCase = Quaternion.Inverse(casing.localRotation) * Vector3.down;
            for (int i = 0; i < piles.Length; i++)
            {
                // Counter-rotate sand within each compartment after the case turns.
                piles[i].localPosition = pilePositions[i] + Vector3.up * .06f + gravityInCase * .06f;
                piles[i].localRotation = Quaternion.Inverse(casing.localRotation);
                piles[i].localScale = new Vector3(1, Mathf.Lerp(.12f, 1, (i + completed) % 2 == 0 ? sand : 1 - sand), 1);
                if (i < streams.Length) streams[i].gameObject.SetActive(running && turn > .99f);
            }
        }
        private void OnDestroy() { if (journey != null) journey.Changed -= Refresh; }
    }
}
