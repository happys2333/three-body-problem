using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(Animator))]
    public sealed class TorchHandIK : MonoBehaviour
    {
        public TorchBearer bearer;
        private Animator animator;
        private float weight;
        private int blendFrame = -1;
        private void Awake(){animator=GetComponent<Animator>();}
        private void OnAnimatorIK(int layer)
        {
            if(animator==null||bearer==null)return;
            if(blendFrame!=Time.frameCount){weight=Mathf.MoveTowards(weight,bearer.IsCarried?1:0,Time.deltaTime/.25f);blendFrame=Time.frameCount;}
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight*.8f);
            animator.SetIKPosition(AvatarIKGoal.LeftHand,bearer.HandTarget);animator.SetIKRotation(AvatarIKGoal.LeftHand,bearer.HandRotation);
        }
    }
}
