using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class TravellerCrowdLibrary
    {
        public const string Root = "Assets/_Project/Characters/Crowd";
        private const string SourceManifest = "Documentation/CharacterProduction/crowd-model-sources.json";
        private static readonly string[] AnchorNames = { "城门卫兵", "入口卫兵", "石殿卫兵", "教廷卫兵", "王都居民", "复苏者" };
        private sealed class Definition
        {
            public string id, source; public TravellerRole role; public float height; public Color cloth;
            public Definition(string id, string source, TravellerRole role, float height, Color cloth) { this.id=id;this.source=source;this.role=role;this.height=height;this.cloth=cloth; }
        }
        private static readonly Definition[] Definitions = {
            new Definition("ShangGuard_Bronze","PlayerTraveller",TravellerRole.ShangGuard,1.8f,new Color(.24f,.19f,.14f)),
            new Definition("ShangGuard_Dark","PlayerTraveller",TravellerRole.ShangGuard,1.82f,new Color(.16f,.21f,.24f)),
            new Definition("ChurchGuard_Steel","KnightD",TravellerRole.ChurchGuard,1.85f,Color.white),
            new Definition("CitizenMale_Hemp","PlayerTraveller",TravellerRole.CitizenMale,1.73f,new Color(.43f,.39f,.27f)),
            new Definition("CitizenMale_Indigo","PlayerTraveller",TravellerRole.CitizenMale,1.7f,new Color(.22f,.3f,.43f)),
            new Definition("CitizenMale_Worker","PeasantMan",TravellerRole.CitizenMale,1.72f,new Color(.34f,.31f,.22f)),
            new Definition("CitizenMale_Slate","PeasantMan",TravellerRole.CitizenMale,1.77f,new Color(.25f,.32f,.35f)),
            new Definition("CitizenFemale_Ochre","PeasantGirl",TravellerRole.CitizenFemale,1.66f,new Color(.5f,.33f,.21f)),
            new Definition("CitizenFemale_Indigo","PeasantGirl",TravellerRole.CitizenFemale,1.62f,new Color(.26f,.32f,.47f))
        };
        [Serializable] public class SourceRecord
        {
            public string id,sourceTitle,sourcePage,downloadFilename,format,rawFile,unityFile,sha256,state;
            public int bytes,triangles,skinBones; public bool validHumanAvatar; public string[] textures;
        }
        [Serializable] public class Sources { public string provider,acquisition,license; public List<SourceRecord> models = new List<SourceRecord>(); }
        [Serializable] public class Variant { public string id,source,role,prefab; public int skinnedTriangles; public float standingHeight; }
        [Serializable] public class LibraryReport { public List<Variant> variants = new List<Variant>(); }
        [Serializable] public class SceneResult { public string scene; public int replaced,humanoidActors,remainingStandIns; public List<string> placements = new List<string>(); }
        [Serializable] public class SceneReport { public List<SceneResult> scenes = new List<SceneResult>(); }
        public static bool IsPrepared => Definitions.All(d => File.Exists(PrefabPath(d.id)));
        public static string PrefabPath(string id) => Root + "/Prefabs/" + id + ".prefab";

        [MenuItem("Tools/Three Body Traveller/Prepare And Replace All Crowd Characters")]
        public static void PrepareAndApply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before replacing crowd characters.");
            Prepare(); ApplyAll();
        }

        public static void Prepare()
        {
            foreach (var folder in new[] { Root,Root+"/Prefabs",Root+"/Materials",Root+"/Meshes" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            if (!File.Exists(SourceManifest)) throw new FileNotFoundException(SourceManifest);
            var sources=JsonUtility.FromJson<Sources>(File.ReadAllText(SourceManifest));
            foreach (var required in new[] { "PeasantGirl","PeasantMan","KnightD" })
                if (!sources.models.Any(m=>m.id==required&&File.Exists(m.unityFile))) throw new FileNotFoundException("Required crowd source has not been downloaded: "+required);
            foreach (var source in sources.models) ConfigureSource(source);
            if (AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath)==null) TravellerStoryCharacters.PrepareAssets();
            var bronze=EquipmentMaterial("CrowdBronze","CapitalBronze",new Color(.42f,.29f,.12f),.8f,.4f);
            var wood=EquipmentMaterial("CrowdShieldWood","CapitalTimber",new Color(.22f,.14f,.075f),0,.19f);
            var preview=EditorSceneManager.NewPreviewScene();var report=new LibraryReport();
            try
            {
                foreach (var def in Definitions)
                {
                    var root=new GameObject(def.id);SceneManager.MoveGameObjectToScene(root,preview);
                    string path=def.source=="PlayerTraveller"?GeneratedCharacterLibrary.Root+"/Prefabs/PlayerTraveller.prefab":Root+"/Sources/"+def.source+"/"+def.source+".fbx";
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),preview);model.transform.SetParent(root.transform,false);
                    var animator=model.GetComponentInChildren<Animator>();
                    var settings=AssetDatabase.LoadAssetAtPath<TravellerCastSettings>(TravellerStoryCharacters.SettingsPath);
                    animator.runtimeAnimatorController=settings.controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    TravellerStoryCharacters.WithSample(animator,"MX_Idle",.2f,()=>{
                    var bounds=WorldBounds(model);model.transform.localScale*=def.height/bounds.size.y;
                    model.transform.position-=Vector3.up*(WorldBounds(model).min.y-.01f);
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if (def.role!=TravellerRole.ChurchGuard) skin.sharedMesh=ClothMask(def.source,skin,model);
                        skin.sharedMaterials=skin.sharedMaterials.Select(m=>VariantMaterial(def,m)).ToArray();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                    }
                    var actor=root.AddComponent<TravellerActor>();actor.role=def.role;actor.settings=settings;actor.animator=animator;
                    var member=root.AddComponent<TravellerCrowdMember>();member.isGuard=def.role==TravellerRole.ShangGuard||def.role==TravellerRole.ChurchGuard;
                    if (def.role==TravellerRole.ShangGuard) TravellerCrowdEquipment.AddShangArmour(root,animator,bronze,wood);
                    else if (def.role==TravellerRole.ChurchGuard) TravellerCrowdEquipment.AddWeapons(root,animator,bronze,wood);
                    var capsule=root.AddComponent<CapsuleCollider>();capsule.height=def.height;capsule.radius=.29f;capsule.center=Vector3.up*def.height*.5f;
                    var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.012f,root.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
                    animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
                        if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath(def.id));
                    if (prefab==null) throw new IOException("Could not save "+def.id);
                    report.variants.Add(new Variant{id=def.id,source=def.source,role=def.role.ToString(),prefab=PrefabPath(def.id),standingHeight=def.height,
                        skinnedTriangles=root.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(s=>s.sharedMesh.triangles.Length/3)});
                    });
                    Object.DestroyImmediate(root);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllText(SourceManifest,JsonUtility.ToJson(sources,true));
            File.WriteAllText("Documentation/CharacterProduction/crowd-library-validation.json",JsonUtility.ToJson(report,true));
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureSource(SourceRecord source)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(source.unityFile);
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
            string textures=Path.GetDirectoryName(source.unityFile)+"/Textures";Directory.CreateDirectory(textures);importer.ExtractTextures(textures);AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(textures).Where(f=>!f.EndsWith(".meta")))
            {
                var t=AssetImporter.GetAtPath(file) as TextureImporter;if(t==null)continue;
                bool normal=Path.GetFileName(file).IndexOf("normal",StringComparison.OrdinalIgnoreCase)>=0;
                t.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;t.sRGBTexture=!normal;
                t.maxTextureSize=2048;t.textureCompression=TextureImporterCompression.CompressedHQ;t.SaveAndReimport();
            }
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(source.unityFile);var avatar=GeneratedCharacterLibrary.AvatarFor(source.unityFile);
            source.validHumanAvatar=avatar!=null&&avatar.isValid&&avatar.isHuman;
            source.triangles=model.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(s=>s.sharedMesh.triangles.Length/3);
            source.skinBones=model.GetComponentsInChildren<SkinnedMeshRenderer>().Max(s=>s.bones.Length);
            source.textures=Directory.GetFiles(textures).Where(f=>!f.EndsWith(".meta")).ToArray();source.state="unity_humanoid_import_verified";
            if(!source.validHumanAvatar||source.triangles<2000)throw new InvalidDataException("Invalid complete character: "+source.id);
        }

        private static Material VariantMaterial(Definition def,Material source)
        {
            string key=Regex.Replace(def.id+"_"+source.name,"[^A-Za-z0-9_-]","_");string path=Root+"/Materials/"+key+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            bool steel=def.role==TravellerRole.ChurchGuard;
            var shader=steel?Shader.Find("Standard"):Shader.Find("Traveller/Crowd Cloth Palette");
            if(shader==null||!shader.isSupported)throw new InvalidDataException("Crowd shader not ready.");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            Texture diffuse=source.mainTexture,normal=source.HasProperty("_BumpMap")?source.GetTexture("_BumpMap"):null;
            if(def.source!="PlayerTraveller")
            {
                string folder=Root+"/Sources/"+def.source+"/Textures/";
                if(diffuse!=null){var extracted=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+diffuse.name+".png");if(extracted!=null)diffuse=extracted;}
                if(normal==null)
                {
                    string file=Directory.GetFiles(folder,"*.png").FirstOrDefault(f=>f.IndexOf("normal",StringComparison.OrdinalIgnoreCase)>=0);
                    if(file!=null)normal=AssetDatabase.LoadAssetAtPath<Texture2D>(file);
                }
            }
            if(diffuse==null)throw new InvalidDataException("Character has no albedo: "+def.id);
            material.SetTexture("_MainTex",diffuse);material.SetTexture("_BumpMap",normal);material.SetFloat("_Glossiness",steel ? 0.42f : 0.18f);
            if(steel){material.color=Color.white;material.SetFloat("_Metallic",.65f);if(normal!=null)material.EnableKeyword("_NORMALMAP");}
            else{material.SetColor("_ClothColor",def.cloth);material.SetFloat("_TintStrength",def.role==TravellerRole.CitizenFemale ? 0.58f : 0.78f);}
            EditorUtility.SetDirty(material);return material;
        }

        private static Mesh ClothMask(string source,SkinnedMeshRenderer skin,GameObject model)
        {
            var mesh=Object.Instantiate(skin.sharedMesh);mesh.name=source+"_"+skin.name+"_ClothMask";
            var baked=new Mesh();skin.BakeMesh(baked,false);var vertices=baked.vertices;var weights=mesh.boneWeights;
            var bounds=WorldBounds(model);var colors=new Color32[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];float exposed=0;
                int[] indices={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};
                for(int j=0;j<4;j++)
                {
                    string n=skin.bones[indices[j]].name.ToLowerInvariant();
                    if(n.Contains("head")||n.Contains("neck")||n.Contains("hand")||n.Contains("forearm")||n.Contains("finger"))exposed+=values[j];
                }
                Vector3 point=skin.transform.TransformPoint(vertices[i]);
                float height=(point.y-bounds.min.y)/bounds.size.y;
                bool chest=height>.72f&&Mathf.Abs(point.x-model.transform.position.x)<bounds.size.y*.1f;
                colors[i]=new Color32(255,255,(byte)(chest?255:0),(byte)((exposed>.1f||height<.12f)?0:255));
            }
            mesh.colors32=colors;if(mesh.tangents.Length!=mesh.vertexCount)mesh.RecalculateTangents();Object.DestroyImmediate(baked);
            return SaveMesh(mesh);
        }

        public static Mesh SaveMesh(Mesh mesh)
        {
            string path=Root+"/Meshes/"+Regex.Replace(mesh.name,"[^A-Za-z0-9_-]","_")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
        }

        private static Material EquipmentMaterial(string id,string original,Color color,float metallic,float smoothness)
        {
            string path=Root+"/Materials/"+id+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Traveller/"+original+".mat");
                mat=source!=null?new Material(source):new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);
            }
            mat.color=color;mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Glossiness",smoothness);EditorUtility.SetDirty(mat);return mat;
        }

        public static Bounds WorldBounds(GameObject model)
        {
            bool first=true;var bounds=new Bounds();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var next=GeneratedCharacterLibrary.SkinnedWorldBounds(skin);
                if(first){bounds=next;first=false;}else bounds.Encapsulate(next);
            }
            return bounds;
        }

        public static void ApplyAll()
        {
            if(!IsPrepared)throw new InvalidOperationException("Prepare all crowd prefabs before scene replacement.");
            var setup=EditorSceneManager.GetSceneManagerSetup();var report=new SceneReport();
            string backup="Logs/scene-backups/FullCrowd_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);
            try
            {
                foreach(var name in TravellerStage.SceneNames)
                {
                    string path=TravellerSceneBuilder.SceneFolder+"/"+name+".unity";File.Copy(path,backup+"/"+name+".unity",true);
                    var scene=SceneManager.GetSceneByPath(path);bool loaded=scene.IsValid()&&scene.isLoaded;
                    if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    report.scenes.Add(ApplyToScene(scene));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                    if(!loaded)EditorSceneManager.CloseScene(scene,true);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            File.WriteAllText("Documentation/CharacterProduction/crowd-scene-validation.json",JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();
        }

        public static SceneResult ApplyToScene(Scene scene)
        {
            var stage=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TravellerStage>(true)).Single();
            var targets=stage.GetComponentsInChildren<Transform>(true).Where(t=>AnchorNames.Any(n=>t.name.StartsWith(n+" [",StringComparison.Ordinal)))
                .OrderBy(t=>t.name).ThenBy(t=>t.position.z).ThenBy(t=>t.position.x).ToArray();
            foreach(var t in targets)foreach(var c in t.GetComponentsInChildren<Collider>())c.enabled=false;
            var result=new SceneResult{scene=scene.name};int resident=0,guard=0;
            foreach(var target in targets)
            {
                bool church=target.name.StartsWith("教廷卫兵",StringComparison.Ordinal),soldier=target.name.Contains("卫兵"),reviving=target.name.StartsWith("复苏者",StringComparison.Ordinal);
                int variant=soldier?guard++:resident++;
                var choices=Definitions.Where(d=>church?d.role==TravellerRole.ChurchGuard:soldier?d.role==TravellerRole.ShangGuard:d.role==TravellerRole.CitizenMale||d.role==TravellerRole.CitizenFemale).ToArray();
                // Alternate genders through the settlement rather than putting one group in a single row.
                if(!soldier)choices=Definitions.Where(d=>variant%3==1?d.role==TravellerRole.CitizenFemale:d.role==TravellerRole.CitizenMale).ToArray();
                var def=choices[(variant/2)%choices.Length];
                var actorRoot=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(def.id)),scene);
                actorRoot.name=target.name.Substring(0,target.name.IndexOf(" [",StringComparison.Ordinal))+" [Humanoid]";
                actorRoot.transform.SetParent(target.parent,false);actorRoot.transform.SetSiblingIndex(target.GetSiblingIndex());
                actorRoot.transform.position=target.position;actorRoot.transform.rotation=soldier?Quaternion.Euler(0,180,0):Quaternion.Euler(0,reviving?270:variant*137%360,0);
                float scale=soldier?1:1+(variant%5-2)*.018f;actorRoot.transform.localScale=Vector3.one*scale;
                var collider=actorRoot.GetComponent<CapsuleCollider>();collider.enabled=false;Physics.SyncTransforms();
                Vector3 placed;if(!FindPlacement(actorRoot,target.position,out placed))throw new InvalidOperationException("No safe crowd placement near "+target.name+" "+target.position);
                actorRoot.transform.position=placed;collider.enabled=true;
                var actor=actorRoot.GetComponent<TravellerActor>();actor.initialAnimationPhase=Mathf.Repeat(variant*.381966f,.99f);
                var member=actorRoot.GetComponent<TravellerCrowdMember>();member.isReviving=reviving;member.variation=variant;
                var points=new List<Vector3>();Vector3 end;
                if(!soldier&&!reviving&&FindPlacement(actorRoot,placed+Vector3.forward*.9f,out end)&&Vector3.Distance(end,placed)<1.5f){points.Add(end);points.Add(placed);}
                member.strollPoints=points.ToArray();
                foreach(var o in new Object[]{actorRoot.transform,actor,member,collider})PrefabUtility.RecordPrefabInstancePropertyModifications(o);
                result.placements.Add(actorRoot.name+" = "+def.id+" at "+placed);
                Object.DestroyImmediate(target.gameObject);result.replaced++;Physics.SyncTransforms();
            }
            result.humanoidActors=stage.GetComponentsInChildren<TravellerActor>(true).Length;
            result.remainingStandIns=stage.GetComponentsInChildren<Transform>(true).Count(t=>t.name.Contains("[scene stand-in]"));
            if(result.remainingStandIns!=0)throw new InvalidDataException("Unreplaced scene characters in "+scene.name);
            return result;
        }

        private static bool FindPlacement(GameObject actor,Vector3 desired,out Vector3 result)
        {
            Vector3 inward=Vector3.right*(desired.x>0?-1:1);
            var offsets=new[]{Vector3.zero,inward*.9f,inward*1.8f,inward*2.7f,inward*3.6f,Vector3.forward*1.2f,Vector3.back*1.2f};
            foreach(var offset in offsets)
            {
                Vector3 p=desired+offset;
                var ground=Physics.RaycastAll(p+Vector3.up*2.5f,Vector3.down,5f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>h.collider.gameObject.scene==actor.scene&&h.normal.y>.7f&&Mathf.Abs(h.point.y-desired.y)<.75f&&!h.collider.transform.IsChildOf(actor.transform)&&h.collider.GetComponentInParent<TravellerActor>()==null)
                    .OrderByDescending(h=>h.point.y).ToArray();
                if(ground.Length==0)continue;p.y=ground[0].point.y+.01f;
                var hits=Physics.OverlapCapsule(p+Vector3.up*.36f,p+Vector3.up*1.45f,.27f,~0,QueryTriggerInteraction.Ignore);
                if(hits.Any(c=>c.gameObject.scene==actor.scene&&!c.transform.IsChildOf(actor.transform)))continue;
                result=p;return true;
            }
            result=desired;return false;
        }
    }
}
