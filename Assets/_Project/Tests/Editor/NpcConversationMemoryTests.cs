using NUnit.Framework;
using UnityEngine;

namespace ThreeBodyProblem.Tests
{
    public sealed class NpcConversationMemoryTests
    {
        [SetUp] public void StartFresh() => NpcConversationMemory.Clear();
        [TearDown] public void CleanUp() => NpcConversationMemory.Clear();
        [Test]
        public void MoreThanSixTurnsRemainAndOtherPeopleDoNotInheritThem()
        {
            for (int i=0;i<40;i++) NpcConversationMemory.RememberTurn("major:wenwang", "问题"+i, "回答"+i);
            var memory=NpcConversationMemory.History("major:wenwang");
            Assert.That(memory.Count, Is.EqualTo(80)); Assert.That(memory[0].content, Is.EqualTo("问题0"));
            Assert.That(NpcConversationMemory.History("major:king"), Is.Empty);
            Assert.That(NpcConversationMemory.History("major:pope"), Is.Empty);
        }
        [Test]
        public void MajorIdentitySurvivesAReplacementSceneObject()
        {
            var persona=ScriptableObject.CreateInstance<NpcPersona>();persona.characterId="wenwang";
            var first=new GameObject("Gobi traveller");var second=new GameObject("Capital companion");
            try
            {
                var a=first.AddComponent<NpcIdentity>();a.persona=persona;var b=second.AddComponent<NpcIdentity>();b.persona=persona;
                NpcConversationMemory.RememberTurn(NpcConversationMemory.Key(a),"我在找回家的路。","我们先结伴去朝歌。");
                Assert.That(NpcConversationMemory.Key(a), Is.EqualTo(NpcConversationMemory.Key(b)));
                Assert.That(NpcConversationMemory.History(NpcConversationMemory.Key(b))[0].content, Does.Contain("回家"));
                persona.characterId="citizen";
                Assert.That(NpcConversationMemory.Key(a), Is.Not.EqualTo(NpcConversationMemory.Key(b)));
            }
            finally { Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(persona); }
        }
        [Test]
        public void SnapshotEditsCannotRewriteARealConversation()
        {
            NpcConversationMemory.RememberTurn("major:king","请阅表章。","呈上来。");
            var snapshot=NpcConversationMemory.History("major:king");snapshot[0].content="forged";snapshot.Clear();
            Assert.That(NpcConversationMemory.History("major:king")[0].content, Is.EqualTo("请阅表章。"));
        }
        [Test]
        public void RepeatedStageLineIsRememberedOnceAndNewRunClearsBothKindsOfMemory()
        {
            NpcConversationMemory.RememberEvent("major:wenwang","shelter-intro","你已向玩家介绍姓名。");
            NpcConversationMemory.RememberEvent("major:wenwang","shelter-intro","你已向玩家介绍姓名。");
            Assert.That(NpcConversationMemory.KnownEvents("major:wenwang").Count, Is.EqualTo(1));
            Assert.That(NpcConversationMemory.KnownEvents("major:king"), Is.Empty);
            NpcConversationMemory.RememberTurn("major:wenwang","我迷路了。","先在这里歇歇。");
            NpcConversationMemory.Clear();
            Assert.That(NpcConversationMemory.History("major:wenwang"), Is.Empty);
            Assert.That(NpcConversationMemory.KnownEvents("major:wenwang"), Is.Empty);
        }
    }
}
