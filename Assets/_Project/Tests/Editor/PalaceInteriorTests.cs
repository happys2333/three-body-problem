using System;
using System.Linq;
using NUnit.Framework;
using ThreeBodyProblem.Editor;
using UnityEditor;
using UnityEngine;

namespace ThreeBodyProblem.Tests
{
    public sealed class PalaceInteriorTests
    {
        [Test]
        public void SavedFurnishingsHaveValidGeometryAndDistanceDetail()
        {
            foreach (string id in new[] { "RoyalSeat", "RoyalRelief", "CalendarTable", "BronzeJue", "BronzeGu" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceInteriorProduction.Root + "/Prefabs/" + id + ".prefab");
                Assert.That(prefab, Is.Not.Null, id);
                var levels = prefab.GetComponent<LODGroup>().GetLODs(); Assert.That(levels.Length, Is.EqualTo(3), id);
                var counts = levels.Select(l => l.renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3)).ToArray();
                Assert.That(counts[0], Is.GreaterThan(counts[1])); Assert.That(counts[1], Is.GreaterThan(counts[2]));
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>())
                {
                    Assert.That(renderer.sharedMaterials.All(m => m != null && m.shader.isSupported), Is.True, id);
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), id);
                    Assert.That(mesh.normals.All(n => n.sqrMagnitude > .5f), Is.True, id);
                    Assert.That(mesh.vertices.All(v => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)), Is.True, id);
                }
            }
        }
        [Test]
        public void FloorEffectCannotBlockWalkingAndTimerHasFiveIndependentCompartments()
        {
            var floor = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceInteriorProduction.Root + "/Prefabs/DivinationFloor.prefab");
            Assert.That(floor.GetComponentsInChildren<Collider>().Length, Is.Zero);
            var timer = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceInteriorProduction.Root + "/Prefabs/FiveTierSandTimer.prefab").GetComponent<FiveTierSandTimer>();
            Assert.That(timer.piles.Length, Is.EqualTo(5)); Assert.That(timer.streams.Length, Is.EqualTo(5));
            Assert.That(timer.settings, Is.Not.Null, "A reusable timer must retain its timing configuration.");
            Assert.That(timer.piles.Distinct().Count(), Is.EqualTo(5));
        }
        [Test]
        public void ConfiguredVerificationAllowsTheRitualToFinishBeforeDeparture()
        {
            var p = new JourneyProgress(); p.ConfigureCalendar(26); p.Begin(); p.WakeUp(); p.DiscoverTraveller(); p.MeetTraveller();
            p.RecordNpcAnswer(); p.ChooseStoryIntent("sunrise"); p.MarkGiantSunVisible(); p.Arrive(); p.IntroduceTraveller();
            p.Tick(p.MinimumShelterTalkSeconds); p.ChooseStoryIntent("sunset"); p.Tick(p.SuddenSunsetSeconds);
            p.AddNightWalking(p.MinimumNightWalkMetres); p.Tick(p.MinimumNightWalkSeconds); p.Arrive(); p.Arrive(); p.Continue();
            Assert.That(p.Step, Is.EqualTo(JourneyStep.CalendarWait)); p.Tick(7);
            Assert.That(p.Step, Is.EqualTo(JourneyStep.CalendarWait)); p.Tick(19);
            Assert.That(p.Step, Is.EqualTo(JourneyStep.WalkToWater));
        }
        [Test]
        public void MissionHighlightingCannotInterpretInjectedMarkup()
        {
            var text = TravellerHudText.Objective("与姬昌一同前往朝歌 <size=90>text</size>");
            Assert.That(text, Does.Contain("<color=#F2D35E>姬昌</color>"));
            Assert.That(text, Does.Not.Contain("<size")); Assert.That(text, Does.Contain("＜size=90＞"));
        }
        [Test]
        public void LongSubtitlesPreserveWordsPunctuationAndSurrogatePairs()
        {
            string value = "姬昌：不要只相信眼前的一次应验。我们要把每一次日照记下来，等候天窗再次打开。观察与推演应当互相查验。🌞 The calendar is a proposal, and the sky is our evidence.";
            var pages = TravellerHudText.SubtitlePages(value, 28);
            Assert.That(pages.Count, Is.GreaterThan(2));
            Func<string,string> compact = s => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
            Assert.That(compact(string.Concat(pages)), Is.EqualTo(compact(value)));
            Assert.That(pages.All(s => !char.IsLowSurrogate(s[0]) && !char.IsHighSurrogate(s[s.Length - 1])), Is.True);
        }
    }
}
