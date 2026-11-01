using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static class PalaceInteriorBuild
    {
        public const string Output="Builds/PalaceInterior20261101/Traveller.app";
        [Serializable] private class Result{public string result,output;public double seconds;public ulong bytes;public int errors,warnings;}
        public static void RunNow()
        {
            EraGuardProduction.GuardProject();
            if(EditorApplication.isCompiling||BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Editor is busy.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=TravellerStage.SceneNames.Select(n=>TravellerSceneBuilder.SceneFolder+"/"+n+".unity").ToArray(),locationPathName=Output,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development|BuildOptions.CompressWithLz4|BuildOptions.DetailedBuildReport});
            var summary=report.summary;File.WriteAllText(PalaceInteriorProduction.Docs+"/native-build.json",JsonUtility.ToJson(new Result{result=summary.result.ToString(),output=Output,seconds=summary.totalTime.TotalSeconds,bytes=summary.totalSize,errors=summary.totalErrors,warnings=summary.totalWarnings},true));
            if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Palace build failed.");
        }
    }
}
