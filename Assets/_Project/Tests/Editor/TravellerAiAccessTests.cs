using NUnit.Framework;
using UnityEngine;

namespace ThreeBodyProblem.Tests
{
    public class TravellerAiAccessTests
    {
        private NpcDialogueSettings defaults;

        [SetUp] public void SetUp()
        {
            TravellerAiAccess.Clear();
            defaults = ScriptableObject.CreateInstance<NpcDialogueSettings>();
        }
        [TearDown] public void TearDown()
        {
            TravellerAiAccess.Clear();
            Object.DestroyImmediate(defaults);
        }

        private static TravellerPreferences Configured() => new TravellerPreferences
        {
            connection = new NpcApiConnection { mode = "custom", apiUrl = "https://example.test/v1", model = "test-model", apiKey = "test-only-key" }
        };

        [TestCase("scripted")]
        [TestCase("gateway")]
        public void OfflineAndEnvironmentModesCannotUnlockGameplay(string mode)
        {
            var value = Configured(); value.connection.mode = mode;
            TravellerAiAccess.Confirm(value, defaults);
            Assert.That(TravellerAiAccess.CanPlay(value, defaults, out _), Is.False);
        }

        [Test] public void EmptyKeyAndUntestedCredentialsCannotUnlockGameplay()
        {
            var value = Configured();
            Assert.That(TravellerAiAccess.CanPlay(value, defaults, out _), Is.False);
            value.connection.apiKey = " ";
            TravellerAiAccess.Confirm(value, defaults);
            Assert.That(TravellerAiAccess.CanPlay(value, defaults, out _), Is.False);
        }

        [Test] public void SuccessfulTestOnlyUnlocksTheTestedConnection()
        {
            var value = Configured(); TravellerAiAccess.Confirm(value, defaults);
            Assert.That(TravellerAiAccess.CanPlay(value, defaults, out _), Is.True);
            var edited = value.Copy(); edited.connection.apiKey = "different-key";
            Assert.That(TravellerAiAccess.CanPlay(edited, defaults, out _), Is.False);
            edited = value.Copy(); edited.connection.model = "another-model";
            Assert.That(TravellerAiAccess.CanPlay(edited, defaults, out _), Is.False);
            edited = value.Copy(); edited.gatewayUrl = "http://127.0.0.1:9876";
            Assert.That(TravellerAiAccess.CanPlay(edited, defaults, out _), Is.False);
        }

        [Test] public void SavingPresentationSettingsKeepsTheConnectionVerified()
        {
            var value = Configured(); value.connection.apiKey = "  test-only-key  ";
            TravellerAiAccess.Confirm(value, defaults);
            value.connection.apiKey = value.connection.apiKey.Trim();
            value.graphicsQuality = TravellerGraphicsQuality.Low;
            value.displayWidth = 1280; value.displayHeight = 720;
            Assert.That(TravellerAiAccess.CanPlay(value, defaults, out _), Is.True);
            var restored = TravellerPreferences.FromSavedJson(value.ToSavedJson());
            Assert.That(TravellerAiAccess.CanPlay(restored, defaults, out _), Is.False);
        }

        [Test] public void StartingWithoutVerifiedAiStaysOnTheTitle()
        {
            var go = new GameObject("AI required entry regression");
            try
            {
                var journey = go.AddComponent<TravellerJourney>();
                journey.ReturnToTitle(); journey.Begin();
                Assert.That(journey.Progress.Step, Is.EqualTo(JourneyStep.Title));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
