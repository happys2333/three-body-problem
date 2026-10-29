using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class ShangCapitalPreviews
    {
        [MenuItem("Tools/Three Body Traveller/Capture Shang Capital")]
        public static void Capture()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Capture in Edit mode.");
            string original=SceneManager.GetActiveScene().path;
            Directory.CreateDirectory("Documentation/Previews");
            EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder+"/02_ChaogeCourt.unity");
            var stage=Object.FindFirstObjectByType<TravellerStage>();
            var authoredSky=stage.skyMaterial;
            stage.skyMaterial=new Material(authoredSky);
            float shadowDistance=QualitySettings.shadowDistance;
            stage.ApplyMoment(0,true);
            QualitySettings.shadowDistance=550f;
            View(stage,new Vector3(235,195,-275),new Vector3(0,0,-5),"Shang_Capital_Overview");
            QualitySettings.shadowDistance=shadowDistance;
            View(stage,stage.profile.moments[0].viewPosition,stage.profile.moments[0].viewTarget,"Shang_Capital_Gate");
            View(stage,new Vector3(-16,12,-155),new Vector3(0,31,15),"Shang_Capital_Palace");
            View(stage,new Vector3(-16,12,-155),new Vector3(0,31,15),"02_ChaogeCourt");
            View(stage,new Vector3(0,5.75f,-60),new Vector3(0,6,-32),"Shang_Pyramid_Entrance");
            View(stage,new Vector3(0,5.75f,-29),new Vector3(0,6,12),"Shang_Pyramid_Passage");
            Object.FindFirstObjectByType<PalaceSkylight>().SetOpening(1);
            View(stage,new Vector3(-5,5.75f,9),new Vector3(0,8.3f,31),"Shang_Capital_Court");
            View(stage,new Vector3(0,5.75f,23.5f),new Vector3(-0.85f,77,24.8f),"Shang_Pyramid_Skylight");
            View(stage,new Vector3(23,1.75f,-158),new Vector3(60,5,-151),"Shang_Capital_Street");
            View(stage,new Vector3(227,2.8f,-245),new Vector3(278,5,-115),"Shang_River_Landscape");
            stage.ApplyMoment(1,true);View(stage,stage.Current.viewPosition,stage.Current.viewTarget,"Shang_Capital_WaterGarden");
            stage.ApplyMoment(2,true);View(stage,stage.Current.viewPosition,stage.Current.viewTarget,"Shang_Capital_LongNight");
            Object.DestroyImmediate(stage.skyMaterial);stage.skyMaterial=authoredSky;
            EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder+"/01_GobiJourney.unity");
            stage=Object.FindFirstObjectByType<TravellerStage>();authoredSky=stage.skyMaterial;stage.skyMaterial=new Material(authoredSky);stage.ApplyMoment(5,true);
            View(stage,new Vector3(0,1.75f,105),new Vector3(0,6,155),"Gobi_Departure");
            View(stage,new Vector3(22,2.2f,58),new Vector3(-60,12,115),"Gobi_Rockscape");
            stage.ApplyMoment(0,true);View(stage,stage.Current.viewPosition,stage.Current.viewTarget,"Gobi_Opening");
            stage.ApplyMoment(1,true);View(stage,stage.Current.viewPosition,stage.Current.viewTarget,"01_GobiJourney");
            Object.DestroyImmediate(stage.skyMaterial);stage.skyMaterial=authoredSky;
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(original)?TravellerSceneBuilder.SceneFolder+"/01_GobiJourney.unity":original);
        }

        private static void View(TravellerStage stage,Vector3 position,Vector3 target,string name)
        {
            stage.player.Teleport(position,target);TravellerSceneChecks.Capture(stage.player.viewCamera,"Documentation/Previews/"+name+".jpg");
        }
    }
}
