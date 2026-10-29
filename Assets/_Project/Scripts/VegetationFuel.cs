using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class VegetationFuel : MonoBehaviour
    {
        public Renderer[] foliage;
        public Collider[] solids;
        [System.NonSerialized] public GameObject ashes;
        [System.NonSerialized] public float ignitionAt=-1;
        [System.NonSerialized] public int phase;
        [System.NonSerialized] public ParticleSystem fire;
        [System.NonSerialized] public Color[] originalColours;
    }
}
