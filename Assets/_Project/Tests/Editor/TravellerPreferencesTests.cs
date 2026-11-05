using NUnit.Framework;

namespace ThreeBodyProblem.Tests
{
    public sealed class TravellerPreferencesTests
    {
        [TestCase("https://api.deepseek.com", "https://api.deepseek.com/chat/completions")]
        [TestCase("https://example.test", "https://example.test/v1/chat/completions")]
        [TestCase("http://127.0.0.1:11434/v1/", "http://127.0.0.1:11434/v1/chat/completions")]
        [TestCase("https://example.test/proxy/v1/chat/completions", "https://example.test/proxy/v1/chat/completions")]
        public void BaseAndFullUrlsProduceOneChatCompletionsEndpoint(string url, string expected)
        {
            string result, error;
            Assert.That(NpcApiConnection.TryEndpoint(url, out result, out error), Is.True, error);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("file:///tmp/key")]
        [TestCase("https://user:secret@example.test/v1")]
        [TestCase("https://example.test/v1?api_key=secret")]
        [TestCase("not-a-url")]
        public void InvalidOrCredentialBearingAddressesAreRejected(string url)
        { string result, error; Assert.That(NpcApiConnection.TryEndpoint(url, out result, out error), Is.False); }

        [Test]
        public void PreferencesPersistChoicesButNeverPersistTheSessionKey()
        {
            var value = new TravellerPreferences { mouseSensitivity = 3.1f, invertMouseY = true, speakReplies = false,
                microphoneEnabled = false, microphoneDevice = "USB Audio 麦克风", fullscreen = true,
                displayWidth = 1600, displayHeight = 900, graphicsQuality = TravellerGraphicsQuality.High };
            value.connection.mode = "deepseek"; value.connection.apiKey = "test-session-secret-marker";
            value.connection.timeoutSeconds = 35;
            value.connection.contextWindowTokens = 1000000;
            string saved = value.ToSavedJson();
            Assert.That(saved, Does.Not.Contain(value.connection.apiKey)); Assert.That(saved, Does.Not.Contain("apiKey"));
            var loaded = TravellerPreferences.FromSavedJson(saved);
            Assert.That(loaded.connection.model, Is.EqualTo(value.connection.model));
            Assert.That(loaded.connection.mode, Is.EqualTo("deepseek")); Assert.That(loaded.connection.timeoutSeconds, Is.EqualTo(35));
            Assert.That(loaded.connection.apiKey, Is.Empty); Assert.That(loaded.mouseSensitivity, Is.EqualTo(3.1f));
            Assert.That(loaded.connection.contextWindowTokens, Is.EqualTo(1000000));
            Assert.That(loaded.invertMouseY, Is.True); Assert.That(loaded.speakReplies, Is.False);
            Assert.That(loaded.fullscreen, Is.True); Assert.That(loaded.graphicsQuality, Is.EqualTo(TravellerGraphicsQuality.High));
            Assert.That(loaded.displayWidth, Is.EqualTo(1600)); Assert.That(loaded.displayHeight, Is.EqualTo(900));
            Assert.That(loaded.microphoneEnabled, Is.False); Assert.That(loaded.microphoneDevice, Is.EqualTo("USB Audio 麦克风"));
        }

        [Test]
        public void EditingACopyCannotChangeTheActiveConnectionBeforeSave()
        {
            var value = new TravellerPreferences(); value.connection.apiKey = "original-session-key";
            var draft = value.Copy(); draft.connection.apiKey = "replacement"; draft.connection.model = "another-model";
            Assert.That(value.connection.apiKey, Is.EqualTo("original-session-key"));
            Assert.That(value.connection.model, Is.EqualTo("deepseek-flash"));
        }

        [Test]
        public void InvalidStoredPreferencesFallBackToUsableDefaults()
        {
            Assert.That(TravellerPreferences.FromSavedJson("{broken").connection.mode, Is.EqualTo("deepseek"));
            var value = new TravellerPreferences { mouseSensitivity = float.NaN }; string error;
            Assert.That(value.Validate(out error), Is.False);
            value = new TravellerPreferences { gatewayUrl = "https://example.test" };
            Assert.That(value.Validate(out error), Is.False);
        }

        [TestCase(false, false, true, true)]
        [TestCase(true, false, true, false)]
        [TestCase(false, true, true, false)]
        [TestCase(false, false, false, false)]
        public void FirstPersonCaptureDependsOnGameplayMenusAndFocus(bool blocked, bool pointerReleased, bool focused, bool expected)
        { Assert.That(TravellerPlayer.ShouldCaptureMouse(blocked, pointerReleased, focused), Is.EqualTo(expected)); }

        [TestCase(32000, true)]
        [TestCase(128000, true)]
        [TestCase(512000, true)]
        [TestCase(1000000, true)]
        [TestCase(0, false)]
        [TestCase(9999999, false)]
        public void ContextWindowUsesSupportedPresets(int tokens, bool valid)
        { string error; var value = new NpcApiConnection { contextWindowTokens = tokens }; Assert.That(value.Validate(out error), Is.EqualTo(valid)); }

        [Test]
        public void LegacyPreferencesReceiveTheStandardContextBudget()
        {
            var value = TravellerPreferences.FromSavedJson("{\"mode\":\"gateway\",\"mouseSensitivity\":2,\"timeoutSeconds\":20,\"speakReplies\":true}");
            Assert.That(value.connection.contextWindowTokens, Is.EqualTo(32000));
            Assert.That(value.microphoneEnabled, Is.True); Assert.That(value.microphoneDevice, Is.Empty);
        }

        [Test]
        public void DisconnectedDeviceChoiceSurvivesSavingWithoutChangingOtherPreferences()
        {
            var value = new TravellerPreferences { microphoneEnabled = true, microphoneDevice = "currently unplugged headset" };
            value.connection.contextWindowTokens = 1000000;
            var loaded = TravellerPreferences.FromSavedJson(value.ToSavedJson());
            Assert.That(loaded.microphoneDevice, Is.EqualTo(value.microphoneDevice));
            Assert.That(loaded.microphoneEnabled, Is.True); Assert.That(loaded.connection.contextWindowTokens, Is.EqualTo(1000000));
        }

        [Test]
        public void MicrophoneNamesWithControlCharactersAreRejected()
        {
            var value = new TravellerPreferences { microphoneDevice = "headset\ninvalid" }; string error;
            Assert.That(value.Validate(out error), Is.False);
        }
    }
}
