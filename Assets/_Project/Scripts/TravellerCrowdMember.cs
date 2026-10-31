using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerActor))]
    public sealed class TravellerCrowdMember : MonoBehaviour
    {
        public bool isGuard, isReviving;
        public int variation;
        public Vector3[] strollPoints;
        public float strollSpeed = 0.85f;
        private TravellerActor actor;
        private TravellerStage stage;
        private float elapsed, nextStroll;
        private RehydratingCitizen rehydration;

        private void Start()
        {
            actor = GetComponent<TravellerActor>(); stage = GetComponentInParent<TravellerStage>();
            rehydration = GetComponent<RehydratingCitizen>();
            stage.Changed += Refresh;
            nextStroll = 6f + variation * 0.71f;
            Refresh();
        }

        private void Refresh()
        {
            if (actor == null || rehydration != null && !rehydration.IsRecovered) return;
            bool cold = stage.Current.frost > 0.5f;
            bool hot = stage.Current.heat > 0.7f;
            actor.SetRestMotion(isGuard ? "MX_WeightShift" : cold ? "MX_Shiver" : hot ? "MX_InjuredHurtingIdle" : variation % 3 == 0 ? "MX_WeightShift" : "MX_Idle");
            if (cold || hot) actor.StopTravelling();
        }

        public void RefreshCondition() { Refresh(); }

        private void Update()
        {
            if (actor.Paused || isGuard || isReviving || stage.Current.frost > 0.5f || stage.Current.heat > 0.7f || strollPoints == null || strollPoints.Length == 0) return;
            elapsed += Time.deltaTime;
            if (elapsed < nextStroll || actor.IsTravelling) return;
            actor.FollowRoute(strollPoints, strollSpeed);
            nextStroll = elapsed + 14f + variation * 0.43f;
        }

        private void OnDestroy() { if (stage != null) stage.Changed -= Refresh; }
    }
}
