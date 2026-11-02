using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class TorchBearer : MonoBehaviour
    {
        public GameObject torchPrefab;
        public bool equipped=true;
        public bool IsLit {get;private set;}
        public bool IsCarried {get;private set;}
        public bool CarrySuppressed {get;set;}
        public Transform Grip {get;private set;}
        public Vector3 HandTarget {get;private set;}
        public Quaternion HandRotation {get;private set;}
        private TravellerActor actor;private TravellerStage stage;private Transform hand,shoulder,view;
        private GameObject instance;private Light lightSource;private ParticleSystem[] flames;
        private Renderer[] flameCards;
        private float nextLightingUpdate;
        private bool manualCarry;
        private EraGuardEquipment guardEquipment;
        private float armReach;
        private Quaternion carryRotation;
        public void ToggleCarrying()
        {
            equipped=!IsCarried;
            manualCarry=equipped;
        }
        private void Start()
        {
            actor=GetComponent<TravellerActor>();stage=GetComponentInParent<TravellerStage>();
            guardEquipment=GetComponent<EraGuardEquipment>();
            if(actor==null||actor.animator==null||!actor.animator.isHuman||torchPrefab==null){enabled=false;return;}
            hand=actor.animator.GetBoneTransform(HumanBodyBones.LeftHand);if(hand==null){enabled=false;return;}
            shoulder=actor.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var forearm=actor.animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if(shoulder!=null&&forearm!=null)armReach=(Vector3.Distance(shoulder.position,forearm.position)+Vector3.Distance(forearm.position,hand.position))*.98f;
            if(actor.firstPersonPlayer!=null)actor.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var ik=actor.animator.GetComponent<TorchHandIK>();if(ik==null)ik=actor.animator.gameObject.AddComponent<TorchHandIK>();ik.bearer=this;
            instance=Instantiate(torchPrefab);instance.name="Held travel torch";Grip=instance.transform;
            if(actor.firstPersonPlayer!=null)Grip.localScale*=.75f;
            lightSource=instance.GetComponentInChildren<Light>();flames=instance.GetComponentsInChildren<ParticleSystem>();
            var cards=instance.GetComponentsInChildren<TorchFlameBillboard>();
            flameCards=new Renderer[cards.Length];
            for(int i=0;i<cards.Length;i++){flameCards[i]=cards[i].GetComponent<Renderer>();flameCards[i].enabled=false;}
            foreach(var flame in flames)flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(lightSource!=null)lightSource.enabled=false;
            instance.SetActive(false);
            if(stage!=null)view=stage.player.viewCamera.transform;
        }
        private void Update()
        {
            if(actor==null)return;
            bool first=actor.firstPersonPlayer!=null;
            Vector3 forward=first?actor.firstPersonPlayer.transform.forward:transform.forward;
            Vector3 right=first?actor.firstPersonPlayer.transform.right:transform.right;
            Vector3 basePoint=first?actor.firstPersonPlayer.transform.position:transform.position;
            float sitting=actor.seated?.25f:0;
            HandTarget=basePoint+Vector3.up*(1.13f-sitting)+forward*(first?.56f:.28f)-right*(first?.23f:.30f);
            if(!first&&guardEquipment!=null&&guardEquipment.profile!=null)
                HandTarget=transform.TransformPoint(guardEquipment.profile.torchGrip);
            float pitch=first&&view!=null?Mathf.DeltaAngle(0,view.eulerAngles.x):0;
            carryRotation=Quaternion.Euler(first?40+pitch:-7,transform.eulerAngles.y,8);
            var requestedRotation=carryRotation;
            if(first&&view!=null)
            {
                // Place the burning end in the lower-left view, then solve the
                // wrist from the real handle length. The same pose is used by
                // LateUpdate, so looking down cannot detach fire from wood.
                Vector3 wick=view.TransformPoint(new Vector3(-.25f,-.14f,.42f));
                float handleToWick=.75f*Grip.lossyScale.y-.15f;
                if(shoulder!=null&&armReach>0)
                {
                    // Tilt the handle within the arm's reach cone instead of
                    // clamping only the wrist (which sends the flame offscreen).
                    Vector3 delta=wick-shoulder.position;
                    float distance=Mathf.Clamp(delta.magnitude,Mathf.Max(.001f,handleToWick-armReach),handleToWick+armReach);
                    Vector3 radial=delta.sqrMagnitude>.000001f?delta.normalized:Vector3.up;
                    wick=shoulder.position+radial*distance;
                    float cosine=(distance*distance+handleToWick*handleToWick-armReach*armReach)/(2*distance*handleToWick);
                    float angle=Mathf.Acos(Mathf.Clamp(cosine,-1,1));
                    Vector3 axis=Vector3.RotateTowards(radial,carryRotation*Vector3.up,angle,0);
                    carryRotation=Quaternion.FromToRotation(carryRotation*Vector3.up,axis)*carryRotation;
                }
                HandTarget=wick-carryRotation*Vector3.up*handleToWick;
            }
            HandRotation=Quaternion.LookRotation(first&&view!=null?view.forward:forward,first&&view!=null?view.up:Vector3.up)*Quaternion.Euler(0,0,-15);
            if(first)HandRotation=carryRotation*Quaternion.Inverse(requestedRotation)*HandRotation;
            bool darkness=stage==null||stage.Current.sunCount==0;
            IsCarried=equipped&&(darkness||manualCarry)&&!CarrySuppressed&&!actor.PresentationControlled;
            bool lit=IsCarried;
            instance.SetActive(IsCarried);
            if(lit!=IsLit)
            {
                IsLit=lit;foreach(var flame in flames){if(lit)flame.Play();else flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
                foreach(var card in flameCards)card.enabled=lit;
            }
            if(lightSource!=null)
            {
                if(Time.time<nextLightingUpdate)return;nextLightingUpdate=Time.time+.2f;
                lightSource.enabled=lit&&(first||view==null||Vector3.Distance(view.position,transform.position)<24);
                lightSource.intensity=2.1f+Mathf.PerlinNoise(Time.time*5.5f,transform.position.x*.1f)*.35f;
                lightSource.renderMode=first?LightRenderMode.ForcePixel:LightRenderMode.ForceVertex;
            }
        }
        private void LateUpdate()
        {
            if(instance==null)return;
            if(!IsCarried)return;
            // Carry the wood with the wrist; the foot-pivoted fire independently
            // stays upright after this pose has been applied.
            bool first=actor.firstPersonPlayer!=null;
            var rotation=carryRotation;
            Vector3 palm=guardEquipment!=null?guardEquipment.LeftPalmCenter(hand):hand.position;
            Vector3 grip=palm-rotation*Vector3.up*(first?.15f:.2f);
            instance.transform.SetPositionAndRotation(grip,rotation);
        }
        private void OnDestroy(){if(instance!=null)Destroy(instance);}
    }
}
