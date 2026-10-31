using System.Collections.Generic;
using UnityEngine;

namespace ThreeBodyProblem
{
    public static class NpcQuestionTargeting
    {
        // Called once at question start. Looking elsewhere never changes an active turn.
        public static NpcIdentity Find(Camera camera,IList<NpcIdentity> identities,Transform player=null)
        {
            if(camera==null||identities==null)return null;
            NpcIdentity only=null;int count=0;
            foreach(var npc in identities)if(npc!=null&&npc.CanAnswer){only=npc;count++;}
            if(count<2)return only;
            NpcIdentity best=null,fallback=null;float bestAngle=float.MaxValue,bestDistance=float.MaxValue;
            float fallbackAngle=float.MaxValue,fallbackDistance=float.MaxValue;
            foreach(var npc in identities)
            {
                if(npc==null||!npc.CanAnswer)continue;
                Vector3 offset=npc.ConversationPoint-camera.transform.position;
                float distance=offset.sqrMagnitude,angle=Vector3.Angle(camera.transform.forward,offset);
                if(Better(angle,distance,fallbackAngle,fallbackDistance)){fallback=npc;fallbackAngle=angle;fallbackDistance=distance;}
                if(angle>=90||!Visible(camera.transform.position,npc,player))continue;
                if(Better(angle,distance,bestAngle,bestDistance)){best=npc;bestAngle=angle;bestDistance=distance;}
            }
            // No hard range gate: a sole actor, or a scene without a visible actor, can still hear F.
            return best!=null?best:fallback;
        }

        private static bool Better(float angle,float distance,float bestAngle,float bestDistance)
        {return angle<bestAngle-.5f||Mathf.Abs(angle-bestAngle)<=.5f&&distance<bestDistance;}

        private static bool Visible(Vector3 origin,NpcIdentity npc,Transform player)
        {
            Vector3 offset=npc.ConversationPoint-origin;
            foreach(var hit in Physics.RaycastAll(origin,offset.normalized,offset.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(npc.transform)||player!=null&&hit.transform.IsChildOf(player))continue;
                return false;
            }
            return true;
        }
    }

    public sealed class NpcQuestionTurn
    {
        public NpcIdentity Recipient {get;private set;}
        public int Id {get;private set;}
        public bool IsActive {get;private set;}
        private bool submitted;
        public void Begin(NpcIdentity recipient){Id++;Recipient=recipient;submitted=false;IsActive=recipient!=null&&recipient.CanAnswer;}
        public bool Accepts(int id,NpcIdentity recipient)=>IsActive&&!submitted&&Id==id&&Recipient==recipient&&recipient!=null&&recipient.CanAnswer;
        public bool TrySubmit(int id,NpcIdentity recipient){if(!Accepts(id,recipient))return false;submitted=true;return true;}
        public void Complete(){IsActive=false;}
        public void Cancel(){Id++;IsActive=false;Recipient=null;}
    }
}
