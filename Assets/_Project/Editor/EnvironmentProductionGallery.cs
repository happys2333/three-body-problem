using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem.Editor
{
    public static partial class EnvironmentProduction
    {
        public const string GalleryScene = "Assets/_Project/Scenes/Environment/Environment_ArtYard.unity";
        public const string PreviewBuild = "Builds/EnvironmentArtYard.app";
        [Serializable] private class BuildAudit { public string result,outputPath;public double seconds;public int errors,warnings;public ulong bytes; }
        private static readonly Vector3[] Positions = {
            new Vector3(-190,0,180), new Vector3(160,0,240), new Vector3(-80,0,130), new Vector3(80,0,150),
            new Vector3(-35,0,-30), new Vector3(50,0,100), new Vector3(-10,0,-30), new Vector3(-12,0,-38),
            new Vector3(-5,0,-36), new Vector3(5,0,-36), new Vector3(-18,0,0), new Vector3(18,0,0),
            new Vector3(-18,0,20), new Vector3(18,0,20), new Vector3(0,0,60), new Vector3(0,0,-8),
            new Vector3(-18,0,40), new Vector3(18,0,40), new Vector3(-18,0,60), new Vector3(18,0,60)
        };

        [MenuItem("Tools/Three Body Traveller/Assets/Build Environment Art Yard")]
        public static string BuildArtYard()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var original = SceneManager.GetActiveScene();
            if (original.isDirty) throw new InvalidOperationException("Save the current scene before building the isolated art yard.");
            string returnPath = original.path;
            Directory.CreateDirectory(Path.GetDirectoryName(GalleryScene)); Directory.CreateDirectory(Docs + "/Previews");
            var report = JsonUtility.FromJson<ReadyReport>(File.ReadAllText(Docs + "/ready-validation.json"));
            if (report.assets.Count != 20 || report.assets.Any(a => !a.passed)) throw new InvalidDataException("Repair and validate all 20 prefabs first.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                var yard = new GameObject("Environment Art Yard");
                var preview = yard.AddComponent<ThreeBodyProblem.EnvironmentAssetPreview>();
                preview.stations = new Transform[20]; preview.assetIds = new string[20];
                preview.viewpoints = new Vector3[20]; preview.viewTargets = new Vector3[20];
                SurfaceCache.Clear();
                for (int i = 0; i < report.assets.Count; i++)
                {
                    var entry = report.assets[i];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab));
                    instance.transform.position = Positions[i]; instance.transform.SetParent(yard.transform, true);
                    preview.stations[i] = instance.transform; preview.assetIds[i] = entry.id;
                    if (entry.id == "RammedEarthGatehouse") preview.gate = instance.transform;
                    if (entry.id == "PyramidPortal") preview.portal = instance.transform;
                }
                // Giant rocks can enclose another station's naive front camera. Test clear sight lines.
                var stationBounds=preview.stations.Select(t=>t.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds).ToArray();
                for(int i=0;i<stationBounds.Length;i++)
                {
                    var bounds=stationBounds[i];preview.viewTargets[i]=bounds.center;
                    float radius=Mathf.Max(3,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))*1.8f);
                    bool found=false;
                    foreach(float angle in new[]{24f,0f,-24f,60f,-60f,90f,-90f,135f,-135f,180f})
                    {
                        float radians=angle*Mathf.Deg2Rad;
                        var eye=new Vector3(bounds.center.x+Mathf.Sin(radians)*radius,Mathf.Max(1.65f,bounds.size.y*.52f),bounds.center.z+Mathf.Cos(radians)*radius);
                        bool blocked=false;
                        for(int j=0;j<stationBounds.Length&&!blocked;j++)
                        {
                            if(j==i)continue;
                            var other=stationBounds[j];other.Expand(.4f);
                            if(other.Contains(eye)){blocked=true;break;}
                            foreach(var target in new[]{bounds.center,bounds.center+Vector3.right*bounds.extents.x*.65f,bounds.center-Vector3.right*bounds.extents.x*.65f})
                            {
                                float hit;var delta=target-eye;
                                if(other.IntersectRay(new Ray(eye,delta.normalized),out hit)&&hit<delta.magnitude){blocked=true;break;}
                            }
                        }
                        if(blocked)continue;preview.viewpoints[i]=eye;found=true;break;
                    }
                    if(!found)throw new InvalidOperationException("No clear inspection camera for "+preview.assetIds[i]);
                }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Walkable desert ground";
                floor.isStatic = true;
                var floorMesh=UnityEngine.Object.Instantiate(floor.GetComponent<MeshFilter>().sharedMesh);floorMesh.name="YardGround";
                floorMesh.vertices=floorMesh.vertices.Select(v=>new Vector3(v.x*80,v.y,v.z*80)).ToArray();floorMesh.RecalculateBounds();
                string floorMeshPath=Root+"/Meshes/YardGround.asset";var savedFloorMesh=AssetDatabase.LoadAssetAtPath<Mesh>(floorMeshPath);
                if(savedFloorMesh==null){AssetDatabase.CreateAsset(floorMesh,floorMeshPath);savedFloorMesh=floorMesh;}else{EditorUtility.CopySerialized(floorMesh,savedFloorMesh);UnityEngine.Object.DestroyImmediate(floorMesh);}
                floor.GetComponent<MeshFilter>().sharedMesh=savedFloorMesh;floor.GetComponent<MeshCollider>().sharedMesh=savedFloorMesh;
                var ground = new Material(Shader.Find("Traveller/Environment Surface"));
                const string maps = "Assets/ThirdParty/Environment/PolyHaven/brown_mud_dry";
                ground.mainTexture = PhotoMap(maps, "brown_mud_dry", "diff"); ground.SetTexture("_BumpMap", PhotoMap(maps,"brown_mud_dry","nor_gl"));
                ground.SetTexture("_SurfaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/YardGround_Surface.png"));
                ground.SetFloat("_DetailMetres", 4f); ground.SetFloat("_NormalStrength", .6f); ground.color = new Color(.86f,.80f,.69f);
                string groundPath=Root+"/Materials/YardGround.mat";
                var savedGround=AssetDatabase.LoadAssetAtPath<Material>(groundPath);
                if(savedGround==null){AssetDatabase.CreateAsset(ground,groundPath);savedGround=ground;}else{EditorUtility.CopySerialized(ground,savedGround);UnityEngine.Object.DestroyImmediate(ground);}
                floor.GetComponent<Renderer>().sharedMaterial=savedGround;
                var sun = new GameObject("Daylight").AddComponent<Light>(); sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(43, -35, 0); sun.intensity = 1.25f; sun.color = new Color(1,.95f,.85f); sun.shadows = LightShadows.Soft;
                RenderSettings.sun=sun; RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.50f,.55f,.62f);
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=180;RenderSettings.fogEndDistance=700;RenderSettings.fogColor=new Color(.62f,.68f,.73f);
                var sky = new Material(Shader.Find("Skybox/Procedural")); sky.SetColor("_SkyTint", new Color(.5f,.58f,.7f)); sky.SetFloat("_AtmosphereThickness",1.05f);
                string skyPath=Root+"/Materials/YardSky.mat";var savedSky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if(savedSky==null){AssetDatabase.CreateAsset(sky,skyPath);savedSky=sky;}else{EditorUtility.CopySerialized(sky,savedSky);UnityEngine.Object.DestroyImmediate(sky);}
                RenderSettings.skybox=savedSky;
                var playerObject=new GameObject("First Person Visitor",typeof(CharacterController));
                playerObject.transform.position=new Vector3(0,.05f,-30);
                var capsule=playerObject.GetComponent<CharacterController>();capsule.height=1.8f;capsule.radius=.3f;capsule.center=Vector3.up*.9f;capsule.skinWidth=.03f;capsule.stepOffset=.25f;
                var cameraObject=new GameObject("Visitor Camera",typeof(Camera),typeof(AudioListener));cameraObject.transform.SetParent(playerObject.transform,false);
                var camera=cameraObject.GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=60;camera.nearClipPlane=.08f;camera.farClipPlane=1400;camera.allowHDR=true;
                var player=playerObject.AddComponent<ThreeBodyProblem.TravellerPlayer>();player.viewCamera=camera;player.eyeHeight=1.65f;player.walkSpeed=4;preview.player=player;
                preview.uiFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSansCJK/NotoSansCJKsc-Regular.otf");
                player.Teleport(new Vector3(0,1.7f,-30),new Vector3(0,2,20));
                EditorSceneManager.SaveScene(scene,GalleryScene);AssetDatabase.SaveAssets();
                TravellerSceneChecks.Capture(camera,Docs+"/Previews/ArtYard_Entry.jpg");
                return GalleryScene;
            }
            finally { if(!string.IsNullOrEmpty(returnPath))EditorSceneManager.OpenScene(returnPath); }
        }

        [MenuItem("Tools/Three Body Traveller/Assets/Open Environment Art Yard")]
        public static void OpenArtYard() => EditorSceneManager.OpenScene(GalleryScene);

        [MenuItem("Tools/Three Body Traveller/Assets/Build Standalone Environment Preview")]
        public static string BuildStandalonePreview()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            Directory.CreateDirectory("Builds");
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{GalleryScene},locationPathName=PreviewBuild,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            File.WriteAllText(Docs+"/preview-build.json",JsonUtility.ToJson(new BuildAudit{result=result.summary.result.ToString(),outputPath=result.summary.outputPath,seconds=result.summary.totalTime.TotalSeconds,errors=result.summary.totalErrors,warnings=result.summary.totalWarnings,bytes=result.summary.totalSize},true));
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Environment preview build failed: "+result.summary.result);
            return Path.GetFullPath(PreviewBuild);
        }
    }
}
