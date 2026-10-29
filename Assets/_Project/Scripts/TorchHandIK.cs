using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(Animator))]
    public sealed class TorchHandIK : MonoBehaviour
    {
        public TorchBearer bearer;
        private Animator animator;
        private void Awake(){animator=GetComponent<Animator>();}
        private void OnAnimatorIK(int layer)
        {
            if(animator==null||bearer==null||!bearer.equipped)return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,1);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,.8f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand,bearer.HandTarget);animator.SetIKRotation(AvatarIKGoal.LeftHand,bearer.HandRotation);
        }
    }
}
