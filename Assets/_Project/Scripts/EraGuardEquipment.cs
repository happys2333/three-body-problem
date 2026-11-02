using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerActor))]
    public sealed class EraGuardEquipment : MonoBehaviour
    {
        public EraGuardProfile profile;
        public Transform polearm, shield;
        public Quaternion rightHandLocalRotation = Quaternion.identity;
        public Quaternion leftHandLocalRotation = Quaternion.identity;
        public Vector3 rightPalmOffset, leftPalmOffset;
        public bool equipped = true;
        public bool CombatControlled { get; set; }
        public float CombatAim { get; set; }
        public float CombatThrust { get; set; }
        private Quaternion CombatRotation => Quaternion.Euler(CombatAim * 82, 0, 0);
        private TravellerActor actor;
        private TorchBearer torch;
        private float stowed;
        private SkinnedMeshRenderer[] skins;
        private int[] rightShapes, leftShapes;
        private float rightGripWeight, leftGripWeight;

        public bool HoldsPolearm => enabled && equipped && profile != null &&
            actor != null && !actor.PresentationControlled;
        public bool HoldsShield => HoldsPolearm && (torch == null || !torch.IsCarried) &&
            string.IsNullOrEmpty(actor.CurrentGesture);
        public Vector3 RightHandTarget => transform.TransformPoint(profile.rightGrip + Vector3.forward * (CombatThrust * .65f));
        public Vector3 LeftHandTarget => transform.TransformPoint(profile.leftGrip);
        public Quaternion RightHandRotation => transform.rotation * CombatRotation * rightHandLocalRotation;
        public Quaternion LeftHandRotation => transform.rotation * leftHandLocalRotation;
        public Vector3 LeftPalmCenter(Transform hand) => hand.position + hand.rotation * (leftPalmOffset * transform.lossyScale.x);
        public Vector3 RightElbowHint => transform.TransformPoint(new Vector3(.64f, 1.24f, -.08f));
        public Vector3 LeftElbowHint => transform.TransformPoint(new Vector3(-.63f, 1.16f, -.02f));

        private void Start()
        {
            actor = GetComponent<TravellerActor>();
            torch = GetComponent<TorchBearer>();
            if (profile == null || polearm == null || shield == null || actor.animator == null)
            {
                enabled = false;
                return;
            }
            var ik = actor.animator.GetComponent<TorchHandIK>();
            if (ik == null) ik = actor.animator.gameObject.AddComponent<TorchHandIK>();
            ik.guard = this;
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            rightShapes = new int[skins.Length]; leftShapes = new int[skins.Length];
            for (int i = 0; i < skins.Length; i++)
            {
                rightShapes[i] = skins[i].sharedMesh.GetBlendShapeIndex("GuardGripRight");
                leftShapes[i] = skins[i].sharedMesh.GetBlendShapeIndex("GuardGripLeft");
            }
            var stage = GetComponentInParent<TravellerStage>();
            stowed = stage != null && stage.Current.sunCount == 0 ? 1 : 0;
            ApplyEquipment();
            UpdateFingerGrips();
        }

        private void UpdateFingerGrips()
        {
            float step = 100 * Time.deltaTime / Mathf.Max(.05f, profile.handBlendSeconds);
            float right = Mathf.MoveTowards(rightGripWeight, HoldsPolearm ? 100 : 0, step);
            float left = Mathf.MoveTowards(leftGripWeight, HoldsShield || torch != null && torch.IsCarried ? 100 : 0, step);
            for (int i = 0; i < skins.Length; i++)
            {
                if (right != rightGripWeight && rightShapes[i] >= 0) skins[i].SetBlendShapeWeight(rightShapes[i], right);
                if (left != leftGripWeight && leftShapes[i] >= 0) skins[i].SetBlendShapeWeight(leftShapes[i], left);
            }
            rightGripWeight = right; leftGripWeight = left;
        }

        private void Update()
        {
            if (profile == null || actor == null || actor.Paused) return;
            stowed = Mathf.MoveTowards(stowed, HoldsShield ? 0 : 1,
                Time.deltaTime / Mathf.Max(.05f, profile.shieldStowSeconds));
            ApplyEquipment();
            UpdateFingerGrips();
        }

        private void ApplyEquipment()
        {
            polearm.gameObject.SetActive(HoldsPolearm);
            polearm.localPosition = profile.rightGrip + CombatRotation * (profile.polearmPosition - profile.rightGrip) + Vector3.forward * (CombatThrust * .65f);
            polearm.localRotation = CombatRotation * Quaternion.Euler(profile.polearmEuler);
            shield.gameObject.SetActive(HoldsPolearm);
            // Move around the outside of the torso instead of through the chest.
            float arc = Mathf.Sin(stowed * Mathf.PI);
            shield.localPosition = Vector3.Lerp(profile.shieldPosition, profile.shieldBackPosition, stowed)
                + new Vector3(-.45f, .18f, 0) * arc;
            shield.localRotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.Euler(0, 180, 0), stowed);
        }
    }
}
