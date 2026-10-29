using UnityEngine;
namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName="Three Body/Dialogue Settings")]
    public sealed class NpcDialogueSettings : ScriptableObject
    {
        public string gatewayUrl="http://127.0.0.1:8769";
        public float replyTimeoutSeconds=10;
        public TextAsset sharedPrompt;
        public bool synthesiseReplies=true;
        public float conversationDistance=5;
    }
}
