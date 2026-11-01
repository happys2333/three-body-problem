using System.Linq;
using NUnit.Framework;
using ThreeBodyProblem.Editor;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace ThreeBodyProblem.Tests
{
    public sealed class CourtAudienceTests
    {
        [Test]
        public void CeremonialPropsHaveThreeUsableLevelsAndNoWalkingObstacles()
        {
            foreach(string id in new[]{"RoyalFeatherFan","BambooPetition","CeremonialHu"})
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(CourtRetinueProduction.Root+"/Props/"+id+".prefab");
                Assert.That(asset,Is.Not.Null,id);var levels=asset.GetComponent<LODGroup>().GetLODs();Assert.That(levels.Length,Is.EqualTo(3));
                var counts=levels.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
                Assert.That(counts[0],Is.GreaterThan(counts[1]));Assert.That(counts[1],Is.GreaterThan(counts[2]));Assert.That(asset.GetComponentsInChildren<Collider>().Length,Is.Zero);
                Assert.That(asset.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported)),Is.True);
            }
        }
        [Test]
        public void BothFanBearersUseFemaleHumanoidsAndPreparedHands()
        {
            foreach(string id in new[]{"RitualAttendant","ChamberAttendant"})
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(CourtRetinueProduction.Root+"/Prefabs/"+id+".prefab");
                Assert.That(root.GetComponent<TravellerActor>().role,Is.EqualTo(TravellerRole.CitizenFemale));
                Assert.That(root.GetComponent<RoyalFanBearer>().fan,Is.Not.Null);Assert.That(root.GetComponent<CourtPropGrip>().kind,Is.EqualTo(CourtPropGrip.GripKind.Staff));
                Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s=>s.sharedMesh.GetBlendShapeIndex("CourtGripRight")>=0),Is.True);
            }
        }
        [Test]
        public void OfficialsRobesRemainFiniteDuringWalkingBowAndCarrying()
        {
            foreach(string id in new[]{"CourtSteward","CalendarAttendant"})
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CourtRetinueProduction.Root+"/Prefabs/"+id+".prefab"));
                try
                {
                    var actor=root.GetComponent<TravellerActor>();var cloth=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="Skinned long robe, sleeves and court crown");
                    Assert.That(cloth.sharedMesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f),Is.True);
                    foreach(string motion in new[]{"MX_Walking","MX_Bow","MX_CarryBoxIdle"})
                    TravellerStoryCharacters.WithSample(actor.animator,motion,.35f,()=>{
                        var baked=new Mesh();try{cloth.BakeMesh(baked);Assert.That(baked.vertices.All(p=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)),Is.True);Assert.That(baked.bounds.size.magnitude,Is.LessThan(4),motion);}finally{Object.DestroyImmediate(baked);}
                    });
                }
                finally{Object.DestroyImmediate(root);}
            }
        }
    }
}
