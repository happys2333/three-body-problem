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
        public string observationId;
        public string confirmedBy;
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

        internal bool Record(SceneObservation observation, string confirmedBy = null)
        {
            var entry = entries.Find(item => item.observationId == observation.id ||
                (string.IsNullOrEmpty(item.observationId) && item.category == "亲眼观察" &&
                    (item.text == observation.text || item.text == observation.legacyText)));
            if (entry == null)
            {
                entries.Add(new JournalEntry
                {
                    observationId = observation.id,
                    text = observation.text,
                    category = confirmedBy == null ? "亲眼观察" : "交谈确认",
                    confirmedBy = confirmedBy
                });
            }
            else
            {
                if (confirmedBy == null && entry.category == "交谈确认")
                    entry.category = "亲眼观察";
                else if (confirmedBy != null && string.IsNullOrEmpty(entry.confirmedBy))
                    entry.confirmedBy = confirmedBy;
                else if (!string.IsNullOrEmpty(entry.observationId)) return false;
                entry.observationId = observation.id;
            }

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
