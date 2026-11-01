using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName="Three Body/Court Audience")]
    public sealed class CourtAudienceSettings : ScriptableObject
    {
        public float walkSpeed=1.25f, transferSeconds=1.25f, courtesySeconds=.8f;
        public Vector3 companionHandoff=new Vector3(-2.3f,4.01f,19.8f);
        public Vector3 officerHandoff=new Vector3(-3.35f,4.01f,19.8f);
        public Vector3[] ascent;
        public Vector3 companionCarry=new Vector3(0,1.12f,.36f), officerCarry=new Vector3(0,1.10f,.36f), kingCarry=new Vector3(0,1.12f,.40f);
    }
}
