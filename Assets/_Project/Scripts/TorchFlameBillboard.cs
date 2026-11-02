using UnityEngine;
namespace ThreeBodyProblem
{
    // Run after the animated hand places the torch. Its mesh has a foot pivot.
    [DefaultExecutionOrder(200)]
    public sealed class TorchFlameBillboard : MonoBehaviour
    {
        private Transform view;
        private static readonly int Phase = Shader.PropertyToID("_Phase");
        private void Awake()
        {
            var renderer = GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(Phase, Mathf.Repeat(GetInstanceID() * .618034f, 1f) * 60f);
            renderer.SetPropertyBlock(properties);
        }
        private void Start() { if (Camera.main != null) view = Camera.main.transform; }
        public void FaceCamera(Vector3 cameraPosition)
        {
            Vector3 direction = transform.position - cameraPosition;
            direction.y = 0;
            if (direction.sqrMagnitude > .00001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
        private void LateUpdate() { if (view != null) FaceCamera(view.position); }
    }
}
