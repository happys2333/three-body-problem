using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem.Tests
{
    public sealed class ActOneMechanicsTests
    {
        [Test] public void SunburnIsTenPointsPerExposureTickAndCoverRestoresOneHundred()
        {
            var v=new SunExposureVitality();Assert.That(v.Health,Is.EqualTo(100));Assert.That(v.Tick(true,0),Is.EqualTo(10));Assert.That(v.Health,Is.EqualTo(90));
            v.Tick(true,.9f);Assert.That(v.Health,Is.EqualTo(90));v.Tick(true,.11f);Assert.That(v.Health,Is.EqualTo(80));v.Tick(false,0);Assert.That(v.Health,Is.EqualTo(100));
            v.Tick(true,0);Assert.That(v.Health,Is.EqualTo(90));
        }
        [Test] public void ContinuousSunburnClampsAtZeroAndCanRecover()
        {var v=new SunExposureVitality();v.Tick(true,100);Assert.That(v.Health,Is.Zero);Assert.That(v.IsDown,Is.True);v.Restore();Assert.That(v.IsDown,Is.False);Assert.That(v.Health,Is.EqualTo(100));}
        [Test] public void ShadeQueriesUseRealGeometryAndIgnoreTheActorItself()
        {
            string previous=SceneManager.GetActiveScene().path;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.transform.position=new Vector3(0,3,4);roof.transform.localScale=new Vector3(4,1,4);Physics.SyncTransforms();
                var sun=new Vector3(0,.3f,1);Assert.That(SunExposureSystem.IsSheltered(new Vector3(0,1,0),sun,null),Is.True);
                Assert.That(SunExposureSystem.IsSheltered(new Vector3(6,1,0),sun,null),Is.False);
                Assert.That(SunExposureSystem.IsSheltered(new Vector3(0,1,0),sun,roof.transform),Is.False);
            }
            finally{if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
        }
        [Test] public void WaveInputIsResampledToEditableMonoSpeechAndDecodes()
        {
            var samples=new float[48000*2];for(int i=0;i<samples.Length;i++)samples[i]=.25f;
            var bytes=VoiceWave.EncodeMono16(samples,48000,2);Assert.That(bytes.Length,Is.EqualTo(44+16000*2));
            var clip=VoiceWave.DecodePcm16(bytes);Assert.That(clip,Is.Not.Null);Assert.That(clip.channels,Is.EqualTo(1));Assert.That(clip.frequency,Is.EqualTo(16000));Assert.That(clip.length,Is.EqualTo(1).Within(.001));Object.DestroyImmediate(clip);
        }
        [Test] public void UntrustedAiIntentCannotBecomeAStateChangingCommand()
        {Assert.That(NpcDialogueDirector.GestureClip("advance_scene"),Is.EqualTo("MX_TalkCalm"));Assert.That(NpcDialogueDirector.GestureClip("set_health_100"),Is.EqualTo("MX_TalkCalm"));}
        [Test] public void SavedOpeningIsNightAndUsesTheRequestedRealDurations()
        {
            var profile=AssetDatabase.LoadAssetAtPath<TravellerSceneProfile>("Assets/_Project/Settings/Traveller/01_GobiJourney.asset");
            Assert.That(profile.moments[0].sunCount,Is.Zero);Assert.That(profile.moments[0].stars,Is.GreaterThan(.9f));Assert.That(profile.gobiStory.shelterWaitSeconds,Is.EqualTo(600));
            Assert.That(profile.gobiStory.sunlightDamage,Is.EqualTo(10));Assert.That(profile.gobiStory.maximumHealth,Is.EqualTo(100));
        }
    }
}
