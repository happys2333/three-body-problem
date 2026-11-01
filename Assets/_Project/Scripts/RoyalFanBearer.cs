using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(CourtPropGrip))]
    public sealed class RoyalFanBearer : MonoBehaviour
    {
        public Transform fan;
        public Vector3 gripPosition = new Vector3(.09f,1.22f,.22f);
        public float side = 1, phase;
        public float leanDegrees = 10, amplitudeDegrees = 5, periodSeconds = 4.8f;
        private TravellerActor actor;
        private TravellerHud hud;
        private float age;
        public float Age => age;
        public float RollDegrees => side * leanDegrees + Mathf.Sin(age*Mathf.PI*2/periodSeconds+phase)*amplitudeDegrees;
        private void Start()
        {
            actor=GetComponent<TravellerActor>();var stage=GetComponentInParent<TravellerStage>();
            if(stage!=null)hud=stage.GetComponentInChildren<TravellerHud>();
            var retainer=GetComponent<CourtRetainer>();if(retainer!=null)retainer.DutyControlled=true;
            Pose();GetComponent<CourtPropGrip>().Attach(fan);
        }
        private void Update()
        {
            if(actor==null||actor.Paused||hud!=null&&hud.IsWorldPaused)return;
            age+=Time.deltaTime;Pose();
        }
        private void Pose(){if(fan!=null){fan.localPosition=gripPosition;fan.localRotation=Quaternion.Euler(0,0,RollDegrees);}}
    }
}
