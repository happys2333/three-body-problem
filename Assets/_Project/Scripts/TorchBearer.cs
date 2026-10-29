using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class TorchBearer : MonoBehaviour
    {
        public GameObject torchPrefab;
        public bool equipped=true;
        public bool IsLit {get;private set;}
        public Transform Grip {get;private set;}
        public Vector3 HandTarget {get;private set;}
        public Quaternion HandRotation {get;private set;}
        private TravellerActor actor;private TravellerStage stage;private Transform hand,shoulder,view;
        private GameObject instance;private Light lightSource;private ParticleSystem[] flames;
        private TorchFlameBillboard[] flameCards;
        private float nextLightingUpdate;
        private void Start()
        {
            actor=GetComponent<TravellerActor>();stage=GetComponentInParent<TravellerStage>();
            if(actor==null||actor.animator==null||!actor.animator.isHuman||torchPrefab==null){enabled=false;return;}
            hand=actor.animator.GetBoneTransform(HumanBodyBones.LeftHand);if(hand==null){enabled=false;return;}
            shoulder=actor.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if(actor.firstPersonPlayer!=null)actor.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var ik=actor.animator.GetComponent<TorchHandIK>();if(ik==null)ik=actor.animator.gameObject.AddComponent<TorchHandIK>();ik.bearer=this;
            instance=Instantiate(torchPrefab);instance.name="Held travel torch";Grip=instance.transform;
            if(actor.firstPersonPlayer!=null)Grip.localScale*=.75f;
            lightSource=instance.GetComponentInChildren<Light>();flames=instance.GetComponentsInChildren<ParticleSystem>();
            flameCards=instance.GetComponentsInChildren<TorchFlameBillboard>();
            foreach(var card in flameCards)card.GetComponent<Renderer>().enabled=false;
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
            if(first&&shoulder!=null)HandTarget=shoulder.position+forward*.33f-Vector3.up*.17f-right*.02f;
            HandRotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(0,0,-15);
            if(Time.time<nextLightingUpdate)return;nextLightingUpdate=Time.time+.2f;
            bool darkness=stage==null||stage.Current.sunCount==0||stage.Current.frost>.5f||actor.seated;
            bool lit=equipped&&darkness;
            if(lit!=IsLit)
            {
                IsLit=lit;foreach(var flame in flames){if(lit)flame.Play();else flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
                foreach(var card in flameCards)card.GetComponent<Renderer>().enabled=lit;
            }
            if(lightSource!=null)
            {
                lightSource.enabled=lit&&(first||view==null||Vector3.Distance(view.position,transform.position)<24);
                lightSource.intensity=2.6f+Mathf.Sin(Time.time*16+transform.position.x)*.18f;
                lightSource.renderMode=first?LightRenderMode.ForcePixel:LightRenderMode.ForceVertex;
            }
        }
        private void LateUpdate()
        {
            if(instance==null)return;
            instance.SetActive(equipped);
            // Keep the flame upright while the animated hand carries the handle.
            bool first=actor.firstPersonPlayer!=null;
            var rotation=Quaternion.Euler(first?40:-7,transform.eulerAngles.y,8);
            Vector3 grip=hand.position-rotation*Vector3.up*(first?.15f:.2f);
            instance.transform.SetPositionAndRotation(grip,rotation);
        }
        private void OnDestroy(){if(instance!=null)Destroy(instance);}
    }
}
