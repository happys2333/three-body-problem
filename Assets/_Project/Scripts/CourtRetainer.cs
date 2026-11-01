using UnityEngine;

namespace ThreeBodyProblem
{
    // Ceremonial posts remain occupied. Conversation can turn an attendant,
    // and a brief audience bow never takes ownership of the story clock.
    [RequireComponent(typeof(TravellerActor))]
    public sealed class CourtRetainer : MonoBehaviour
    {
        public string stationId;
        public bool spearGuard;
        public bool DutyControlled {get;set;}
        private RoyalFanBearer fanBearer;
        private TravellerActor actor;
        private TravellerStage stage;
        private NpcIdentity identity;
        private NpcDialogueDirector dialogue;
        private TravellerJourney journey;
        private Quaternion facing;
        private bool bowed;

        private void Start()
        {
            actor = GetComponent<TravellerActor>(); stage = GetComponentInParent<TravellerStage>();
            fanBearer = GetComponent<RoyalFanBearer>();
            identity = GetComponent<NpcIdentity>();
            if (stage == null) { enabled = false; return; }
            dialogue = stage.GetComponent<NpcDialogueDirector>(); journey = stage.Journey;
            facing = transform.rotation; journey.Changed += Refresh; Refresh();
        }
        private void Refresh()
        {
            if (journey.IsTitle) bowed = false;
            if (DutyControlled || fanBearer != null) return;
            actor.SetRestMotion(stage.Current.frost > .5f ? "MX_Shiver" : "MX_Idle");
            if (spearGuard || bowed || journey.IsPreviewing || journey.Progress.Step != JourneyStep.CourtTalk) return;
            bowed = true; actor.Gesture("MX_Bow");
        }
        private void Update()
        {
            if (spearGuard || DutyControlled || actor.Paused || actor.PresentationControlled || stage.IsLoading) return;
            bool speaking = dialogue != null && (dialogue.QuestionTarget == identity || dialogue.Current == identity && dialogue.IsVocalising);
            actor.FaceTarget = speaking ? stage.player.transform : null;
            if (!speaking && string.IsNullOrEmpty(actor.CurrentGesture))
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, actor.settings.turnDegreesPerSecond * Time.deltaTime);
        }
        private void OnDestroy() { if (journey != null) journey.Changed -= Refresh; }
    }
}
