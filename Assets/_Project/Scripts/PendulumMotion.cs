using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class PendulumMotion : MonoBehaviour
    {
        public float angle = 12f;
        public float period = 7f;
        public float offset;
        private Quaternion resting;

        private void Awake() => resting = transform.localRotation;
        private void Update() => transform.localRotation = resting * Quaternion.Euler(0f, 0f, Mathf.Sin((Time.time + offset) * Mathf.PI * 2f / period) * angle);
    }
}
