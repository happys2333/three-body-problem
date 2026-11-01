using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName = "Three Body/Palace Ritual")]
    public sealed class PalaceRitualSettings : ScriptableObject
    {
        public float calendarSeconds = 26;
        public float revealSeconds = 2.6f;
        public float fadeSeconds = .9f;
        public float arrivalDistance = .22f;
        public float timerFlipSeconds = 2.4f;
        public float timerTurnSeconds = .65f;
        public float glowIntensity = 1.15f;
        public float radius = 3.2f;
        public string castingMotion = "MX_PrayStanding";
        public string castingLine = "姬昌：阴阳相推，变化皆有其位。请大王看清这些卦象，再以日光与沙漏验我所言。";
    }
}
