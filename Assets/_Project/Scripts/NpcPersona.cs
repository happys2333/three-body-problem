using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName="Three Body/NPC Persona")]
    public sealed class NpcPersona : ScriptableObject
    {
        public string characterId,displayName;
        public TextAsset prompt;
        public string fallbackReply="这件事还要看眼前的证据。你想先看看什么？";
    }
}
