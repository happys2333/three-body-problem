using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeBodyProblem
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class TravellerRayTracedOcclusion : MonoBehaviour
    {
        private static readonly int CameraDepth = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int Geometry = Shader.PropertyToID("_SceneGeometry");
        private static readonly int Occlusion = Shader.PropertyToID("_Occlusion");
        private static readonly int InverseViewProjection = Shader.PropertyToID("_InverseViewProjection");
        private static readonly int ViewPosition = Shader.PropertyToID("_ViewPosition");
        private static readonly int OutputSize = Shader.PropertyToID("_OutputSize");

        private Camera view;
        private TravellerSceneUpscaler upscaler;
        private RayTracingAccelerationStructure geometry;
        private ComputeShader trace;
        private int kernel;
        private Material composite;
        private CommandBuffer commands;
        private RenderTexture occlusion;

        private const string TraceResource = "TravellerRendering/TravellerOcclusion";
        public static bool IsSupported => GraphicsSettings.currentRenderPipeline == null &&
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 && SystemInfo.supportsInlineRayTracing &&
            SystemInfo.usesReversedZBuffer && SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.RFloat) &&
            Resources.Load<ComputeShader>(TraceResource) != null;

        public static void Apply(Camera camera, bool enabled)
        {
            var effect = camera.GetComponent<TravellerRayTracedOcclusion>();
            if (enabled && IsSupported)
            {
                if (effect == null) camera.gameObject.AddComponent<TravellerRayTracedOcclusion>();
            }
            else if (effect != null)
            {
                effect.enabled = false;
                Destroy(effect);
            }
        }

        private void OnEnable()
        {
            view = GetComponent<Camera>();
            upscaler = GetComponent<TravellerSceneUpscaler>();
            trace = Resources.Load<ComputeShader>(TraceResource);
            var blend = Resources.Load<Shader>("TravellerRendering/TravellerOcclusionComposite");
            if (!IsSupported || trace == null || !trace.HasKernel("TraceOcclusion") || blend == null || !blend.isSupported)
            {
                enabled = false;
                return;
            }
            kernel = trace.FindKernel("TraceOcclusion");
            try
            {
                geometry = new RayTracingAccelerationStructure(new RayTracingAccelerationStructure.Settings(
                    RayTracingAccelerationStructure.ManagementMode.Manual,
                    RayTracingAccelerationStructure.RayTracingModeMask.Static, view.cullingMask)
                    { enableCompaction = true });
                AddStaticGeometry();
                geometry.Build();
                composite = new Material(blend) { hideFlags = HideFlags.HideAndDontSave };
                commands = new CommandBuffer { name = "Ray traced ambient occlusion" };
                upscaler.SetOcclusionDepth(true);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Ray traced ambient occlusion unavailable: " + error.GetType().Name);
                enabled = false;
            }
        }

        private void AddStaticGeometry()
        {
            var excludedLods = new HashSet<Renderer>();
            foreach (var group in FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                var levels = group.GetLODs();
                if (levels.Length == 0) continue;
                foreach (var level in levels)
                    foreach (var renderer in level.renderers) excludedLods.Add(renderer);
                // Use one simplified mesh per group for the short occlusion rays.
                foreach (var renderer in levels[Mathf.Min(1, levels.Length - 1)].renderers) excludedLods.Remove(renderer);
            }
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.gameObject.isStatic || !renderer.enabled || excludedLods.Contains(renderer) ||
                    renderer.gameObject.scene != gameObject.scene || (view.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var materials = renderer.sharedMaterials;
                for (int submesh = 0; submesh < Mathf.Min(materials.Length, mesh.subMeshCount); submesh++)
                {
                    var material = materials[submesh];
                    if (material == null || material.renderQueue >= (int)RenderQueue.AlphaTest || mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var instance = new RayTracingMeshInstanceConfig(mesh, (uint)submesh, material)
                    { subMeshFlags = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly, mask = 0xff };
                    geometry.AddInstance(instance, renderer.transform.localToWorldMatrix);
                }
            }
        }

        [ImageEffectOpaque]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (commands == null || !EnsureTexture(source.width, source.height))
            {
                Graphics.Blit(source, destination);
                return;
            }
            var projection = GL.GetGPUProjectionMatrix(view.projectionMatrix, true);
            commands.Clear();
            commands.SetRayTracingAccelerationStructure(trace, kernel, Geometry, geometry);
            commands.SetComputeTextureParam(trace, kernel, CameraDepth, new RenderTargetIdentifier(CameraDepth));
            commands.SetComputeTextureParam(trace, kernel, Occlusion, occlusion);
            commands.SetComputeMatrixParam(trace, InverseViewProjection, (projection * view.worldToCameraMatrix).inverse);
            commands.SetComputeVectorParam(trace, ViewPosition, view.transform.position);
            commands.SetComputeIntParams(trace, OutputSize, occlusion.width, occlusion.height);
            commands.DispatchCompute(trace, kernel, (occlusion.width + 7) / 8, (occlusion.height + 7) / 8, 1);
            Graphics.ExecuteCommandBuffer(commands);
            composite.SetTexture(Occlusion, occlusion);
            Graphics.Blit(source, destination, composite);
        }

        private bool EnsureTexture(int width, int height)
        {
            width = Mathf.Max(1, width / 2);
            height = Mathf.Max(1, height / 2);
            if (occlusion != null && occlusion.width == width && occlusion.height == height) return occlusion.IsCreated();
            if (occlusion != null) { occlusion.Release(); Destroy(occlusion); }
            occlusion = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                { name = "Half-resolution ray traced occlusion", enableRandomWrite = true, wrapMode = TextureWrapMode.Clamp };
            return occlusion.Create();
        }

        private void OnDisable()
        {
            if (upscaler != null) upscaler.SetOcclusionDepth(false);
            geometry?.Dispose();
            geometry = null;
            commands?.Dispose();
            commands = null;
            if (occlusion != null) { occlusion.Release(); Destroy(occlusion); occlusion = null; }
            if (composite != null) { Destroy(composite); composite = null; }
        }
    }
}
