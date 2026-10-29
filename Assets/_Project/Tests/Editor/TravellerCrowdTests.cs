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

        [TestCase(0,2,0)] [TestCase(1,28,25)] [TestCase(2,6,2)]
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
                { Assert.That(material.shader.isSupported,Is.True,material.name);Assert.That(material.mainTexture,Is.Not.Null,material.name); }
            }
        }

        [Test]
        public void TheCapitalHasWomenMenAndAnArmouredGuardRoster()
        {
            Open(1);var members=Object.FindObjectsByType<TravellerCrowdMember>(FindObjectsSortMode.None);
            Assert.That(members.Count(m=>m.isGuard),Is.EqualTo(6));
            Assert.That(members.Count(m=>m.isReviving),Is.EqualTo(7));
            Assert.That(members.Count(m=>m.GetComponent<TravellerActor>().role==TravellerRole.CitizenFemale),Is.GreaterThanOrEqualTo(5));
            foreach(var guard in members.Where(m=>m.isGuard))
            {
                Assert.That(guard.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh.name.Contains("helmet")),Is.True,guard.name);
                Assert.That(guard.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh.name.Contains("lamellar")),Is.True,guard.name);
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
        public void GuardEquipmentKeepsItsOrientationAfterRetargeting()
        {
            foreach(var id in new[]{"ShangGuard_Bronze","ShangGuard_Dark","ChurchGuard_Steel"})
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TravellerCrowdLibrary.PrefabPath(id)));
                try
                {
                    var animator=root.GetComponentInChildren<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    foreach(float phase in new[]{0f,.25f,.55f})
                    TravellerStoryCharacters.WithSample(animator,"MX_WeightShift",phase,()=>{
                        var shaft=root.GetComponentsInChildren<MeshRenderer>().Single(m=>m.name.Replace(' ','_').Contains("spear_shaft"));
                        var tip=root.GetComponentsInChildren<MeshRenderer>().Single(m=>m.name.Replace(' ','_').Contains("spear_head"));
                        Assert.That(shaft.bounds.size.y,Is.GreaterThan(1.7f),id+" spear fell sideways.");
                        Assert.That(tip.bounds.center.y,Is.GreaterThan(animator.GetBoneTransform(HumanBodyBones.Head).position.y+.3f),id);
                        if(id.StartsWith("Shang"))
                        {
                            var helmet=root.GetComponentsInChildren<MeshRenderer>().Single(m=>m.name.Contains("helmet"));
                            Assert.That(helmet.bounds.min.y,Is.GreaterThan(animator.GetBoneTransform(HumanBodyBones.Head).position.y+.08f),id+" helmet covers the face.");
                        }
                    });
                }
                finally{Object.DestroyImmediate(root);}
            }
        }

        private static void Open(int index) => EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder+"/"+TravellerStage.SceneNames[index]+".unity");
    }
}
