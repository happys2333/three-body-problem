using UnityEngine;

namespace ThreeBodyProblem
{
    // The stone plate opens when the court begins checking the proposed calendar.
    // Keeping the physical aperture and plate separate also permits sky/occlusion checks.
    public sealed class PalaceSkylight : MonoBehaviour
    {
        public TravellerStage stage;
        public Transform cover;
        public Vector3 closedPosition;
        public Vector3 openPosition;
        public Vector3 observationPoint = new Vector3(0,4.05f,23.5f);
        [SerializeField, Range(0,1)] private float opening;
        public float Opening => opening;

        public void SetOpening(float fraction)
        {
            opening=Mathf.Clamp01(fraction);
            if(cover!=null)cover.localPosition=Vector3.Lerp(closedPosition,openPosition,Mathf.SmoothStep(0,1,opening));
        }

        private void Update()
        {
            if(stage==null||stage.Journey==null||stage.Journey.IsPreviewing||stage.Journey.IsTitle)return;
            var step=stage.Journey.Progress.Step;
            bool open=step>=JourneyStep.CalendarWait&&step<=JourneyStep.WaitForDawn&&stage.MomentIndex<2;
            SetOpening(Mathf.MoveTowards(opening,open?1:0,Time.deltaTime/1.5f));
        }
    }
}
