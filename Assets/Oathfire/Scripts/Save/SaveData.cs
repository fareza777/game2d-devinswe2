using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oathfire.Save
{
    /// <summary>One save slot. Story progress is a flag set plus counters, so new content never breaks old saves.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string chapterId = "chapter1";
        public string sceneName = "Prologue";
        public string checkpointId = string.Empty;
        public string savedAtIso = string.Empty;
        public float playSeconds;

        public Vector3 playerPosition;
        public int playerLevel = 1;
        public int playerHealth = 100;
        /// <summary>The quest the Warden chose to follow; empty follows the story.</summary>
        public string trackedQuest = string.Empty;
        /// <summary>The two skills carried on the controls (skill ids from the skill book).</summary>
        public string skillSlot1 = string.Empty;
        public string skillSlot2 = string.Empty;

        public List<string> flags = new List<string>();
        public List<CounterEntry> counters = new List<CounterEntry>();
        public List<ReputationEntry> reputation = new List<ReputationEntry>();

        [Serializable]
        public class CounterEntry
        {
            public string key;
            public int value;
        }

        [Serializable]
        public class ReputationEntry
        {
            public string faction;
            public int value;
        }

        public bool HasFlag(string flag) => flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (!flags.Contains(flag))
                flags.Add(flag);
        }

        public int GetCounter(string key)
        {
            foreach (CounterEntry entry in counters)
                if (entry.key == key)
                    return entry.value;
            return 0;
        }

        public void AddCounter(string key, int amount)
        {
            foreach (CounterEntry entry in counters)
            {
                if (entry.key == key)
                {
                    entry.value += amount;
                    return;
                }
            }
            counters.Add(new CounterEntry { key = key, value = amount });
        }

        public int GetReputation(string faction)
        {
            foreach (ReputationEntry entry in reputation)
                if (entry.faction == faction)
                    return entry.value;
            return 0;
        }

        public void AddReputation(string faction, int amount)
        {
            foreach (ReputationEntry entry in reputation)
            {
                if (entry.faction == faction)
                {
                    entry.value = Mathf.Clamp(entry.value + amount, -100, 100);
                    return;
                }
            }
            reputation.Add(new ReputationEntry { faction = faction, value = Mathf.Clamp(amount, -100, 100) });
        }
    }
}
