using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ThreeBodyProblem.Editor;

namespace ThreeBodyProblem.Tests
{
    public sealed class GobiRenderBudgetTests
    {
        private static void InGobi(Action<TravellerStage> check)
        {
            string previous=SceneManager.GetActiveScene().path;
            try {EditorSceneManager.OpenScene(GobiEnvironmentDressing.ScenePath);check(UnityEngine.Object.FindFirstObjectByType<TravellerStage>());}
            finally {if(!string.IsNullOrEmpty(previous)&&previous!=GobiEnvironmentDressing.ScenePath)EditorSceneManager.OpenScene(previous);}
        }
        [Test] public void RockShadowGeometryIsInvisibleCheaperAndExcludedFromBurnablePlants()
        {
            InGobi(stage=> {
                var proxies=stage.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name==ActOnePerformanceProduction.ShadowName).ToArray();
                Assert.That(proxies.Length,Is.GreaterThan(0));long original=0,simplified=0;
                foreach(var renderer in proxies)
                {
                    Assert.That(renderer.shadowCastingMode,Is.EqualTo(ShadowCastingMode.ShadowsOnly));
                    Assert.That(renderer.enabled,Is.False,"Individual authoring proxies must not double-draw the combined shadows.");
                    Assert.That(renderer.GetComponents<Collider>(),Is.Empty);
                    Assert.That(renderer.GetComponentInParent<VegetationFuel>(),Is.Null,"Burned plants must not leave independent shadows.");
                    Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("Traveller/Gobi Rock Shadow"));
                    var group=renderer.GetComponentInParent<LODGroup>();
                    original+=group.GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
                    simplified+=renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
                }
                Assert.That(simplified,Is.LessThan(original/5),"Shadow-only copies must reduce geometry substantially.");
                var batches=stage.transform.Find("Environment/"+ActOnePerformanceProduction.BatchRootName).GetComponentsInChildren<MeshRenderer>();
                Assert.That(batches.Length,Is.LessThan(proxies.Length));long combined=0;
                foreach(var batch in batches)
                {
                    Assert.That(batch.enabled,Is.True);Assert.That(batch.shadowCastingMode,Is.EqualTo(ShadowCastingMode.ShadowsOnly));
                    Assert.That(batch.GetComponents<Collider>(),Is.Empty);
                    combined+=batch.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
                }
                Assert.That(combined,Is.EqualTo(simplified),"Spatial batching dropped or duplicated a shadow submesh.");
            });
        }
        [Test] public void TheShelterKeepsItsDetailedVisibleMeshAndOriginalPhysicalCover()
        {
            InGobi(stage=> {
                var canopy=stage.GetComponentsInChildren<LODGroup>().First(g=>g.name.StartsWith("Shelter continuous canopy",StringComparison.Ordinal));
                var detailed=canopy.GetLODs()[0].renderers[0];
                Assert.That(AssetDatabase.GetAssetPath(detailed.GetComponent<MeshFilter>().sharedMesh),Is.EqualTo("Assets/_Project/Art/EnvironmentReady/GobiDressing/ShelterCanopy_LOD0.asset"));
                Assert.That(detailed.sharedMaterial.shader.name,Is.EqualTo("Traveller/Environment Surface"));
                Assert.That(AssetDatabase.GetAssetPath(canopy.GetComponentInChildren<MeshCollider>().sharedMesh),Is.EqualTo("Assets/_Project/Art/EnvironmentReady/GobiDressing/ShelterCanopy_Collision.asset"));
                Assert.That(canopy.GetComponentInChildren<MeshCollider>().enabled,Is.True);
                Physics.SyncTransforms();
                Assert.That(SunExposureSystem.IsSheltered(new Vector3(-13,1.1f,12),stage.profile.moments[1].sunA,null),Is.True);
                Assert.That(SunExposureSystem.IsSheltered(new Vector3(9,1.1f,-18),stage.profile.moments[1].sunA,null),Is.False);
            });
        }
        [Test] public void SkylineLodChangesDoNotDowngradeNearbyCanopyGeometry()
        {
            InGobi(stage=> {
                var groups=stage.GetComponentsInChildren<LODGroup>();
                var canopy=groups.First(g=>g.name.StartsWith("Shelter continuous canopy",StringComparison.Ordinal));
                var skyline=groups.First(g=>g.name.Contains("skyline"));
                Assert.That(skyline.GetLODs()[0].screenRelativeTransitionHeight,Is.GreaterThan(canopy.GetLODs()[0].screenRelativeTransitionHeight));
                Assert.That(skyline.GetLODs()[1].screenRelativeTransitionHeight,Is.GreaterThan(canopy.GetLODs()[1].screenRelativeTransitionHeight));
                Assert.That(skyline.GetLODs()[2].renderers[0].sharedMaterial.shader.name,Is.EqualTo("Traveller/Environment Surface"));
            });
        }
    }
}
