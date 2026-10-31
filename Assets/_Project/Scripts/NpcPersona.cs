using System.Collections.Generic;
using UnityEngine;

namespace ThreeBodyProblem
{
    [CreateAssetMenu(menuName="Three Body/NPC Persona")]
    public sealed class NpcPersona : ScriptableObject
    {
        public string characterId,displayName;
        public string[] aliases=new string[0];
        public bool IsMajorCharacter => characterId=="wenwang"||characterId=="king"||characterId=="pope"||characterId=="scholar"||characterId=="davinci";
        public IEnumerable<string> Addresses
        {
            get
            {
                yield return displayName;
                if(aliases!=null)foreach(var alias in aliases)yield return alias;
                if(characterId=="wenwang"){yield return "姬昌";yield return "周文王";yield return "文王";yield return "旅人";}
                if(characterId=="king"){yield return "纣王";yield return "商王";yield return "大王";}
            }
        }
        public TextAsset prompt;
        public string fallbackReply="这件事还要看眼前的证据。你想先看看什么？";
    }
}
