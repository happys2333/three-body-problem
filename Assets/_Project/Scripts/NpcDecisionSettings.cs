using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName = "Three Body/NPC Decision and Guards")]
    public sealed class NpcDecisionSettings : ScriptableObject
    {
        [Min(1)] public int maximumGuards = 3;
        [Min(60)] public int frameRateLimit = 90;
        [Min(1)] public int maximumHealth = 100;
        [Min(1)] public int attackDamage = 25;
        [Min(.5f)] public float pursuitSpeed = 3.6f;
        [Min(.5f)] public float attackRange = 1.7f;
        [Min(.1f)] public float attackWindup = .7f;
        [Min(.1f)] public float attackRecovery = 1.1f;
        [Min(0)] public float initialGraceSeconds = 3;
        [Min(0)] public float negotiationSeconds = 65;
        [Min(.1f)] public float pathRefreshSeconds = .4f;
        [Min(.1f)] public float defeatFadeSeconds = 1.2f;
    }
}
