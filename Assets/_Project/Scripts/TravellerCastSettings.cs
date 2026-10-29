using UnityEngine;

namespace ThreeBodyProblem
{
    public enum TravellerRole { PlayerTraveller, WenWang, KingZhou, Pope, Scholar, DaVinci, ShangGuard, ChurchGuard, CitizenMale, CitizenFemale }

    [CreateAssetMenu(menuName = "Three Body/Character Performance Settings")]
    public sealed class TravellerCastSettings : ScriptableObject
    {
        public RuntimeAnimatorController controller;
        public AnimationClip[] clips;
        public float transitionSeconds = 0.2f;
        public float referenceHumanScale = 1f;
        public float walkMetresPerSecond = 1.5f;
        public float runMetresPerSecond = 4f;
        public float turnDegreesPerSecond = 160f;
        public float conversationDistance = 8f;
        public float gestureSeconds = 4f;
    }
}
