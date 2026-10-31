using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName = "Three Body/Capital Revival Settings")]
    public sealed class CapitalRevivalSettings : ScriptableObject
    {
        [Min(0.1f)] public float wettingSeconds = 1.6f;
        [Min(1f)] public float inflationSeconds = 8f;
        [Min(0f)] public float staggerSeconds = 0.55f;
        [Min(0f)] public float settleSeconds = 1.4f;
        [Min(1f)] public float activationDistance = 24f;
        public float waterSurfaceY = 0.055f;
        [Range(0.003f, 0.03f)] public float paperThickness = 0.012f;
        [Range(0f, 0.1f)] public float getUpStartPhase = 0.02f;
        public string getUpMotion = "MX_GetUpProne";
    }
}
