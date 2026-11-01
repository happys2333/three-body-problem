using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName = "Three Body/Era Guard Equipment")]
    public sealed class EraGuardProfile : ScriptableObject
    {
        public TravellerRole role = TravellerRole.ShangGuard;
        public string bodySource;
        public int layoutVersion;
        public float standingHeight = 1.83f;
        public Vector3 polearmPosition = new Vector3(.28f, .08f, .24f);
        public float polearmGripHeight = 1.18f;
        public Vector3 polearmEuler = new Vector3(-2, 0, -3);
        public Vector3 rightGrip = new Vector3(.34f, 1.12f, .26f);
        public Vector3 shieldPosition = new Vector3(-.32f, 1.17f, .25f);
        public Vector3 torchGrip = new Vector3(-.30f, 1.20f, .23f);
        public Vector3 shieldBackPosition = new Vector3(0, 1.2f, -.27f);
        public Vector3 leftGrip = new Vector3(-.35f, 1.06f, .24f);
        public float handBlendSeconds = .25f;
        public float gripRadius = .028f;
        public float knuckleDistance = .055f;
        public float shieldStowSeconds = .45f;
        public float patrolSpeed = 1.05f;
        public float patrolInterval = 22;
        public float playerClearance = 5;
        public float patrolHalfWidth = 1.4f;
        public string idleMotion = "MX_WeightShift";
    }
}
