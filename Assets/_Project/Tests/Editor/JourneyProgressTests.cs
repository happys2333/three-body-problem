using NUnit.Framework;
namespace ThreeBodyProblem.Tests
{
    public class JourneyProgressTests
    {
        [Test] public void CompanionWaitsWhenPlayerLagsButCatchesUpWhenPlayerRunsAhead()
        {
            Assert.That(TravellerJourney.ShouldWaitForPlayer(new UnityEngine.Vector3(0,0,30),UnityEngine.Vector3.zero,new UnityEngine.Vector3(0,0,60)),Is.True);
            Assert.That(TravellerJourney.ShouldWaitForPlayer(UnityEngine.Vector3.zero,new UnityEngine.Vector3(0,0,30),new UnityEngine.Vector3(0,0,60)),Is.False);
        }
        [Test] public void TitleCannotAdvanceAndShelterNeedsTheFirstGiantSun()
        {
            var p=new JourneyProgress();p.Tick(1000);Assert.That(p.Step,Is.EqualTo(JourneyStep.Title));Assert.That(p.Continue(),Is.False);
            p.Begin();p.Tick(100);Assert.That(p.Arrive(),Is.False);p.MarkGiantSunVisible();Assert.That(p.Arrive(),Is.True);Assert.That(p.Step,Is.EqualTo(JourneyStep.ShelterTalk));
        }
        [Test] public void ShelterLastsSixHundredSecondsRegardlessOfDialogueChoice()
        {
            var p=Opening();p.IdentifyTraveller();Assert.That(p.Continue(),Is.False);
            p.Tick(599.9f);Assert.That(p.Step,Is.EqualTo(JourneyStep.ShelterTalk));p.Tick(.11f);Assert.That(p.Step,Is.EqualTo(JourneyStep.Sunset));
            p.Tick(1.99f);Assert.That(p.Step,Is.EqualTo(JourneyStep.Sunset));p.Tick(.02f);Assert.That(p.Step,Is.EqualTo(JourneyStep.WalkToChaoge));
        }
        [Test] public void AutoTravelNeedsBothActualWalkingAndTime()
        {
            var p=NightWalk();p.Tick(12);Assert.That(p.Arrive(),Is.False);p.AddNightWalking(34.9f);Assert.That(p.Arrive(),Is.False);
            p.AddNightWalking(.2f);Assert.That(p.Arrive(),Is.True);Assert.That(p.Step,Is.EqualTo(JourneyStep.CourtApproach));Assert.That(p.SceneIndex,Is.EqualTo(1));
        }
        [Test] public void WalkingMetresCannotBeBankedBeforeTheNightWalk()
        {
            var p=Opening();p.AddNightWalking(100);Assert.That(p.NightWalkMetres,Is.Zero);
            p.Tick(600);p.Tick(2);p.AddNightWalking(40);Assert.That(p.Arrive(),Is.False);p.Tick(12);Assert.That(p.Arrive(),Is.True);
        }
        [Test] public void OptionalStarObservationsNeverBlockOrDuplicateTravel()
        {
            var p=NightWalk();Assert.That(p.Observe(0,3),Is.True);Assert.That(p.Observe(0,3),Is.False);Assert.That(p.Observe(0,4),Is.True);Assert.That(p.Observe(0,4),Is.False);
            Assert.That(p.Step,Is.EqualTo(JourneyStep.WalkToChaoge));Assert.That(p.ObservationsThisRun,Is.EqualTo(2));p.Tick(12);p.AddNightWalking(35);Assert.That(p.Arrive(),Is.True);
        }
        [Test] public void NewRunClearsItsOwnEvidenceAndSunState()
        {
            var p=NightWalk();p.Observe(0,3);p.Begin();Assert.That(p.GiantSunSeen,Is.False);Assert.That(p.NightWalkMetres,Is.Zero);Assert.That(p.ObservationsThisRun,Is.Zero);
        }
        [TestCase(true)] [TestCase(false)]
        public void TheLaterChaptersStillReachTheirEvidenceBasedSummary(bool explain)
        {
            var p=NightWalk();p.Tick(12);p.AddNightWalking(35);p.Arrive();
            p.Arrive();p.Continue();p.Tick(7);p.Arrive();p.Observe(1,1);p.Continue();p.Tick(5);p.Observe(1,2);p.Continue();
            Assert.That(p.SceneIndex,Is.EqualTo(2));p.Arrive();Assert.That(p.Continue(),Is.False);p.Explain(explain);p.Continue();p.Arrive();
            Assert.That(p.Observe(2,1),Is.False);p.Tick(3);p.Observe(2,1);p.Continue();p.Tick(9);
            Assert.That(p.Step,Is.EqualTo(JourneyStep.Summary));Assert.That(p.OfferedExplanation,Is.EqualTo(explain));Assert.That(p.ObservationsThisRun,Is.EqualTo(3));
        }
        private static JourneyProgress Opening(){var p=new JourneyProgress();p.Begin();p.Tick(12);p.MarkGiantSunVisible();p.Arrive();return p;}
        private static JourneyProgress NightWalk(){var p=Opening();p.Tick(600);p.Tick(2);return p;}
    }
}
