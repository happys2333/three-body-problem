using System;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.NVIDIA;
#endif

namespace ThreeBodyProblem
{
    internal sealed class TravellerDlssPass : IDisposable
    {
        private const string InputShader = "TravellerRendering/TravellerDlssInputs";
        public static bool IsSupported
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (GraphicsSettings.currentRenderPipeline != null || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 ||
                    SystemInfo.graphicsDeviceVendorID != 0x10de || !SystemInfo.supportsMotionVectors) return false;
                return Device != null;
#else
                return false;
#endif
            }
        }

        public static bool TryGetInputSize(TravellerUpscaling mode, int width, int height, out Vector2Int input)
        {
            input = new Vector2Int(width, height);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!IsSupported) return false;
            Device.GetOptimalSettings((uint)width, (uint)height, Quality(mode), out var settings);
            if (settings.outRenderWidth < 2 || settings.outRenderHeight < 2 ||
                settings.outRenderWidth > width || settings.outRenderHeight > height) return false;
            input = new Vector2Int((int)settings.outRenderWidth, (int)settings.outRenderHeight);
            return true;
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static GraphicsDevice device;
        private static bool attempted;
        private static GraphicsDevice Device
        {
            get
            {
                if (attempted) return device;
                attempted = true;
                var candidate = GraphicsDevice.CreateGraphicsDevice();
                if (candidate != null && candidate.IsFeatureAvailable(GraphicsDeviceFeature.DLSS)) device = candidate;
                return device;
            }
        }

        private Camera camera;
        private Material copyInputs;
        private CommandBuffer capture;
        private RenderTexture depth, motion, output;
        private DLSSContext context;
        private bool previousJitteredTransparents, projectionChanged;
        private Vector3 lastPosition, lastDirection;
        private float lastFov;
        private int phase;

        private static DLSSQuality Quality(TravellerUpscaling mode) => mode switch
        {
            TravellerUpscaling.DlssPerformance => DLSSQuality.MaximumPerformance,
            TravellerUpscaling.DlssBalanced => DLSSQuality.Balanced,
            _ => DLSSQuality.MaximumQuality
        };
#endif

        public bool Configure(Camera source, TravellerUpscaling mode, Vector2Int input, Vector2Int display)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var shader = Resources.Load<Shader>(InputShader);
            if (!IsSupported || shader == null || !shader.isSupported) return false;
            depth = MakeTexture(input, RenderTextureFormat.RFloat, "DLSS depth");
            motion = MakeTexture(input, RenderTextureFormat.RGHalf, "DLSS motion vectors");
            output = MakeTexture(display, RenderTextureFormat.ARGBHalf, "DLSS output", true);
            if (!depth.Create() || !motion.Create() || !output.Create()) return false;

            using var setup = new CommandBuffer { name = "Create NVIDIA DLSS context" };
            var settings = new DLSSCommandInitializationData
            {
                inputRTWidth = (uint)input.x, inputRTHeight = (uint)input.y,
                outputRTWidth = (uint)display.x, outputRTHeight = (uint)display.y, quality = Quality(mode)
            };
            settings.SetFlag(DLSSFeatureFlags.IsHDR, true);
            settings.SetFlag(DLSSFeatureFlags.MVLowRes, true);
            settings.SetFlag(DLSSFeatureFlags.DepthInverted, SystemInfo.usesReversedZBuffer);
            context = Device.CreateFeature(setup, settings);
            if (context == null) return false;
            Graphics.ExecuteCommandBuffer(setup);

            camera = source;
            previousJitteredTransparents = camera.useJitteredProjectionMatrixForTransparentRendering;
            camera.useJitteredProjectionMatrixForTransparentRendering = true;
            copyInputs = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            capture = new CommandBuffer { name = "Capture DLSS depth and motion" };
            capture.Blit(Texture2D.blackTexture, depth, copyInputs, 0);
            capture.Blit(Texture2D.blackTexture, motion, copyInputs, 1);
            camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, capture);
            phase = 0;
            return true;
#else
            return false;
#endif
        }

        public void RecordFrame(RenderTexture color, CommandBuffer commands)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            bool reset = phase == 0 || (camera.transform.position - lastPosition).sqrMagnitude > 16f ||
                Vector3.Dot(lastDirection, camera.transform.forward) < .8f || Mathf.Abs(lastFov - camera.fieldOfView) > 5f;
            lastPosition = camera.transform.position;
            lastDirection = camera.transform.forward;
            lastFov = camera.fieldOfView;
            int sample = phase++ % 16 + 1;
            Vector2 jitter = new Vector2(Halton(sample, 2) - .5f, Halton(sample, 3) - .5f);
            var projection = camera.projectionMatrix;
            camera.nonJitteredProjectionMatrix = projection;
            projection.m02 += 2f * jitter.x / color.width;
            projection.m12 += 2f * jitter.y / color.height;
            camera.projectionMatrix = projection;
            projectionChanged = true;

            context.executeData.mvScaleX = -color.width;
            context.executeData.mvScaleY = -color.height;
            context.executeData.subrectWidth = (uint)color.width;
            context.executeData.subrectHeight = (uint)color.height;
            context.executeData.jitterOffsetX = jitter.x;
            context.executeData.jitterOffsetY = jitter.y;
            context.executeData.preExposure = 1f;
            context.executeData.invertYAxis = 1;
            context.executeData.reset = reset ? 1 : 0;
            var textures = new DLSSTextureTable { colorInput = color, depth = depth, motionVectors = motion, colorOutput = output };
            commands.Clear();
            Device.ExecuteDLSS(commands, context, textures);
            commands.Blit(output, BuiltinRenderTextureType.CameraTarget);
#endif
        }

        public void ResetProjection()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!projectionChanged || camera == null) return;
            camera.ResetProjectionMatrix();
            projectionChanged = false;
#endif
        }

        public void Dispose()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            ResetProjection();
            if (camera != null)
            {
                if (capture != null) camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, capture);
                camera.useJitteredProjectionMatrixForTransparentRendering = previousJitteredTransparents;
            }
            capture?.Dispose();
            if (context != null)
            {
                using var release = new CommandBuffer { name = "Release NVIDIA DLSS context" };
                Device.DestroyFeature(release, context);
                Graphics.ExecuteCommandBuffer(release);
            }
            if (copyInputs != null) UnityEngine.Object.Destroy(copyInputs);
            Release(depth); Release(motion); Release(output);
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static RenderTexture MakeTexture(Vector2Int size, RenderTextureFormat format, string name, bool randomWrite = false) =>
            new RenderTexture(size.x, size.y, 0, format, RenderTextureReadWrite.Linear)
            { name = name, enableRandomWrite = randomWrite, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };

        private static float Halton(int index, int radix)
        {
            float value = 0f;
            for (float weight = 1f / radix; index > 0; weight /= radix, index /= radix)
                value += index % radix * weight;
            return value;
        }

        private static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }
#endif
    }
}
