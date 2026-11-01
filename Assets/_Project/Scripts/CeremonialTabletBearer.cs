using UnityEngine;
namespace ThreeBodyProblem
{
    [RequireComponent(typeof(CourtPropGrip))]
    public sealed class CeremonialTabletBearer : MonoBehaviour
    {
        public Transform tablet;
        private void Start(){GetComponent<CourtPropGrip>().Attach(tablet);}
    }
}
