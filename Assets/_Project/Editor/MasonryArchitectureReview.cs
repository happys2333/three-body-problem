using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class MasonryArchitectureReview
    {
        [Serializable] public class AssetCheck
        {
            public string id,prefab;public bool passed;public int lodGroups,colliders,missingScripts;public long highTriangles;public Vector3 size;
        }
        [Serializable] public class Review
        {
            public bool passed,doorWalked,sideWallSolid;public List<AssetCheck> assets=new List<AssetCheck>();
        }
        public static Review Audit()
        {
            var report=new Review();
            foreach(string file in Directory.GetFiles(MasonryArchitectureLibrary.Root+"/Prefabs","*.prefab"))
            {
                var root=PrefabUtility.LoadPrefabContents(file);
                try
                {
                    var filters=root.GetComponentsInChildren<MeshFilter>(true);var renderers=root.GetComponentsInChildren<MeshRenderer>(true);var groups=root.GetComponentsInChildren<LODGroup>(true);
                    var meshes=filters.Select(f=>f.sharedMesh).Distinct().ToArray();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    var colliders=root.GetComponentsInChildren<Collider>(true);
                    bool geometry=meshes.All(m=>m!=null&&m.vertexCount>0&&m.normals.Length==m.vertexCount&&m.uv.Length==m.vertexCount&&m.tangents.Length==m.vertexCount
                        &&m.vertices.All(v=>Finite(v.x)&&Finite(v.y)&&Finite(v.z))&&m.normals.All(n=>n.sqrMagnitude>.5f)
                        &&m.triangles.All(i=>i>=0&&i<m.vertexCount));
                    bool lods=groups.Length>0&&groups.All(g=>{var levels=g.GetLODs();var n=levels.Select(l=>l.renderers.Sum(v=>v.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();return levels.Length==3&&n[0]>=n[1]&&n[1]>=n[2]&&n[2]>0&&levels[0].screenRelativeTransitionHeight>levels[1].screenRelativeTransitionHeight&&levels[1].screenRelativeTransitionHeight>levels[2].screenRelativeTransitionHeight;});
                    var check=new AssetCheck{id=root.name,prefab=file,lodGroups=groups.Length,colliders=colliders.Length,size=bounds.size,
                        highTriangles=groups.Sum(g=>(long)g.GetLODs()[0].renderers.Sum(v=>v.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)),
                        missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))};
                    check.passed=geometry&&lods&&root.transform.localScale==Vector3.one&&Mathf.Abs(bounds.min.y)<.03f&&check.missingScripts==0
                        &&renderers.All(v=>v.sharedMaterials.All(m=>m!=null&&m.shader.isSupported))&&colliders.Length>0&&colliders.All(c=>!(c is MeshCollider)||((MeshCollider)c).sharedMesh!=null);
                    report.assets.Add(check);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            TestDoor(report);report.passed=report.assets.Count==8&&report.assets.All(a=>a.passed)&&report.doorWalked&&report.sideWallSolid;
            File.WriteAllText(MasonryArchitectureLibrary.Docs+"/saved-prefab-audit.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidDataException("Masonry saved-prefab/controller audit failed.");return report;
        }
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        private static void TestDoor(Review report)
        {
            var original=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var gate=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MasonryArchitectureLibrary.Root+"/Prefabs/Doorway_5p6_Clear.prefab"),scene);gate.transform.position=Vector3.right*1000;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.05f,0);floor.transform.localScale=new Vector3(50,.1f,50);
                var visitor=new GameObject("Actual first-person controller",typeof(CharacterController));var cc=visitor.GetComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=Vector3.up*.9f;cc.stepOffset=.35f;
                var camera=new GameObject("Door test camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(visitor.transform,false);camera.transform.localPosition=Vector3.up*1.65f;
                var player=visitor.AddComponent<TravellerPlayer>();player.viewCamera=camera;player.enabled=false;player.Teleport(new Vector3(1000,1.70f,7),new Vector3(1000,1.70f,-7));Physics.SyncTransforms();
                RaycastHit hit;report.sideWallSolid=Physics.Raycast(new Vector3(1004.2f,1.4f,7),Vector3.back,out hit,10)&&hit.transform.IsChildOf(gate.transform);
                for(int i=0;i<400;i++)player.MoveInput(Vector2.up,false,1f/60);
                report.doorWalked=visitor.transform.position.z<-3&&Mathf.Abs(visitor.transform.position.y)<.2f;
            }
            finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
        }

        public static void Capture()
        {
            // Camera.scene isolates preview scenes only. An additive ordinary scene can
            // still render the Gobi/inspection-yard objects loaded beside the capital.
            string path=TravellerSceneBuilder.SceneFolder+"/02_ChaogeCourt.unity";
            var scene=EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var stage=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TravellerStage>(true)).Single();
                    var directory=MasonryArchitectureLibrary.Docs+"/Previews";Directory.CreateDirectory(directory);
                    Action<string,Vector3,Vector3> view=(name,eye,target)=>{
                        stage.player.Teleport(eye,target);var camera=stage.player.viewCamera;var previousScene=camera.scene;camera.scene=scene;
                        try{TravellerSceneChecks.Capture(camera,directory+"/"+name+".jpg");}
                        finally{camera.scene=previousScene;}
                    };
                    view("Capital_Overview",new Vector3(235,195,-275),new Vector3(0,20,0));
                    view("Palace_FromStreet",new Vector3(-10,1.75f,-128),new Vector3(0,35,12));
                    view("GiantBlocks_AtEyeLevel",new Vector3(3.8f,5.75f,-55),new Vector3(3.5f,6.7f,-45.3f));
                    view("ThickEntrance",new Vector3(0,5.75f,-58),new Vector3(0,6,-32));
                    view("StonePassage",new Vector3(0,5.75f,-30),new Vector3(0,6,-5));
                    scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PalaceSkylight>(true)).Single().SetOpening(1);
                    view("RoyalChamber",new Vector3(-5,5.75f,9),new Vector3(0,8.3f,31));
                    view("SkyShaft",new Vector3(0,5.75f,23.5f),new Vector3(-.85f,77,24.8f));
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        public static void BuildNative()
        {
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=TravellerStage.SceneNames.Select(n=>TravellerSceneBuilder.SceneFolder+"/"+n+".unity").ToArray(),
                locationPathName="Builds/MonumentalCapitalPreview.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            File.WriteAllText(MasonryArchitectureLibrary.Docs+"/build.json","{\"result\":\""+result.summary.result+"\",\"errors\":"+result.summary.totalErrors+",\"warnings\":"+result.summary.totalWarnings+"}");
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Capital preview build failed.");
        }
        public static void ExportKit()
        {
            Audit();AssetDatabase.ExportPackage(new[]{MasonryArchitectureLibrary.Root+"/Prefabs",MasonryArchitectureLibrary.Root+"/Documentation"},"Builds/MonumentalMasonryKit_20261031.unitypackage",ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
        }
        public static void RebuildMasonry()
        {
            TravellerSceneBuilder.UpgradePalaceToAuthoredMasonry();
            Capture();Audit();BuildNative();ExportKit();AssetDatabase.SaveAssets();
        }
        // Invoked by the project-matched Editor in batch mode only when no Editor owns the project.
        public static void BatchPrepare()
        {
            Directory.CreateDirectory(MasonryArchitectureLibrary.Docs);File.WriteAllText(MasonryArchitectureLibrary.Docs+"/batch-status.txt","preparing");
            HunyuanEnvironmentLibrary.PrepareManifest("Documentation/EnvironmentProduction/2026-10-31/manifest.json","Assets/_Project/Art/HunyuanEnvironment20261031","Documentation/EnvironmentProduction/2026-10-31/unity-validation.json",6,1,true);
            EnvironmentExpansionLibrary.ImportAvailable();EnvironmentLibraryAudit.Audit(EnvironmentExpansionLibrary.Docs+"/ready-validation.json",EnvironmentExpansionLibrary.Docs+"/saved-prefab-audit.json");
            EnvironmentExpansionLibrary.BuildYard();EnvironmentExpansionLibrary.BuildPreview();Capture();Audit();BuildNative();ExportKit();
            AssetDatabase.SaveAssets();File.WriteAllText(MasonryArchitectureLibrary.Docs+"/batch-status.txt","completed");
        }
    }
}
