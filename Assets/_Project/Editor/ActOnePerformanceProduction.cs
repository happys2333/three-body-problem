using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using Object=UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class ActOnePerformanceProduction
    {
        public const string Docs="Documentation/Performance/ActOne20261102";
        public const string BuildOutput="Builds/ActOnePerformance20261102/Traveller.app";
        private const string Request="ThreeBody.ActOnePerformance.BuildPending";

        public const string ShadowName="Efficient rock shadow";
        public const string BatchRootName="Gobi shadow batches";
        public const string ShadowMaterialPath="Assets/_Project/Art/ActOne/Materials/GobiRockShadow.mat";
        [Serializable] public sealed class OptimizationReport
        {
            public int shadowProxies,backdropLods,shadowBatches;public long proxyTriangles,originalNearShadowTriangles;
        }
        [MenuItem("Tools/Three Body Traveller/Gobi/Optimize Rendering")]
        public static string OptimizeScene()
        {
            EraGuardProduction.GuardProject();
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before optimizing scene assets.");
            var scene=SceneManager.GetActiveScene();if(scene.isDirty)throw new InvalidOperationException("Save scene first.");
            if(scene.path!=GobiEnvironmentDressing.ScenePath)scene=EditorSceneManager.OpenScene(GobiEnvironmentDressing.ScenePath);
            var result=OptimizeStage(Object.FindFirstObjectByType<TravellerStage>());
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Docs+"/Unity");File.WriteAllText(Docs+"/Unity/scene-optimization.json",JsonUtility.ToJson(result,true));
            return JsonUtility.ToJson(result);
        }
        public static OptimizationReport OptimizeStage(TravellerStage stage)
        {
            var result=new OptimizationReport();
            var material=AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
            if(material==null){material=new Material(Shader.Find("Traveller/Gobi Rock Shadow")){name="Gobi opaque rock shadows",enableInstancing=true};AssetDatabase.CreateAsset(material,ShadowMaterialPath);}
            var landscape=stage.transform.Find("Environment/Prepared Gobi landscape");
            if(landscape==null)throw new InvalidOperationException("The dressed Gobi landscape is missing.");
            var shadowSources=new List<MeshFilter>();
            foreach(var group in landscape.GetComponentsInChildren<LODGroup>())
            {
                var lods=group.GetLODs();if(lods.Length<3||lods[0].renderers.Length!=1)continue;
                bool backdrop=group.name.Contains("skyline")||group.name.StartsWith("Far ",StringComparison.Ordinal)||group.name.StartsWith("Northern saddle",StringComparison.Ordinal);
                if(backdrop)
                {
                    lods[0].screenRelativeTransitionHeight=.80f;lods[1].screenRelativeTransitionHeight=.30f;
                    group.SetLODs(lods);EditorUtility.SetDirty(group);result.backdropLods++;
                }
                // Fuel renderers must disappear together with their own shadows after burning.
                if(group.name.Contains("DesertThornBush")||group.name.Contains("DeadTamarisk")||group.GetComponentInParent<VegetationFuel>()!=null)continue;
                var source=lods[0].renderers[0];
                if(source==null||source.bounds.size.magnitude<4.5f)continue;
                if(source.sharedMaterials.Any(m=>m==null||m.renderQueue>=2450))continue;
                if(lods.Any(l=>l.renderers.Any(r=>r.shadowCastingMode==ShadowCastingMode.TwoSided)))continue;
                var existing=group.transform.Find(ShadowName);
                if(existing==null&&!lods.Any(l=>l.renderers.Any(r=>r.shadowCastingMode!=ShadowCastingMode.Off)))continue;
                int level=group.name.StartsWith("Shelter continuous canopy",StringComparison.Ordinal)?1:2;
                var filter=lods[level].renderers[0].GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                var visible=source.GetComponent<MeshFilter>();
                result.originalNearShadowTriangles+=Triangles(visible.sharedMesh);result.proxyTriangles+=Triangles(filter.sharedMesh);
                GameObject shadow;
                if(existing==null)
                {
                    shadow=new GameObject(ShadowName);SceneManager.MoveGameObjectToScene(shadow,stage.gameObject.scene);
                    shadow.transform.SetParent(group.transform,false);shadow.AddComponent<MeshFilter>();shadow.AddComponent<MeshRenderer>();
                }
                else shadow=existing.gameObject;
                shadow.layer=source.gameObject.layer;
                shadow.transform.localPosition=filter.transform.localPosition;shadow.transform.localRotation=filter.transform.localRotation;shadow.transform.localScale=filter.transform.localScale;
                shadow.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var renderer=shadow.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                GameObjectUtility.SetStaticEditorFlags(shadow,StaticEditorFlags.BatchingStatic);
                foreach(var lod in lods)foreach(var original in lod.renderers){original.shadowCastingMode=ShadowCastingMode.Off;EditorUtility.SetDirty(original);}
                renderer.enabled=false;shadowSources.Add(shadow.GetComponent<MeshFilter>());
                EditorUtility.SetDirty(renderer);result.shadowProxies++;
            }
            result.shadowBatches=BuildShadowBatches(stage,shadowSources,material);
            return result;
        }
        private static int BuildShadowBatches(TravellerStage stage,List<MeshFilter> sources,Material material)
        {
            const string folder="Assets/_Project/Art/ActOne/Meshes/GobiShadows";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_Project/Art/ActOne/Meshes","GobiShadows");
            var world=stage.transform.Find("Environment");var old=world.Find(BatchRootName);
            if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(BatchRootName);SceneManager.MoveGameObjectToScene(root,stage.gameObject.scene);root.transform.SetParent(world,false);
            var cells=new Dictionary<Vector3Int,List<MeshFilter>>();
            foreach(var source in sources)
            {
                Vector3 centre=source.GetComponent<Renderer>().bounds.center;
                var cell=new Vector3Int(Mathf.FloorToInt(centre.x/64),Mathf.FloorToInt(centre.z/64),source.gameObject.layer);
                List<MeshFilter> items;if(!cells.TryGetValue(cell,out items)){items=new List<MeshFilter>();cells.Add(cell,items);}items.Add(source);
            }
            foreach(var cell in cells)
            {
                var combine=new List<CombineInstance>();
                foreach(var source in cell.Value)for(int sub=0;sub<source.sharedMesh.subMeshCount;sub++)
                    combine.Add(new CombineInstance{mesh=source.sharedMesh,subMeshIndex=sub,transform=root.transform.worldToLocalMatrix*source.transform.localToWorldMatrix});
                string name="ShadowCell_"+cell.Key.x+"_"+cell.Key.y+"_L"+cell.Key.z;string path=folder+"/"+name+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=mesh==null;
                if(create)mesh=new Mesh{name=name};else mesh.Clear();
                mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(combine.ToArray(),true,true);mesh.RecalculateBounds();mesh.UploadMeshData(false);
                if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
                var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,stage.gameObject.scene);go.transform.SetParent(root.transform,false);
                go.layer=cell.Key.z;go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;
                renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                // Already combined in spatial cells; preserve their separate culling bounds.
                GameObjectUtility.SetStaticEditorFlags(go,0);
            }
            return cells.Count;
        }
        private static long Triangles(Mesh mesh)
        {long result=0;if(mesh!=null)for(int i=0;i<mesh.subMeshCount;i++)result+=(long)mesh.GetIndexCount(i)/3;return result;}
        public static string[] CollisionSnapshot()
        {
            var stage=Object.FindFirstObjectByType<TravellerStage>();var lines=new List<string>();
            foreach(var c in stage.GetComponentsInChildren<Collider>(true))
            {
                string geometry="";var mesh=c as MeshCollider;var box=c as BoxCollider;var sphere=c as SphereCollider;var capsule=c as CapsuleCollider;var terrain=c as TerrainCollider;
                if(mesh!=null)geometry=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh.sharedMesh))+" convex="+mesh.convex;
                if(box!=null)geometry=box.center+" "+box.size;
                if(sphere!=null)geometry=sphere.center+" "+sphere.radius;
                if(capsule!=null)geometry=capsule.center+" "+capsule.radius+" "+capsule.height+" "+capsule.direction;
                if(terrain!=null)geometry=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain.terrainData));
                lines.Add(AnimationUtility.CalculateTransformPath(c.transform,stage.transform)+"|"+c.GetType().Name+"|"+c.enabled+"|"+c.isTrigger+"|"+geometry+"|"+c.transform.localToWorldMatrix.ToString());
            }
            lines.Sort(StringComparer.Ordinal);return lines.ToArray();
        }

        [InitializeOnLoadMethod] private static void Resume(){EditorApplication.update-=BuildPending;EditorApplication.update+=BuildPending;}
        public static void QueueBuild(){EraGuardProduction.GuardProject();SessionState.SetBool(Request,true);Resume();}
        private static void BuildPending()
        {
            if(!SessionState.GetBool(Request,false)||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer)return;
            SessionState.SetBool(Request,false);
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before building.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene before building.");
            Directory.CreateDirectory(Docs+"/Unity");Directory.CreateDirectory(Path.GetDirectoryName(BuildOutput));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=TravellerStage.SceneNames.Select(n=>TravellerSceneBuilder.SceneFolder+"/"+n+".unity").ToArray(),locationPathName=BuildOutput,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development|BuildOptions.CompressWithLz4|BuildOptions.DetailedBuildReport});
            var s=result.summary;File.WriteAllText(Docs+"/Unity/native-build.json","{\"result\":\""+s.result+"\",\"errors\":"+s.totalErrors+",\"warnings\":"+s.totalWarnings+",\"seconds\":"+s.totalTime.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
        }
    }
}
