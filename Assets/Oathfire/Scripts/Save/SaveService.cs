using System;
using System.IO;
using UnityEngine;

namespace Oathfire.Save
{
    /// <summary>
    /// JSON saves in Application.persistentDataPath: three manual slots plus one autosave.
    /// Writes go to a temp file first and are then swapped in, so a kill mid-write cannot corrupt a save.
    /// </summary>
    public class SaveService
    {
        public const int SlotCount = 3;
        public const int AutosaveSlot = -1;

        public SaveData Current { get; private set; }
        public int CurrentSlot { get; private set; } = AutosaveSlot;

        float sessionStart;

        static string PathFor(int slot) =>
            Path.Combine(Application.persistentDataPath, slot == AutosaveSlot ? "autosave.json" : $"slot{slot}.json");

        public SaveData NewGame()
        {
            Current = new SaveData();
            sessionStart = Time.realtimeSinceStartup;
            // The notice board belongs to the run, not to the device. Without this a new game inherits the
            // last one's postings, and the board refuses to roll again because it already rolled that night.
            Contracts.ContractBoard.ClearForSlot(CurrentSlot);
            return Current;
        }

        public bool SlotExists(int slot) => File.Exists(PathFor(slot));

        public SaveData Peek(int slot)
        {
            try
            {
                return File.Exists(PathFor(slot)) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot))) : null;
            }
            catch (Exception error)
            {
                Debug.LogError($"[Save] Could not read slot {slot}: {error.Message}");
                return null;
            }
        }

        public bool Load(int slot)
        {
            SaveData data = Peek(slot);
            if (data == null)
                return false;
            Current = data;
            CurrentSlot = slot;
            sessionStart = Time.realtimeSinceStartup;
            Contracts.ContractBoard.Reset();   // the cached board belongs to the slot we just left
            return true;
        }

        public bool Save(int slot, string checkpointId, string sceneName)
        {
            if (Current == null)
                NewGame();

            Current.checkpointId = checkpointId;
            Current.sceneName = sceneName;
            Current.savedAtIso = DateTime.UtcNow.ToString("o");
            Current.playSeconds += Time.realtimeSinceStartup - sessionStart;
            sessionStart = Time.realtimeSinceStartup;

            string path = PathFor(slot);
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(Current, true));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temp, path);
                CurrentSlot = slot;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"[Save] Could not write slot {slot}: {error.Message}");
                return false;
            }
        }

        public bool Autosave(string checkpointId, string sceneName) => Save(AutosaveSlot, checkpointId, sceneName);

        public void Delete(int slot)
        {
            try
            {
                if (File.Exists(PathFor(slot)))
                    File.Delete(PathFor(slot));
            }
            catch (Exception error)
            {
                Debug.LogError($"[Save] Could not delete slot {slot}: {error.Message}");
            }
        }

        public int MostRecentSlot()
        {
            int best = int.MinValue;
            DateTime newest = DateTime.MinValue;
            for (int slot = AutosaveSlot; slot < SlotCount; slot++)
            {
                SaveData data = Peek(slot);
                if (data == null || !DateTime.TryParse(data.savedAtIso, out DateTime when) || when <= newest)
                    continue;
                newest = when;
                best = slot;
            }
            return best;
        }
    }
}
