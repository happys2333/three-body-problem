using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerActor), typeof(EraGuardEquipment))]
    public sealed class GuardPostController : MonoBehaviour
    {
        public bool allowPatrol;
        private TravellerActor actor;
        private EraGuardEquipment equipment;
        private TravellerStage stage;
        private NpcDialogueDirector dialogue;
        private Vector3[] patrol;
        private float nextPatrol;
        private bool ownsFacing;
        private bool ownsPatrol;

        private void Start()
        {
            actor = GetComponent<TravellerActor>();
            equipment = GetComponent<EraGuardEquipment>();
            stage = GetComponentInParent<TravellerStage>();
            if (stage == null || equipment.profile == null) { enabled = false; return; }
            dialogue = stage.GetComponent<NpcDialogueDirector>();
            Vector3 p = transform.position;
            float span = equipment.profile.patrolHalfWidth;
            patrol = new[] { p + Vector3.right * span, p - Vector3.right * span, p };
            nextPatrol = Time.time + 9 + Mathf.Abs(p.x) * .19f;
            stage.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (actor == null) return;
            actor.SetRestMotion(stage.Current.frost > .5f ? "MX_Shiver" : equipment.profile.idleMotion);
            if (ownsPatrol && (stage.Current.frost > .5f || stage.Current.heat > .7f))
            { actor.StopTravelling(); ownsPatrol = false; }
        }

        private void Update()
        {
            if (actor.Paused || actor.PresentationControlled || stage.IsLoading) return;
            if (ownsPatrol && !actor.IsTravelling) ownsPatrol = false;
            bool speaking = dialogue != null && dialogue.QuestionTarget != null &&
                dialogue.QuestionTarget.transform == transform;
            speaking |= !string.IsNullOrEmpty(actor.CurrentGesture);
            float distance = (stage.player.transform.position - transform.position).sqrMagnitude;
            float clear = equipment.profile.playerClearance;
            if (speaking || distance < clear * clear)
            {
                if (ownsPatrol) { actor.StopTravelling(); ownsPatrol = false; }
                if (speaking) { actor.FaceTarget = stage.player.transform; ownsFacing = true; }
                nextPatrol = Time.time + 5;
                return;
            }
            if (ownsFacing) { actor.FaceTarget = null; ownsFacing = false; }
            if (!allowPatrol || actor.IsTravelling || Time.time < nextPatrol ||
                stage.Current.frost > .5f || stage.Current.heat > .7f) return;
            actor.FollowRoute(patrol, equipment.profile.patrolSpeed);
            ownsPatrol = true;
            nextPatrol = Time.time + equipment.profile.patrolInterval;
        }

        private void OnDestroy() { if (stage != null) stage.Changed -= Refresh; }
    }
}
