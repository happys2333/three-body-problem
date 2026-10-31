using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(Camera))]
    public sealed class SunDamageFeedback : MonoBehaviour
    {
        public SunExposureSystem exposure;
        public TravellerPlayer player;
        public Shader shader;
        private Material material;
        private GobiSequenceController sequence;
        private void Awake(){if(shader!=null&&shader.isSupported)material=new Material(shader);if(exposure!=null)sequence=exposure.GetComponent<GobiSequenceController>();}
        private void LateUpdate()
        {
            if(player==null||exposure==null)return;
            float pulse=exposure.HitPulse;
            float wake=sequence!=null?Mathf.SmoothStep(0,1,sequence.WakeProgress):1;
            transform.localPosition=Vector3.up*(player.eyeHeight*Mathf.Lerp(.38f,1,wake))+new Vector3(Mathf.Sin(Time.unscaledTime*47),Mathf.Sin(Time.unscaledTime*57),0)*pulse*.012f;
        }
        private void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(material==null||exposure==null){Graphics.Blit(source,destination);return;}
            material.SetFloat("_Pulse",exposure.HitPulse);material.SetFloat("_Blackout",exposure.Blackout);
            material.SetFloat("_Injury",exposure.Vitality==null?0:1-exposure.Vitality.Health/(float)exposure.Vitality.Maximum);
            Graphics.Blit(source,destination,material);
        }
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
