using UnityEngine;
namespace ThreeBodyProblem
{
    public sealed class TorchFlameBillboard : MonoBehaviour
    {
        private Transform view;
        private void Start(){if(Camera.main!=null)view=Camera.main.transform;}
        private void LateUpdate(){if(view!=null)transform.rotation=Quaternion.LookRotation(transform.position-view.position,Vector3.up);}
    }
}
