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
        [Test] public void WakingAndExploringNeverStartTheSunByTheClock()
        {
            var p=new JourneyProgress();p.Tick(1000);Assert.That(p.Step,Is.EqualTo(JourneyStep.Title));Assert.That(p.Continue(),Is.False);
            p.Begin();Assert.That(p.Step,Is.EqualTo(JourneyStep.Awakening));p.MarkGiantSunVisible();Assert.That(p.GiantSunSeen,Is.False);
            p.WakeUp();p.Tick(10000);Assert.That(p.Step,Is.EqualTo(JourneyStep.ExploreDesert));Assert.That(p.ChooseStoryIntent("sunrise"),Is.False);
            Assert.That(p.MeetTraveller(),Is.False);Assert.That(p.Arrive(),Is.False);
        }
        [Test] public void FirstMeetingAnswersHaveAHardBudgetAndCannotRevealIdentity()
        {
            var p=Meeting();p.IdentifyTraveller();Assert.That(p.KnowsIdentity,Is.False);
            Assert.That(p.RecordNpcAnswer(),Is.True);Assert.That(p.RecordNpcAnswer(),Is.True);Assert.That(p.RecordNpcAnswer(),Is.True);Assert.That(p.RecordNpcAnswer(),Is.False);
            Assert.That(p.OpeningAnswers,Is.EqualTo(3));Assert.That(p.ChooseStoryIntent("sunrise"),Is.True);Assert.That(p.Step,Is.EqualTo(JourneyStep.Sunrise));
            Assert.That(p.Arrive(),Is.False);p.MarkGiantSunVisible();Assert.That(p.Arrive(),Is.True);
        }
        [Test] public void ShelterNeedsIdentityOriginQuestionAndAnAllowedDecision()
        {
            var p=Opening();p.Tick(1000);Assert.That(p.Step,Is.EqualTo(JourneyStep.ShelterTalk));Assert.That(p.ChooseStoryIntent("sunset"),Is.False);
            p.IntroduceTraveller();Assert.That(p.ChooseStoryIntent("sunrise"),Is.False);Assert.That(p.ChooseStoryIntent("kill_player"),Is.False);
            Assert.That(p.ChooseStoryIntent("sunset"),Is.True);Assert.That(p.ChooseStoryIntent("sunset"),Is.False);
            p.Tick(1.99f);Assert.That(p.Step,Is.EqualTo(JourneyStep.Sunset));p.Tick(.02f);Assert.That(p.Step,Is.EqualTo(JourneyStep.WalkToChaoge));
        }
        [Test] public void ShelterCannotEndBeforeItsMinimumConversationTime()
        {
            var p=Opening();p.IntroduceTraveller();p.Tick(19.9f);Assert.That(p.ChooseStoryIntent("sunset"),Is.False);
            p.Tick(.11f);Assert.That(p.ChooseStoryIntent("sunset"),Is.True);
        }
        [Test] public void AutoTravelNeedsBothActualWalkingAndTime()
        {
            var p=NightWalk();p.Tick(12);Assert.That(p.Arrive(),Is.False);p.AddNightWalking(34.9f);Assert.That(p.Arrive(),Is.False);
            p.AddNightWalking(.2f);Assert.That(p.Arrive(),Is.True);Assert.That(p.Step,Is.EqualTo(JourneyStep.CourtApproach));Assert.That(p.SceneIndex,Is.EqualTo(1));
        }
        [Test] public void WalkingMetresCannotBeBankedBeforeTheNightWalk()
        {
            var p=Opening();p.AddNightWalking(100);Assert.That(p.NightWalkMetres,Is.Zero);
            p.IntroduceTraveller();p.Tick(20);p.ChooseStoryIntent("sunset");p.Tick(2);p.AddNightWalking(40);Assert.That(p.Arrive(),Is.False);p.Tick(12);Assert.That(p.Arrive(),Is.True);
        }
        [Test] public void OptionalStarObservationsNeverBlockOrDuplicateTravel()
        {
            var p=NightWalk();Assert.That(p.Observe(0,3),Is.True);Assert.That(p.Observe(0,3),Is.False);Assert.That(p.Observe(0,4),Is.True);Assert.That(p.Observe(0,4),Is.False);
            Assert.That(p.Step,Is.EqualTo(JourneyStep.WalkToChaoge));Assert.That(p.ObservationsThisRun,Is.EqualTo(2));p.Tick(12);p.AddNightWalking(35);Assert.That(p.Arrive(),Is.True);
        }
        [Test] public void NewRunClearsEvidenceAnswersIdentityAndSunState()
        {
            var p=NightWalk();p.Observe(0,3);p.Begin();Assert.That(p.GiantSunSeen,Is.False);Assert.That(p.NightWalkMetres,Is.Zero);Assert.That(p.ObservationsThisRun,Is.Zero);
            Assert.That(p.OpeningAnswers,Is.Zero);Assert.That(p.ShelterAnswers,Is.Zero);Assert.That(p.KnowsIdentity,Is.False);Assert.That(p.AskedPlayerOrigin,Is.False);
        }
        [TestCase("三体世界有三个太阳。")][TestCase("我是周文王姬昌。")][TestCase("This simulation has three suns.")]
        public void OpeningRejectsSpoilersEvenWhenAnUpstreamReturnsThem(string speech)
        {
            string result=GobiDialoguePolicy.SafeSpeech(speech,"你是谁？",JourneyStep.OpeningTalk);
            Assert.That(result,Does.Not.Contain("三体"));Assert.That(result,Does.Not.Contain("姬昌"));Assert.That(result,Does.Not.Contain("three suns"));
        }
        [TestCase(true)] [TestCase(false)]
        public void TheLaterChaptersStillReachTheirEvidenceBasedSummary(bool explain)
        {
            var p=NightWalk();p.Tick(12);p.AddNightWalking(35);p.Arrive();p.Arrive();p.Continue();p.Tick(7);p.Arrive();p.Observe(1,1);p.Continue();p.Tick(5);p.Observe(1,2);p.Continue();
            Assert.That(p.SceneIndex,Is.EqualTo(2));p.Arrive();Assert.That(p.Continue(),Is.False);p.Explain(explain);p.Continue();p.Arrive();
            Assert.That(p.Observe(2,1),Is.False);p.Tick(3);p.Observe(2,1);p.Continue();p.Tick(9);
            Assert.That(p.Step,Is.EqualTo(JourneyStep.Summary));Assert.That(p.OfferedExplanation,Is.EqualTo(explain));Assert.That(p.ObservationsThisRun,Is.EqualTo(3));
        }
        private static JourneyProgress Meeting(){var p=new JourneyProgress();p.Begin();p.WakeUp();p.DiscoverTraveller();p.MeetTraveller();return p;}
        private static JourneyProgress Opening(){var p=Meeting();p.RecordNpcAnswer();p.ChooseStoryIntent("sunrise");p.MarkGiantSunVisible();p.Arrive();return p;}
        private static JourneyProgress NightWalk(){var p=Opening();p.IntroduceTraveller();p.Tick(20);p.ChooseStoryIntent("sunset");p.Tick(2);return p;}
    }
}
