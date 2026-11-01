using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static class ShangCivicBuild
    {
        public const string Output = "Builds/ShangCapital20261101/Traveller.app";
        private const string ReportPath = ShangCivicAssetProduction.Docs + "/native-build.json";
        [Serializable] private class Result { public string result, output, error; public double seconds; public ulong bytes; public int errors, warnings; }

        public static void Start()
        {
            RunNow();
        }
        public static void RunNow()
        {
            CheckProject(); EditorApplication.delayCall -= Build;
            Build();
        }
        private static void CheckProject()
        {
            if (Application.dataPath != "/Users/happys/Documents/GitHub/three-body-problem/Assets") throw new InvalidOperationException("Wrong project.");
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Editor is busy.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before building.");
        }
        private static void Build()
        {
            try
            {
                CheckProject(); Directory.CreateDirectory(Path.GetDirectoryName(Output));
                File.WriteAllText(ReportPath, JsonUtility.ToJson(new Result { result = "building", output = Output }, true));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = TravellerStage.SceneNames.Select(n => TravellerSceneBuilder.SceneFolder + "/" + n + ".unity").ToArray(),
                    locationPathName = Output, target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.Development | BuildOptions.CompressWithLz4 | BuildOptions.DetailedBuildReport });
                var summary = report.summary;
                File.WriteAllText(ReportPath, JsonUtility.ToJson(new Result { result = summary.result.ToString(), output = Output,
                    seconds = summary.totalTime.TotalSeconds, bytes = summary.totalSize, errors = summary.totalErrors, warnings = summary.totalWarnings }, true));
            }
            catch (Exception e) { File.WriteAllText(ReportPath, JsonUtility.ToJson(new Result { result = "Failed", output = Output, error = e.ToString() }, true)); Debug.LogException(e); }
        }
    }
}
