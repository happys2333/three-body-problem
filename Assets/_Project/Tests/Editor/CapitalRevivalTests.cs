using System.Linq;
using NUnit.Framework;
using ThreeBodyProblem.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Tests
{
    public sealed class CapitalRevivalTests
    {
        private string original;
        [SetUp] public void Remember() { original = SceneManager.GetActiveScene().path; EditorSceneManager.OpenScene(CapitalRevivalProduction.ScenePath); }
        [TearDown] public void Restore() { if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original); }

        [Test] public void EveryReviverHasACompleteProneBodyAndAContinuousVolumeMaterial()
        {
            var director = Object.FindFirstObjectByType<CapitalRevivalDirector>(); Assert.That(director, Is.Not.Null);
            Assert.That(director.citizens.Length, Is.EqualTo(7));
            foreach (var citizen in director.citizens)
            {
                Assert.That(citizen.bakedHeight, Is.InRange(.15f, .8f));
                Assert.That(citizen.settings.paperThickness, Is.LessThan(.02f));
                Assert.That(citizen.paperRenderers.Length, Is.GreaterThan(0));
                foreach (var renderer in citizen.paperRenderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.vertexCount, Is.GreaterThan(2000));
                    Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                    Assert.That(renderer.sharedMaterials.All(m => m.shader.isSupported && m.HasProperty("_Hydration") && m.mainTexture != null), Is.True);
                }
                Assert.That(citizen.GetComponent<TorchBearer>(), Is.Not.Null);
                Assert.That(citizen.getUpFloorCorrection.length, Is.GreaterThan(20));
            }
        }

        [Test] public void GetUpFloorCorrectionKeepsTheWholeRetargetedBodyAboveTheWater()
        {
            var director = Object.FindFirstObjectByType<CapitalRevivalDirector>();
            foreach (var citizen in director.citizens)
            {
                var actor = citizen.GetComponent<TravellerActor>(); actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                foreach (float phase in new[] { .02f, .25f, .5f, .75f, 1f })
                    TravellerStoryCharacters.WithSample(actor.animator, citizen.settings.getUpMotion, phase, () => {
                        float floor = TravellerCrowdLibrary.WorldBounds(actor.gameObject).min.y + citizen.getUpFloorCorrection.Evaluate(phase) * citizen.transform.lossyScale.y;
                        Assert.That(floor, Is.EqualTo(citizen.settings.waterSurfaceY).Within(.035f), actor.name + " phase=" + phase);
                    });
            }
        }
    }
}
