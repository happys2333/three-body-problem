using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerStage))]
    public sealed class CapitalRevivalDirector : MonoBehaviour
    {
        public RehydratingCitizen[] citizens;
        public CapitalRevivalSettings settings;
        public bool HasStarted { get; private set; }
        public bool IsComplete
        {
            get { foreach (var citizen in citizens) if (!citizen.IsRecovered) return false; return citizens.Length > 0; }
        }
        private TravellerStage stage;
        private TravellerHud hud;
        private int previousMoment = -1;
        private bool completionAnnounced;

        private void Start()
        {
            stage = GetComponent<TravellerStage>(); hud = GetComponentInChildren<TravellerHud>();
            stage.Changed += Refresh;
            foreach (var citizen in citizens) citizen.ResetDry();
            Refresh();
        }

        private void Refresh()
        {
            if (previousMoment != stage.MomentIndex && stage.MomentIndex == 0)
            {
                HasStarted = false; completionAnnounced = false;
                foreach (var citizen in citizens) citizen.ResetDry();
            }
            previousMoment = stage.MomentIndex;
            foreach (var citizen in citizens) citizen.SetSequenceRunning(stage.MomentIndex == 1);
        }

        private void Update()
        {
            if (stage.MomentIndex != 1 || hud != null && hud.IsWorldPaused) return;
            if (!HasStarted)
            {
                bool nearby = false;
                foreach (var citizen in citizens)
                    if ((stage.player.transform.position - citizen.transform.position).sqrMagnitude < settings.activationDistance * settings.activationDistance) { nearby = true; break; }
                if (!nearby) return;
                HasStarted = true;
                foreach (var citizen in citizens) citizen.BeginSoaking();
            }
            foreach (var citizen in citizens) citizen.Advance(Time.deltaTime);
            if (!completionAnnounced && IsComplete)
            {
                completionAnnounced = true;
                stage.NotifyWorldPresentationChanged();
            }
        }

        private void OnDestroy() { if (stage != null) stage.Changed -= Refresh; }
    }
}
