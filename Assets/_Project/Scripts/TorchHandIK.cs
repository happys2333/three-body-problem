using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(Animator))]
    public sealed class TorchHandIK : MonoBehaviour
    {
        public TorchBearer bearer;
        public EraGuardEquipment guard;
        public CourtPropGrip courtProp;
        private Animator animator;
        private float weight, rightWeight;
        private int blendFrame = -1;
        private Transform rightHand, leftHand;
        private Quaternion rightOffset = Quaternion.identity, leftOffset = Quaternion.identity;
        private Quaternion sentRight, sentLeft;
        private bool rightCalibrated, leftCalibrated;
        private int ikFrame = -1;
        private void Awake()
        {
            animator=GetComponent<Animator>();
            if(animator.isHuman)
            {
                rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            }
        }
        private void LateUpdate()
        {
            if((guard==null&&(courtProp==null||!courtProp.IsHolding))||ikFrame!=Time.frameCount)return;
            // Humanoid IK goals include the Avatar's hand post-rotation. Capture
            // that constant mapping after one solved frame, not the animated
            // forearm pose before IK. This also works for rigs without fingers.
            if(!rightCalibrated&&rightWeight>.999f&&rightHand!=null)
            {rightOffset=Quaternion.Inverse(sentRight)*rightHand.rotation;rightCalibrated=true;}
            if(!leftCalibrated&&weight>.999f&&leftHand!=null)
            {leftOffset=Quaternion.Inverse(sentLeft)*leftHand.rotation;leftCalibrated=true;}
        }
        private void OnAnimatorIK(int layer)
        {
            if(animator!=null&&courtProp!=null&&courtProp.IsHolding)
            {
                if(blendFrame!=Time.frameCount)
                {float step=courtProp.IsPaused?0:Time.deltaTime/.25f;weight=Mathf.MoveTowards(weight,1,step);rightWeight=Mathf.MoveTowards(rightWeight,1,step);blendFrame=Time.frameCount;}
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand,rightWeight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,rightWeight);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight);
                animator.SetIKPosition(AvatarIKGoal.RightHand,courtProp.RightTarget);animator.SetIKPosition(AvatarIKGoal.LeftHand,courtProp.LeftTarget);
                sentRight=courtProp.RightRotation*Quaternion.Inverse(rightOffset);sentLeft=courtProp.LeftRotation*Quaternion.Inverse(leftOffset);
                animator.SetIKRotation(AvatarIKGoal.RightHand,sentRight);animator.SetIKRotation(AvatarIKGoal.LeftHand,sentLeft);
                animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,0);animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,0);ikFrame=Time.frameCount;return;
            }
            if(animator==null||(bearer==null&&guard==null&&courtProp==null))return;
            bool torch=bearer!=null&&bearer.IsCarried;
            bool shield=!torch&&guard!=null&&guard.HoldsShield;
            bool polearm=guard!=null&&guard.HoldsPolearm;
            if(blendFrame!=Time.frameCount)
            {
                float seconds=guard!=null&&guard.profile!=null?guard.profile.handBlendSeconds:.25f;
                float step=Time.deltaTime/Mathf.Max(.05f,seconds);
                weight=Mathf.MoveTowards(weight,torch||shield?1:0,step);
                rightWeight=Mathf.MoveTowards(rightWeight,polearm?1:0,step);
                blendFrame=Time.frameCount;
            }
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,weight*(guard!=null?1:.8f));
            if(torch){animator.SetIKPosition(AvatarIKGoal.LeftHand,bearer.HandTarget);sentLeft=guard!=null?guard.LeftHandRotation*Quaternion.Inverse(leftOffset):bearer.HandRotation;animator.SetIKRotation(AvatarIKGoal.LeftHand,sentLeft);}
            else if(guard!=null)
            {
                animator.SetIKPosition(AvatarIKGoal.LeftHand,guard.LeftHandTarget);
                sentLeft=guard.LeftHandRotation*Quaternion.Inverse(leftOffset);
                animator.SetIKRotation(AvatarIKGoal.LeftHand,sentLeft);
            }
            if(guard==null)
            {
                rightWeight=Mathf.MoveTowards(rightWeight,0,Time.deltaTime/.25f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand,rightWeight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,rightWeight);return;
            }
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,rightWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand,rightWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand,guard.RightHandTarget);
            sentRight=guard.RightHandRotation*Quaternion.Inverse(rightOffset);
            animator.SetIKRotation(AvatarIKGoal.RightHand,sentRight);
            ikFrame=Time.frameCount;
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,rightWeight*.65f);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow,guard.RightElbowHint);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,shield?weight*.5f:0);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow,guard.LeftElbowHint);
        }
    }
}
