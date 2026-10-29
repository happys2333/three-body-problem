using System.Linq;
using NUnit.Framework;
using ThreeBodyProblem.Editor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Tests
{
    public sealed class TravellerCharacterIntegrationTests
    {
        private SceneSetup[] original;
        [SetUp] public void Remember() => original = EditorSceneManager.GetSceneManagerSetup();
        [TearDown] public void Restore()
        {
            // Test Runner can start with an unsaved temporary scene, which has no restorable setup.
            if (original.Length > 0 && original.Count(s => s.isActive && s.isLoaded) == 1 && original.All(s => !s.isLoaded || !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(original);
            else Open(0);
        }

        [TestCase(0, new[] { TravellerRole.PlayerTraveller, TravellerRole.WenWang })]
        [TestCase(1, new[] { TravellerRole.PlayerTraveller, TravellerRole.WenWang, TravellerRole.KingZhou })]
        [TestCase(2, new[] { TravellerRole.PlayerTraveller, TravellerRole.Pope, TravellerRole.Scholar, TravellerRole.DaVinci })]
        public void StoryUsesTheExpectedHumanoidCast(int scene, TravellerRole[] roles)
        {
            Open(scene);
            var actors = Object.FindObjectsByType<TravellerActor>(FindObjectsSortMode.None).Where(a => (int)a.role <= (int)TravellerRole.DaVinci).ToArray();
            CollectionAssert.AreEquivalent(roles, actors.Select(a => a.role));
            foreach (var actor in actors)
            {
                Assert.That(actor.animator.avatar.isHuman && actor.animator.avatar.isValid, Is.True, actor.name);
                Assert.That(actor.animator.applyRootMotion, Is.False, "Path movement must not be applied a second time by animation.");
                Assert.That(actor.animator.runtimeAnimatorController, Is.EqualTo(actor.settings.controller));
                Assert.That(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.GreaterThan(0));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void PlayerCameraIsIndependentOfAnimationAndTheVisibleMeshHasNoHead(int scene)
        {
            Open(scene);
            var player = Object.FindFirstObjectByType<TravellerPlayer>();
            Assert.That(player.viewCamera.transform.parent, Is.EqualTo(player.transform));
            Assert.That(player.viewCamera.GetComponentInParent<Animator>(), Is.Null, "Walking must not shake the camera through a head bone.");
            Assert.That(player.viewCamera.transform.localPosition, Is.EqualTo(Vector3.up * player.eyeHeight));
            var skins = player.bodyActor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var visible = skins.Single(s => s.shadowCastingMode == ShadowCastingMode.Off);
            var shadow = skins.Single(s => s.shadowCastingMode == ShadowCastingMode.ShadowsOnly);
            Assert.That(visible.sharedMesh.triangles.Length, Is.GreaterThan(1000));
            Assert.That(visible.sharedMesh.triangles.Length, Is.LessThan(shadow.sharedMesh.triangles.Length));
            var head = player.bodyActor.animator.GetBoneTransform(HumanBodyBones.Head);
            var neck = player.bodyActor.animator.GetBoneTransform(HumanBodyBones.Neck);
            var weights = visible.sharedMesh.boneWeights;
            foreach (var index in visible.sharedMesh.triangles.Distinct())
            {
                var w = weights[index];
                AssertNoHeadInfluence(visible, head, neck, w.boneIndex0, w.weight0);
                AssertNoHeadInfluence(visible, head, neck, w.boneIndex1, w.weight1);
                AssertNoHeadInfluence(visible, head, neck, w.boneIndex2, w.weight2);
                AssertNoHeadInfluence(visible, head, neck, w.boneIndex3, w.weight3);
            }
        }

        [TestCase(1, TravellerRole.KingZhou)] [TestCase(2, TravellerRole.Pope)]
        public void SeatedGesturesCannotOverrideTheLegsOrRoot(int scene, TravellerRole role)
        {
            Open(scene);
            var actor = Object.FindObjectsByType<TravellerActor>(FindObjectsSortMode.None).Single(a => a.role == role);
            Assert.That(actor.seated, Is.True);
            var controller = (AnimatorController)actor.animator.runtimeAnimatorController;
            var layer = controller.layers.Single(l => l.name == "Seated Gestures");
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False);
            Assert.That(layer.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head), Is.True);
            Assert.That(layer.stateMachine.defaultState.motion.name, Is.EqualTo("MX_SitIdle"));
        }

        private static void AssertNoHeadInfluence(SkinnedMeshRenderer skin, Transform head, Transform neck, int bone, float weight)
        {
            if (skin.bones[bone] == head || skin.bones[bone] == neck) Assert.That(weight, Is.LessThan(0.05f), "The first-person body still contains a head/neck triangle.");
        }

        private static void Open(int scene) => EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/" + TravellerStage.SceneNames[scene] + ".unity");
    }
}
