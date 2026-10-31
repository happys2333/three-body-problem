using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName="Three Body/Gobi Story Settings")]
    public sealed class GobiStorySettings : ScriptableObject
    {
        public float wakeSeconds=3.5f;
        public Vector3 investigationGoal=new Vector3(7,0,-19);
        public Vector3 meetingPosition=new Vector3(-6,0,-2);
        public float discoverDistance=20;
        public float greetingDistance=4.2f;
        public float minimumExplorationMetres=4;
        public float minimumOpeningTalkSeconds=8;
        public float minimumShelterTalkSeconds=20;
        public float narrativeCheckSeconds=12;
        public float offlineOpeningSilenceSeconds=24;
        public float offlineShelterSilenceSeconds=60;
        public float sunriseSeconds=3;
        public float dangerRunSpeed=4.2f;
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
        public AudioClip callLine,meetingLine,warningLine,shelterLine,identityLine,hourglassLine,departureLine;
    }
}
