using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(Camera))]
    public sealed class HeatDistortion : MonoBehaviour
    {
        public TravellerStage stage;
        public Shader shader;
        private Material material;

        private void Awake()
        {
            if (shader != null && shader.isSupported) material = new Material(shader);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null || stage == null || stage.Current.heat < 0.1f)
            {
                Graphics.Blit(source, destination);
                return;
            }
            material.SetFloat("_Heat", stage.Current.heat);
            Graphics.Blit(source, destination, material);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
