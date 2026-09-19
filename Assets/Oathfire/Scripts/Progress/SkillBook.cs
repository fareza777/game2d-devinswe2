using System;
using System.Collections.Generic;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace Oathfire.Progress
{
    /// <summary>
    /// The skills the Warden can learn, which ones level has opened, and which two are carried into a fight.
    /// The list comes from Docs/skills.json, joined to the pack's ability assets by the scene builder; the choice
    /// of carried skills lives in the save, so it follows the Warden from map to map.
    /// </summary>
    public static class SkillBook
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public int level;
            public AbilityDefinition ability;

            public string NameKey => $"skill.{id}.name";
            public string DescriptionKey => $"skill.{id}.desc";
        }

        /// <summary>How many learned skills can be on the controls at once (the dodge has its own button).</summary>
        public const int Slots = 2;

        static Entry[] entries = Array.Empty<Entry>();

        public static IReadOnlyList<Entry> Entries => entries;

        public static void Register(Entry[] list)
        {
            if (list != null && list.Length > 0)
                entries = list;
        }

        static int Level => PlayerState.Instance?.Level ?? 1;

        public static bool IsLearned(Entry entry) => entry != null && Level >= entry.level;

        public static Entry Find(string id)
        {
            foreach (Entry entry in entries)
                if (entry.id == id)
                    return entry;
            return null;
        }

        /// <summary>The skills that open at exactly this level; the level banner names them.</summary>
        public static IEnumerable<Entry> LearnedAt(int level)
        {
            foreach (Entry entry in entries)
                if (entry.level == level)
                    yield return entry;
        }

        public static Entry InSlot(int slot)
        {
            Entry entry = Find(SlotId(slot));
            return IsLearned(entry) ? entry : null;
        }

        /// <summary>Puts a learned skill in a slot; if it was already in the other one, the two swap.</summary>
        public static void Assign(int slot, string id)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null || !IsLearned(Find(id)))
                return;
            string previous = SlotId(slot);
            for (int other = 0; other < Slots; other++)
                if (other != slot && SlotId(other) == id)
                    SetSlotId(other, previous);
            SetSlotId(slot, id);
        }

        /// <summary>A newly learned skill goes straight into an empty slot, so a level-up is felt at once.</summary>
        public static void FillEmptySlots()
        {
            if (Core.GameServices.Save.Current == null)
                return;
            for (int slot = 0; slot < Slots; slot++)
            {
                if (InSlot(slot) != null)
                    continue;
                foreach (Entry entry in entries)
                {
                    if (!IsLearned(entry) || IsCarried(entry.id))
                        continue;
                    SetSlotId(slot, entry.id);
                    break;
                }
            }
        }

        public static bool IsCarried(string id)
        {
            for (int slot = 0; slot < Slots; slot++)
                if (SlotId(slot) == id && InSlot(slot) != null)
                    return true;
            return false;
        }

        static string SlotId(int slot)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null)
                return string.Empty;
            return slot == 0 ? save.skillSlot1 : save.skillSlot2;
        }

        static void SetSlotId(int slot, string id)
        {
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null)
                return;
            if (slot == 0)
                save.skillSlot1 = id ?? string.Empty;
            else
                save.skillSlot2 = id ?? string.Empty;
        }
    }
}
