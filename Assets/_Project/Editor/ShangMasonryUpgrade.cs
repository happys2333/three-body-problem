using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        [MenuItem("Tools/Three Body Traveller/Upgrade Palace To Authored Masonry")]
        public static string UpgradePalaceToAuthoredMasonry()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before upgrading architecture.");
            string scenePath=SceneFolder+"/02_ChaogeCourt.unity";
            var original=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(scenePath);bool opened=!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new InvalidOperationException("The capital has unsaved edits; they have been preserved.");
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            GameObject replacement=null;
            try
            {
                SceneManager.SetActiveScene(scene);
                var transforms=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                var previous=transforms.Single(t=>t.name=="Pyramid masonry and enclosed palace");
                if(previous.parent.position.sqrMagnitude>.001f||previous.parent.rotation!=Quaternion.identity||previous.parent.lossyScale!=Vector3.one)
                    throw new InvalidOperationException("Palace parent coordinates changed; do not overwrite a transformed palace.");
                string backup="Logs/masonry-backups/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(scenePath,backup+"/02_ChaogeCourt.unity");
                var preserved=transforms.Where(t=>!t.IsChildOf(previous)).Select(t=>t.GetInstanceID()).OrderBy(i=>i).ToArray();
                MasonryArchitectureLibrary.BuildKit();Materials.Clear();Meshes.Clear();
                foreach(string key in new[]{"PyramidFloor","CapitalRoad","CapitalBronze","RoyalBronzeGold","Ivory","CapitalTimber"})
                {
                    var material=AssetDatabase.LoadAssetAtPath<Material>(MatFolder+"/"+key+".mat");
                    if(material==null)throw new FileNotFoundException(key);Materials[key]=material;
                }
                Materials["PyramidStone"]=MasonryArchitectureLibrary.Stone;Materials["PyramidInterior"]=MasonryArchitectureLibrary.Interior;
                replacement=BuildRoyalStructure(previous.parent);
                Object.DestroyImmediate(previous.gameObject);
                foreach(var cover in transforms.Where(t=>t!=null&&t.name=="Movable stone skylight cover"))
                    cover.GetComponent<Renderer>().sharedMaterial=MasonryArchitectureLibrary.Stone;
                var after=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>!t.IsChildOf(replacement.transform)).Select(t=>t.GetInstanceID()).OrderBy(i=>i).ToArray();
                if(!preserved.SequenceEqual(after))throw new InvalidOperationException("An object outside palace geometry changed.");
                string prefab=MasonryArchitectureLibrary.Root+"/Prefabs/RoyalPalace_120x156x78.prefab";
                PrefabUtility.SaveAsPrefabAsset(replacement,prefab);
                // The portable kit keeps object-space triplanar rendering unbatched.
                // This palace is authored at world origin with every chunk's matrix identity,
                // so static batching preserves the exact same texture coordinates in this scene.
                int batched=0;
                foreach(var renderer in replacement.GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.GetComponentInParent<LODGroup>()!=null&&renderer.transform.position.sqrMagnitude<.000001f
                        &&renderer.transform.rotation==Quaternion.identity&&renderer.transform.lossyScale==Vector3.one)
                    {GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)|StaticEditorFlags.BatchingStatic);batched++;}
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                File.WriteAllText(MasonryArchitectureLibrary.Docs+"/integration.json","{\n  \"capitalScene\": \""+scenePath+"\",\n  \"palacePrefab\": \""+prefab+"\",\n  \"preservedObjectsOutsideGeometry\": "+after.Length+",\n  \"originAlignedBatchedRenderers\": "+batched+",\n  \"backup\": \""+backup+"\"\n}\n");
                return prefab;
            }
            finally
            {
                if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
                if(opened&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
            }
        }
    }
}
