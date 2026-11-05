using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = TravellerStage.SceneNames.Select(name => TravellerSceneBuilder.SceneFolder + "/" + name + ".unity").ToArray(),
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode
                });
                File.WriteAllText(output + ".build.json", JsonUtility.ToJson(new BuildSummaryRecord
                {
                    result = result.summary.result.ToString(), bytes = result.summary.totalSize,
                    errors = result.summary.totalErrors, warnings = result.summary.totalWarnings,
                    entryScene = TravellerProjectStartup.EntryScenePath,
                    unityVersion = Application.unityVersion, development = false, aiRequired = true
                }, true));
                if (result.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + result.summary.result);
                Debug.Log("TRAVELLER_WINDOWS_BUILD: " + output + "; " + result.summary.totalSize + " bytes; AI required.");
            }
            finally { PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend); }
        }

        [Serializable] private class BuildSummaryRecord
        {
            public string result, entryScene, unityVersion;
            public ulong bytes;
            public int errors, warnings;
            public bool development, aiRequired;
        }
    }
}
