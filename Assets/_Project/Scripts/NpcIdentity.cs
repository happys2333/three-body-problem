using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class NpcIdentity : MonoBehaviour
    {
        public NpcPersona persona;
        public bool interactive=true;
        public bool CanAnswer => interactive&&persona!=null&&isActiveAndEnabled;
        public Vector3 ConversationPoint => transform.position+Vector3.up*1.5f;
    }
}
