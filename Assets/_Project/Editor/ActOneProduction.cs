using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class ActOneProduction
    {
        public const string Root="Assets/_Project/Art/ActOne";
        public const string Docs="Documentation/ActOne";
        private static Material[] surfaces;
        private static Material particles;
        private static GameObject torch,ash,bushFire;
        [MenuItem("Tools/Three Body Traveller/Act One/Prepare Story Resources And Integrate")]
        public static string Prepare()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play and save the current scene first.");
            foreach(var folder in new[]{Root+"/Meshes",Root+"/Materials",Root+"/Prefabs","Assets/_Project/Dialogue/Personas","Assets/_Project/Settings/ActOne",Docs+"/Previews"})Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            surfaces=new[]{Surface("TorchWood","Wood",new Color(.40f,.25f,.13f),.13f),Surface("PitchLinen","Thatch",new Color(.35f,.25f,.15f),.045f),Surface("Charcoal","Rock",new Color(.055f,.045f,.035f),.10f),Surface("Ash","Rock",new Color(.35f,.32f,.28f),.18f)};
            particles=SaveMaterial(new Material(Shader.Find("Traveller/Atmosphere Particle")){name="Flame and smoke"},Root+"/Materials/Flame.mat");
            torch=BuildProp("Torch");ash=BuildProp("Ash");bushFire=BuildFire();
            var story=GetOrCreate<GobiStorySettings>("Assets/_Project/Settings/ActOne/GobiStory.asset");
            story.suddenSunsetSeconds=2;story.minimumNightWalkSeconds=12;story.minimumNightWalkMetres=35;story.departureGoal=new Vector3(2,0,64);
            story.maximumHealth=100;story.sunlightDamage=10;story.sunlightDamageInterval=1;story.burnSeconds=32;story.torchPrefab=torch;story.ashPrefab=ash;story.vegetationFirePrefab=bushFire;
            story.callLine=Audio("Call");story.meetingLine=Audio("Meeting");story.warningLine=Audio("Warning");story.shelterLine=Audio("Shelter");story.identityLine=Audio("Identity");story.hourglassLine=Audio("Hourglass");story.departureLine=Audio("Departure");EditorUtility.SetDirty(story);
            var dialogue=GetOrCreate<NpcDialogueSettings>("Assets/_Project/Settings/ActOne/Dialogue.asset");dialogue.sharedPrompt=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Dialogue/Prompts/shared.txt");EditorUtility.SetDirty(dialogue);
            var personas=new Dictionary<string,NpcPersona>();
            var ids=new[]{"wenwang","king","pope","scholar","davinci","shang_guard","church_guard","citizen"};
            var names=new[]{"周文王","纣王","教皇","学者","达芬奇","朝歌卫兵","守卫","居民"};
            for(int i=0;i<ids.Length;i++)
            {var p=GetOrCreate<NpcPersona>("Assets/_Project/Dialogue/Personas/"+ids[i]+".asset");p.characterId=ids[i];p.displayName=names[i];p.prompt=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Dialogue/Prompts/"+(p.IsMajorCharacter?ids[i]:"bystander")+".txt");personas.Add(ids[i],p);EditorUtility.SetDirty(p);}
            int carriers=0,fuelCount=0;
            foreach(var name in TravellerStage.SceneNames)
            {
                var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Traveller/"+name+".unity");
                var stage=Object.FindFirstObjectByType<TravellerStage>();
                var npc=GetOrAdd<NpcDialogueDirector>(stage.gameObject);npc.settings=dialogue;GetOrAdd<PlayerMicrophoneInput>(stage.gameObject);
                foreach(var actor in stage.GetComponentsInChildren<TravellerActor>(true))
                {
                    var bearer=GetOrAdd<TorchBearer>(actor.gameObject);bearer.torchPrefab=torch;carriers++;
                    var controller=actor.animator.runtimeAnimatorController as AnimatorController;
                    if(controller!=null){var layers=controller.layers;foreach(var layer in layers)layer.iKPass=true;controller.layers=layers;EditorUtility.SetDirty(controller);}
                    if(actor.role!=TravellerRole.PlayerTraveller)
                    {var identity=GetOrAdd<NpcIdentity>(actor.gameObject);identity.persona=personas[PersonaId(actor.role)];identity.interactive=true;}
                }
                if(stage.profile.sceneIndex==0)
                {
                    stage.profile.gobiStory=story;stage.profile.transitionSeconds=2;
                    SetNight(stage.profile.moments[0],"夜色 · 戈壁相遇",0);
                    stage.profile.moments[0].caption="旅人：一个人在夜里赶路？举着火把，跟我走。";
                    SetNight(stage.profile.moments[2],"骤落 · 重返黑夜",0);
                    SetNight(stage.profile.moments[5],"夜行 · 前往朝歌",1);
                    stage.profile.moments[5].description="太阳突然沉落。跟随火光走过夜路，旅程随后过渡到数日后的朝歌。";
                    stage.profile.moments[5].objective="跟随周文王走过夜路。";
                    EditorUtility.SetDirty(stage.profile);
                    GetOrAdd<GobiSequenceController>(stage.gameObject).settings=story;
                    GetOrAdd<GobiVegetationFire>(stage.gameObject).settings=story;
                    var exposure=GetOrAdd<SunExposureSystem>(stage.gameObject);exposure.settings=story;
                    var feedback=GetOrAdd<SunDamageFeedback>(stage.player.viewCamera.gameObject);feedback.exposure=exposure;feedback.player=stage.player;feedback.shader=Shader.Find("Hidden/Traveller/Sun Damage");
                    foreach(var fire in stage.fires)if(fire!=null)fire.gameObject.SetActive(false);stage.fires=new ParticleSystem[0];
                    var world=stage.transform.Find("Environment");
                    foreach(Transform candidate in world)
                        if(candidate.name=="Dry scrub"||candidate.name=="Photographic dry grass")fuelCount+=AddFuel(candidate.gameObject);
                    var dressed=world.Find("Prepared Gobi landscape");
                    foreach(Transform candidate in dressed)
                        if(candidate.name.Contains("DesertThornBush")||candidate.name.Contains("DeadTamarisk"))fuelCount+=AddFuel(candidate.gameObject);
                    // A small shaded sample makes the protected-vegetation contrast visible from the conversation spot.
                    var sample=dressed.Find("Sheltered dry shrub");
                    if(sample==null)
                    {
                        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/EnvironmentReady/Prefabs/DesertThornBush.prefab");
                        sample=((GameObject)PrefabUtility.InstantiatePrefab(source)).transform;sample.name="Sheltered dry shrub";sample.SetParent(dressed,false);sample.position=new Vector3(-20,0,15);sample.localScale=Vector3.one*.7f;
                    }
                    fuelCount+=AddFuel(sample.gameObject);
                    var nightSky=SaveMaterial(new Material(stage.skyMaterial){name="Gobi night opening sky"},Root+"/Materials/GobiSky.mat");stage.skyMaterial=nightSky;
                    stage.ApplyMoment(0,true);EditorUtility.SetDirty(nightSky);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            PlayerSettings.macOS.microphoneUsageDescription="按住说话时，用麦克风把你的话转为可编辑文字，与旅人交流。";
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(GobiEnvironmentDressing.ScenePath);
            UpdateOpening();
            File.WriteAllText(Docs+"/resources.json","{\"torchCarriers\":"+carriers+",\"vegetationFuel\":"+fuelCount+",\"aiDirectedOpening\":true,\"maximumOpeningAnswers\":3,\"sunlightDamage\":10,\"health\":100,\"nightOpening\":true}");
            return "Act one integrated: "+carriers+" torch carriers, "+fuelCount+" burnable plants.";
        }
        [MenuItem("Tools/Three Body Traveller/Act One/Update Desert Awakening")]
        public static string UpdateOpening()
        {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play and save the current scene first.");
            var scene=EditorSceneManager.OpenScene(GobiEnvironmentDressing.ScenePath);
            var stage=Object.FindFirstObjectByType<TravellerStage>();var story=stage.profile.gobiStory;
            story.wakeSeconds=3.5f;story.minimumOpeningTalkSeconds=8;story.minimumShelterTalkSeconds=20;story.sunriseSeconds=3;story.suddenSunsetSeconds=2;
            story.meetingPosition=new Vector3(-6,0,-2);story.investigationGoal=new Vector3(7,0,-19);story.discoverDistance=20;story.greetingDistance=4.2f;story.minimumExplorationMetres=4;
            story.narrativeCheckSeconds=12;story.offlineOpeningSilenceSeconds=24;story.offlineShelterSilenceSeconds=60;
            story.callLine=Audio("Call");story.meetingLine=Audio("Meeting");story.warningLine=Audio("Warning");story.shelterLine=Audio("Shelter");story.identityLine=Audio("Identity");story.departureLine=Audio("Departure");
            var profile=stage.profile;profile.moments[0].title="苏醒 · 陌生的荒漠";profile.moments[0].caption="";profile.moments[0].objective="调查四周，看看发生了什么事";
            profile.subtitle="荒漠苏醒 · 火光相遇 · 巨日避险 · 夜行朝歌";
            profile.moments[0].description="在夜色中的戈壁醒来。四周空阔，散落着枯木；远处陌生旅人的火把是唯一的人迹。";
            profile.moments[0].viewTarget=new Vector3(30,2,-17);profile.moments[0].sunA=new Vector3(.3f,-.32f,1);
            profile.moments[2].sunA=profile.moments[0].sunA;profile.moments[2].sunRadii=profile.moments[1].sunRadii;
            profile.moments[5].objective="与姬昌一同前往朝歌";
            var court=AssetDatabase.LoadAssetAtPath<TravellerSceneProfile>("Assets/_Project/Settings/Traveller/02_ChaogeCourt.asset");
            var medieval=AssetDatabase.LoadAssetAtPath<TravellerSceneProfile>("Assets/_Project/Settings/Traveller/03_MedievalTribunal.asset");
            court.chapter="第二幕 / 朝歌 · 殷商王都";medieval.chapter="第三幕 / 中世纪文明";EditorUtility.SetDirty(court);EditorUtility.SetDirty(medieval);
            var dialogue=stage.GetComponent<NpcDialogueDirector>().settings;dialogue.autoSubmitMicrophone=true;
            foreach(var actor in stage.GetComponentsInChildren<TravellerActor>())if(actor.role==TravellerRole.WenWang)actor.transform.position=story.meetingPosition;
            var environment=stage.transform.Find("Environment");var landmarks=environment.Find("Opening deadwood");
            if(landmarks==null)
            {
                landmarks=new GameObject("Opening deadwood").transform;landmarks.SetParent(environment,false);
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/EnvironmentReady/Prefabs/DeadTamarisk.prefab");
                var points=new[]{new Vector3(10,0,-18),new Vector3(-12,0,-31),new Vector3(5,0,-27)};
                var terrain=stage.GetComponentInChildren<Terrain>();
                for(int i=0;i<points.Length;i++)
                {
                    var tree=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);tree.name="Awakening weathered tamarisk "+i;tree.transform.SetParent(landmarks,false);
                    var point=points[i];if(terrain!=null)point.y=terrain.SampleHeight(point)+terrain.transform.position.y;
                    tree.transform.position=point;tree.transform.localScale=Vector3.one*(i==0?1.15f:.8f);tree.transform.rotation=Quaternion.Euler(0,73*i,0);AddFuel(tree);
                }
            }
            var nearTree=landmarks.Find("Awakening weathered tamarisk 2");
            if(nearTree!=null){var point=new Vector3(5,0,-27);var floor=stage.GetComponentInChildren<Terrain>();if(floor!=null)point.y=floor.SampleHeight(point)+floor.transform.position.y;nearTree.position=point;}
            EditorUtility.SetDirty(story);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(dialogue);stage.ApplyMoment(0,true);
            PlayerSettings.macOS.microphoneUsageDescription="按住 F 时录音，松开后转写并向提问开始时看向的人物提问；也可以打字交谈。";
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Desert awakening, deadwood, traveller rendezvous and AI story settings saved.";
        }
        private static int AddFuel(GameObject go)
        {
            if(!go.activeInHierarchy)return 0;var fuel=GetOrAdd<VegetationFuel>(go);fuel.foliage=go.GetComponentsInChildren<Renderer>(true);fuel.solids=go.GetComponentsInChildren<Collider>(true);return 1;
        }
        private static string PersonaId(TravellerRole role)
        {
            switch(role){case TravellerRole.WenWang:return "wenwang";case TravellerRole.KingZhou:return "king";case TravellerRole.Pope:return "pope";case TravellerRole.Scholar:return "scholar";case TravellerRole.DaVinci:return "davinci";case TravellerRole.ShangGuard:return "shang_guard";case TravellerRole.ChurchGuard:return "church_guard";default:return "citizen";}
        }
        private static void SetNight(SceneMoment m,string title,int flying)
        {
            m.title=title;m.sunCount=0;m.heat=0;m.stars=1;m.flyingStars=flying;m.sunA=new Vector3(.3f,-.3f,1);
            m.zenith=new Color(.005f,.012f,.032f);m.horizon=new Color(.035f,.065f,.105f);m.fog=new Color(.035f,.055f,.08f);
            m.ambient=new Color(.09f,.115f,.16f);m.lightIntensity=.10f;m.lightColor=new Color(.4f,.58f,.85f);m.fogDensity=.0015f;
        }
        private static AudioClip Audio(string id)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/ActOne/"+id+".wav");
        private static T GetOrAdd<T>(GameObject go) where T:Component {var value=go.GetComponent<T>();return value!=null?value:go.AddComponent<T>();}
        private static T GetOrCreate<T>(string path) where T:ScriptableObject {var value=AssetDatabase.LoadAssetAtPath<T>(path);if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}return value;}
        private static Material Surface(string name,string source,Color colour,float metres)
        {var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/EnvironmentReady/Materials/"+source+".mat")){name=name,color=colour};material.SetFloat("_DetailMetres",metres);return SaveMaterial(material,Root+"/Materials/"+name+".mat");}
        private static Material SaveMaterial(Material material,string path)
        {var saved=AssetDatabase.LoadAssetAtPath<Material>(path);if(saved==null){AssetDatabase.CreateAsset(material,path);return material;}EditorUtility.CopySerialized(material,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(material);return saved;}

        private static GameObject BuildProp(string id)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject(id);SceneManager.MoveGameObjectToScene(root,scene);var lods=new LOD[3];
                for(int level=0;level<3;level++)
                {
                    var child=new GameObject("LOD "+level);SceneManager.MoveGameObjectToScene(child,scene);child.transform.SetParent(root.transform,false);
                    child.AddComponent<MeshFilter>().sharedMesh=ReadMesh(id,level);var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterials=surfaces;renderer.shadowCastingMode=level==2?ShadowCastingMode.Off:ShadowCastingMode.On;
                    lods[level]=new LOD(level==0?.3f:level==1?.06f:.003f,new Renderer[]{renderer});
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                if(id=="Torch")
                {
                    root.transform.localScale=new Vector3(.65f,1,.65f);
                    var fire=MakeFlame(root.transform,"Torch sparks",new Vector3(0,.77f,0),.012f,3,new Color(1,.58f,.05f,.8f));
                    var main=fire.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.20f,.45f);main.startSpeed=new ParticleSystem.MinMaxCurve(.15f,.4f);
                    var card=GameObject.CreatePrimitive(PrimitiveType.Quad);SceneManager.MoveGameObjectToScene(card,scene);card.name="Living torch flame";card.transform.SetParent(root.transform,false);card.transform.localPosition=new Vector3(0,.83f,0);card.transform.localScale=new Vector3(.13f,.30f,1);Object.DestroyImmediate(card.GetComponent<Collider>());
                    card.GetComponent<Renderer>().sharedMaterial=SaveMaterial(new Material(Shader.Find("Traveller/Torch Flame")),Root+"/Materials/TorchFlame.mat");card.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;card.AddComponent<TorchFlameBillboard>();
                    var lamp=new GameObject("Torch light").AddComponent<Light>();lamp.transform.SetParent(root.transform,false);lamp.transform.localPosition=new Vector3(0,.83f,0);lamp.type=LightType.Point;lamp.range=9.5f;lamp.intensity=2.6f;lamp.color=new Color(1,.48f,.17f);lamp.shadows=LightShadows.None;
                }
                return PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+id+".prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        private static GameObject BuildFire()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Vegetation flame and smoke");SceneManager.MoveGameObjectToScene(root,scene);
                var effect=root.AddComponent<ParticleSystem>();effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=effect.main;main.startLifetime=.9f;main.startSpeed=.65f;main.startSize=.35f;main.startColor=new Color(1,.37f,.018f,.75f);main.maxParticles=80;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var shape=effect.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.30f;shape.angle=14;
                var emission=effect.emission;emission.rateOverTime=24;
                var colour=effect.colorOverLifetime;colour.enabled=true;colour.color=FlameGradient();
                var renderer=effect.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particles;renderer.shadowCastingMode=ShadowCastingMode.Off;
                var smoke=MakeFlame(root.transform,"Smoke",Vector3.up*.5f,.8f,6,new Color(.09f,.08f,.07f,.22f));var smokeMain=smoke.main;smokeMain.startLifetime=3;smokeMain.startSpeed=.6f;smokeMain.maxParticles=36;
                return PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/VegetationFire.prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        private static ParticleSystem MakeFlame(Transform parent,string name,Vector3 position,float size,float rate,Color colour)
        {
            var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,parent.gameObject.scene);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var fire=go.AddComponent<ParticleSystem>();fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=fire.main;main.startLifetime=.45f;main.startSpeed=.2f;main.startSize=size;main.startColor=colour;main.maxParticles=36;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var shape=fire.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=size*.25f;var emission=fire.emission;emission.rateOverTime=rate;
            var renderer=fire.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particles;renderer.shadowCastingMode=ShadowCastingMode.Off;return fire;
        }
        private static Gradient FlameGradient()
        {var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.8f,.1f),0),new GradientColorKey(new Color(1,.25f,.015f),.5f),new GradientColorKey(new Color(.1f,.04f,.01f),1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.6f,.5f),new GradientAlphaKey(0,1)});return gradient;}
        private static Mesh ReadMesh(string id,int lod)
        {
            var mesh=new Mesh{name=id+" LOD "+lod,indexFormat=IndexFormat.UInt32};
            using(var reader=new BinaryReader(File.OpenRead("ArtSource/ActOne/Output/"+id+"_LOD"+lod+".meshbin")))
            {
                if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4))!="TBM1")throw new InvalidDataException(id);
                int count=reader.ReadInt32(),indexCount=reader.ReadInt32(),slots=reader.ReadInt32();var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
                for(int i=0;i<count;i++){float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle();vertices[i]=new Vector3(x,z,-y);}
                for(int i=0;i<count;i++){float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle();normals[i]=new Vector3(x,z,-y);}
                for(int i=0;i<count;i++)uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                var indices=new int[indexCount];for(int i=0;i<indexCount;i++)indices[i]=reader.ReadInt32();var triangles=new List<int>[slots];for(int i=0;i<slots;i++)triangles[i]=new List<int>();
                for(int i=0;i<indexCount;i+=3){int slot=reader.ReadInt32();triangles[slot].Add(indices[i]);triangles[slot].Add(indices[i+1]);triangles[slot].Add(indices[i+2]);}
                mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=slots;for(int i=0;i<slots;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();
            }
            string path=Root+"/Meshes/"+id+"_LOD"+lod+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.uv=mesh.uv;saved.tangents=mesh.tangents;saved.subMeshCount=mesh.subMeshCount;for(int i=0;i<mesh.subMeshCount;i++)saved.SetTriangles(mesh.GetTriangles(i),i);saved.UploadMeshData(false);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);return saved;
        }
        public static string BuildPlayer()
        {
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=TravellerStage.SceneNames.Select(n=>"Assets/_Project/Scenes/Traveller/"+n+".unity").ToArray(),locationPathName="Builds/ActOneTraveller.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            File.WriteAllText(Docs+"/build.json","{\"result\":\""+build.summary.result+"\",\"errors\":"+build.summary.totalErrors+",\"warnings\":"+build.summary.totalWarnings+"}");
            if(build.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Act one build failed.");return Path.GetFullPath("Builds/ActOneTraveller.app");
        }
    }
}
