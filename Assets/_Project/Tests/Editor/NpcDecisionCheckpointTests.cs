using NUnit.Framework;

namespace ThreeBodyProblem.Tests
{
    public class NpcDecisionCheckpointTests
    {
        [TearDown] public void Cleanup() { NpcConversationMemory.Clear(); }
        [Test] public void AudienceRetryRewindsCourtMemoryAndPreservesTheEarlierJourney()
        {
            NpcConversationMemory.Clear();
            NpcConversationMemory.RememberTurn("major:wenwang", "我叫阿遥。", "阿遥，跟紧我。");
            var point=NpcConversationMemory.Capture();
            NpcConversationMemory.RememberTurn("major:king", "我要冲上王座！", "卫兵拿下！");
            NpcConversationMemory.RememberEvent("major:wenwang","death","你在场看见卫兵击倒玩家。");
            NpcConversationMemory.Restore(point);
            Assert.That(NpcConversationMemory.History("major:wenwang").Count,Is.EqualTo(2));
            Assert.That(NpcConversationMemory.History("major:king"),Is.Empty);
            Assert.That(NpcConversationMemory.KnownEvents("major:wenwang"),Is.Empty);
            NpcConversationMemory.RememberTurn("major:wenwang","再说一句。","好。");
            NpcConversationMemory.Restore(point);
            Assert.That(NpcConversationMemory.History("major:wenwang").Count,Is.EqualTo(2));
        }
    }
}
