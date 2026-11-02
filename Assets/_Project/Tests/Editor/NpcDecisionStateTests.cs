using NUnit.Framework;

namespace ThreeBodyProblem.Tests
{
    public class NpcDecisionStateTests
    {
        private static NpcDecisionOutcome Apply(NpcDecisionState s, string intent, string conduct, bool online = true,
            string npc = "king", JourneyStep step = JourneyStep.CourtTalk, bool ready = true, bool guards = true, int revision = -1) =>
            s.Apply(npc, step, ready, guards, intent, conduct, online, revision < 0 ? s.Revision : revision);
        [TestCase("neutral")][TestCase("question")][TestCase("clarification")][TestCase("disruption")][TestCase("apology")][TestCase("violent_threat")]
        public void ChatterAndMisconductCannotCompleteTheTask(string conduct)
        { var s = new NpcDecisionState(); Assert.That(Apply(s, "advance", conduct), Is.EqualTo(NpcDecisionOutcome.None)); }
        [TestCase("neutral")][TestCase("question")][TestCase("clarification")][TestCase("relevant_proposal")][TestCase("agreement")][TestCase("apology")]
        public void QuestionsEvidenceAndCooperationCannotTriggerPunishment(string conduct)
        {
            var s = new NpcDecisionState(); Assert.That(Apply(s, "warn", conduct), Is.EqualTo(NpcDecisionOutcome.None));
            Apply(s, "warn", "disruption"); Assert.That(Apply(s, "pursue", conduct), Is.EqualTo(NpcDecisionOutcome.None));
        }
        [Test] public void AttackNeedsAPriorSeparateWarningAndContinuedDefiance()
        {
            var s = new NpcDecisionState(); Assert.That(Apply(s, "pursue", "violent_threat"), Is.EqualTo(NpcDecisionOutcome.None));
            int before = s.Revision; Assert.That(Apply(s, "warn", "disruption"), Is.EqualTo(NpcDecisionOutcome.Warning));
            Assert.That(Apply(s, "pursue", "persistent_defiance", revision: before), Is.EqualTo(NpcDecisionOutcome.None));
            Assert.That(Apply(s, "pursue", "persistent_defiance"), Is.EqualTo(NpcDecisionOutcome.Pursue)); Assert.That(s.Pursuing, Is.True);
        }
        [Test] public void ApologyRecallsGuardsAndMakesOldOrdersStale()
        {
            var s = new NpcDecisionState(); Apply(s, "warn", "disruption"); Apply(s, "pursue", "persistent_defiance"); int old = s.Revision;
            Assert.That(Apply(s, "stand_down", "apology"), Is.EqualTo(NpcDecisionOutcome.StandDown)); Assert.That(s.Pursuing, Is.False);
            Assert.That(Apply(s, "pursue", "persistent_defiance", revision: old), Is.EqualTo(NpcDecisionOutcome.None));
        }
        [Test] public void ARequestToArrestNeverSilentlyBecomesALethalOrder()
        {
            var s=new NpcDecisionState();Apply(s,"warn","disruption");Apply(s,"pursue","persistent_defiance");
            Assert.That(s.Lethal,Is.False);Assert.That(Apply(s,"attack","violent_threat"),Is.EqualTo(NpcDecisionOutcome.Attack));
            Assert.That(s.Lethal,Is.True);Apply(s,"stand_down","apology");Assert.That(s.Lethal,Is.False);
        }
        [Test] public void LethalForceAlsoRequiresAPreviousWarning()
        {var s=new NpcDecisionState();Assert.That(Apply(s,"attack","violent_threat"),Is.EqualTo(NpcDecisionOutcome.None));}
        [Test] public void RoyalWarningsUseAngerAndSunWarningsStillShieldEyes()
        {
            Assert.That(NpcDialogueDirector.GestureClip("warn",JourneyStep.CourtTalk),Is.EqualTo("MX_AngryGesture"));
            Assert.That(NpcDialogueDirector.GestureClip("warn",JourneyStep.SeekShelter),Is.EqualTo("MX_ShieldEyes"));
        }
        [Test] public void RefusalsOfflineRepliesAndUnfinishedCeremonyNeverAct()
        {
            var s = new NpcDecisionState(); Assert.That(Apply(s, "advance", "agreement", online: false), Is.EqualTo(NpcDecisionOutcome.None));
            Assert.That(Apply(s, "warn", "disruption", online: false), Is.EqualTo(NpcDecisionOutcome.None));
            Assert.That(Apply(s, "advance", "agreement", ready: false), Is.EqualTo(NpcDecisionOutcome.None));
        }
        [Test] public void OtherPeopleCannotIssueOrdersOrAdvanceTheWrongChapter()
        {
            var s = new NpcDecisionState();
            foreach (string npc in new[] { "wenwang", "shang_guard", "citizen", "scholar", "davinci" })
                Assert.That(Apply(s, "warn", "violent_threat", npc: npc), Is.EqualTo(NpcDecisionOutcome.None));
            Assert.That(Apply(s, "advance", "agreement", npc: "pope"), Is.EqualTo(NpcDecisionOutcome.None));
            Assert.That(Apply(s, "advance", "agreement", npc: "scholar", step: JourneyStep.TribunalTalk), Is.EqualTo(NpcDecisionOutcome.Advance));
        }
        [Test] public void NavigationAndLifeStateAreRequiredForCombat()
        {
            var s = new NpcDecisionState(); Apply(s, "warn", "disruption");
            Assert.That(Apply(s, "pursue", "persistent_defiance", guards: false), Is.EqualTo(NpcDecisionOutcome.None));
            Apply(s, "pursue", "persistent_defiance"); Assert.That(Apply(s, "advance", "agreement"), Is.EqualTo(NpcDecisionOutcome.None));
            s.MarkDefeated(); Assert.That(s.Allowed("king", JourneyStep.CourtTalk, true, true), Is.EquivalentTo(new[] { "continue" }));
            s.Reset(); Assert.That(s.Defeated || s.WarningActive || s.Pursuing, Is.False);
        }
    }
}
