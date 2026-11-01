using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class CourtAudienceProduction
    {
        public const string Root=CourtRetinueProduction.Root;
        public const string Docs="Documentation/CourtAudience/2026-11-01";
        private const string Work="ArtSource/CourtAudience/2026-11-01";
        public static void PrepareAssets()
        {
            EraGuardProduction.GuardProject();
            foreach(string folder in new[]{Root+"/Meshes",Root+"/Props",Root+"/Settings",Docs})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            ImportProp("RoyalFeatherFan",true);ImportProp("BambooPetition",false);ImportProp("CeremonialHu",false);
            foreach(string id in new[]{"CourtSteward","RitualAttendant","CalendarAttendant","ChamberAttendant"})Enhance(id);
            string path=Root+"/Settings/Audience.asset";if(AssetDatabase.LoadAssetAtPath<CourtAudienceSettings>(path)==null)AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<CourtAudienceSettings>(),path);
            AssetDatabase.SaveAssets();foreach(string mesh in Directory.GetFiles(Root+"/Meshes","*.asset"))AssetDatabase.ImportAsset(mesh,ImportAssetOptions.ForceUpdate);
        }
        private static Material Surface(string id,Color color,float metal=0)
        {
            string path=Root+"/Materials/"+id+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Traveller/Palace Craft Surface"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_Color",color);m.SetColor("_Secondary",color*.72f);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",metal>0?.28f:.16f);m.SetFloat("_Hide",0);m.SetFloat("_PatternScale",60);EditorUtility.SetDirty(m);return m;
        }
        private static void ImportProp(string id,bool fan)
        {
            var materials=new[]{Surface("Dark hardwood",new Color(.26f,.15f,.065f)),Surface("Fan bronze",new Color(.37f,.25f,.105f),.8f),
                Surface("Warm ivory feathers",new Color(.78f,.72f,.52f)),Surface("Pheasant quills and binding",new Color(.19f,.115f,.05f))};
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject(id);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);var lods=new LOD[3];
                for(int i=0;i<3;i++)
                {
                    var mesh=ReadMesh(Work+"/"+id+"/"+id+"_LOD"+i+".meshbin",id+"_LOD"+i);
                    var child=new GameObject("LOD"+i,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(root.transform,false);child.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var r=child.GetComponent<MeshRenderer>();r.sharedMaterials=materials;r.shadowCastingMode=i<2?ShadowCastingMode.On:ShadowCastingMode.Off;
                    lods[i]=new LOD(i==0?.20f:i==1?.055f:.006f,new Renderer[]{r});
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(root,Root+"/Props/"+id+".prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        private static Mesh ReadMesh(string path,string id)
        {
            using(var r=new BinaryReader(File.OpenRead(path)))
            {
                if(System.Text.Encoding.ASCII.GetString(r.ReadBytes(4))!="TBM1")throw new InvalidDataException(path);
                int n=r.ReadInt32(),count=r.ReadInt32(),slots=r.ReadInt32();if(slots!=4)throw new InvalidDataException(path);
                var v=new Vector3[n];var normals=new Vector3[n];var uv=new Vector2[n];
                for(int i=0;i<n;i++){float x=r.ReadSingle(),y=r.ReadSingle(),z=r.ReadSingle();v[i]=new Vector3(x,z,-y);}
                for(int i=0;i<n;i++){float x=r.ReadSingle(),y=r.ReadSingle(),z=r.ReadSingle();normals[i]=new Vector3(x,z,-y).normalized;}
                for(int i=0;i<n;i++)uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                var all=new int[count];for(int i=0;i<count;i++)all[i]=r.ReadInt32();var triangles=Enumerable.Range(0,4).Select(_=>new List<int>()).ToArray();
                for(int i=0;i<count/3;i++){int slot=r.ReadInt32();triangles[slot].AddRange(new[]{all[i*3],all[i*3+1],all[i*3+2]});}
                var mesh=new Mesh{name=id,indexFormat=IndexFormat.UInt32,vertices=v,normals=normals,uv=uv,subMeshCount=4};for(int i=0;i<4;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();
                string output=Root+"/Meshes/"+id+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(output);if(saved==null){AssetDatabase.CreateAsset(mesh,output);return mesh;}
                EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
            }
        }
        private static void Enhance(string id)
        {
            string path=Root+"/Prefabs/"+id+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool fan=id=="RitualAttendant"||id=="ChamberAttendant";
                var actor=root.GetComponent<TravellerActor>();var hands=root.GetComponent<CourtPropGrip>();if(hands==null)hands=root.AddComponent<CourtPropGrip>();
                GuardGripAuthoring.PrepareCourtHands(actor,hands,id,fan);
                if(fan)
                {
                    var prop=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Props/RoyalFeatherFan.prefab"),root.scene);
                    prop.transform.SetParent(root.transform,false);prop.name="Long handled feather fan";
                    var bearer=root.AddComponent<RoyalFanBearer>();bearer.fan=prop.transform;bearer.side=id=="RitualAttendant"?1:-1;bearer.phase=id=="RitualAttendant"?0:Mathf.PI*.7f;
                    prop.transform.localPosition=bearer.gripPosition;prop.transform.localRotation=Quaternion.Euler(0,0,bearer.RollDegrees);
                }
                else
                {
                    CourtCostumeProduction.Add(actor,id,id=="CalendarAttendant"?new Color(.29f,.105f,.065f):new Color(.10f,.17f,.235f));
                    if(id=="CourtSteward")
                    {
                        var prop=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Props/CeremonialHu.prefab"),root.scene);
                        prop.transform.SetParent(root.transform,false);prop.transform.localPosition=new Vector3(0,1.12f,.34f);prop.transform.localRotation=Quaternion.Euler(65,0,0);
                        root.AddComponent<CeremonialTabletBearer>().tablet=prop.transform;hands.rightContact=new Vector3(.035f,0,0);hands.leftContact=new Vector3(-.035f,0,0);
                    }
                }
                var group=root.GetComponent<LODGroup>();group.SetLODs(new[]{new LOD(.012f,root.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<Renderer>().ToArray())});group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Apply(TravellerStage stage,Transform district)
        {
            var cast=stage.GetComponent<TravellerCastDirector>();var companions=new[]{cast.companion,cast.king};
            foreach(var actor in companions)
            {
                var hands=actor.GetComponent<CourtPropGrip>();if(hands==null)hands=actor.gameObject.AddComponent<CourtPropGrip>();
                GuardGripAuthoring.PrepareCourtHands(actor,hands,actor.role==TravellerRole.WenWang?"JiChang":"KingZhou",false);
            }
            var courier=district.GetComponentsInChildren<CourtRetainer>().Single(r=>r.stationId=="CalendarAttendant").GetComponent<TravellerActor>();
            var settings=AssetDatabase.LoadAssetAtPath<CourtAudienceSettings>(Root+"/Settings/Audience.asset");
            var points=new[]{new Vector3(-2.7f,0,24.6f),new Vector3(-2.7f,0,27.95f),new Vector3(-2.7f,0,29.15f),new Vector3(-2.7f,0,30.35f),new Vector3(-2.7f,0,31.55f),new Vector3(-.65f,0,32.1f)};
            settings.ascent=points.Select(p=>{var h=Physics.RaycastAll(new Vector3(p.x,11.8f,p.z),Vector3.down,12).Where(x=>x.normal.y>.7f&&(x.collider.name=="Royal stone dais step"||x.collider.name=="Enclosed palace stone floor")).OrderByDescending(x=>x.point.y).First();return new Vector3(p.x,h.point.y+.01f,p.z);}).ToArray();EditorUtility.SetDirty(settings);
            var motor=courier.GetComponent<CharacterController>();if(motor==null)motor=courier.gameObject.AddComponent<CharacterController>();motor.height=1.73f;motor.radius=.26f;motor.center=Vector3.up*.865f;motor.stepOffset=.55f;motor.skinWidth=.02f;motor.slopeLimit=55;motor.minMoveDistance=0;motor.enabled=false;
            var item=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Props/BambooPetition.prefab"),district.gameObject.scene);item.transform.SetParent(district,false);item.name="Presented bamboo petition";item.SetActive(false);
            var root=new GameObject("Court petition presentation");root.transform.SetParent(district,false);var sequence=root.AddComponent<CourtAudienceSequence>();
            sequence.stage=stage;sequence.settings=settings;sequence.companion=cast.companion;sequence.king=cast.king;sequence.officer=courier;
            sequence.companionHands=cast.companion.GetComponent<CourtPropGrip>();sequence.kingHands=cast.king.GetComponent<CourtPropGrip>();sequence.officerHands=courier.GetComponent<CourtPropGrip>();sequence.petition=item.transform;sequence.motor=motor;sequence.restingCapsule=courier.GetComponent<CapsuleCollider>();
            AssetDatabase.SaveAssets();foreach(var file in Directory.GetFiles(Root+"/Meshes","*.asset"))AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceUpdate);
        }
    }
}
