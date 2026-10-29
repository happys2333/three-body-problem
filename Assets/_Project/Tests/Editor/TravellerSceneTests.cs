using System.Linq;
using NUnit.Framework;
using ThreeBodyProblem.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Tests
{
    public class TravellerSceneTests
    {
        private string originalScene;
        [SetUp] public void RememberScene() => originalScene = SceneManager.GetActiveScene().path;
        [TearDown] public void RestoreScene()
        {
            if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene);
        }

        [Test]
        public void AllThreeScenesAreEnabledInJourneyOrder()
        {
            CollectionAssert.AreEqual(TravellerStage.SceneNames,
                EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path)).ToArray());
        }

        [TestCase("01_GobiJourney")]
        [TestCase("02_ChaogeCourt")]
        [TestCase("03_MedievalTribunal")]
        public void SceneHasCompleteRuntimeAndRenderingReferences(string name)
        {
            EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/" + name + ".unity");
            var stage = Object.FindFirstObjectByType<TravellerStage>();
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.profile.moments.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(stage.player.viewCamera, Is.EqualTo(Camera.main));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(stage.sun.type, Is.EqualTo(LightType.Directional));
            Assert.That(stage.skyMaterial.shader.isSupported, Is.True);
            Assert.That(RenderSettings.skybox, Is.EqualTo(stage.skyMaterial));
            Assert.That(stage.ambience.clip, Is.Not.Null);
            var hud = Object.FindFirstObjectByType<TravellerHud>();
            Assert.That(hud.font, Is.Not.Null); Assert.That(hud.stylesheet, Is.Not.Null);
            foreach (var transform in stage.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
            foreach (var renderer in stage.GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null, renderer.name);
                Assert.That(material.shader.isSupported, Is.True, material.name);
            }
        }

        [TestCase("01_GobiJourney")]
        [TestCase("02_ChaogeCourt")]
        [TestCase("03_MedievalTribunal")]
        public void EveryAuthoredViewHasGroundAndAnUnobstructedPlayerCapsule(string name)
        {
            EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/" + name + ".unity");
            var stage = Object.FindFirstObjectByType<TravellerStage>();
            stage.player.GetComponent<CharacterController>().enabled = false;
            Physics.SyncTransforms();
            foreach (var moment in stage.profile.moments)
            {
                Vector3 feet = moment.viewPosition - Vector3.up * 1.65f;
                Assert.That(Physics.Raycast(moment.viewPosition, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore), Is.True, moment.title + ": no ground");
                var overlaps = Physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.5f, 0.25f, ~0, QueryTriggerInteraction.Ignore);
                Assert.That(overlaps, Is.Empty, moment.title + ": inside " + string.Join(", ", overlaps.Select(collider => collider.name)));
            }
        }

        [Test]
        public void FlyingStarsAndThreeSunsRemainDistinctPhenomena()
        {
            var gobi = AssetDatabase.LoadAssetAtPath<TravellerSceneProfile>(TravellerSceneBuilder.Root + "/Settings/Traveller/01_GobiJourney.asset");
            Assert.That(gobi.moments.Any(moment => moment.flyingStars == 1 && moment.sunCount == 0 && !string.IsNullOrEmpty(moment.observation)), Is.True);
            Assert.That(gobi.moments.Any(moment => moment.flyingStars == 2 && moment.sunCount == 0 && !string.IsNullOrEmpty(moment.interpretation)), Is.True);
            var medieval = AssetDatabase.LoadAssetAtPath<TravellerSceneProfile>(TravellerSceneBuilder.Root + "/Settings/Traveller/03_MedievalTribunal.asset");
            Assert.That(medieval.moments[1].sunCount, Is.EqualTo(3));
            Assert.That(medieval.moments[1].flyingStars, Is.Zero);
            Assert.That(medieval.moments[1].heat, Is.LessThan(medieval.moments[2].heat));
        }
    }
}
