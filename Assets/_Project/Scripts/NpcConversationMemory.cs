using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ThreeBodyProblem
{
    // Session memory is shared by scene instances of a major character. It is
    // never shared between people, stored with credentials, or loaded from an old run.
    public static class NpcConversationMemory
    {
        private const int MaximumBytesPerPerson = 8 * 1024 * 1024;
        internal sealed class Diary
        {
            public readonly List<NpcDialogueDirector.Message> turns = new List<NpcDialogueDirector.Message>();
            public readonly List<string> events = new List<string>();
            public readonly HashSet<string> eventKeys = new HashSet<string>();
            public int bytes;
        }
        private static readonly Dictionary<string, Diary> people = new Dictionary<string, Diary>();
        public sealed class Checkpoint { internal readonly Dictionary<string, Diary> diaries = new Dictionary<string, Diary>(); }
        private static Diary Copy(Diary source)
        {
            var value = new Diary { bytes = source.bytes };
            foreach (var item in source.turns) value.turns.Add(new NpcDialogueDirector.Message(item.role, item.content));
            value.events.AddRange(source.events); value.eventKeys.UnionWith(source.eventKeys); return value;
        }
        public static Checkpoint Capture()
        { var point = new Checkpoint(); foreach (var item in people) point.diaries.Add(item.Key, Copy(item.Value)); return point; }
        public static void Restore(Checkpoint point)
        { people.Clear(); if (point != null) foreach (var item in point.diaries) people.Add(item.Key, Copy(item.Value)); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() { people.Clear(); }
        public static string Key(NpcIdentity person)
        {
            if (person.persona.IsMajorCharacter) return "major:" + person.persona.characterId;
            var path = new StringBuilder();
            for (Transform t = person.transform; t != null; t = t.parent)
                path.Insert(0, "/" + t.name + "[" + t.GetSiblingIndex() + "]");
            return person.gameObject.scene.path + ":" + person.persona.characterId + path;
        }
        private static Diary For(string key)
        { Diary value; if (!people.TryGetValue(key, out value)) { value = new Diary(); people.Add(key, value); } return value; }
        public static List<NpcDialogueDirector.Message> History(string key)
        {
            var result = new List<NpcDialogueDirector.Message>();
            foreach (var item in For(key).turns) result.Add(new NpcDialogueDirector.Message(item.role, item.content));
            return result;
        }
        public static List<string> KnownEvents(string key) => new List<string>(For(key).events);
        public static void RememberTurn(string key, string question, string answer)
        {
            if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer)) return;
            var value = For(key); value.turns.Add(new NpcDialogueDirector.Message("user", question)); value.turns.Add(new NpcDialogueDirector.Message("assistant", answer));
            value.bytes += Encoding.UTF8.GetByteCount(question) + Encoding.UTF8.GetByteCount(answer) + 256;
            while ((value.bytes > MaximumBytesPerPerson || value.turns.Count > 20000) && value.turns.Count >= 2)
            {
                value.bytes -= Encoding.UTF8.GetByteCount(value.turns[0].content) + Encoding.UTF8.GetByteCount(value.turns[1].content) + 256;
                value.turns.RemoveRange(0, 2);
            }
        }
        public static void RememberEvent(string key, string eventId, string text)
        {
            var value = For(key); if (!value.eventKeys.Add(eventId)) return;
            value.events.Add(text.Length > 512 ? text.Substring(0, 512) : text);
            if (value.events.Count > 128) value.events.RemoveAt(0);
        }
    }
}
