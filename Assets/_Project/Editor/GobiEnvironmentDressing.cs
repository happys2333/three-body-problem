using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    // A local, repeatable art pass. Never rebuilds the story or its profiles.
    public static class GobiEnvironmentDressing
    {
        public const string ScenePath = "Assets/_Project/Scenes/Traveller/01_GobiJourney.unity";
        public const string Docs = "Documentation/GobiIntegration";
        private const string Prefabs = "Assets/_Project/Art/EnvironmentReady/Prefabs/";
        private const string Derived = "Assets/_Project/Art/EnvironmentReady/GobiDressing";
        private const string RootName = "Prepared Gobi landscape";
        private static Terrain terrain;
        private static Transform root;
        private static readonly Vector2[] Route = {
            new Vector2(0,-35),new Vector2(-7,-10),new Vector2(-15,5),new Vector2(-13,12),
            new Vector2(-15,18),new Vector2(-14,26),new Vector2(-12,31),new Vector2(2,42),new Vector2(2,64),new Vector2(0,97),
            new Vector2(0,128),new Vector2(0,190),new Vector2(0,650)
        };
        [Serializable] private class Placement { public string asset,zone; public Vector3 position,scale;public float yaw; }
        [Serializable] private class LayoutReport {
            public string scene;public int instances,assetTypes;public List<Placement> placements = new List<Placement>();
            public List<string> disabledLegacy = new List<string>();public List<string> errors = new List<string>();public bool passed;
        }
        private static LayoutReport report;

        [MenuItem("Tools/Three Body Traveller/Gobi/Arrange Prepared Landscape")]
        public static string Arrange()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before arranging the landscape.");
            var scene=SceneManager.GetActiveScene();
            if(scene.isDirty)throw new InvalidOperationException("Save the current scene before applying the art pass.");
            if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
            var stage=Object.FindFirstObjectByType<TravellerStage>();
            var world=stage.transform.Find("Environment");
            terrain=world.GetComponentInChildren<Terrain>();
            Directory.CreateDirectory(Derived);Directory.CreateDirectory(Docs+"/Previews");
            AssetDatabase.Refresh();
            report=new LayoutReport{scene=ScenePath};
            var previous=world.Find(RootName);if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            root=new GameObject(RootName).transform;root.SetParent(world,false);

            foreach(Transform old in world)
            {
                if(old.name==RootName)continue;
                if(old.name.StartsWith("Scanned weathered outcrop",StringComparison.Ordinal)||
                   old.name.StartsWith("Scanned distant escarpment",StringComparison.Ordinal)||
                   old.name=="Departure pass west"||old.name=="Departure pass east"||
                   old.name=="Traveller's satchel"||old.name.StartsWith("Observation waystone",StringComparison.Ordinal)||
                   old.name=="Departure waystone"||old.name=="Road direction marker"||old.name=="Bronze observation stud")
                {old.gameObject.SetActive(false);report.disabledLegacy.Add(old.name);}
            }
            // Keep the shelter anchor referenced by the journey; replace only its old stone geometry.
            var shelter=world.Find("Giant rock shelter");
            foreach(Transform child in shelter)
                if(child!=stage.shelterCenter){child.gameObject.SetActive(false);report.disabledLegacy.Add("Shelter/"+child.name);}

            PaintRoad();
            // Broad western mass and an eroded cap replace the symmetric dolmen silhouette.
            Place("RockOverhang","Shelter western outcrop",-31,22,1.04f,-23,2f);
            var roof=Place("RockOverhang","Shelter continuous canopy",-13,19,1,0,0);
            roof.transform.position=new Vector3(-13,4.2f,19);roof.transform.localScale=new Vector3(1.02f,.9f,.92f);
            for(int lod=0;lod<3;lod++)
            {
                var filter=roof.transform.Find("LOD "+lod).GetComponent<MeshFilter>();
                filter.sharedMesh=CanopyMesh(filter.sharedMesh,lod);
            }
            roof.GetComponentInChildren<MeshCollider>().sharedMesh=CanopyMesh(null,1,false);
            roof.GetComponent<LODGroup>().RecalculateBounds();
            var support=Place("WindErodedCliff","Shelter eastern spur",-.7f,25,.44f,160,1.4f);
            support.transform.localScale=new Vector3(.40f,.40f,.50f);
            Place("GobiRockCluster","Shelter talus",-25,7,1.15f,71,.2f);
            Place("GobiRockCluster","Shelter talus",-4,27,.72f,205,.16f);
            Place("CaravanSupplies","Traveller camp",-17.5f,11.9f,1,28,.015f);
            Place("CaravanSupplies","Traveller camp",-19.0f,15.5f,.78f,152,.015f);

            // Rim peaks frame the sky rather than filling the observation cone.
            Place("GobiRidge","Western skyline",-177,145,1.05f,38,5,true);
            Place("GobiMountainPeak","Western skyline",-263,245,1.15f,-12,6,true);
            Place("GobiRidge","Western skyline",-192,356,1.22f,91,7,true);
            Place("GobiMountainPeak","Eastern skyline",171,205,.95f,213,5,true);
            Place("GobiRidge","Eastern skyline",255,338,1.10f,168,7,true);
            Place("GobiMountainPeak","Southern skyline",-185,-138,.85f,37,4,true);
            Place("GobiRidge","Southern skyline",178,-142,.94f,128,6,true);
            // Low distant saddle leaves moving stars at x=-63..58, z=175..185 unobstructed.
            Place("YardangMesa","Northern saddle",-85,254,.70f,31,2,true);
            Place("YardangMesa","Northern saddle",81,282,.90f,148,3,true);
            Place("GobiRidge","Far western ridge",-345,434,1.3f,43,8,true);
            Place("GobiRidge","Far eastern ridge",340,477,1.2f,201,8,true);

            Place("YardangMesa","Western valley shoulder",-69,67,.9f,58,2,true);
            Place("YardangMesa","Eastern valley shoulder",73,103,1.05f,147,2,true);
            Place("WindErodedCliff","Departure narrows",-25,155,.88f,28,1.2f);
            Place("WindErodedCliff","Departure narrows",29,176,1.12f,207,1.8f);
            Place("WindErodedCliff","Western valley shoulder",-48,-18,.72f,115,1.5f);
            Place("WindErodedCliff","Eastern valley shoulder",49,25,.83f,263,1.7f);
            Place("WindErodedCliff","Eastern valley shoulder",59,138,.76f,174,1.1f);

            // Three readable stations and a few roadside stone cairns.
            Place("GobiWaystone","Approach marker",-7,-22,.76f,9,.06f);
            Place("GobiWaystone","First observation",6,42,.82f,12,.06f);
            Place("GobiWaystone","Second observation",6,64,.94f,-6,.06f);
            Place("GobiWaystone","Departure marker",5,127,1.18f,16,.08f);
            Place("GobiRockCluster","First observation",8.3f,44,.35f,40,.08f);
            Place("GobiRockCluster","Second observation",8.4f,66,.43f,154,.08f);

            var random=new System.Random(20261028);
            var patches=new[]{new Vector2(-16,-32),new Vector2(13,-16),new Vector2(-30,-5),
                new Vector2(24,14),new Vector2(-31,38),new Vector2(20,56),new Vector2(-20,72),
                new Vector2(23,88),new Vector2(-20,113),new Vector2(20,131),new Vector2(-39,145),new Vector2(39,165)};
            foreach(var patch in patches)
            {
                Place("GobiRockCluster","Roadside erosion",patch.x,patch.y,.45f+(float)random.NextDouble()*.58f,(float)random.NextDouble()*360,.16f);
                for(int i=0;i<4;i++)
                {
                    float x=patch.x+((float)random.NextDouble()-.5f)*12;
                    float z=patch.y+((float)random.NextDouble()-.5f)*15;
                    if(RouteDistance(new Vector2(x,z))<4.5f)continue;
                    Place("DesertThornBush","Sparse desert vegetation",x,z,.55f+(float)random.NextDouble()*.65f,(float)random.NextDouble()*360,.035f);
                }
                if(RouteDistance(patch)>6)
                    Place("DeadTamarisk","Wind-shaped tamarisk",patch.x-2.5f,patch.y+4,.72f+(float)random.NextDouble()*.4f,(float)random.NextDouble()*360,.06f);
            }
            Physics.SyncTransforms();
            Validate(stage);
            for(int i=0;i<report.placements.Count;i++)
            {report.placements[i].position=root.GetChild(i).position;report.placements[i].scale=root.GetChild(i).localScale;}
            report.instances=report.placements.Count;report.assetTypes=report.placements.Select(p=>p.asset).Distinct().Count();
            report.passed=report.errors.Count==0;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Docs+"/layout.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException(string.Join("\n",report.errors));
            return "Saved Gobi: "+report.instances+" placements using "+report.assetTypes+" prepared asset types.";
        }

        private static GameObject Place(string id,string zone,float x,float z,float scale,float yaw,float bury,bool backdrop=false)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+id+".prefab");
            if(prefab==null)throw new FileNotFoundException(id);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name=zone+" / "+id;instance.transform.SetParent(root,false);
            instance.transform.localScale=Vector3.one*scale;instance.transform.rotation=Quaternion.Euler(0,yaw,0);
            float y=terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y;
            if(backdrop)
            {
                var size=prefab.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds.size*scale;
                for(int ix=-1;ix<=1;ix++)for(int iz=-1;iz<=1;iz++)
                    y=Mathf.Min(y,terrain.SampleHeight(new Vector3(x+ix*size.x*.38f,0,z+iz*size.z*.38f))+terrain.transform.position.y);
                if(Mathf.Abs(x)>120||z>220)
                {
                    foreach(var renderer in instance.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.Off;
                    foreach(var collider in instance.GetComponentsInChildren<Collider>())collider.enabled=false;
                }
            }
            instance.transform.position=new Vector3(x,y-bury,z);
            foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.isStatic=true;
            report.placements.Add(new Placement{asset=id,zone=zone,position=instance.transform.position,scale=instance.transform.localScale,yaw=yaw});
            return instance;
        }

        private static Mesh CanopyMesh(Mesh source,int lod,bool render=true)
        {
            string path=Derived+"/ShelterCanopy_"+(render?"LOD"+lod:"Collision")+".asset";
            var copy=new Mesh{name="Eroded shelter canopy LOD "+lod,indexFormat=IndexFormat.UInt32};
            using(var reader=new BinaryReader(File.OpenRead("ArtSource/Environment/GobiDressing/Canopy_LOD"+lod+".meshbin")))
            {
                if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4))!="TBM1")throw new InvalidDataException("Canopy mesh header.");
                int count=reader.ReadInt32(),indices=reader.ReadInt32();reader.ReadInt32();
                var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var triangles=new int[indices];
                for(int i=0;i<count;i++){float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle();vertices[i]=new Vector3(x,z,-y);}
                for(int i=0;i<count;i++){float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle();normals[i]=new Vector3(x,z,-y);}
                for(int i=0;i<count;i++)uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                for(int i=0;i<indices;i++)triangles[i]=reader.ReadInt32();
                copy.vertices=vertices;copy.normals=normals;copy.uv=uv;
                copy.triangles=triangles;
                copy.RecalculateBounds();copy.RecalculateTangents();
                var tangents=copy.tangents;
                for(int i=0;i<count;i++)if(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z).sqrMagnitude<.01f)
                {var t=Vector3.Cross(Mathf.Abs(normals[i].y)>.95f?Vector3.right:Vector3.up,normals[i]).normalized;tangents[i]=new Vector4(t.x,t.y,t.z,1);}
                copy.tangents=tangents;
            }
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(copy,path);return copy;}
            EditorUtility.CopySerialized(copy,saved);
            // CopySerialized updates CPU data but may leave the Editor's existing GPU buffers stale.
            saved.vertices=copy.vertices;saved.normals=copy.normals;saved.uv=copy.uv;saved.tangents=copy.tangents;saved.triangles=copy.triangles;
            saved.UploadMeshData(false);EditorUtility.SetDirty(saved);
            Object.DestroyImmediate(copy);return saved;
        }

        private static void PaintRoad()
        {
            string path=Derived+"/GobiDressedTerrain.asset";
            var source=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Art/Terrain/GobiErodedValley.asset");
            var data=Object.Instantiate(source);data.name="Gobi valley with worn caravan trail";
            var heights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
            for(int z=0;z<data.heightmapResolution;z++)for(int x=0;x<data.heightmapResolution;x++)
            {
                float worldZ=terrain.transform.position.z+z*data.size.z/(data.heightmapResolution-1);
                if(worldZ<160)continue;
                float worldX=terrain.transform.position.x+x*data.size.x/(data.heightmapResolution-1);
                float halfWidth=30+(worldZ-160)*.08f;
                float blend=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(halfWidth,halfWidth+65,Mathf.Abs(worldX))))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(160,215,worldZ));
                float floor=(Mathf.Max(0,worldZ-180)*.025f-terrain.transform.position.y)/data.size.y;
                heights[z,x]=Mathf.Lerp(heights[z,x],Mathf.Min(heights[z,x],floor),blend);
            }
            data.SetHeights(0,0,heights);
            var alpha=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);
            for(int z=0;z<data.alphamapHeight;z++)for(int x=0;x<data.alphamapWidth;x++)
            {
                var point=new Vector2(terrain.transform.position.x+x*data.size.x/(data.alphamapWidth-1),terrain.transform.position.z+z*data.size.z/(data.alphamapHeight-1));
                float road=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.3f,4.5f,RouteDistance(point)));
                float removed=alpha[z,x,2]*.7f;alpha[z,x,2]-=removed;alpha[z,x,0]+=removed;
                for(int layer=0;layer<data.alphamapLayers;layer++)
                    alpha[z,x,layer]=Mathf.Lerp(alpha[z,x,layer],layer==0?.86f:layer==1?.14f:0,road*.8f);
            }
            data.SetAlphamaps(0,0,alpha);
            var saved=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if(saved==null){AssetDatabase.CreateAsset(data,path);saved=data;}
            else{EditorUtility.CopySerialized(data,saved);Object.DestroyImmediate(data);}
            terrain.terrainData=saved;terrain.GetComponent<TerrainCollider>().terrainData=saved;
        }

        private static float RouteDistance(Vector2 point)
        {
            float nearest=float.MaxValue;
            for(int i=1;i<Route.Length;i++)
            {var delta=Route[i]-Route[i-1];float t=Mathf.Clamp01(Vector2.Dot(point-Route[i-1],delta)/delta.sqrMagnitude);nearest=Mathf.Min(nearest,Vector2.Distance(point,Route[i-1]+delta*t));}
            return nearest;
        }

        private static void Validate(TravellerStage stage)
        {
            var controller=stage.player.GetComponent<CharacterController>();bool enabled=controller.enabled;controller.enabled=false;
            try
            {
                foreach(var moment in stage.profile.moments)CheckClear(moment.viewPosition-Vector3.up*stage.player.eyeHeight,"Authored view: "+moment.title);
                for(int i=1;i<Route.Length-1;i++)
                {
                    float distance=Vector2.Distance(Route[i-1],Route[i]);
                    for(float d=0;d<=distance;d+=.8f)
                    {var p=Vector2.Lerp(Route[i-1],Route[i],d/distance);float y=terrain.SampleHeight(new Vector3(p.x,0,p.y))+terrain.transform.position.y;CheckClear(new Vector3(p.x,y+.1f,p.y),"Story route");}
                }
                foreach(int moment in new[]{3,4})
                    for(int star=0;star<stage.profile.moments[moment].flyingStars;star++)
                        foreach(float drift in new[]{-12f,0,12f})
                        {
                            var eye=stage.profile.moments[moment].viewPosition;var target=stage.flyingStars[star].position+Vector3.right*drift;
                            RaycastHit hit;if(Physics.Linecast(eye,target,out hit,~0,QueryTriggerInteraction.Ignore))report.errors.Add("Star sightline blocked by "+hit.collider.name);
                        }
            }
            finally{controller.enabled=enabled;}
        }

        private static void CheckClear(Vector3 feet,string label)
        {
            var hits=Physics.OverlapCapsule(feet+Vector3.up*.35f,feet+Vector3.up*1.5f,.28f,~0,QueryTriggerInteraction.Ignore);
            if(hits.Length>0)report.errors.Add(label+" "+feet+" blocked by "+string.Join(",",hits.Select(h=>h.name)));
        }

        public static string CaptureViews()
        {
            var stage=Object.FindFirstObjectByType<TravellerStage>();
            if(SceneManager.GetActiveScene().path!=ScenePath||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Capture the saved Gobi scene.");
            var sky=stage.skyMaterial;stage.skyMaterial=new Material(sky);
            try
            {
                for(int i=0;i<stage.profile.moments.Length;i++)
                {stage.ApplyMoment(i,true);TravellerSceneChecks.Capture(stage.player.viewCamera,Docs+"/Previews/Moment_"+i+".jpg");}
                stage.ApplyMoment(5,true);stage.player.Teleport(new Vector3(80,90,-100),new Vector3(-8,0,65));
                TravellerSceneChecks.Capture(stage.player.viewCamera,Docs+"/Previews/Layout.jpg");
            }
            finally{Object.DestroyImmediate(stage.skyMaterial);stage.skyMaterial=sky;EditorSceneManager.OpenScene(ScenePath);}
            return Docs+"/Previews";
        }

        public static string BuildPlayer()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before building.");
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=TravellerStage.SceneNames.Select(n=>"Assets/_Project/Scenes/Traveller/"+n+".unity").ToArray(),locationPathName="Builds/GobiDressed.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            File.WriteAllText(Docs+"/build.json",JsonUtility.ToJson(new BuildAudit{result=build.summary.result.ToString(),errors=build.summary.totalErrors,warnings=build.summary.totalWarnings,seconds=build.summary.totalTime.TotalSeconds},true));
            if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Gobi build failed.");
            return Path.GetFullPath("Builds/GobiDressed.app");
        }
        [Serializable] private class BuildAudit{public string result;public int errors,warnings;public double seconds;}
    }
}
