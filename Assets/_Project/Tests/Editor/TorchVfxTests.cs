using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ThreeBodyProblem.Tests
{
    public sealed class TorchVfxTests
    {
        [TestCase(40f, 0f, 8f)]
        [TestCase(40f, 135f, 8f)]
        [TestCase(-7f, 240f, 8f)]
        public void SavedTorchFlameStartsAtTheWick(float pitch, float yaw, float roll)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(pitch, yaw, roll));
                var flame = instance.GetComponentInChildren<TorchFlameBillboard>(true);
                Assert.That(flame, Is.Not.Null);
                var mesh = flame.GetComponent<MeshFilter>().sharedMesh;
                // A centre-pivoted card moves the visible foot below the burning end when turned.
                Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(.001f), "Fire geometry needs a foot pivot at the wick.");
                Vector3 foot = flame.transform.TransformPoint(new Vector3(0, mesh.bounds.min.y, 0));
                Vector3 wick = instance.transform.TransformPoint(Vector3.up * .76f);
                Assert.That(Vector3.Distance(foot, wick), Is.LessThan(.016f), "Flame foot has separated from the torch head.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(2f, 6f, -3f)]
        [TestCase(-2f, -4f, 3f)]
        [TestCase(4f, 0f, 2f)]
        public void FacingAnElevatedOrLowCameraKeepsTheFootFixedAndFireUpright(float x, float y, float z)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(40, 135, 8));
                var flame = instance.GetComponentInChildren<TorchFlameBillboard>();
                var foot = flame.transform.position;
                flame.FaceCamera(foot + new Vector3(x, y, z));
                Assert.That(Vector3.Dot(flame.transform.up, Vector3.up), Is.GreaterThan(.99999f));
                Assert.That(Vector3.Distance(flame.transform.position, foot), Is.LessThan(.00001f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test] public void SmokeAndEmbersRiseInWorldSpaceFromTheWick()
        {
            var torch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/ActOne/Prefabs/Torch.prefab");
            var particles = torch.GetComponentsInChildren<ParticleSystem>();
            Assert.That(particles.Length, Is.EqualTo(2));
            foreach (var p in particles)
            {
                Assert.That(p.transform.localPosition.y, Is.EqualTo(.76f).Within(.001f));
                Assert.That(p.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(p.main.startSpeed.constant, Is.Zero, "Default particle Z speed must not send sparks down the angled handle.");
                Assert.That(p.velocityOverLifetime.space, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(p.velocityOverLifetime.y.constantMin, Is.GreaterThan(0));
                Assert.That(p.main.maxParticles, Is.LessThanOrEqualTo(12));
            }
        }
    }
}
