using System.Linq;
using NUnit.Framework;
using ThreeBodyProblem.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Tests
{
    public sealed class TravellerCrowdTests
    {
        private string original;
        [SetUp] public void Remember() => original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        [TearDown] public void Restore() => EditorSceneManager.OpenScene(string.IsNullOrEmpty(original) ? TravellerSceneBuilder.SceneFolder+"/01_GobiJourney.unity" : original);

        // The medieval cast now includes the dedicated manuscript clerk.
        [TestCase(0,2,0)] [TestCase(1,36,33)] [TestCase(2,7,3)]
        public void EverySceneCharacterHasARealSkinnedHumanoid(int index,int expected,int crowd)
        {
            Open(index);var stage=Object.FindFirstObjectByType<TravellerStage>();
            var actors=stage.GetComponentsInChildren<TravellerActor>(true);
            Assert.That(actors.Length,Is.EqualTo(expected));
            Assert.That(stage.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("[scene stand-in]")),Is.False,"Primitive person remains in the scene.");
            Assert.That(actors.Count(a=>(int)a.role>(int)TravellerRole.DaVinci),Is.EqualTo(crowd));
            foreach(var actor in actors)
            {
                Assert.That(actor.animator.avatar.isValid&&actor.animator.avatar.isHuman,Is.True,actor.name);
                Assert.That(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(s=>s.sharedMesh.vertexCount),Is.GreaterThan(2000),actor.name);
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach(var material in skin.sharedMaterials)
                {
                    Assert.That(material.shader.isSupported,Is.True,material.name);
                    if(material.HasProperty("_MainTex"))Assert.That(material.mainTexture,Is.Not.Null,material.name);
                    else
                    {
                        Assert.That(material.shader.name,Is.EqualTo("Traveller/Palace Craft Surface"),material.name);
                        Assert.That(material.GetFloat("_PatternScale"),Is.GreaterThan(0),"Authored cloth must retain its procedural surface detail.");
                    }
                }
            }
        }

        [Test]
        public void TheCapitalHasWomenMenAndAnArmouredGuardRoster()
        {
            Open(1);var members=Object.FindObjectsByType<TravellerCrowdMember>(FindObjectsSortMode.None);
            Assert.That(members.Count(m=>m.isGuard),Is.EqualTo(10));
            Assert.That(members.Count(m=>m.isReviving),Is.EqualTo(7));
            Assert.That(members.Count(m=>m.GetComponent<TravellerActor>().role==TravellerRole.CitizenFemale),Is.GreaterThanOrEqualTo(5));
            foreach(var guard in members.Where(m=>m.isGuard))
            {
                var equipment = guard.GetComponent<EraGuardEquipment>();
                Assert.That(equipment, Is.Not.Null, guard.name);
                Assert.That(equipment.profile.role, Is.EqualTo(TravellerRole.ShangGuard), guard.name);
                Assert.That(equipment.profile.bodySource, Does.Contain("ShangGuard"), guard.name);
                Assert.That(guard.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s =>
                    AssetDatabase.GetAssetPath(s.sharedMesh).Contains("PlayerTraveller")), Is.False, guard.name);
            }
        }

        [TestCase(1)] [TestCase(2)]
        public void CrowdCapsulesArePlacedAboveGroundAndOutsideArchitecture(int index)
        {
            Open(index);Physics.SyncTransforms();
            foreach(var member in Object.FindObjectsByType<TravellerCrowdMember>(FindObjectsSortMode.None))
            {
                Vector3 p=member.transform.position;
                Assert.That(Physics.RaycastAll(p+Vector3.up*.15f,Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.7f&&!h.collider.transform.IsChildOf(member.transform)),Is.True,member.name+" has no ground.");
                var overlaps=Physics.OverlapCapsule(p+Vector3.up*.36f,p+Vector3.up*1.45f,.25f,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(member.transform)).ToArray();
                Assert.That(overlaps,Is.Empty,member.name+" intersects "+string.Join(",",overlaps.Select(c=>c.name)));
            }
        }

        [Test]
        public void EraGuardEquipmentAndDedicatedBodiesSurviveRetargeting()
        {
            foreach(var id in new[]{"ShangGuard_Bronze","ShangGuard_Dark","ChurchGuard_Steel","ShangSpearSentry"})
            {
                string path=id=="ShangSpearSentry"?CourtRetinueProduction.Root+"/Prefabs/"+id+".prefab":TravellerCrowdLibrary.PrefabPath(id);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    var animator=root.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var equipment = root.GetComponent<EraGuardEquipment>();
                    Assert.That(equipment, Is.Not.Null, id);
                    if(id=="ShangSpearSentry")
                    {
                        Assert.That(Vector3.Dot(equipment.polearm.up,Vector3.up),Is.GreaterThan(.999f),"Ceremonial spear must stand upright.");
                        Assert.That(equipment.polearm.GetComponentInChildren<MeshFilter>().sharedMesh.name,Does.StartWith("BronzeSpear"));
                        Assert.That(root.GetComponent<GuardPostController>().allowPatrol,Is.False);
                        Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>().All(s=>s.sharedMesh.GetBlendShapeIndex("GuardGripRight")>=0),Is.True,"Prepared gripping fingers must be retained.");
                    }
                    Assert.That(equipment.profile.bodySource, Does.Contain(id.StartsWith("Shang") ? "ShangGuard" : "ChurchGuard"), id);
                    foreach(float phase in new[]{0f,.25f,.55f})
                    TravellerStoryCharacters.WithSample(animator,"MX_WeightShift",phase,()=>{
                        var weapon=equipment.polearm.Find("LOD0").GetComponent<MeshRenderer>();
                        Assert.That(weapon.bounds.size.y,Is.GreaterThan(1.9f),id+" polearm fell sideways.");
                        Assert.That(weapon.bounds.max.y,Is.GreaterThan(animator.GetBoneTransform(HumanBodyBones.Head).position.y+.25f),id);
                        Assert.That(equipment.shield.GetComponent<LODGroup>().GetLODs().Length, Is.EqualTo(3), id);
                    });
                }
                finally{Object.DestroyImmediate(root);}
            }
        }

        private static void Open(int index) => EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder+"/"+TravellerStage.SceneNames[index]+".unity");
    }
}
