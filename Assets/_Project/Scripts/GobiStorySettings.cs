using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName="Three Body/Gobi Story Settings")]
    public sealed class GobiStorySettings : ScriptableObject
    {
        public float nightMeetingSeconds=12;
        public float dangerRunSpeed=4.2f;
        public float shelterWaitSeconds=600;
        public float suddenSunsetSeconds=2;
        public float minimumNightWalkSeconds=12;
        public float minimumNightWalkMetres=35;
        public Vector3 departureGoal=new Vector3(2,0,64);
        public int maximumHealth=100;
        public int sunlightDamage=10;
        public float sunlightDamageInterval=1;
        public float burnSeconds=32;
        public float recoveryFadeSeconds=1.5f;
        public int frameRateLimit=90;
        public float shadowDistance=70;
        public float terrainPixelError=15;
        public float lodBias=1;
        public int shadowCascades=2;
        public int antiAliasingSamples=2;
        public GameObject torchPrefab,vegetationFirePrefab,ashPrefab;
        public AudioClip meetingLine,warningLine,shelterLine,identityLine,hourglassLine,departureLine;
    }
}
