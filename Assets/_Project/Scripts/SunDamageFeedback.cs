using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(Camera))]
    public sealed class SunDamageFeedback : MonoBehaviour
    {
        public SunExposureSystem exposure;
        public NpcDecisionController combat;
        public TravellerPlayer player;
        public Shader shader;
        private Material material;
        private GobiSequenceController sequence;
        private void Awake(){if(shader!=null&&shader.isSupported)material=new Material(shader);if(exposure!=null)sequence=exposure.GetComponent<GobiSequenceController>();}
        private void LateUpdate()
        {
            if(player==null||exposure==null&&combat==null)return;
            float pulse=Mathf.Max(exposure!=null?exposure.HitPulse:0,combat!=null?combat.HitPulse:0);
            float wake=sequence!=null?Mathf.SmoothStep(0,1,sequence.WakeProgress):1;
            transform.localPosition=Vector3.up*(player.eyeHeight*Mathf.Lerp(.38f,1,wake))+new Vector3(Mathf.Sin(Time.unscaledTime*47),Mathf.Sin(Time.unscaledTime*57),0)*pulse*.012f;
        }
        private void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(material==null||exposure==null&&combat==null){Graphics.Blit(source,destination);return;}
            material.SetFloat("_Pulse",Mathf.Max(exposure!=null?exposure.HitPulse:0,combat!=null?combat.HitPulse:0));
            material.SetFloat("_Blackout",Mathf.Max(exposure!=null?exposure.Blackout:0,combat!=null?combat.Blackout:0));
            float injury=exposure==null||exposure.Vitality==null?0:1-exposure.Vitality.Health/(float)exposure.Vitality.Maximum;
            if(combat!=null&&combat.settings!=null)injury=Mathf.Max(injury,1-combat.Health/(float)combat.settings.maximumHealth);
            material.SetFloat("_Injury",injury);
            Graphics.Blit(source,destination,material);
        }
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
