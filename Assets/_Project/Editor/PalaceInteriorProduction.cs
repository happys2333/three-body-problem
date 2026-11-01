using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class PalaceInteriorProduction
    {
        public const string Root = "Assets/_Project/Art/PalaceInterior";
        public const string Docs = "Documentation/InteriorProduction/2026-11-01";
        private const string Work = "ArtSource/PalaceInterior/2026-11-01";
        private const string ScenePath = "Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity";
        private const string District = "11 Palace interior and divination";

        [MenuItem("Tools/Three Body Traveller/Capital/Prepare Palace Interior")]
        public static void Prepare()
        {
            EraGuardProduction.GuardProject();
            foreach (var folder in new[] { Root + "/Meshes", Root + "/Materials", Root + "/Prefabs", Root + "/Settings", Docs }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var materials = Materials();
            foreach (string id in new[] { "RoyalSeat", "RoyalRelief", "CalendarTable", "BronzeJue", "BronzeGu" }) Import(id, materials);
            var settings = AssetDatabase.LoadAssetAtPath<PalaceRitualSettings>(Root + "/Settings/Ritual.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<PalaceRitualSettings>(); AssetDatabase.CreateAsset(settings, Root + "/Settings/Ritual.asset"); }
            MakeTimer(materials);
            MakeSigil();
            AssetDatabase.SaveAssets();
            foreach (string mesh in Directory.GetFiles(Root + "/Meshes", "*.asset")) AssetDatabase.ImportAsset(mesh, ImportAssetOptions.ForceUpdate);
        }

        private static Material[] Materials()
        {
            var bronze = Craft("PatinatedBronze", new Color(.32f,.23f,.105f), new Color(.075f,.17f,.135f), .86f, .31f, false);
            var hide = Craft("WornHide", new Color(.28f,.18f,.08f), new Color(.055f,.030f,.015f), 0, .21f, true);
            var result = new[] { AssetDatabase.LoadAssetAtPath<Material>(MasonryArchitectureLibrary.Root + "/Materials/Dressed sandstone blocks.mat"),
                AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root + "/Materials/Wood.mat"), bronze, hide };
            if (result.Any(m => m == null)) throw new FileNotFoundException("Palace furnishing surface is missing.");
            return result;
        }
        private static Material Craft(string id, Color primary, Color secondary, float metal, float gloss, bool hide)
        {
            string path = Root + "/Materials/" + id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Traveller/Palace Craft Surface")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_Color", primary); m.SetColor("_Secondary", secondary); m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", gloss); m.SetFloat("_Hide", hide ? 1 : 0); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }

        private static void Import(string id, Material[] materials)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = New(id, null); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var lods = new LOD[3];
                for (int i = 0; i < 3; i++)
                {
                    var mesh = ReadMesh(Work + "/" + id + "/" + id + "_LOD" + i + ".meshbin", id + "_LOD" + i);
                    var renderer = MeshObject("LOD" + i, mesh, materials, root.transform);
                    renderer.shadowCastingMode = i < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    lods[i] = new LOD(i == 0 ? .20f : i == 1 ? .055f : .006f, new Renderer[] { renderer });
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                if (id == "RoyalSeat" || id == "CalendarTable")
                {
                    var b = lods[0].renderers[0].bounds; var box = root.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + id + ".prefab");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Mesh ReadMesh(string path, string id)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)) != "TBM1") throw new InvalidDataException(path);
                int n = reader.ReadInt32(), count = reader.ReadInt32(), slots = reader.ReadInt32();
                if (n < 3 || slots != 4 || count % 3 != 0) throw new InvalidDataException(path);
                var v = new Vector3[n]; var normals = new Vector3[n]; var uv = new Vector2[n];
                for (int i = 0; i < n; i++) { float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle(); v[i]=new Vector3(x,z,-y); }
                for (int i = 0; i < n; i++) { float x=reader.ReadSingle(),y=reader.ReadSingle(),z=reader.ReadSingle(); normals[i]=new Vector3(x,z,-y).normalized; }
                for (int i = 0; i < n; i++) uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                var indices = new int[count]; for (int i=0;i<count;i++) indices[i]=reader.ReadInt32();
                var triangles=Enumerable.Range(0,slots).Select(i=>new List<int>()).ToArray();
                for (int i=0;i<count/3;i++) { int slot=reader.ReadInt32(); for(int k=0;k<3;k++)triangles[slot].Add(indices[i*3+k]); }
                var mesh = new Mesh { name=id,indexFormat=IndexFormat.UInt32,vertices=v,normals=normals,uv=uv,subMeshCount=slots };
                for(int i=0;i<slots;i++)mesh.SetTriangles(triangles[i],i);
                mesh.RecalculateBounds();mesh.RecalculateTangents();return SaveMesh(mesh,id);
            }
        }
        private static Mesh SaveMesh(Mesh mesh, string id)
        {
            string path=Root+"/Meshes/"+id+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
        }
        private static GameObject New(string name, Transform parent)
        {
            var obj=new GameObject(name);if(parent!=null)obj.transform.SetParent(parent,false);return obj;
        }
        private static MeshRenderer MeshObject(string name, Mesh mesh, Material[] materials, Transform parent)
        {
            var obj=New(name,parent);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;return renderer;
        }
        private static Transform Box(string name,Vector3 position,Vector3 size,Material material,Transform parent)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());return obj.transform;
        }
        private static void CombineChildren(Transform parent, Material material, string id)
        {
            var filters=parent.GetComponentsInChildren<MeshFilter>();
            var combine=filters.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
            var mesh=new Mesh{name=id,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine,true,true);
            foreach(var child in parent.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            MeshObject("Joined timber frame",SaveMesh(mesh,id),new[]{material},parent);
        }
        private static Material Plain(string id,Color color,float metal=0)
        {
            string path=Root+"/Materials/"+id+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",.18f);EditorUtility.SetDirty(m);return m;
        }

        private static void MakeTimer(Material[] materials)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=New("Five compartment wooden sand timer",null);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var casing=New("Balanced turning case",root.transform).transform;
                var frame=New("Timber case",casing).transform;
                foreach(float side in new[]{-1f,1f})
                {
                    Box("Case upright",new Vector3(side*.265f,0,.10f),new Vector3(.055f,1.16f,.28f),materials[1],frame);
                    Box("Case end",new Vector3(0,side*.56f,.10f),new Vector3(.56f,.055f,.28f),materials[1],frame);
                }
                Box("Timber back",new Vector3(0,0,.22f),new Vector3(.50f,1.10f,.025f),materials[1],frame);
                for(int i=0;i<6;i++)Box("Compartment shelf",new Vector3(0,(i-2.5f)*.18f,.09f),new Vector3(.50f,.018f,.23f),materials[1],frame);
                CombineChildren(frame,materials[1],"FiveTierCase");
                var door=New("Open front panel hinge",casing).transform;door.localPosition=new Vector3(-.29f,0,-.04f);door.localRotation=Quaternion.Euler(0,-108,0);
                Box("Hinged front panel",new Vector3(.28f,0,0),new Vector3(.55f,1.15f,.025f),materials[1],door);
                var sand=Plain("Sand",new Color(.64f,.53f,.31f));
                var piles=new Transform[5];var streams=new Transform[5];
                for(int i=0;i<5;i++)
                {
                    float y=(i-2)*.18f;
                    var mound=New("Sand mound "+i,casing).transform;mound.localPosition=new Vector3(0,y-.06f,.07f);
                    var b=new ShapeBuilder();b.Cone(Vector3.zero,.13f,.042f,32);
                    MeshObject("Fine sand",SaveMesh(b.Mesh("TimerSand"+i),"TimerSand"+i),new[]{sand},mound);piles[i]=mound;
                    streams[i]=Box("Falling stream "+i,new Vector3(0,y+.025f,.07f),new Vector3(.004f,.10f,.004f),sand,casing);
                }
                var component=root.AddComponent<FiveTierSandTimer>();component.casing=casing;component.piles=piles;component.streams=streams;
                component.settings=AssetDatabase.LoadAssetAtPath<PalaceRitualSettings>(Root+"/Settings/Ritual.asset");
                PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/FiveTierSandTimer.prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        private static void MakeSigil()
        {
            var mesh=new ShapeBuilder();
            mesh.Ring(3.2f,.024f,512);mesh.Ring(2.77f,.018f,448);mesh.Ring(1.78f,.018f,256);mesh.Ring(.86f,.022f,192);
            for(int i=0;i<64;i++)
            {
                float angle=i*Mathf.PI*2/64;
                for(int line=0;line<6;line++)
                {
                    float r=2.84f+line*.048f;
                    if((i&(1<<line))!=0)mesh.RadialBar(angle,r,-.075f,.075f,.021f);
                    else {mesh.RadialBar(angle,r,-.075f,-.021f,.021f);mesh.RadialBar(angle,r,.021f,.075f,.021f);}
                }
            }
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;
                for(int line=0;line<3;line++)
                {
                    float r=1.38f+line*.095f;
                    if((i&(1<<line))!=0)mesh.RadialBar(angle,r,-.19f,.19f,.04f);
                    else {mesh.RadialBar(angle,r,-.19f,-.045f,.04f);mesh.RadialBar(angle,r,.045f,.19f,.04f);}
                }
                mesh.RadialBar(angle,2.35f,-.018f,.018f,.33f);
            }
            mesh.Arc(new Vector3(0,0,.43f),.43f,-Mathf.PI/2,Mathf.PI/2,96,.022f);
            mesh.Arc(new Vector3(0,0,-.43f),.43f,Mathf.PI/2,Mathf.PI*1.5f,96,.022f);
            mesh.Ring(.075f,.025f,36,new Vector3(0,0,.43f));mesh.Ring(.075f,.025f,36,new Vector3(0,0,-.43f));
            var saved=SaveMesh(mesh.Mesh("Sixty four unique binary hexagrams"),"DivinationFloor");
            var glow=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/DivinationGlow.mat");
            if(glow==null){glow=new Material(Shader.Find("Traveller/Divination Glow"));AssetDatabase.CreateAsset(glow,Root+"/Materials/DivinationGlow.mat");}
            var etched=Plain("EtchedBronze",new Color(.22f,.18f,.10f),.3f);
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=New("Divination floor",null);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                MeshObject("Engraved yin yang and hexagrams",saved,new[]{etched},root.transform).shadowCastingMode=ShadowCastingMode.Off;
                var light=MeshObject("Living divination strokes",saved,new[]{glow},root.transform);light.transform.localPosition=Vector3.up*.006f;
                light.shadowCastingMode=ShadowCastingMode.Off;light.receiveShadows=false;light.enabled=false;
                PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/DivinationFloor.prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }

        [MenuItem("Tools/Three Body Traveller/Capital/Apply Palace Interior")]
        public static void Apply()
        {
            EraGuardProduction.GuardProject();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=ScenePath||scene.isDirty)throw new InvalidOperationException("Open the saved capital scene first.");
            string backup="Logs/interior-backups/"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(ScenePath,backup+"/02_ChaogeCourt.unity");
            var stage=Object.FindFirstObjectByType<TravellerStage>();
            var world=stage.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Shang Capital");
            var old=world.Find(District);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var district=New(District,world).transform;
            RemoveLegacyFurniture(world);
            foreach(var t in world.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Hourglass"&&Vector3.Distance(t.position,new Vector3(-5.8f,5.31f,17))<.1f).ToArray())Object.DestroyImmediate(t.gameObject);
            var seat=Place("RoyalSeat",new Vector3(0,5.8f,33.75f),180,district);
            foreach(float side in new[]{-1f,1f})Place("RoyalRelief",new Vector3(side*5.7f,6.5f,41.40f),180,district);
            var table=Place("CalendarTable",new Vector3(-5,4,17),180,district);
            var settings=AssetDatabase.LoadAssetAtPath<PalaceRitualSettings>(Root+"/Settings/Ritual.asset");
            var timer=Place("FiveTierSandTimer",new Vector3(-5.65f,5.48f,17),0,district).GetComponent<FiveTierSandTimer>();timer.stage=stage;timer.settings=settings;
            PlaceExisting("Assets/_Project/Art/HunyuanExpansion/Prefabs/CalendarChest.prefab",new Vector3(-4.47f,4.9f,17.08f),180,.58f,district);
            var bone=PlaceExisting("Assets/_Project/Art/EnvironmentExpansionReady/Prefabs/ShangOracleBone.prefab",new Vector3(-4.45f,4.92f,16.7f),0,.65f,district);bone.localRotation=Quaternion.Euler(90,15,0);
            Place("BronzeJue",new Vector3(-6.05f,4.9f,16.6f),40,district);
            Place("BronzeGu",new Vector3(-6.05f,4.9f,17.35f),0,district);
            AddBraziers(stage,district);
            AddRitualDing(stage,district);
            AddSideFurnishings(district);
            var floor=Place("DivinationFloor",new Vector3(0,4.035f,23.5f),0,district);
            var cast=stage.GetComponent<TravellerCastDirector>();
            var ritual=district.gameObject.AddComponent<PalaceDivination>();ritual.stage=stage;ritual.settings=settings;ritual.companion=cast.companion;ritual.ritualCentre=floor;
            ritual.glow=floor.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="Living divination strokes");
            ritual.motes=AddMotes(floor);
            AddSoundscape(stage,district);
            AlignKing(cast.king,seat);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Docs+"/interior-integration.json",JsonUtility.ToJson(new Integration{scene=ScenePath,backup=backup,newLocalModels=5,hexagrams=64,source="Locally authored Blender furnishings; existing project PBR and props reused",runtime=new[]{"PalaceDivination","FiveTierSandTimer","PalaceSoundscape"}},true));
        }
        [Serializable]private class Integration{public string scene,backup,source;public int newLocalModels,hexagrams;public string[] runtime;}

        private static void AddSoundscape(TravellerStage stage,Transform district)
        {
            var sound=district.gameObject.AddComponent<PalaceSoundscape>();sound.stage=stage;sound.skylight=stage.GetComponentInChildren<PalaceSkylight>();
            sound.sand=Foley("Five-tier sand flow","SandTrickle",new Vector3(-5.65f,5.5f,17),.055f,true,district);
            sound.stone=Foley("Skylight stone friction","StoneSlide",new Vector3(0,12,23.5f),.13f,true,district);
            sound.footsteps=Foley("Interior stone footsteps","StoneFootstep",Vector3.zero,.14f,false,district);sound.footsteps.spatialBlend=0;
            sound.footsteps.gameObject.AddComponent<AudioReverbFilter>().reverbPreset=AudioReverbPreset.Stoneroom;
            var fires=new List<AudioSource>();
            foreach(float x in new[]{-9.5f,9.5f})foreach(float z in new[]{9f,34f})fires.Add(Foley("Brazier crackle","EmberCrackle",new Vector3(x,5.2f,z),.045f,true,district));
            fires.Add(Foley("Ritual hearth crackle","EmberCrackle",new Vector3(-9.4f,4.4f,30),.035f,true,district));sound.fires=fires.ToArray();
        }
        private static AudioSource Foley(string name,string clip,Vector3 position,float volume,bool loop,Transform parent)
        {
            var source=New(name,parent).AddComponent<AudioSource>();source.transform.position=position;
            source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/"+clip+".wav");
            if(source.clip==null)throw new FileNotFoundException(clip);
            source.volume=volume;source.loop=loop;source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=.8f;source.maxDistance=14;return source;
        }

        private static Transform Place(string id,Vector3 p,float yaw,Transform parent)
        {return PlaceExisting(Root+"/Prefabs/"+id+".prefab",p,yaw,1,parent);}
        private static Transform PlaceExisting(string path,Vector3 p,float yaw,float scale,Transform parent)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new FileNotFoundException(path);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent.gameObject.scene);obj.transform.SetParent(parent,false);
            obj.transform.position=p;obj.transform.rotation=Quaternion.Euler(0,yaw,0);obj.transform.localScale*=scale;return obj.transform;
        }
        private static void AlignKing(TravellerActor actor,Transform seat)
        {
            float hip=0;TravellerStoryCharacters.WithSample(actor.animator,"MX_SitIdle",.25f,()=>hip=actor.animator.GetBoneTransform(HumanBodyBones.Hips).position.y);
            // Cushion top is 0.74 m above the dais. Pelvis centre lies above it.
            float target=seat.position.y+.74f+.13f;
            var visual=actor.animator.transform;visual.position+=Vector3.up*(target-hip);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }
        private static ParticleSystem AddMotes(Transform parent)
        {
            var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab").GetComponentsInChildren<ParticleSystem>(true).First();
            var fx=Object.Instantiate(template.gameObject,parent);fx.name="Divination light motes";fx.transform.localPosition=Vector3.up*.06f;
            var p=fx.GetComponent<ParticleSystem>();var m=p.main;m.loop=true;m.playOnAwake=false;m.startLifetime=2.5f;m.startSpeed=.12f;m.startSize=.026f;m.maxParticles=96;m.startColor=new Color(1,.74f,.32f,.52f);
            var e=p.emission;e.rateOverTime=14;var shape=p.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=2.65f;
            fx.transform.localRotation=Quaternion.Euler(-90,0,0);p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return p;
        }
        private static void AddBraziers(TravellerStage stage,Transform district)
        {
            foreach(float side in new[]{-1f,1f})foreach(float z in new[]{9f,34f})
            {
                var p=new Vector3(side*9.5f,4,z);
                foreach(var legacy in stage.GetComponentsInChildren<Transform>(true).Where(t=>
                    (t.name=="Brazier stem"||t.name=="Brazier bowl"||t.name=="Brazier flame")&&
                    Mathf.Abs(t.position.x-p.x)<.1f&&Mathf.Abs(t.position.z-p.z)<.1f&&t.position.y>4&&t.position.y<6).ToArray())Object.DestroyImmediate(legacy.gameObject);
                var fire=PlaceExisting("Assets/_Project/Art/EnvironmentExpansionReady/Prefabs/ShangBronzeBrazier.prefab",p,0,1,district);
                foreach(var light in fire.GetComponentsInChildren<Light>(true))light.enabled=false;
            }
        }
        private static void AddRitualDing(TravellerStage stage,Transform district)
        {
            var coal=Plain("EmberCoal",new Color(.045f,.025f,.018f));coal.EnableKeyword("_EMISSION");coal.SetColor("_EmissionColor",new Color(.19f,.025f,.003f));EditorUtility.SetDirty(coal);
            var hearth=New("Charcoal beneath ritual vessel",district).transform;
            hearth.position=new Vector3(-9.4f,4.08f,30);
            for(int i=0;i<14;i++)
            {
                float a=i*Mathf.PI*2/14;
                var c=Box("Charcoal",new Vector3(Mathf.Cos(a)*.43f,.06f,Mathf.Sin(a)*.34f),new Vector3(.25f,.10f,.17f),coal,hearth);
                c.localRotation=Quaternion.Euler(i*13,i*73,i*7);
            }
            CombineChildren(hearth,coal,"RitualCharcoal");
            var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
            var flame=template.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Living torch flame");
            var fx=Object.Instantiate(flame.gameObject,hearth);fx.transform.localPosition=new Vector3(0,.16f,0);fx.transform.localScale=flame.localScale*.8f;
            var steamMaterial=SteamMaterial();
            var steam=New("Gentle ritual steam",district).AddComponent<ParticleSystem>();steam.transform.position=new Vector3(-9.4f,6.48f,30);steam.transform.rotation=Quaternion.Euler(-90,0,0);
            var main=steam.main;main.loop=true;main.playOnAwake=true;main.startLifetime=3;main.startSpeed=.22f;main.startSize=.48f;main.maxParticles=48;main.startColor=new Color(.69f,.72f,.70f,.12f);
            var emission=steam.emission;emission.rateOverTime=8;
            var shape=steam.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.45f;
            var size=steam.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.25f,1,1.8f));
            var color=steam.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.6f,.2f),new GradientAlphaKey(0,1)});color.color=gradient;
            steam.GetComponent<ParticleSystemRenderer>().sharedMaterial=steamMaterial;
        }
        private static Material SteamMaterial()
        {
            string path=Root+"/Materials/SoftSteam.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m!=null)return m;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Soft steam opacity"};
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float r=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/31.5f;
                texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2)));
            }
            texture.Apply();AssetDatabase.CreateAsset(texture,Root+"/Materials/SteamOpacity.asset");
            m=new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));m.mainTexture=texture;m.SetColor("_TintColor",new Color(.7f,.72f,.70f,.35f));
            AssetDatabase.CreateAsset(m,path);return m;
        }
        private static void AddSideFurnishings(Transform district)
        {
            var wood=AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root+"/Materials/Wood.mat");
            var woven=AssetDatabase.LoadAssetAtPath<Material>(EnvironmentProduction.Root+"/Materials/Thatch.mat");
            var guard=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Characters/Crowd/Prefabs/ShangGuard_Bronze.prefab").GetComponent<EraGuardEquipment>();
            foreach(float side in new[]{-1f,1f})
            {
                var rack=New("Timber guard weapon rack",district).transform;rack.position=new Vector3(side*12.4f,4,16);
                Box("Foot",new Vector3(0,.07f,0),new Vector3(.65f,.14f,2.2f),wood,rack);
                foreach(float end in new[]{-.8f,.8f})Box("Post",new Vector3(0,.72f,end),new Vector3(.10f,1.4f,.10f),wood,rack);
                Box("Cross bar",new Vector3(0,1.31f,0),new Vector3(.10f,.11f,1.9f),wood,rack);
                CombineChildren(rack,wood,"WeaponRackTimber");
                for(int i=0;i<3;i++)
                {
                    var weapon=Object.Instantiate(guard.polearm.gameObject,rack).transform;weapon.name="Racked bronze ge";
                    weapon.localPosition=new Vector3(-side*.15f,0,(i-1)*.55f);weapon.localRotation=Quaternion.Euler(0,90,side*6);
                }
                var mat=Box("Woven side seating mat",new Vector3(side*8.0f,4.018f,19),new Vector3(2.2f,.025f,1.3f),woven,district);
                mat.localRotation=Quaternion.Euler(0,side*5,0);
            }
        }
        private static void RemoveLegacyFurniture(Transform world)
        {
            var regions=new List<Bounds>{new Bounds(new Vector3(0,6.05f,34),new Vector3(3.8f,.5f,2.1f)+Vector3.one*.008f),
                new Bounds(new Vector3(0,7.1f,35),new Vector3(4.2f,2.6f,.65f)+Vector3.one*.008f),
                new Bounds(new Vector3(-5,4.65f,17),new Vector3(3,1.3f,1.5f)+Vector3.one*.008f),
                new Bounds(new Vector3(-4.42f,5.33f,17),new Vector3(1.5f,.04f,.86f)+Vector3.one*.008f)};
            var palace=world.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Pyramid masonry and enclosed palace");
            foreach(var filter in palace.GetComponentsInChildren<MeshFilter>(true))
            {
                var source=filter.sharedMesh;if(source==null||source.name.EndsWith("_InteriorTrim",StringComparison.Ordinal))continue;
                var renderer=filter.GetComponent<Renderer>();if(renderer!=null&&!regions.Any(b=>b.Intersects(renderer.bounds)))continue;
                var vertices=source.vertices;bool any=vertices.Any(v=>regions.Any(b=>b.Contains(filter.transform.TransformPoint(v))));if(!any)continue;
                var copy=Object.Instantiate(source);bool changed=false;
                for(int slot=0;slot<source.subMeshCount;slot++)
                {
                    var triangles=source.GetTriangles(slot);var keep=new List<int>(triangles.Length);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        bool remove=regions.Any(b=>b.Contains(filter.transform.TransformPoint(vertices[triangles[i]]))&&b.Contains(filter.transform.TransformPoint(vertices[triangles[i+1]]))&&b.Contains(filter.transform.TransformPoint(vertices[triangles[i+2]])));
                        if(remove){changed=true;continue;}keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);
                    }
                    copy.SetTriangles(keep,slot);
                }
                if(changed)
                {
                    copy.name=source.name+"_InteriorTrim";filter.sharedMesh=SaveMesh(copy,copy.name);
                    foreach(var col in palace.GetComponentsInChildren<MeshCollider>(true))if(col.sharedMesh==source)col.sharedMesh=filter.sharedMesh;
                }
                else Object.DestroyImmediate(copy);
            }
        }

        private sealed class ShapeBuilder
        {
            private readonly List<Vector3> v=new List<Vector3>();private readonly List<int> t=new List<int>();private readonly List<Vector2> uv=new List<Vector2>();private readonly List<Color> colors=new List<Color>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int i=v.Count;v.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                for(int n=0;n<4;n++)colors.Add(new Color(1,1,Mathf.Clamp01(v[i+n].magnitude/3.2f),1));
                t.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
            }
            public void Ring(float radius,float thickness,int segments,Vector3 centre=default(Vector3)){Arc(centre,radius,0,Mathf.PI*2,segments,thickness);}
            public void Arc(Vector3 centre,float radius,float start,float end,int segments,float width)
            {
                for(int i=0;i<segments;i++)
                {
                    float a=Mathf.Lerp(start,end,(float)i/segments),b=Mathf.Lerp(start,end,(float)(i+1)/segments);
                    var x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    Quad(centre+x*(radius-width/2),centre+x*(radius+width/2),centre+y*(radius+width/2),centre+y*(radius-width/2));
                }
            }
            public void RadialBar(float angle,float radius,float from,float to,float thickness)
            {
                var outward=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));var across=new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle));
                Quad(outward*(radius-thickness/2)+across*from,outward*(radius-thickness/2)+across*to,outward*(radius+thickness/2)+across*to,outward*(radius+thickness/2)+across*from);
            }
            public void Cone(Vector3 p,float radius,float height,int segments)
            {
                for(int i=0;i<segments;i++)
                {
                    var a=p+new Vector3(Mathf.Cos(i*Mathf.PI*2/segments),0,Mathf.Sin(i*Mathf.PI*2/segments))*radius;
                    var b=p+new Vector3(Mathf.Cos((i+1)*Mathf.PI*2/segments),0,Mathf.Sin((i+1)*Mathf.PI*2/segments))*radius;
                    int j=v.Count;v.AddRange(new[]{a,p+Vector3.up*height,b});
                    uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.right});colors.AddRange(new[]{Color.white,Color.white,Color.white});
                    t.AddRange(new[]{j,j+1,j+2});
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32,vertices=v.ToArray(),uv=uv.ToArray(),colors=colors.ToArray(),triangles=t.ToArray()};
                mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
