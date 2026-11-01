using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerActor))]
    public sealed class CourtPropGrip : MonoBehaviour
    {
        public enum GripKind { Staff, Tablet }
        public GripKind kind;
        public Vector3 rightPalmOffset, leftPalmOffset;
        public Quaternion rightReference = Quaternion.identity, leftReference = Quaternion.identity;
        public Vector3 rightContact = Vector3.zero, leftContact = new Vector3(0,-.15f,0);
        [Range(0,100)] public float fingerCurl = 100;
        [HideInInspector] public Mesh[] sourceMeshes;
        private TravellerActor actor;
        private TorchBearer torch;
        private Transform prop;
        private bool held, priorSuppression;
        private float curl;
        private SkinnedMeshRenderer[] skins;
        private int[] rightShapes, leftShapes;
        public bool IsHolding => held && prop != null && isActiveAndEnabled;
        public bool IsPaused => actor != null && actor.Paused;
        public Transform Prop => prop;
        public Quaternion RightRotation => (kind == GripKind.Staff ? prop.rotation : transform.rotation) * rightReference;
        public Quaternion LeftRotation => (kind == GripKind.Staff ? prop.rotation : transform.rotation) * leftReference;
        public Vector3 RightTarget => Contact(rightContact) - RightRotation * (rightPalmOffset * transform.lossyScale.x);
        public Vector3 LeftTarget => Contact(leftContact) - LeftRotation * (leftPalmOffset * transform.lossyScale.x);
        private Vector3 Contact(Vector3 point)
        {
            if (kind == GripKind.Tablet && Vector3.Dot(transform.right, prop.right) < 0) point.x = -point.x;
            return prop.TransformPoint(point);
        }
        private void Awake()
        {
            actor = GetComponent<TravellerActor>(); torch = GetComponent<TorchBearer>();
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true); rightShapes = new int[skins.Length]; leftShapes = new int[skins.Length];
            for (int i=0;i<skins.Length;i++)
            { rightShapes[i]=skins[i].sharedMesh.GetBlendShapeIndex("CourtGripRight");leftShapes[i]=skins[i].sharedMesh.GetBlendShapeIndex("CourtGripLeft"); }
        }
        private void Start()
        {
            var ik=actor.animator.GetComponent<TorchHandIK>();if(ik==null)ik=actor.animator.gameObject.AddComponent<TorchHandIK>();ik.courtProp=this;
        }
        public void Attach(Transform value)
        {
            if (!held) priorSuppression = torch != null && torch.CarrySuppressed;
            prop=value;held=value!=null;if(torch!=null)torch.CarrySuppressed=held||priorSuppression;
        }
        public void Release()
        { held=false;prop=null;if(torch!=null)torch.CarrySuppressed=priorSuppression; }
        private void Update()
        {
            if(actor.Paused)return;
            float value=Mathf.MoveTowards(curl,IsHolding?fingerCurl:0,400*Time.deltaTime);
            if(Mathf.Approximately(value,curl))return;curl=value;
            for(int i=0;i<skins.Length;i++)
            {if(rightShapes[i]>=0)skins[i].SetBlendShapeWeight(rightShapes[i],curl);if(leftShapes[i]>=0)skins[i].SetBlendShapeWeight(leftShapes[i],curl);}
        }
        private void OnDisable(){Release();}
    }
}
