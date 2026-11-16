using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeBodyProblem
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class TravellerSceneUpscaler : MonoBehaviour
    {
        private static readonly int OutputSize = Shader.PropertyToID("_FsrOutputSize");
        private static readonly int Easu0 = Shader.PropertyToID("_FsrEasu0");
        private static readonly int Easu1 = Shader.PropertyToID("_FsrEasu1");
        private static readonly int Easu2 = Shader.PropertyToID("_FsrEasu2");
        private static readonly int Easu3 = Shader.PropertyToID("_FsrEasu3");
        private static readonly int Rcas = Shader.PropertyToID("_FsrRcas");
        private const string ShaderResource = "FidelityFX/TravellerFsr";

        private Camera sceneCamera, outputCamera;
        private Material material;
        private CommandBuffer commands;
        private RenderTexture sceneColor, filteredColor;
        private int requestedSamples, outputWidth, outputHeight;
        private bool allocationFailed;
        private TravellerUpscaling activeMode;
        private TravellerDlssPass dlss;
        private DepthTextureMode originalDepthMode;
        private bool occlusionNeedsDepth;

        public static bool IsSupported
        {
            get
            {
                if (GraphicsSettings.currentRenderPipeline != null || SystemInfo.graphicsShaderLevel < 45) return false;
                var shader = Resources.Load<Shader>(ShaderResource);
                return shader != null && shader.isSupported;
            }
        }

        private void Awake()
        {
            sceneCamera = GetComponent<Camera>();
            originalDepthMode = sceneCamera.depthTextureMode;
        }

        internal void SetOcclusionDepth(bool requested)
        {
            occlusionNeedsDepth = requested;
            ApplyDepthRequests();
        }

        private void ApplyDepthRequests()
        {
            if (sceneCamera == null) return;
            var mode = originalDepthMode;
            if (dlss != null) mode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            if (occlusionNeedsDepth) mode |= DepthTextureMode.Depth;
            sceneCamera.depthTextureMode = mode;
        }

        private void LateUpdate()
        {
            var settings = TravellerUserSettings.Current;
            if (activeMode != settings.upscaling)
            {
                ReleaseTargets();
                activeMode = settings.upscaling;
                allocationFailed = false;
            }
            if (activeMode == TravellerUpscaling.Native || !sceneCamera.enabled || sceneCamera.targetTexture != sceneColor ||
                GraphicsSettings.currentRenderPipeline != null)
            {
                if (sceneColor != null) ReleaseTargets();
                allocationFailed = false;
                return;
            }
            if (allocationFailed) return;

            int width = Screen.width, height = Screen.height;
            if (width < 2 || height < 2 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize) return;
            var display = new Vector2Int(width, height);
            bool useDlss = settings.UsesDlss;
            int samples = sceneCamera.allowMSAA && !useDlss ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;
            if (sceneColor != null && outputWidth == width && outputHeight == height && requestedSamples == samples) return;

            var input = Vector2Int.RoundToInt((Vector2)display * settings.RenderScale);
            if (useDlss && !TravellerDlssPass.TryGetInputSize(activeMode, width, height, out input))
            {
                if (sceneColor != null) ReleaseTargets();
                return;
            }
            ReleaseTargets();
            if (!EnsureOutput(useDlss)) return;
            requestedSamples = samples;
            outputWidth = width;
            outputHeight = height;
            var sceneFormat = new RenderTextureDescriptor(input.x, input.y, useDlss ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32, 24);
            sceneFormat.msaaSamples = samples;
            sceneFormat.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(sceneFormat);
            sceneColor = new RenderTexture(sceneFormat) { name = "Upscaling scene color", wrapMode = TextureWrapMode.Clamp };
            if (!sceneColor.Create() || !ConfigureOutput(useDlss, input, display))
            {
                ReleaseTargets();
                allocationFailed = true;
                return;
            }

            sceneCamera.targetTexture = sceneColor;
            ApplyDepthRequests();
            outputCamera.depth = sceneCamera.depth + 1f;
            outputCamera.targetDisplay = sceneCamera.targetDisplay;
            outputCamera.enabled = true;
        }

        private bool ConfigureOutput(bool useDlss, Vector2Int input, Vector2Int display)
        {
            if (useDlss)
            {
                dlss = new TravellerDlssPass();
                return dlss.Configure(sceneCamera, activeMode, input, display);
            }
            filteredColor = new RenderTexture(display.x, display.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = "FSR EASU output", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (!filteredColor.Create()) return false;
            ConfigureFilter(input.x, input.y, display.x, display.y);
            commands.Blit(sceneColor, filteredColor, material, 0);
            commands.Blit(filteredColor, BuiltinRenderTextureType.CameraTarget, material, 1);
            return true;
        }

        private bool EnsureOutput(bool useDlss)
        {
            if (!useDlss && material == null)
            {
                if (!IsSupported)
                {
                    allocationFailed = true;
                    return false;
                }
                material = new Material(Resources.Load<Shader>(ShaderResource)) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (outputCamera != null) return true;
            var output = new GameObject("Upscaling presentation camera");
            output.transform.SetParent(transform, false);
            outputCamera = output.AddComponent<Camera>();
            outputCamera.enabled = false;
            outputCamera.cullingMask = 0;
            outputCamera.clearFlags = CameraClearFlags.Nothing;
            outputCamera.allowHDR = false;
            outputCamera.allowMSAA = false;
            outputCamera.useOcclusionCulling = false;
            outputCamera.renderingPath = RenderingPath.Forward;
            outputCamera.stereoTargetEye = StereoTargetEyeMask.None;
            commands = new CommandBuffer { name = "Scene upscaling" };
            outputCamera.AddCommandBuffer(CameraEvent.AfterEverything, commands);
            return true;
        }

        private void ConfigureFilter(int inputWidth, int inputHeight, int width, int height)
        {
            float x = (float)inputWidth / width, y = (float)inputHeight / height;
            float texelX = 1f / inputWidth, texelY = 1f / inputHeight;
            material.SetVector(OutputSize, new Vector4(width, height, 0, 0));
            // FsrEasuCon for a full input viewport. Pass floating-point bits to
            // the SDK filter and keep the setup off the per-pixel path.
            material.SetVector(Easu0, new Vector4(x, y, 0.5f * x - 0.5f, 0.5f * y - 0.5f));
            material.SetVector(Easu1, new Vector4(texelX, texelY, texelX, -texelY));
            material.SetVector(Easu2, new Vector4(-texelX, 2f * texelY, texelX, 2f * texelY));
            material.SetVector(Easu3, new Vector4(0, 4f * texelY, 0, 0));
            material.SetVector(Rcas, new Vector4(Mathf.Pow(2f, -1.4f), 0, 0, 0));
        }

        private void ReleaseTargets()
        {
            if (outputCamera != null) outputCamera.enabled = false;
            commands?.Clear();
            dlss?.Dispose();
            dlss = null;
            ApplyDepthRequests();
            if (sceneColor != null)
            {
                if (sceneCamera != null && sceneCamera.targetTexture == sceneColor) sceneCamera.targetTexture = null;
                sceneColor.Release();
                Destroy(sceneColor);
                sceneColor = null;
            }
            if (filteredColor != null)
            {
                filteredColor.Release();
                Destroy(filteredColor);
                filteredColor = null;
            }
        }

        private void OnPreCull() { if (dlss != null) dlss.RecordFrame(sceneColor, commands); }
        private void OnPostRender() => dlss?.ResetProjection();
        private void OnDisable() => ReleaseTargets();

        private void OnDestroy()
        {
            if (outputCamera != null) outputCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, commands);
            commands?.Dispose();
            if (outputCamera != null) Destroy(outputCamera.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
