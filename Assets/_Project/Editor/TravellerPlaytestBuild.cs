using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThreeBodyProblem.Editor
{
    public static class TravellerPlaytestBuild
    {
        [MenuItem("Tools/Three Body Traveller/Build Windows Playtest")]
        public static void BuildWindows()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Windows Build Support (Mono) for this Unity version first.");
            if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
                throw new InvalidOperationException("The release player must allow HTTP to communicate with its loopback AI service.");
            string output = Environment.GetEnvironmentVariable("TRAVELLER_WINDOWS_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = Path.GetFullPath("Builds/Windows/ThreeBodyTraveller/ThreeBodyTraveller.exe");
            else output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            const BuildTarget windows = BuildTarget.StandaloneWindows64;
            bool autoGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(windows);
            var graphicsApis = PlayerSettings.GetGraphicsAPIs(windows);
            const BuildOptions options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode;
            bool storyQa = Environment.GetEnvironmentVariable("TRAVELLER_STORY_QA") == "1";
            string rayTracingBuildAssets = null;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetUseDefaultGraphicsAPIs(windows, false);
                PlayerSettings.SetGraphicsAPIs(windows, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });
                // Unity's Direct3D DXC compiler is available in the Windows
                // Editor. Keep the DXR kernel out of macOS cross-build imports.
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    rayTracingBuildAssets = "Assets/TravellerDxrBuild_" + Guid.NewGuid().ToString("N");
                    string shader = rayTracingBuildAssets + "/Resources/TravellerRendering/TravellerOcclusion.compute";
                    Directory.CreateDirectory(Path.GetDirectoryName(shader));
                    File.Copy("Tools/Rendering/TravellerOcclusion.compute", shader);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (AssetDatabase.LoadAssetAtPath<ComputeShader>(shader) == null)
                        throw new InvalidOperationException("The DXR compute shader could not be imported.");
                }
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = TravellerStage.SceneNames.Select(name => TravellerSceneBuilder.SceneFolder + "/" + name + ".unity").ToArray(),
                    locationPathName = output,
                    target = windows,
                    extraScriptingDefines = storyQa ? new[] { "TRAVELLER_STORY_QA" } : Array.Empty<string>(),
                    options = options
                });
                var summary = new BuildSummaryRecord
                {
                    result = result.summary.result.ToString(), bytes = result.summary.totalSize,
                    errors = result.summary.totalErrors, warnings = result.summary.totalWarnings,
                    entryScene = TravellerProjectStartup.EntryScenePath,
                    unityVersion = Application.unityVersion, development = false, aiRequired = true, storyQa = storyQa,
                    rayTracingShaderIncluded = rayTracingBuildAssets != null
                };
                try
                {
                    if (result.summary.result == BuildResult.Succeeded)
                    {
                        string engine = BuildPipeline.GetPlaybackEngineDirectory(windows, options);
                        summary.bytes += WindowsNvidiaRuntime.CopyMissing(engine, output);
                        summary.nvidiaRuntimeIncluded = true;
                    }
                }
                catch
                {
                    summary.result = BuildResult.Failed.ToString();
                    summary.errors++;
                    throw;
                }
                finally
                {
                    File.WriteAllText(output + ".build.json", JsonUtility.ToJson(summary, true));
                }
                if (result.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + result.summary.result);
                Debug.Log("TRAVELLER_WINDOWS_BUILD: " + output + "; " + summary.bytes + " bytes; AI required; DLSS runtime included.");
            }
            finally
            {
                if (rayTracingBuildAssets != null) AssetDatabase.DeleteAsset(rayTracingBuildAssets);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
                PlayerSettings.SetGraphicsAPIs(windows, graphicsApis);
                PlayerSettings.SetUseDefaultGraphicsAPIs(windows, autoGraphics);
            }
        }

        [Serializable] private class BuildSummaryRecord
        {
            public string result, entryScene, unityVersion;
            public ulong bytes;
            public int errors, warnings;
            public bool development, aiRequired, storyQa, rayTracingShaderIncluded, nvidiaRuntimeIncluded;
        }
    }
}
