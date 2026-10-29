using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeBodyProblem
{
    [Serializable]
    public sealed class JournalEntry
    {
        public string category;
        public string text;
    }

    [Serializable]
    public sealed class ObservationJournal
    {
        private const string StorageKey = "ThreeBodyTraveller.Journal.v1";
        public List<JournalEntry> entries = new List<JournalEntry>();
        public string hypothesis = "";

        public bool Add(string category, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (entries.Exists(entry => entry.category == category && entry.text == text)) return false;
            entries.Add(new JournalEntry { category = category, text = text });
            Save();
            return true;
        }

        public void Save()
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static ObservationJournal Load()
        {
            try
            {
                var journal = JsonUtility.FromJson<ObservationJournal>(PlayerPrefs.GetString(StorageKey, ""));
                if (journal != null && journal.entries != null) return journal;
            }
            catch (ArgumentException) { }
            return new ObservationJournal();
        }
    }
}
