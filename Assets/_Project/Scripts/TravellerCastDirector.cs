using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerStage))]
    public sealed class TravellerCastDirector : MonoBehaviour
    {
        public TravellerActor playerActor, companion, king, pope, scholar, daVinci;
        public Vector3[] scholarExitPath, daVinciExitPath;
        public float escapeSpeed = 4.2f;
        private TravellerStage stage;
        private TravellerJourney journey;
        private TravellerHud hud;
        private TravellerActor[] actors;
        private JourneyStep appliedStep = (JourneyStep)(-1);

        private void Start()
        {
            stage = GetComponent<TravellerStage>(); journey = stage.Journey;
            hud = GetComponentInChildren<TravellerHud>();
            actors = GetComponentsInChildren<TravellerActor>();
            journey.Changed += Refresh;
            journey.DialogueSelected += OnDialogue;
            stage.Changed += Refresh;
            foreach (var actor in actors)
                if (actor != playerActor && actor.role != TravellerRole.ShangGuard && actor.role != TravellerRole.ChurchGuard)
                    actor.FaceTarget = stage.player.transform;
            Refresh();
        }

        private void Update()
        {
            bool paused = hud != null && hud.IsWorldPaused;
            foreach (var actor in actors) actor.Paused = paused;
        }

        private void Refresh()
        {
            if (journey == null) return;
            if (companion != null)
                companion.SetRestMotion(stage.Current.frost > 0.5f ? "MX_Shiver" : "MX_Idle");
            var step = journey.Progress.Step;
            if (journey.IsPreviewing || appliedStep == step) return;
            appliedStep = step;
            switch (step)
            {
                case JourneyStep.ShelterTalk: Cue(companion, "MX_TalkCalm"); break;
                case JourneyStep.Sunset: Cue(companion, "MX_ShieldEyes"); break;
                case JourneyStep.ObserveFirstStar: case JourneyStep.ObserveTwoStars: Cue(companion, "MX_LookSky", 6.5f); break;
                case JourneyStep.CourtTalk:
                    Cue(companion, "MX_Bow"); Cue(king, "MX_TalkCalm"); break;
                case JourneyStep.CalendarWait: Cue(companion, "MX_Think"); Cue(king, "MX_HeadNodYes"); break;
                case JourneyStep.ObserveRevival: Cue(companion, "MX_HappyHandGesture"); break;
                case JourneyStep.ObserveLongNight: Cue(companion, "Despair"); break;
                case JourneyStep.TribunalTalk: Cue(scholar, "MX_TalkArgue", 6f); Cue(daVinci, "MX_Think"); break;
                case JourneyStep.WalkOutside:
                    if (scholar != null) scholar.FollowRoute(scholarExitPath, escapeSpeed);
                    if (daVinci != null) daVinci.FollowRoute(daVinciExitPath, escapeSpeed);
                    Cue(pope, "MX_AnnoyedHeadShake"); break;
                case JourneyStep.ObserveThreeSuns: Cue(scholar, "MX_LookSky", 6.5f); Cue(daVinci, "MX_LookSky", 6.5f); break;
                case JourneyStep.AfterThreeSuns: Cue(scholar, "MX_Fear", 6f); Cue(daVinci, "MX_Fear", 6f); break;
                case JourneyStep.Collapse:
                    if (scholar != null) scholar.SetRestMotion("MX_InjuredHurtingIdle");
                    if (daVinci != null) daVinci.SetRestMotion("MX_InjuredHurtingIdle"); break;
                case JourneyStep.Title:
                    foreach (var actor in actors) actor.ResetMotionTracking(); break;
            }
        }

        private void OnDialogue(string choice)
        {
            switch (choice)
            {
                case "identity": case "hourglass": case "calendar": Cue(companion, "MX_TalkCalm", 6f); break;
                case "evidence": Cue(king, "MX_TalkCalm"); break;
                case "verification": Cue(king, "MX_HeadNodYes"); break;
                case "explain": Cue(scholar, "MX_TalkArgue", 7f); Cue(pope, "MX_ShakingHeadNo"); Cue(daVinci, "MX_HeadNodYes"); break;
                case "uncertain": Cue(scholar, "MX_Think"); break;
            }
        }

        private static void Cue(TravellerActor actor, string id, float seconds = 0)
        {
            if (actor != null) actor.Gesture(id, seconds);
        }

        private void OnDestroy()
        {
            if (journey != null) { journey.Changed -= Refresh; journey.DialogueSelected -= OnDialogue; }
            if (stage != null) stage.Changed -= Refresh;
        }
    }
}
