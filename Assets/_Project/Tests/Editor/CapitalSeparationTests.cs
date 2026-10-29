using System.Linq;
using NUnit.Framework;
using ThreeBodyProblem.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Tests
{
    public class CapitalSeparationTests
    {
        private string original;
        [SetUp] public void Remember() => original=SceneManager.GetActiveScene().path;
        [TearDown] public void Restore() { if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original); }

        [Test] public void GobiContainsTravelAndShelterWithoutCapitalArchitecture()
        {
            EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder+"/01_GobiJourney.unity");
            var stage=Object.FindFirstObjectByType<TravellerStage>();
            Assert.That(stage.GetComponentsInChildren<PendulumMotion>(true),Is.Empty);
            Assert.That(stage.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("Chaoge gate")||t.name=="Shang Capital"),Is.False);
            Assert.That(stage.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Departure waystone"),Is.True);
            Assert.That(stage.shelterCenter,Is.Not.Null);
        }

        [Test] public void CapitalStartsOutsideItsGateAndContainsDistinctCityPrecincts()
        {
            var stage=OpenCapital();var route=stage.profile;
            Assert.That(route.hasCapitalRoute,Is.True);
            Assert.That(route.moments[0].viewPosition.z,Is.LessThan(-220));
            Assert.That(route.courtGoal.y,Is.EqualTo(4f));
            Assert.That(route.courtWaypoints.Length,Is.GreaterThanOrEqualTo(5));
            Assert.That(route.waterWaypoints.Length,Is.GreaterThanOrEqualTo(4));
            var names=stage.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
            foreach(string name in new[]{"Shang Capital","03 Stone pyramid palace","04 Ancestral temple precinct","05 Residential wards","06 Bronze workshops and granaries","07 Revival water gardens"})CollectionAssert.Contains(names,name);
            Assert.That(names.Contains("Giant rock shelter"),Is.False);
            Assert.That(stage.GetComponentsInChildren<LODGroup>().Length,Is.GreaterThan(50));
        }

        [Test] public void CapitalArrivalCourtAndGardenRoutesHaveClearWalkingSpace()
        {
            var stage=OpenCapital();stage.player.GetComponent<CharacterController>().enabled=false;Physics.SyncTransforms();
            var entry=stage.profile.moments[0].viewPosition-Vector3.up*1.65f;
            CheckRoute(new[]{entry}.Concat(stage.profile.courtWaypoints).Concat(new[]{stage.profile.courtGoal}).ToArray());
            CheckRoute(new[]{stage.profile.courtGoal}.Concat(stage.profile.waterWaypoints).Concat(new[]{stage.profile.waterGoal}).ToArray());
        }

        [Test] public void PyramidLeavesAllThreeLongNightStarsInView()
        {
            var stage=OpenCapital();var moment=stage.profile.moments[2];var camera=stage.player.viewCamera;
            stage.player.Teleport(moment.viewPosition,moment.viewTarget);camera.aspect=16f/9f;Physics.SyncTransforms();
            foreach(var star in stage.flyingStars)
            foreach(float motion in new[]{-12f,0f,12f})
            {
                var point=star.position+Vector3.right*motion;var viewport=camera.WorldToViewportPoint(point);
                Assert.That(viewport.z,Is.GreaterThan(0));
                Assert.That(viewport.x,Is.InRange(0.05f,0.95f),star.name+" outside horizontal view");
                Assert.That(viewport.y,Is.InRange(0.05f,0.95f),star.name+" outside vertical view");
                RaycastHit hit;var direction=point-camera.transform.position;
                Assert.That(Physics.Raycast(camera.transform.position,direction.normalized,out hit,direction.magnitude,~0,QueryTriggerInteraction.Ignore),Is.False,
                    star.name+" occluded by "+(hit.collider==null?"":hit.collider.name));
            }
        }

        [Test] public void PyramidHasAnEnclosedHallAndAnOperableSkyAperture()
        {
            var stage=OpenCapital();var skylight=Object.FindFirstObjectByType<PalaceSkylight>();
            Assert.That(skylight,Is.Not.Null);Assert.That(stage.GetComponentsInChildren<PendulumMotion>().Length,Is.EqualTo(7));
            var names=stage.GetComponentsInChildren<Transform>(true).Select(t=>t.name).ToArray();
            CollectionAssert.Contains(names,"Thick entrance tunnel wall");
            CollectionAssert.Contains(names,"Audience chamber rear wall");
            Assert.That(names.Any(n=>n.Contains("hipped roof")&&n.Contains("Great hall")),Is.False);
            bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try
            {
                var receiver=new Vector3(0,4.1f,23.5f);RaycastHit hit;
                skylight.SetOpening(0);Physics.SyncTransforms();
                Assert.That(Physics.Raycast(receiver,stage.profile.moments[0].sunA.normalized,out hit,120),Is.True);
                Assert.That(hit.collider.name,Is.EqualTo("Movable stone skylight cover"));
                skylight.SetOpening(1);Physics.SyncTransforms();
                Assert.That(Physics.Raycast(receiver,stage.profile.moments[0].sunA.normalized,out hit,120),Is.False,"The open aperture still blocks sunlight: "+(hit.collider==null?"":hit.collider.name));
                var eye=stage.profile.courtGoal+Vector3.up*1.65f;
                Assert.That(Physics.Linecast(eye,skylight.observationPoint,~0,QueryTriggerInteraction.Ignore),Is.False,"The player must see the sun patch from the court's interaction point.");
                Assert.That(Physics.Raycast(new Vector3(-8,5.7f,20),Vector3.up,out hit,20),Is.True,"The hall must have a stone ceiling outside the aperture.");
            }
            finally {Physics.queriesHitBackfaces=backfaces;skylight.SetOpening(0);}
        }

        private static TravellerStage OpenCapital()
        {
            EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder+"/02_ChaogeCourt.unity");return Object.FindFirstObjectByType<TravellerStage>();
        }

        private static void CheckRoute(Vector3[] points)
        {
            for(int segment=1;segment<points.Length;segment++)
            {
                int samples=Mathf.CeilToInt(Vector3.Distance(points[segment-1],points[segment])/2f);
                for(int sample=0;sample<=samples;sample++)
                {
                    var point=Vector3.Lerp(points[segment-1],points[segment],samples==0?0:(float)sample/samples);
                    RaycastHit hit;
                    Assert.That(Physics.Raycast(point+Vector3.up*1.1f,Vector3.down,out hit,3,~0,QueryTriggerInteraction.Ignore),Is.True,"No floor at "+point);
                    Assert.That(Mathf.Abs(hit.point.y-point.y),Is.LessThan(0.65f),"Floor height mismatch at "+point+" on "+hit.collider.name);
                    var feet=hit.point+Vector3.up*0.05f;
                    var occupied=Physics.OverlapCapsule(feet+Vector3.up*0.4f,feet+Vector3.up*1.5f,0.26f,~0,QueryTriggerInteraction.Ignore)
                        .Where(c=>!c.name.StartsWith("周文王 [",System.StringComparison.Ordinal)).ToArray();
                    Assert.That(occupied,Is.Empty,"Blocked at "+point+": "+string.Join(",",occupied.Select(c=>c.name)));
                }
            }
        }
    }
}
