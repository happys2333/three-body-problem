using NUnit.Framework;

namespace ThreeBodyProblem.Tests
{
    public sealed class MicrophoneDeviceSelectionTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void DefaultChoiceUsesTheSystemDefaultInsteadOfTheFirstDevice(string selected)
        {
            string device, error;
            Assert.That(MicrophoneDeviceSelection.TryResolve(selected, new[] { "USB", "Built-in" }, out device, out error), Is.True);
            Assert.That(device, Is.Null); Assert.That(error, Is.Null);
        }
        [Test]
        public void ExplicitDeviceRemainsSelectedWhenTheEnumerationOrderChanges()
        {
            foreach (var devices in new[] { new[] { "USB", "Built-in" }, new[] { "Built-in", "USB" } })
            {
                string device, error;
                Assert.That(MicrophoneDeviceSelection.TryResolve("USB", devices, out device, out error), Is.True);
                Assert.That(device, Is.EqualTo("USB"));
            }
        }
        [Test]
        public void DisconnectedSelectionDoesNotSilentlyRecordFromAnotherDevice()
        {
            string device, error;
            Assert.That(MicrophoneDeviceSelection.TryResolve("USB", new[] { "Built-in" }, out device, out error), Is.False);
            Assert.That(device, Is.Null); Assert.That(error, Does.Contain("重新选择"));
        }
        [Test]
        public void NoAvailableDevicesReturnsATextChatFallback()
        {
            foreach (var devices in new[] { null, new string[0] })
            {
                string device, error;
                Assert.That(MicrophoneDeviceSelection.TryResolve("", devices, out device, out error), Is.False);
                Assert.That(device, Is.Null); Assert.That(error, Does.Contain("Enter"));
            }
        }
    }
}
